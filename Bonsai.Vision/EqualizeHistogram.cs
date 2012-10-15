﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;

namespace Bonsai.Vision
{
    public class EqualizeHistogram : Transform<IplImage, IplImage>
    {
        public override IplImage Process(IplImage input)
        {
            var output = new IplImage(input.Size, input.Depth, input.NumChannels);
            ImgProc.cvEqualizeHist(input, output);
            return output;
        }
    }
}
