using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Dataflow;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using Microsoft.Extensions.Options;
using ParameterDataLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Pipelines
{
    internal class CovPipeline : SimulationPipeline
    {
        private readonly KalmanSim _kalmanSim;
        private readonly CovCalculator _covCalculator;

        private R0Result? _r0;
        private QcResult? _qc;

        private TransformManyBlock<ParameterData[], KalmanFilterResult> _kalmanBlock = null!;
        private ActionBlock<KalmanFilterResult> _covBlock = null!;

        public CovPipeline
        (
            IDatabaseReader reader,
            IOptions<CalcSettings> settings,
            KalmanSim kalmanSim,
            CovCalculator covCalculator
        ) : base(reader, settings)
        {
            _kalmanSim = kalmanSim;
            _covCalculator = covCalculator;
        }

        protected override void BuildBlocks()
        {
            _kalmanBlock = new TransformManyBlock<ParameterData[], KalmanFilterResult>
            (
                _kalmanSim.KalmanDelegate,
                _blockOptions
            );

            _covBlock = new ActionBlock<KalmanFilterResult>
            (
                _covCalculator.RecalculateCovariance,
                _blockOptions
            );

            _kalmanBlock.LinkTo(_covBlock, _linkOptions);

            Completion = _covBlock.Completion;
        }

        protected override async Task SendAsync(ParameterData[] batch)
        {
            await _kalmanBlock.SendAsync(batch);
        }

        protected override void InitSimulation()
        {
            if (_r0 is null || _qc is null)
            {
                throw new NullReferenceException("Pipeline wasn't initialized");
            }

            _kalmanSim.Init(_qc, _r0);
        }

        protected override void SignalCompletion()
        {
            _kalmanBlock.Complete();
        }


        public void InitPipeline(QcResult qc, R0Result r0)
        {
            _r0 = r0;
            _qc = qc;
        }


        public async Task<CovResult> GetCovarianceMatrix()
        {
            await Completion;

            return _covCalculator.GetCovarianceMatrix();
        }
    }
}
