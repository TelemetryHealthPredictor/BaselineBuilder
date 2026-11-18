using System;
using System.Collections.Generic;
using System.Text;
using MongoLib;

namespace BaselineBuilderNew.Data.MongoOutputs
{
    internal class NamedSample
    {
        public string Name { get; set; }
        public TelemetrySample Sample { get; set; }

        public NamedSample(string name, TelemetrySample sample)
        {
            Name = name;
            Sample = sample;
        }
    }
}
