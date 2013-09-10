﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel.Design;
using Bonsai.Expressions;
using Bonsai.Dag;

namespace Bonsai.Design
{
    public class WorkflowEditorLauncher : DialogLauncher
    {
        ExpressionBuilderGraph workflow;
        WorkflowViewModel viewModel;
        Node<ExpressionBuilder, ExpressionBuilderParameter> builderNode;

        public WorkflowEditorLauncher(ExpressionBuilderGraph workflow, Node<ExpressionBuilder, ExpressionBuilderParameter> builderNode)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException("workflow");
            }

            if (builderNode == null)
            {
                throw new ArgumentNullException("builderNode");
            }

            this.workflow = workflow;
            this.builderNode = builderNode;
        }

        public VisualizerLayout VisualizerLayout { get; set; }

        public WorkflowViewModel ViewModel
        {
            get { return viewModel; }
        }

        public void UpdateEditorLayout()
        {
            if (viewModel != null)
            {
                viewModel.UpdateVisualizerLayout();
                VisualizerLayout = viewModel.VisualizerLayout;
                if (VisualizerDialog != null)
                {
                    Bounds = VisualizerDialog.DesktopBounds;
                }
            }
        }

        protected override void InitializeComponents(TypeVisualizerDialog visualizerDialog, IServiceProvider provider)
        {
            var workflowExpressionBuilder = builderNode.Value as WorkflowExpressionBuilder;
            visualizerDialog.Activated += delegate
            {
                if (workflowExpressionBuilder != null && !string.IsNullOrWhiteSpace(workflowExpressionBuilder.Name))
                {
                    visualizerDialog.Text = workflowExpressionBuilder.Name;
                }
                else visualizerDialog.Text = "Workflow Editor";
            };

            visualizerDialog.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    UpdateEditorLayout();
                }
            };

            var graphView = new GraphView { AllowDrop = true, Dock = DockStyle.Fill, Size = new Size(300, 200) };
            visualizerDialog.Padding = new Padding(10);
            visualizerDialog.AddControl(graphView);

            viewModel = new WorkflowViewModel(graphView, provider);
            viewModel.VisualizerLayout = VisualizerLayout;
            viewModel.Workflow = workflowExpressionBuilder.Workflow;
        }
    }
}
