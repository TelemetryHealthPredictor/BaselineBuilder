using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Algorithms.Implementations;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.BaselineData;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers
{
    internal class CusumSim
    {
        private readonly ICusum _cusum;
        private readonly CusumData _cusumData;

        public CusumSim(ICusum cusum, CusumData cusumData)
        {
            _cusum = cusum;
            _cusumData = cusumData;
        }

        public double[] CusumDelegate(KalmanFilterResult kfResult)
        {
            return _cusum.RecalculateCusums(kfResult);
        }

        public void InitForCusums()
        {
            _cusum.Init(_cusumData.SlackValues, []);
        }

        public double CusumRawDelegate(KalmanFilterResult kfResult)
        {
            _cusum.RecalculateCusums(kfResult);
            _cusum.ComputeAnomalies();
            return _cusum.ComputeCusumRaw();
        }

        public void InitForCusumRaw(CusumsPercentilesResult thresholds)
        {
            _cusum.Init(_cusumData.SlackValues, thresholds.ThresholdsForCusums);
        }
    }
}
