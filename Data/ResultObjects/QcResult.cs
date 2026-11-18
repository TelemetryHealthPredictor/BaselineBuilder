using System;
using System.Collections.Generic;
using System.Text;
using MathNet.Numerics.LinearAlgebra;

namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class QcResult
    {
        public required Vector<double> QcVector { get; set; }
    }
}
