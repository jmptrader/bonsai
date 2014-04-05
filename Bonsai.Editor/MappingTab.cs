﻿using Bonsai.Design;
using Bonsai.Editor.Properties;
using Bonsai.Expressions;
using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Windows.Forms.Design;

namespace Bonsai.Editor
{
    class MappingTab : PropertyTab
    {
        public override PropertyDescriptorCollection GetProperties(object component)
        {
            return new PropertyDescriptorCollection(new PropertyDescriptor[0]);
        }

        public override PropertyDescriptorCollection GetProperties(object component, Attribute[] attributes)
        {
            return new PropertyDescriptorCollection(new PropertyDescriptor[0]);
        }

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object component, Attribute[] attributes)
        {
            var properties = new List<PropertyDescriptor>();
            var selectionModel = (WorkflowSelectionModel)context.GetService(typeof(WorkflowSelectionModel));
            if (selectionModel != null)
            {
                var builder = selectionModel.SelectedNodes
                                            .Select(node => ExpressionBuilder.Unwrap((ExpressionBuilder)node.Value))
                                            .SingleOrDefault();

                if (builder != null)
                {
                    var builderProperties = TypeDescriptor.GetProperties(builder);

                    var propertyMappingBuilder = builder as IPropertyMappingBuilder;
                    if (propertyMappingBuilder != null)
                    {
                        using (var provider = new CSharpCodeProvider())
                        {
                            var propertyMappings = propertyMappingBuilder.PropertyMappings;
                            var componentProperties = TypeDescriptor.GetProperties(component);
                            foreach (var descriptor in componentProperties.Cast<PropertyDescriptor>()
                                                                          .Where(descriptor => descriptor.IsBrowsable &&
                                                                                               !descriptor.IsReadOnly))
                            {
                                var propertyAttributes = new Attribute[]
                                {
                                    new EditorAttribute(typeof(MemberSelectorEditor), typeof(UITypeEditor)),
                                    new CategoryAttribute("Property"),
                                    new DescriptionAttribute(descriptor.Description),
                                    new TypeConverterAttribute(typeof(MappingConverter))
                                };

                                var typeRef = new CodeTypeReference(descriptor.PropertyType);
                                properties.Add(new PropertyMappingDescriptor(
                                    descriptor.Name + string.Format(" ({0})", provider.GetTypeOutput(typeRef)),
                                    descriptor,
                                    propertyMappings,
                                    propertyAttributes));
                            }
                        }
                    }
                }
            }

            return new PropertyDescriptorCollection(properties.ToArray());
        }

        public override string TabName
        {
            get { return "Mappings"; }
        }

        public override Bitmap Bitmap
        {
            get
            {
                return Resources.PropertyMappingIcon;
            }
        }

        class MappingConverter : StringConverter
        {
            public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
            {
                if (value == null) return "(unmapped)";
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }

        class PropertyMappingDescriptor : PropertyDescriptor
        {
            MemberDescriptor member;
            PropertyMappingCollection owner;

            public PropertyMappingDescriptor(string name, MemberDescriptor descriptor, PropertyMappingCollection propertyMappings, Attribute[] attributes)
                : base(name, attributes)
            {
                member = descriptor;
                owner = propertyMappings;
            }

            public override bool CanResetValue(object component)
            {
                return ShouldSerializeValue(component);
            }

            public override Type ComponentType
            {
                get { return typeof(ExpressionBuilder); }
            }

            public override object GetValue(object component)
            {
                if (owner.Contains(member.Name))
                {
                    return owner[member.Name].Selector;
                }

                return null;
            }

            public override bool IsReadOnly
            {
                get { return false; }
            }

            public override Type PropertyType
            {
                get { return typeof(string); }
            }

            public override void ResetValue(object component)
            {
                owner.Remove(member.Name);
            }

            public override void SetValue(object component, object value)
            {
                PropertyMapping mapping;
                if (owner.Contains(member.Name))
                {
                    owner.Remove(member.Name);
                }

                mapping = new PropertyMapping(member.Name, (string)value);
                owner.Add(mapping);
            }

            public override bool ShouldSerializeValue(object component)
            {
                return owner.Contains(member.Name);
            }
        }
    }
}
