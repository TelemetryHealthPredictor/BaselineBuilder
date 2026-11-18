using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.KalmanFilterBaseline.Abstractions
{
    internal interface IQcCalculator
    {
        public void Init(R0Result r0);

        public Task<QcResult> CalculateQc();
    }
}
