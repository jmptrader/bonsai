﻿using Bonsai.Resources;
using Bonsai.Shaders.Configuration;
using System.Collections.Generic;
using System.ComponentModel;

namespace Bonsai.Shaders
{
    [DefaultProperty(nameof(Textures))]
    [Description("Creates a collection of texture resources to be loaded into the resource manager.")]
    public class TextureResources : ResourceLoader
    {
        readonly TextureConfigurationCollection textures = new TextureConfigurationCollection();

        [Editor("Bonsai.Shaders.Configuration.Design.TextureConfigurationCollectionEditor, Bonsai.Shaders.Design", DesignTypes.UITypeEditor)]
        [Description("The collection of texture resources to be loaded into the resource manager.")]
        public TextureConfigurationCollection Textures
        {
            get { return textures; }
        }

        protected override IEnumerable<IResourceConfiguration> GetResources()
        {
            return textures;
        }
    }
}
