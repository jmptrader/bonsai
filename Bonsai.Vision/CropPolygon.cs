﻿using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Vision
{
    [Description("Crops a non-rectangular region of interest bounded by a set of polygonal contours.")]
    public class CropPolygon : Transform<IplImage, IplImage>
    {
        bool cropOutput = true;

        public CropPolygon()
            : this(true)
        {
        }

        internal CropPolygon(bool crop)
        {
            cropOutput = crop;
            MaskType = ThresholdTypes.ToZero;
        }

        [Description("The polygonal contours bounding the region of interest.")]
        [Editor("Bonsai.Vision.Design.IplImageInputRoiEditor, Bonsai.Vision.Design", typeof(UITypeEditor))]
        public Point[][] Regions { get; set; }

        [TypeConverter(typeof(ThresholdTypeConverter))]
        [Description("The type of mask operation to apply on the region of interest.")]
        public ThresholdTypes MaskType { get; set; }

        [Description("The value to which all pixels that are not in the selected region will be set to.")]
        public Scalar FillValue { get; set; }

        static Rect ClipRectangle(Rect rect, Size clipSize)
        {
            var clipX = rect.X < 0 ? -rect.X : 0;
            var clipY = rect.Y < 0 ? -rect.Y : 0;
            clipX += Math.Max(0, rect.X + rect.Width - clipSize.Width);
            clipY += Math.Max(0, rect.Y + rect.Height - clipSize.Height);

            rect.X = Math.Max(0, rect.X);
            rect.Y = Math.Max(0, rect.Y);
            rect.Width = rect.Width - clipX;
            rect.Height = rect.Height - clipY;
            return rect;
        }

        public override IObservable<IplImage> Process(IObservable<IplImage> source)
        {
            return Observable.Defer(() =>
            {
                var mask = default(IplImage);
                var boundingBox = default(Rect);
                var currentRegions = default(Point[][]);

                return source.Select(input =>
                {
                    if (Regions != currentRegions)
                    {
                        currentRegions = Regions;
                        if (currentRegions != null)
                        {
                            mask = new IplImage(input.Size, IplDepth.U8, 1);
                            mask.SetZero();

                            var points = currentRegions
                                .SelectMany(region => region)
                                .SelectMany(point => new[] { point.X, point.Y })
                                .ToArray();
                            using (var mat = new Mat(1, points.Length / 2, Depth.S32, 2))
                            {
                                Marshal.Copy(points, 0, mat.Data, points.Length);
                                boundingBox = CV.BoundingRect(mat);
                                boundingBox = ClipRectangle(boundingBox, input.Size);
                            }

                            CV.FillPoly(mask, currentRegions, Scalar.All(255));
                            if (cropOutput) mask = mask.GetSubRect(boundingBox);
                        }
                        else mask = null;
                    }

                    if (currentRegions != null)
                    {
                        var selectionType = MaskType;
                        var template = selectionType <= ThresholdTypes.BinaryInv ? mask : input;

                        var output = new IplImage(mask.Size, template.Depth, template.Channels);
                        var inputRoi = cropOutput && selectionType > ThresholdTypes.BinaryInv ? input.GetSubRect(boundingBox) : input;
                        try
                        {
                            switch (selectionType)
                            {
                                case ThresholdTypes.Binary:
                                    output.SetZero();
                                    CV.Copy(mask, output);
                                    break;
                                case ThresholdTypes.BinaryInv:
                                    output.Set(Scalar.All(255));
                                    output.Set(Scalar.All(0), mask);
                                    break;
                                case ThresholdTypes.ToZeroInv:
                                    var fillRoi = cropOutput ? inputRoi : input;
                                    CV.Copy(fillRoi, output);
                                    output.Set(FillValue, mask);
                                    break;
                                case ThresholdTypes.ToZero:
                                    output.Set(FillValue);
                                    CV.Copy(inputRoi, output, mask);
                                    break;
                                default:
                                    throw new InvalidOperationException("Selection operation is not supported.");
                            }
                        }
                        finally
                        {
                            if (inputRoi != input) inputRoi.Close();
                        }

                        return output;
                    }

                    return input;
                });
            });
        }

        class ThresholdTypeConverter : EnumConverter
        {
            public ThresholdTypeConverter(Type type)
                : base(type)
            {
            }

            public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(
                    base.GetStandardValues(context)
                    .Cast<ThresholdTypes>()
                    .Where(type => type != ThresholdTypes.Truncate &&
                                   type != ThresholdTypes.Otsu)
                    .ToArray());
            }
        }
    }
}