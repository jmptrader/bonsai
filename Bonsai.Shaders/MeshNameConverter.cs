﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Shaders
{
    class MeshNameConverter : ResourceNameConverter
    {
        public MeshNameConverter()
            : base(typeof(Mesh))
        {
        }

        public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var values = base.GetStandardValues(context);
            var configurationNames = ShaderManager.LoadConfiguration().Meshes;
            if (configurationNames.Count > 0)
            {
                var meshNames = configurationNames.Select(configuration => configuration.Name);
                if (values != null) meshNames = meshNames.Concat(values.Cast<string>());
                values = new StandardValuesCollection(meshNames.ToArray());
            }

            return values;
        }
    }
}
