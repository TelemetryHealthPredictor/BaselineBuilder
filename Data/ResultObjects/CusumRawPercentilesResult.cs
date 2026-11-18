using System;
using System.Collections.Generic;
using System.Text;

namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class CusumRawPercentilesResult
    {
        public required double[] Percentiles { get; set; }
        public required double[] Thresholds { get; set; }
    }
}
