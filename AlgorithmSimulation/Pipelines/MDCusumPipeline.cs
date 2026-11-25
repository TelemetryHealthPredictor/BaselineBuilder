using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Dataflow;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers;
using BaselineBuilderNew.AlgorithmSimulation.Calculators;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using Microsoft.Extensions.Options;
using ParameterDataLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Pipelines
{
    internal class MDCusumPipeline : SimulationPipeline
    {
        private readonly KalmanSim _kalmanSim;
        private readonly MahalanobisSim _mahalanobisSim;
        private readonly CusumSim _cusumSim;
        private readonly PercentilesCalculator _mdPercentilesCalc;
        private readonly CusumThresholdsCalculator _cusumPercentilesCalc;

        private TransformManyBlock<ParameterData[], KalmanFilterResult> _kalmanBlock = null!;
        private BroadcastBlock<KalmanFilterResult> _broadcastBlock = null!;
        private TransformBlock<KalmanFilterResult, double> _mahalanobisBlock = null!;
        private TransformBlock<KalmanFilterResult, double[]> _cusumBlock = null!;
        private ActionBlock<double> _mdPercentilesBlock = null!;
        private ActionBlock<double[]> _cusumPercentilesBlock = null!;


        private QcResult? _qc;
        private R0Result? _r0;
        private CovResult? _cov;
        private CusumSlacksResult? _slacks;


        public MDCusumPipeline
        (
            IDatabaseReader reader,
            IOptions<CalcSettings> settings,
            KalmanSim kalmanSim,
            MahalanobisSim mahalanobis,
            CusumSim cusum,
            PercentilesCalculator mdPercentilesCalc,
            CusumThresholdsCalculator cusumPercentilesCalc
        ) : base(reader, settings)
        {
            _kalmanSim = kalmanSim;
            _mahalanobisSim = mahalanobis;
            _cusumSim = cusum;
            _mdPercentilesCalc = mdPercentilesCalc;
            _cusumPercentilesCalc = cusumPercentilesCalc;
        }

        protected override void BuildBlocks()
        {
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
                _cusumPercentilesCalc.RecalculateThresholds,
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
            if (_qc is null || _r0 is null || _cov is null || _slacks is null)
            {
                throw new NullReferenceException("Pipeline wasn't initialized");
            }

            _kalmanSim.Init(_qc, _r0);
            _mahalanobisSim.Init(_cov);
            _cusumSim.InitForCusums(_slacks);
        }

        protected override async Task SendAsync(ParameterData[] batch)
        {
            await _kalmanBlock.SendAsync(batch);
        }

        protected override void SignalCompletion()
        {
            _kalmanBlock.Complete();
        }

        protected override void OnFlightCompleted()
        {
            _cusumPercentilesCalc.EndFlight();
        }


        public void InitPipeline(QcResult qc, R0Result r0, CovResult cov, CusumSlacksResult slacks)
        {
            _r0 = r0;
            _qc = qc;
            _cov = cov;
            _slacks = slacks;
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
