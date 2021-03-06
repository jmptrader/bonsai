﻿using OpenCV.Net;
using OpenTK.Graphics.OpenGL4;
using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Shaders
{
    [Description("Updates the pixel store of the specified texture target.")]
    public class UpdateTexture : Sink<IplImage>
    {
        public UpdateTexture()
        {
            TextureTarget = TextureTarget.Texture2D;
            InternalFormat = PixelInternalFormat.Rgba;
        }

        [TypeConverter(typeof(TextureNameConverter))]
        [Description("The name of the texture to update.")]
        public string TextureName { get; set; }

        [Description("The texture target to update.")]
        public TextureTarget TextureTarget { get; set; }

        [Description("The internal storage format of the texture target.")]
        public PixelInternalFormat InternalFormat { get; set; }

        public override IObservable<IplImage> Process(IObservable<IplImage> source)
        {
            return Observable.Create<IplImage>(observer =>
            {
                var name = TextureName;
                var texture = default(Texture);
                var textureSize = default(Size);
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidOperationException("A texture name must be specified.");
                }

                return source.CombineEither(
                    ShaderManager.WindowSource.Do(window =>
                    {
                        window.Update(() =>
                        {
                            try { texture = window.ResourceManager.Load<Texture>(name); }
                            catch (Exception ex) { observer.OnError(ex); }
                        });
                    }),
                    (input, window) =>
                    {
                        window.Update(() =>
                        {
                            var target = TextureTarget;
                            if (target > TextureTarget.TextureBindingCubeMap && target < TextureTarget.ProxyTextureCubeMap)
                            {
                                GL.BindTexture(TextureTarget.TextureCubeMap, texture.Id);
                            }
                            else GL.BindTexture(target, texture.Id);
                            var internalFormat = textureSize != input.Size ? InternalFormat : (PixelInternalFormat?)null;
                            TextureHelper.UpdateTexture(target, internalFormat, input);
                            textureSize = input.Size;
                        });
                        return input;
                    }).SubscribeSafe(observer);
            });
        }
    }
}
