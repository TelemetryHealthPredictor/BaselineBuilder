using System;
using System.Collections.Generic;
using System.Text;
using MathNet.Numerics.LinearAlgebra;

namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class CovResult
    {
        public required Matrix<double> MDCovarianceMatrix { get; set; }
    }
}
