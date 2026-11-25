using BaselineBuilderNew.Data.ResultObjects;
using ThpUtils.Naming;

namespace BaselineBuilderNew.AlgorithmSimulation.Calculators
{
    /// <summary>
    /// CUSUM threshold h per parameter = the mean, over normal flights, of each flight's
    /// highest CUSUM value for that parameter.
    /// </summary>
    internal class CusumThresholdsCalculator
    {
        // Highest CUSUM value per parameter in the flight currently being simulated
        private readonly double[] _flightMax;

        // Sum of the per-flight maxima per parameter, over all completed flights
        private readonly double[] _maxSums;

        private long _flightCount;

        public CusumThresholdsCalculator(NamingData namingData)
        {
            _flightMax = new double[namingData.ParameterNames.Length];
            _maxSums = new double[namingData.ParameterNames.Length];
        }

        public void RecalculateThresholds(double[] cusums)
        {
            if (cusums.Length != _flightMax.Length)
            {
                throw new ArgumentException(
                    $"Expected {_flightMax.Length} values but got {cusums.Length}.", nameof(cusums));
            }

            for (int i = 0; i < cusums.Length; i++)
            {
                _flightMax[i] = Math.Max(_flightMax[i], cusums[i]);
            }
        }

        /// <summary>
        /// Folds the finished flight's maxima into the sums and starts a new flight.
        /// Must only be called once the flight's frames have fully drained through the pipeline.
        /// A parameter never measured in the flight contributes a maximum of 0.
        /// </summary>
        public void EndFlight()
        {
            for (int i = 0; i < _flightMax.Length; i++)
            {
                _maxSums[i] += _flightMax[i];
            }

            _flightCount++;
            Array.Clear(_flightMax);
        }

        public CusumsPercentilesResult GetPercentileValues()
        {
            if (_flightCount == 0)
            {
                throw new InvalidOperationException("Not enough data: no flight was completed.");
            }

            double[] thresholds = new double[_maxSums.Length];

            for (int i = 0; i < thresholds.Length; i++)
            {
                thresholds[i] = _maxSums[i] / _flightCount;
            }

            return new CusumsPercentilesResult
            {
                ThresholdsForCusums = thresholds
            };
        }
    }
}
