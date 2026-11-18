using BaselineBuilderNew.Data.MongoOutputs;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using MongoLib.Documents;
using ThpUtils.Naming;

namespace BaselineBuilderNew.KalmanFilterBaseline.Implementations
{
    internal class QcCalculator : IQcCalculator
    {
        private readonly IDatabaseReader _reader;
        private readonly NamingData _namingData;

        // R0 diagonal — set by Init(), indexed parallel to _namingData.ParameterNames
        private Vector<double> _r0Diagonal = null!;

        // Per-variable accumulators for computing Var(Δz) and mean(dt)
        private Dictionary<string, List<double>> _deltaZ;   // successive differences of values
        private Dictionary<string, List<double>> _deltaT;   // successive differences of timestamps (seconds)
        private Dictionary<string, double?> _prevValue;
        private Dictionary<string, DateTime?> _prevTimestamp;

        public QcCalculator(IDatabaseReader reader, NamingData namingData)
        {
            _reader = reader;
            _namingData = namingData;

            _deltaZ = [];
            _deltaT = [];
            _prevValue = [];
            _prevTimestamp = [];

            foreach (string fieldName in _namingData.ParameterNames)
            {
                _deltaZ.Add(fieldName, []);
                _deltaT.Add(fieldName, []);
                _prevValue.Add(fieldName, null);
                _prevTimestamp.Add(fieldName, null);
            }
        }

        public void Init(R0Result r0)
        {
            _r0Diagonal = r0.R0DiagonalVector;
        }

        public async Task<QcResult> CalculateQc()
        {
            // Collect all samples via the reader, accumulating Δz and Δt per variable.
            IReadOnlyList<NamedSample> samples = await _reader.GetInitialSamplesAsync();

            foreach (NamedSample sample in samples)
            {
                AccumulateDeltas(sample);
            }

            IReadOnlyList<NamedSample>? next = await _reader.GetNextSamplesAsync();
            while (next is not null)
            {
                foreach (NamedSample sample in next)
                {
                    AccumulateDeltas(sample);
                }

                next = await _reader.GetNextSamplesAsync();
            }

            // Build the Qc vector, one entry per parameter (same ordering as R0).
            // Formula: Qcᵢ ≈ (Var(Δzᵢ) − 2·Rᵢ) / mean(dt)
            List<double> qcValues = [];

            foreach (string variable in _namingData.ParameterNames)
            {
                int r0Index = Array.IndexOf(_namingData.ParameterNames, variable);

                double r0i = _r0Diagonal[r0Index];

                List<double> dz = _deltaZ[variable];
                List<double> dt = _deltaT[variable];

                double varDeltaZ = dz.Count > 1 ? dz.Variance() : 0.0;
                double meanDt = dt.Count > 0 ? dt.Average() : 1.0;   // guard against division by zero

                double qci = (varDeltaZ - 2.0 * r0i) / meanDt;

                // Qc must be non-negative (it represents process noise variance).
                qcValues.Add(Math.Max(0.0, qci));
            }

            return new QcResult
            {
                QcVector = Vector<double>.Build.DenseOfEnumerable(qcValues)
            };
        }

        // ------------------------------------------------------------------ //

        private void AccumulateDeltas(NamedSample sample)
        {
            string name = sample.Name;
            double value = sample.Sample.Value;
            DateTime ts = sample.Sample.Timestamp;

            if (_prevValue[name] is double prev && _prevTimestamp[name] is DateTime prevTs)
            {
                _deltaZ[name].Add(value - prev);
                _deltaT[name].Add((ts - prevTs).TotalSeconds);
            }

            _prevValue[name] = value;
            _prevTimestamp[name] = ts;
        }
    }
}
