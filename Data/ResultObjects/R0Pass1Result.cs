using System;
using System.Collections.Generic;
using System.Text;

namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class R0Pass1Result
    {
        public required Dictionary<string, double> QuietStdThresholds { get; init; }

        public required Dictionary<string, double> MinSteps { get; init; }
    }
}
