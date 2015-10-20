﻿using Bonsai.Design;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace Bonsai.Shaders.Design
{
    class ShaderConfigurationCollectionEditor : DescriptiveCollectionEditor
    {
        CollectionForm collectionForm;

        public ShaderConfigurationCollectionEditor(Type type)
            : base(typeof(ShaderConfigurationCollection))
        {
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var settings = value as ShaderWindowSettings;
            if (settings != null)
            {
                value = settings.Shaders;
            }

            return base.EditValue(context, provider, value);
        }

        protected override CollectionEditor.CollectionForm CreateCollectionForm()
        {
            collectionForm = base.CreateCollectionForm();
            collectionForm.Shown += delegate { ActivateSelection(collectionForm); };
            return collectionForm;
        }

        void ActivateSelection(Control control)
        {
            var listBox = control as ListBox;
            if (listBox != null)
            {
                listBox.KeyDown += listBox_KeyDown;
            }

            foreach (Control child in control.Controls)
            {
                ActivateSelection(child);
            }
        }

        void StoreShaderConfiguration(ListBox listBox)
        {
            var settings = new ShaderConfigurationCollection();
            foreach (var item in listBox.SelectedItems)
            {
                var property = item.GetType().GetProperty("Value");
                if (property != null)
                {
                    var shaderConfiguration = (ShaderConfiguration)property.GetValue(item);
                    settings.Add(shaderConfiguration);
                }
            }

            if (settings.Count > 0)
            {
                var stringBuilder = new StringBuilder();
                using (var writer = XmlWriter.Create(stringBuilder, new XmlWriterSettings { Indent = true }))
                {
                    var serializer = new XmlSerializer(typeof(ShaderConfigurationCollection));
                    serializer.Serialize(writer, settings);
                }

                Clipboard.SetText(stringBuilder.ToString());
            }
        }

        void RetrieveShaderConfiguration(ListBox listBox)
        {
            MethodInfo methodInfo = collectionForm.GetType().GetMethod("AddItems", BindingFlags.NonPublic | BindingFlags.Instance);
            if (methodInfo == null) return;

            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                var stringReader = new StringReader(text);
                using (var reader = XmlReader.Create(stringReader))
                {
                    try
                    {
                        var serializer = new XmlSerializer(typeof(ShaderConfigurationCollection));
                        if (serializer.CanDeserialize(reader))
                        {
                            var settings = (ShaderConfigurationCollection)serializer.Deserialize(reader);
                            try { methodInfo.Invoke(collectionForm, new[] { settings }); }
                            catch (TargetInvocationException ex)
                            {
                                if (ex.InnerException is ArgumentException)
                                {
                                    MessageBox.Show(
                                        collectionForm,
                                        "An item with the same key has already been added.",
                                        "Error",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
                                }
                                else throw;
                            }
                        }
                    }
                    catch (XmlException) { }
                    catch (InvalidOperationException) { }
                }
            }
        }

        void listBox_KeyDown(object sender, KeyEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (e.KeyCode == Keys.C && e.Modifiers == Keys.Control)
            {
                StoreShaderConfiguration(listBox);
            }

            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                RetrieveShaderConfiguration(listBox);
            }
        }

        protected override Type[] CreateNewItemTypes()
        {
            return new[]
            {
                typeof(ShaderConfiguration),
                typeof(PointSprite),
                typeof(TexturedQuad),
                typeof(TexturedModel)
            };
        }

        protected override object CreateInstance(Type itemType)
        {
            var instance = (ShaderConfiguration)base.CreateInstance(itemType);
            if (itemType == typeof(PointSprite))
            {
                instance.RenderState.Add(new EnableState { Capability = EnableCap.Blend });
                instance.RenderState.Add(new BlendFunctionState());
                instance.RenderState.Add(new EnableState { Capability = EnableCap.PointSprite });
                instance.RenderState.Add(new PointSizeState { Size = 10 });
                instance.TextureUnits.Add(new ImageTexture
                {
                    Name = "tex"
                });
            }

            if (itemType == typeof(TexturedQuad) || itemType == typeof(TexturedModel))
            {
                instance.TextureUnits.Add(new Texture2D
                {
                    Name = "tex"
                });
            }

            if (itemType == typeof(TexturedModel))
            {
                instance.RenderState.Add(new EnableState { Capability = EnableCap.DepthTest });
                instance.RenderState.Add(new DepthFunctionState { Function = DepthFunction.Less });
            }

            return instance;
        }
    }
}