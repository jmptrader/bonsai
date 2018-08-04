﻿using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Shaders
{
    [Combinator]
    [WorkflowElementCategory(ElementCategory.Sink)]
    [Description("Binds a texture buffer to the specified texture unit.")]
    public class BindTexture
    {
        public BindTexture()
        {
            TextureSlot = TextureUnit.Texture0;
        }

        [Description("The slot on which to bind the texture.")]
        public TextureUnit TextureSlot { get; set; }

        [TypeConverter(typeof(ShaderNameConverter))]
        [Description("The name of the shader program.")]
        public string ShaderName { get; set; }

        [TypeConverter(typeof(TextureNameConverter))]
        [Description("The optional name of the texture that will be bound to the shader.")]
        public string TextureName { get; set; }

        IObservable<TSource> Process<TSource>(IObservable<TSource> source, Action<int, TSource> update)
        {
            return Observable.Create<TSource>(observer =>
            {
                var textureId = 0;
                var textureName = default(string);
                return source.CombineEither(
                    ShaderManager.ReserveShader(ShaderName),
                    (input, shader) =>
                    {
                        if (textureName != TextureName)
                        {
                            textureName = TextureName;
                            var texture = !string.IsNullOrEmpty(textureName)
                                ? shader.Window.ResourceManager.Load<Texture>(textureName)
                                : null;
                            textureId = texture != null ? texture.Id : 0;
                        }

                        shader.Update(() => update(textureId, input));
                        return input;
                    }).SubscribeSafe(observer);
            });
        }

        public IObservable<TSource> Process<TSource>(IObservable<TSource> source)
        {
            return Process(source, (id, input) =>
            {
                GL.ActiveTexture(TextureSlot);
                GL.BindTexture(TextureTarget.Texture2D, id);
            });
        }

        public IObservable<Texture> Process(IObservable<Texture> source)
        {
            return Process(source, (id, input) =>
            {
                GL.ActiveTexture(TextureSlot);
                GL.BindTexture(TextureTarget.Texture2D, id == 0 && input != null ? input.Id : id);
            });
        }
    }
}
