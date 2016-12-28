﻿using Bonsai.Design;
using Bonsai.NuGet.Properties;
using NuGet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bonsai.NuGet
{
    public partial class PackageManagerDialog : Form
    {
        PackageManagerProxy packageManagerProxy;
        PackageViewController packageViewController;

        TreeNode installedPackagesNode;
        TreeNode onlineNode;
        TreeNode updatesNode;
        TreeNode collapsingNode;
        TreeNode selectingNode;

        public PackageManagerDialog(string path)
        {
            InitializeComponent();
            packageManagerProxy = new PackageManagerProxy();
            packageManagerProxy.PackageInstalling += packageManagerProxy_PackageInstalling;
            packageViewController = new PackageViewController(
                path,
                this,
                packageView,
                packageDetails,
                packagePageSelector,
                packageManagerProxy,
                packageIcons,
                searchComboBox,
                sortComboBox,
                releaseFilterComboBox,
                () => updatesNode.IsExpanded,
                value => multiOperationPanel.Visible = value,
                Enumerable.Empty<string>());
            InitializeRepositoryViewNodes();
            multiOperationPanel.Visible = false;
            multiOperationLabel.Text = Resources.MultipleUpdatesLabel;
            multiOperationButton.Text = Resources.MultipleUpdatesOperationName;
        }

        public string InstallPath { get; set; }

        public IPackageManager PackageManager
        {
            get { return packageManagerProxy; }
        }

        private void InitializeRepositoryViewNode(TreeNode rootNode)
        {
            foreach (var pair in packageViewController.PackageManagers)
            {
                var node = rootNode.Nodes.Add(pair.Key);
                node.Tag = pair.Value;
            }
        }

        private void InitializeRepositoryViewNodes()
        {
            repositoriesView.Nodes.Clear();
            installedPackagesNode = repositoriesView.Nodes.Add(Resources.InstalledPackagesNodeName);
            var allInstalledNode = installedPackagesNode.Nodes.Add(Resources.AllNodeName);
            allInstalledNode.Tag = packageViewController.PackageManagers[Resources.AllNodeName];

            onlineNode = repositoriesView.Nodes.Add(Resources.OnlineNodeName);
            InitializeRepositoryViewNode(onlineNode);
            onlineNode.Expand();

            updatesNode = repositoriesView.Nodes.Add(Resources.UpdatesNodeName);
            InitializeRepositoryViewNode(updatesNode);
        }

        protected override void OnLoad(EventArgs e)
        {
            packageViewController.OnLoad(e);
            SelectDefaultNode();
            base.OnLoad(e);
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            const int MaxImageSize = 256;
            packageView.ItemHeight = (int)(64 * factor.Height);
            packageIcons.ImageSize = new Size(
                Math.Min(MaxImageSize, (int)(32 * factor.Height)),
                Math.Min(MaxImageSize, (int)(32 * factor.Height)));
            base.ScaleControl(factor, specified);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            packageViewController.OnHandleDestroyed(e);
            base.OnHandleDestroyed(e);
        }

        void SelectDefaultNode()
        {
            var selectedNode = onlineNode.Nodes
                .Cast<TreeNode>()
                .FirstOrDefault(node => node.Text == BonsaiMachineWideSettings.SettingsName)
                ?? onlineNode.FirstNode;
            repositoriesView.SelectedNode = selectedNode;
            repositoriesView.Select();
        }

        protected override void OnResizeBegin(EventArgs e)
        {
            packageViewController.OnResizeBegin(e);
            base.OnResizeBegin(e);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            packageViewController.OnResizeEnd(e);
            base.OnResizeEnd(e);
        }

        private void multiOperationButton_Click(object sender, EventArgs e)
        {
            if (packageView.OperationText == Resources.UpdateOperationName)
            {
                var packageFeed = packageViewController.GetPackageFeed();
                var packages = packageFeed()
                    .AsEnumerable()
                    .Where(PackageExtensions.IsListed)
                    .AsCollapsed();
                packageViewController.RunPackageOperation(packages, true);
            }
        }

        private void packageView_OperationClick(object sender, TreeViewEventArgs e)
        {
            bool handleDependencies = true;
            var package = (IPackage)e.Node.Tag;
            if (package != null)
            {
                if (packageViewController.SelectedRepository == packageManagerProxy.LocalRepository)
                {
                    var dependencies = (from dependency in package.GetCompatiblePackageDependencies(null)
                                        let dependencyPackage = packageViewController.SelectedRepository.ResolveDependency(dependency, true, true)
                                        where dependencyPackage != null
                                        select dependencyPackage)
                                        .ToArray();
                    if (dependencies.Length > 0)
                    {
                        var dependencyNotice = new StringBuilder();
                        dependencyNotice.AppendLine(string.Format(Resources.PackageDependencyNotice, package));
                        foreach (var dependency in dependencies)
                        {
                            dependencyNotice.AppendLine(dependency.ToString());
                        }

                        var result = MessageBox.Show(this, dependencyNotice.ToString(), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);
                        if (result == DialogResult.Cancel) return;
                        if (result == DialogResult.No) handleDependencies = false;
                    }
                }

                packageViewController.RunPackageOperation(new[] { package }, handleDependencies);
                if (DialogResult == DialogResult.OK)
                {
                    Close();
                }
            }
        }

        private void saveFileDialog_FileOk(object sender, CancelEventArgs e)
        {
            if (File.Exists(saveFileDialog.FileName))
            {
                var message = string.Format(Resources.SaveFolderExists, Path.GetFileName(saveFileDialog.FileName));
                MessageBox.Show(message, Resources.SaveFolderExistsCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.Cancel = true;
            }
        }

        void packageManagerProxy_PackageInstalling(object sender, PackageOperationEventArgs e)
        {
            var package = e.Package;
            var workflowPath = package.Id + Constants.BonsaiExtension;
            if (package.GetContentFiles().Any(file => file.EffectivePath == workflowPath))
            {
                Invoke((Action)(() =>
                {
                    var message = string.Format(Resources.InstallGalleryPackageWarning, package.Id);
                    var result = MessageBox.Show(this, message, Resources.InstallGalleryPackageCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    if (result == DialogResult.Yes)
                    {
                        saveFileDialog.FileName = package.Id;
                        if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            var targetPath = saveFileDialog.FileName;
                            var targetFileSystem = new PhysicalFileSystem(targetPath);
                            foreach (var file in package.GetContentFiles())
                            {
                                using (var stream = file.GetStream())
                                {
                                    targetFileSystem.AddFile(file.EffectivePath, stream);
                                }
                            }

                            var manifest = Manifest.Create(package);
                            var metadata = Manifest.Create(manifest.Metadata);
                            var metadataPath = package.Id + global::NuGet.Constants.ManifestExtension;
                            using (var stream = targetFileSystem.CreateFile(metadataPath))
                            {
                                metadata.Save(stream);
                            }

                            InstallPath = targetFileSystem.GetFullPath(workflowPath);
                            DialogResult = DialogResult.OK;
                        }
                    }
                }));
            }
        }

        private void repositoriesView_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
        {
            collapsingNode = e.Node;
        }

        private void repositoriesView_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            collapsingNode = null;
            if (repositoriesView.SelectedNode == e.Node && selectingNode == null)
            {
                repositoriesView.SelectedNode = null;
                packageViewController.SetPackageViewStatus(Resources.NoItemsFoundLabel);
                packageViewController.ClearActiveRequests();
            }
        }

        private void repositoriesView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (repositoriesView.SelectedNode != e.Node)
            {
                repositoriesView.SelectedNode = e.Node;
            }
        }

        private void repositoriesView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            selectingNode = null;
            packageManagerProxy.PackageManager = e.Node.Tag as PackageManager;
            if (packageManagerProxy.PackageManager == null) return;
            if (e.Node == installedPackagesNode || e.Node.Parent == installedPackagesNode)
            {
                releaseFilterComboBox.Visible = false;
                packageView.OperationText = Resources.UninstallOperationName;
                packageViewController.SelectedRepository = packageManagerProxy.LocalRepository;
            }
            else
            {
                releaseFilterComboBox.Visible = true;
                packageViewController.SelectedRepository = packageManagerProxy.SourceRepository;
                if (e.Node == updatesNode || e.Node.Parent == updatesNode)
                {
                    packageView.OperationText = Resources.UpdateOperationName;
                }
                else packageView.OperationText = Resources.InstallOperationName;
            }

            searchComboBox.Text = string.Empty;
            packageViewController.UpdatePackageFeed();
        }

        private void repositoriesView_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            selectingNode = node;
            if (node != collapsingNode && node.Parent == null)
            {
                e.Cancel = true;
                var selectedNode = repositoriesView.SelectedNode;
                if (selectedNode != null && selectedNode.Parent != null) selectedNode = selectedNode.Parent;
                if (selectedNode != null) selectedNode.Collapse();

                node.Expand();
                var selectedChild = e.Node.Nodes[0];
                repositoriesView.SelectedNode = selectedChild;
            }
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            Hide();
            if (packageViewController.ShowPackageSourceConfigurationDialog() == DialogResult.OK)
            {
                InitializeRepositoryViewNodes();
                SelectDefaultNode();
            }
            Show();
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
