using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Dataflow;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers;
using BaselineBuilderNew.AlgorithmSimulation.Calculators;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using ParameterDataLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Pipelines
{
    internal class MDCusumPipeline : SimulationPipeline
    {
        private readonly KalmanSim _kalmanSim;
        private readonly MahalanobisSim _mahalanobisSim;
        private readonly CusumSim _cusumSim;
        private readonly PercentilesCalculator _mdPercentilesCalc;
        private readonly CusumPercentilesCalculator _cusumPercentilesCalc;

        private readonly TransformManyBlock<ParameterData[], KalmanFilterResult> _kalmanBlock;
        private readonly BroadcastBlock<KalmanFilterResult> _broadcastBlock;
        private readonly TransformBlock<KalmanFilterResult, double> _mahalanobisBlock;
        private readonly TransformBlock<KalmanFilterResult, double[]> _cusumBlock;
        private readonly ActionBlock<double> _mdPercentilesBlock;
        private readonly ActionBlock<double[]> _cusumPercentilesBlock;


        private QcResult? _qc;
        private R0Result? _r0;
        private CovResult? _cov;


        public MDCusumPipeline
        (
            IDatabaseReader reader,
            KalmanSim kalmanSim,
            MahalanobisSim mahalanobis,
            CusumSim cusum,
            PercentilesCalculator mdPercentilesCalc,
            CusumPercentilesCalculator cusumPercentilesCalc
        ) : base(reader)
        {
            _kalmanSim = kalmanSim;
            _mahalanobisSim = mahalanobis;
            _cusumSim = cusum;
            _mdPercentilesCalc = mdPercentilesCalc;
            _cusumPercentilesCalc = cusumPercentilesCalc;

            _kalmanBlock = new TransformManyBlock<ParameterData[], KalmanFilterResult>
            (
                _kalmanSim.KalmanDelegate,
                _blockOptions
            );

            _mahalanobisBlock = new TransformBlock<KalmanFilterResult, double>
            (
                _mahalanobisSim.MahalanobisDelegate,
                _blockOptions
            );

            _cusumBlock = new TransformBlock<KalmanFilterResult, double[]>
            (
                _cusumSim.CusumDelegate,
                _blockOptions
            );

            _mdPercentilesBlock = new ActionBlock<double>
            (
                _mdPercentilesCalc.RecalculatePercentiles,
                _blockOptions
            );

            _cusumPercentilesBlock = new ActionBlock<double[]>
            (
                _cusumPercentilesCalc.RecalculatePercentiles,
                _blockOptions
            );

            _broadcastBlock = new BroadcastBlock<KalmanFilterResult>(res => res, _blockOptions);

            _kalmanBlock.LinkTo(_broadcastBlock, _linkOptions);
            _broadcastBlock.LinkTo(_mahalanobisBlock, _linkOptions);
            _broadcastBlock.LinkTo(_cusumBlock, _linkOptions);
            _mahalanobisBlock.LinkTo(_mdPercentilesBlock, _linkOptions);
            _cusumBlock.LinkTo(_cusumPercentilesBlock, _linkOptions);

            Completion = Task.WhenAll(_mdPercentilesBlock.Completion, _cusumPercentilesBlock.Completion);
        }



        protected override void InitSimulation()
        {
            if (_qc is null || _r0 is null || _cov is null)
            {
                throw new NullReferenceException("Pipeline wasn't initialized");
            }

            _kalmanSim.Init(_qc, _r0);
            _mahalanobisSim.Init(_cov);
            _cusumSim.InitForCusums();
        }

        protected override async Task SendAsync(ParameterData[] batch)
        {
            await _kalmanBlock.SendAsync(batch);
        }

        protected override void SignalCompletion()
        {
            _kalmanBlock.Complete();
        }


        public void InitPipeline(QcResult qc, R0Result r0, CovResult cov)
        {
            _r0 = r0;
            _qc = qc;
            _cov = cov;
        }

        public async Task<MDPercentilesResult> GetMDPercentiles()
        {
            await Completion;
            return _mdPercentilesCalc.GetMDPercentileValues();
        }

        public async Task<CusumsPercentilesResult> GetCusumPercentiles()
        {
            await Completion;
            return _cusumPercentilesCalc.GetPercentileValues();
        }
    }
}
