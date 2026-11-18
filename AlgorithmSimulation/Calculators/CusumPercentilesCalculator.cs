using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Data.Result_Objects;
using StatsLib;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.AlgorithmSimulation.Calculators
{
    internal class CusumPercentilesCalculator
    {
        private const int MinimumSamples = 1_000;

        private const double Quantile = 0.99;

        private TDigest[]? _digests;

        private long _count;

        public void RecalculatePercentiles(double[] cusums)
        {
            if (_digests is null)
            {
                _digests = new TDigest[cusums.Length];

                for (int i = 0; i < _digests.Length; i++)
                {
                    _digests[i] = new TDigest();
                }
            }
            else if (cusums.Length != _digests.Length)
            {
                throw new ArgumentException(
                    $"Expected {_digests.Length} values but got {cusums.Length}.", nameof(cusums));
            }

            for (int i = 0; i < cusums.Length; i++)
            {
                _digests[i].Add(cusums[i]);
            }

            _count++;
        }

        public CusumsPercentilesResult GetPercentileValues()
        {
            if (_digests is null || _count < MinimumSamples)
            {
                throw new InvalidOperationException(
                    $"Not enough data: {_count} samples, at least {MinimumSamples} required.");
            }

            double[] thresholds = new double[_digests.Length];

            for (int i = 0; i < thresholds.Length; i++)
            {
                thresholds[i] = _digests[i].Quantile(Quantile);
            }

            return new CusumsPercentilesResult
            {
                ThresholdsForCusums = thresholds
            };
        }
    }
}
