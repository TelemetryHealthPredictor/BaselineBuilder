using System;
using System.Collections.Generic;
using System.Text;
using MathNet.Numerics.LinearAlgebra;

namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class R0Result
    {
        public required Vector<double> R0DiagonalVector { get; set; }
    }
}
