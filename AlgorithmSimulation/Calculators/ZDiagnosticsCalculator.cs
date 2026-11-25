using AlgoLib.Data.Result_Objects;
using ThpUtils.Naming;

namespace BaselineBuilderNew.AlgorithmSimulation.Calculators
{
    /// <summary>
    /// Logging only: per-parameter z-score mean, std and mean NIS over all flights, for the profile sanity checks
    /// (on normal flights, mean ≈ 0, std ≈ 1, mean NIS ≈ 1). Nothing it computes goes into the profile.
    /// Only the z-scores refreshed in a frame are counted, so stale values aren't repeated.
    /// </summary>
    internal class ZDiagnosticsCalculator
    {
        private readonly string[] _parameterNames;

        // Welford's online algorithm for mean and variance, plus a plain sum of z² for NIS
        private readonly long[] _counts;
        private readonly double[] _means;
        private readonly double[] _m2;
        private readonly double[] _sumOfSquares;

        public ZDiagnosticsCalculator(NamingData namingData)
        {
            _parameterNames = namingData.ParameterNames;

            _counts = new long[_parameterNames.Length];
            _means = new double[_parameterNames.Length];
            _m2 = new double[_parameterNames.Length];
            _sumOfSquares = new double[_parameterNames.Length];
        }

        public void AddZScores(KalmanFilterResult result)
        {
            foreach (int index in result.IndicesMeasured)
            {
                double z = result.ParameterZScores[index];

                _counts[index]++;
                double delta = z - _means[index];
                _means[index] += delta / _counts[index];
                _m2[index] += delta * (z - _means[index]);

                // NIS of a scalar filter = y²/S = z²
                _sumOfSquares[index] += z * z;
            }
        }

        public void LogDiagnostics()
        {
            Console.WriteLine("Z-score diagnostics (targets on normal flights: mean ≈ 0, std ≈ 1, NIS ≈ 1):");
            Console.WriteLine($"  {"Parameter",-12} {"n",10} {"mean",10} {"std",10} {"NIS",10}");

            for (int i = 0; i < _parameterNames.Length; i++)
            {
                long count = _counts[i];
                double mean = count > 0 ? _means[i] : double.NaN;
                double stdDev = count > 1 ? Math.Sqrt(_m2[i] / (count - 1)) : double.NaN; // sample standard deviation
                double meanNis = count > 0 ? _sumOfSquares[i] / count : double.NaN;

                Console.WriteLine($"  {_parameterNames[i],-12} {count,10} {mean,10:F4} {stdDev,10:F4} {meanNis,10:F4}");
            }
        }
    }
}
