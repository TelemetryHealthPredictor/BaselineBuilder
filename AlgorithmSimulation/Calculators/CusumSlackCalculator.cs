using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.BaselineData;
using BaselineBuilderNew.Data.ResultObjects;
using ThpUtils.Naming;

namespace BaselineBuilderNew.AlgorithmSimulation.Calculators
{
    /// <summary>
    /// Computes each parameter's CUSUM slack k = drift size × std of that parameter's z-scores.
    /// Only the z-scores refreshed in a frame are counted, because those are the only ones CUSUM consumes.
    /// </summary>
    internal class CusumSlackCalculator
    {
        private const int MinimumSamplesPerParameter = 1_000;

        private readonly string[] _parameterNames;
        private readonly double _driftSize;

        // Welford's online algorithm, one running mean/variance per parameter
        private readonly long[] _counts;
        private readonly double[] _means;
        private readonly double[] _m2;

        public CusumSlackCalculator(NamingData namingData, CusumData cusumData)
        {
            _parameterNames = namingData.ParameterNames;
            _driftSize = cusumData.DriftSize;

            _counts = new long[_parameterNames.Length];
            _means = new double[_parameterNames.Length];
            _m2 = new double[_parameterNames.Length];
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
            }
        }

        public CusumSlacksResult GetResult()
        {
            double[] slacks = new double[_parameterNames.Length];

            for (int i = 0; i < slacks.Length; i++)
            {
                if (_counts[i] < MinimumSamplesPerParameter)
                {
                    throw new InvalidOperationException(
                        $"Not enough z-scores for '{_parameterNames[i]}': {_counts[i]}, at least {MinimumSamplesPerParameter} required.");
                }

                double stdDev = Math.Sqrt(_m2[i] / (_counts[i] - 1)); // sample standard deviation
                slacks[i] = _driftSize * stdDev;
            }

            return new CusumSlacksResult
            {
                Slacks = slacks
            };
        }
    }
}
