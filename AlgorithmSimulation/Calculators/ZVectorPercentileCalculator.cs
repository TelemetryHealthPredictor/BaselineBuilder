using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.ResultObjects;
using MathNet.Numerics.LinearAlgebra;
using StatsLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Calculators
{
    /// <summary>
    /// Finds the cut-off used to trim extreme z-vectors before the covariance is estimated.
    /// Σ isn't known yet, so the distance uses the identity matrix in its place: d² = zᵀ I z = ‖z‖².
    /// </summary>
    internal class ZVectorPercentileCalculator
    {
        private const int MinimumSamples = 10_000;

        // Keep the lowest 98% of frames, trim the top 2%
        private const double Quantile = 0.98;

        private readonly TDigest _digest = new();

        private long _count;

        /// <summary>
        /// The squared distance shared by both sweeps, so the cut-off and the trim compare the same number.
        /// </summary>
        public static double IdentityDistanceSquared(Vector<double> z)
        {
            return z.DotProduct(z);
        }

        public void AddZVector(KalmanFilterResult result)
        {
            _digest.Add(IdentityDistanceSquared(result.ParameterZScores));
            _count++;
        }

        public ZVectorPercentileResult GetResult()
        {
            if (_count < MinimumSamples)
            {
                throw new InvalidOperationException(
                    $"Not enough data: {_count} samples, at least {MinimumSamples} required.");
            }

            return new ZVectorPercentileResult
            {
                Threshold = _digest.Quantile(Quantile)
            };
        }
    }
}
