using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Algorithms.Implementations;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.ResultObjects;

namespace BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers
{
    internal class CusumSim
    {
        private readonly ICusum _cusum;

        public CusumSim(ICusum cusum)
        {
            _cusum = cusum;
        }

        public double[] CusumDelegate(KalmanFilterResult kfResult)
        {
            return _cusum.RecalculateCusums(kfResult);
        }

        public void InitForCusums(CusumSlacksResult slacks)
        {
            _cusum.Init(slacks.Slacks, []);
        }

        public double[] CusumRawDelegate(KalmanFilterResult kfResult)
        {
            _cusum.RecalculateCusums(kfResult);
            _cusum.ComputeAnomalies();
            double cusumRaw = _cusum.ComputeCusumRaw();

            if (cusumRaw == 0)
            {
                return [];
            }

            return [cusumRaw];
        }

        public void InitForCusumRaw(CusumSlacksResult slacks, CusumsPercentilesResult thresholds)
        {
            _cusum.Init(slacks.Slacks, thresholds.ThresholdsForCusums);
        }
    }
}
