﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Expressions
{
    class WorkflowTypeDescriptor : CustomTypeDescriptor
    {
        AttributeCollection attributes;
        ExpressionBuilderGraph workflow;
        static readonly Attribute[] EmptyAttributes = new Attribute[0];
        static readonly PropertyDescriptor[] EmptyProperties = new PropertyDescriptor[0];
        static readonly Attribute[] ExternalizableAttributes = new Attribute[] { BrowsableAttribute.Yes, ExternalizableAttribute.Default };

        public WorkflowTypeDescriptor(object instance, params Attribute[] attrs)
        {
            attributes = new AttributeCollection(attrs ?? EmptyAttributes);
            var builder = instance as IWorkflowExpressionBuilder;
            if (builder != null)
            {
                workflow = builder.Workflow;
            }
            else workflow = (ExpressionBuilderGraph)instance;
        }

        public override AttributeCollection GetAttributes()
        {
            return attributes;
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(EmptyAttributes);
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            if (workflow == null) return base.GetProperties(attributes);
            var properties = from node in workflow
                             let property = ExpressionBuilder.Unwrap(node.Value) as ExternalizedProperty
                             where property != null
                             let name = property.Name
                             where !string.IsNullOrEmpty(name)
                             let targetComponents = node.Successors.Select(edge => ExpressionBuilder.GetWorkflowElement(edge.Target.Value)).ToArray()
                             let aggregateProperties = GetAggregateProperties(property, targetComponents)
                             where aggregateProperties.Length > 0
                             select new ExternalizedPropertyDescriptor(property, aggregateProperties, targetComponents);
            return new PropertyDescriptorCollection(properties.ToArray());
        }

        static PropertyDescriptor[] GetAggregateProperties(ExternalizedProperty property, object[] components)
        {
            var propertyType = default(Type);
            var properties = new PropertyDescriptor[components.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                var descriptor = TypeDescriptor.GetProperties(components[i], ExternalizableAttributes)[property.MemberName];
                if (descriptor == null) return EmptyProperties;

                if (propertyType == null)
                {
                    propertyType = descriptor.PropertyType;
                }
                else if (descriptor.PropertyType != propertyType)
                {
                    return EmptyProperties;
                }

                properties[i] = descriptor;
            }

            return properties;
        }
    }
}
