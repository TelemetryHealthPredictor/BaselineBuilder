using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers
{
    internal class MahalanobisSim
    {
        private readonly IMahalanobis _mahalanobis;

        public MahalanobisSim(IMahalanobis mahalanobis)
        {
            _mahalanobis = mahalanobis;
        }

        public double MahalanobisDelegate(KalmanFilterResult kfResult)
        {
            return _mahalanobis.ComputeD2(kfResult);
        }

        public void Init(CovResult cov)
        {
            _mahalanobis.Init(0, cov.MDCovarianceMatrix, 0);
        }
    }
}
