using System;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.KalmanFilterBaseline.Implementations
{
    internal class SampleWindow
    {
        private static readonly TimeSpan WindowSpan = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(2);
        private const int MinSamples = 8;

        private int _count;
        private double _mean;
        private double _m2;

        private DateTime _firstTimestamp;
        private DateTime _lastTimestamp;


        public WindowStats? Add(DateTime timestamp, double value)
        {
            if (_count > 0 && timestamp - _lastTimestamp > MaxGap)
            {
                Reset();
            }

            if (_count == 0)
            {
                _firstTimestamp = timestamp;
            }

            _lastTimestamp = timestamp;

            _count++;
            double delta = value - _mean;
            _mean += delta / _count;
            _m2 += delta * (value - _mean);

            if (_count < MinSamples || timestamp - _firstTimestamp < WindowSpan)
            {
                return null;
            }

            double variance = Math.Max(0.0, _m2 / (_count - 1));

            Reset();

            return new WindowStats(Math.Sqrt(variance), variance);
        }

        public void Reset()
        {
            _count = 0;
            _mean = 0.0;
            _m2 = 0.0;
        }
    }
}
