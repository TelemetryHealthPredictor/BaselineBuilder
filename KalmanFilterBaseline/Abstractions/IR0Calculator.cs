using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.KalmanFilterBaseline.Abstractions
{
    internal interface IR0Calculator
    {
        public Task<R0Result> CalculateR0();
    }
}
