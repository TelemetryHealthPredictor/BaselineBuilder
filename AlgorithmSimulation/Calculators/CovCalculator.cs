using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.AlgorithmSimulation.Calculators;
using MathNet.Numerics.LinearAlgebra;

internal class CovCalculator
{
    private readonly int _dimension;

    // z-vectors whose ‖z‖² is above this cut-off are left out of the covariance
    private double? _threshold;
    private long _trimmedCount;

    private long _count;
    private readonly double[] _mean;
    private readonly double[] _delta;
    private readonly double[,] _coMoment;   // sum of (xi - meanI)(xj - meanJ), stored at [i, j], i <= j

    public CovCalculator(int dimension)
    {
        _dimension = dimension;
        _mean = new double[dimension];
        _delta = new double[dimension];
        _coMoment = new double[dimension, dimension];
    }

    public void Init(ZVectorPercentileResult cutOff)
    {
        _threshold = cutOff.Threshold;
    }

    public void RecalculateCovariance(KalmanFilterResult result)
    {
        if (_threshold is null)
        {
            throw new InvalidOperationException($"{nameof(Init)} must be called first.");
        }

        Vector<double> z = result.ParameterZScores;

        if (ZVectorPercentileCalculator.IdentityDistanceSquared(z) > _threshold)
        {
            _trimmedCount++;
            return;
        }

        _count++;

        for (int i = 0; i < _dimension; i++)
        {
            _delta[i] = z[i] - _mean[i];
            _mean[i] += _delta[i] / _count;
        }

        for (int i = 0; i < _dimension; i++)
        {
            for (int j = i; j < _dimension; j++)
            {
                _coMoment[i, j] += _delta[i] * (z[j] - _mean[j]);
            }
        }
    }

    public CovResult GetCovarianceMatrix()
    {
        long total = _count + _trimmedCount;
        double trimmedPercent = total > 0 ? 100.0 * _trimmedCount / total : 0.0;
        Console.WriteLine($"[CovCalculator] Kept {_count} z-vectors, trimmed {_trimmedCount} ({trimmedPercent:F2}%).");

        Matrix<double> cov = Matrix<double>.Build.Dense(_dimension, _dimension);

        for (int i = 0; i < _dimension; i++)
        {
            for (int j = i; j < _dimension; j++)
            {
                double value = _count >= 2 ? _coMoment[i, j] / (_count - 1) : 0.0;

                cov[i, j] = value;
                cov[j, i] = value;
            }
        }

        for (int i = 0; i < _dimension; i++)
        {
            double variance = cov[i, i];

            bool isProblematic = _count < 2
                || variance == 0.0
                || double.IsNaN(variance)
                || double.IsInfinity(variance);

            if (!isProblematic)
                continue;

            for (int k = 0; k < _dimension; k++)
            {
                cov[i, k] = 0.0;
                cov[k, i] = 0.0;
            }

            cov[i, i] = 1.0;
        }

        return new CovResult { MDCovarianceMatrix = cov };
    }
}