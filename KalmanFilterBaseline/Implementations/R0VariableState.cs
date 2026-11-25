using System;
using BaselineBuilderNew.Data.ResultObjects;
using MongoLib;
using StatsLib;

namespace BaselineBuilderNew.KalmanFilterBaseline.Implementations
{
    internal class R0VariableState
    {
        private readonly SampleWindow _window = new();

        // null = pass 1 (collect window stds), set = pass 2 (average quiet variances)
        private readonly double? _quietStdThreshold;

        private double? _lastValue;

        public TDigest StdDigest { get; } = new();

        public double MinStep { get; private set; } = double.PositiveInfinity;

        public double VarianceMean { get; private set; }

        public int VarianceCount { get; private set; }


        public R0VariableState()
        {
        }

        public R0VariableState(double quietStdThreshold)
        {
            _quietStdThreshold = quietStdThreshold;
        }


        public void Add(TelemetrySample sample)
        {
            if (!double.IsFinite(sample.Value))
            {
                return;
            }

            if (_quietStdThreshold is null)
            {
                TrackStep(sample.Value);
            }

            WindowStats? stats = _window.Add(sample.Timestamp, sample.Value);

            if (stats is null)
            {
                return;
            }

            if (_quietStdThreshold is null)
            {
                StdDigest.Add(stats.Value.Std);
            }
            else if (stats.Value.Std <= _quietStdThreshold.Value)
            {
                VarianceCount++;
                VarianceMean += (stats.Value.Variance - VarianceMean) / VarianceCount;
            }
        }

        private void TrackStep(double value)
        {
            if (_lastValue is double last)
            {
                double step = Math.Abs(value - last);

                if (step > 0.0 && step < MinStep)
                {
                    MinStep = step;
                }
            }

            _lastValue = value;
        }
    }
}
