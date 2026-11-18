using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.ResultObjects;
using MathNet.Numerics.LinearAlgebra;

internal class CovCalculator
{
    private readonly int _dimension;

    // Pair (i, j), i <= j, stored at [i, j]:
    private readonly long[,] _pairCount;       // frames where both i and j were measured
    private readonly double[,] _meanI;         // mean of field i over those frames
    private readonly double[,] _meanJ;         // mean of field j over those frames
    private readonly double[,] _coMoment;      // sum of (xi - meanI)(xj - meanJ)

    public CovCalculator(int dimension)
    {
        _dimension = dimension;
        _pairCount = new long[dimension, dimension];
        _meanI = new double[dimension, dimension];
        _meanJ = new double[dimension, dimension];
        _coMoment = new double[dimension, dimension];
    }

    public void RecalculateCovariance(KalmanFilterResult result)
    {
        int[] measured = result.IndicesMeasured;
        Vector<double> z = result.ParameterZScores;

        for (int a = 0; a < measured.Length; a++)
        {
            for (int b = a; b < measured.Length; b++)
            {
                int i = Math.Min(measured[a], measured[b]);
                int j = Math.Max(measured[a], measured[b]);

                double xi = z[i];
                double xj = z[j];

                _pairCount[i, j]++;
                long n = _pairCount[i, j];

                double deltaI = xi - _meanI[i, j];
                _meanI[i, j] += deltaI / n;

                double deltaJ = xj - _meanJ[i, j];
                _meanJ[i, j] += deltaJ / n;

                _coMoment[i, j] += deltaI * (xj - _meanJ[i, j]);
            }
        }
    }

    public CovResult GetCovarianceMatrix()
    {
        Matrix<double> cov = Matrix<double>.Build.Dense(_dimension, _dimension);

        for (int i = 0; i < _dimension; i++)
        {
            for (int j = i; j < _dimension; j++)
            {
                long n = _pairCount[i, j];
                double value = n >= 2 ? _coMoment[i, j] / (n - 1) : 0.0;
                cov[i, j] = value;
                cov[j, i] = value;
            }
        }

        return new CovResult { MDCovarianceMatrix = cov };
    }
}