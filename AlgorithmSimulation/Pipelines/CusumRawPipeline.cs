using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Dataflow;
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
    internal class CusumRawPipeline : SimulationPipeline
    {
        private readonly KalmanSim _kalmanSim;
        private readonly CusumSim _cusumSim;
        private readonly PercentilesCalculator _percentilesCalc;

        private TransformManyBlock<ParameterData[], KalmanFilterResult> _kalmanBlock = null!;
        private TransformManyBlock<KalmanFilterResult, double> _cusumRawBlock = null!;
        private ActionBlock<double> _percentilesBlock = null!;

        private QcResult? _qc;
        private R0Result? _r0;
        private CusumSlacksResult? _slacks;
        private CusumsPercentilesResult? _thresholds;

        public CusumRawPipeline
        (
            IDatabaseReader reader,
            IOptions<CalcSettings> settings,
            KalmanSim kalmanSim,
            CusumSim cusum,
            PercentilesCalculator percentilesCalc
        ) : base(reader, settings)
        {
            _kalmanSim = kalmanSim;
            _cusumSim = cusum;
            _percentilesCalc = percentilesCalc;
        }

        protected override void BuildBlocks()
        {
            _kalmanBlock = new TransformManyBlock<ParameterData[], KalmanFilterResult>
            (
                _kalmanSim.KalmanDelegate,
                _blockOptions
            );

            _cusumRawBlock = new TransformManyBlock<KalmanFilterResult, double>
            (
                _cusumSim.CusumRawDelegate,
                _blockOptions
            );

            _percentilesBlock = new ActionBlock<double>
            (
                _percentilesCalc.RecalculatePercentiles,
                _blockOptions
            );

            _kalmanBlock.LinkTo(_cusumRawBlock, _linkOptions);
            _cusumRawBlock.LinkTo(_percentilesBlock, _linkOptions);

            Completion = _percentilesBlock.Completion;
        }



        protected override void InitSimulation()
        {
            if (_r0 is null || _qc is null || _slacks is null || _thresholds is null)
            {
                throw new NullReferenceException("Pipeline wasn't initialized");
            }

            _kalmanSim.Init(_qc, _r0);
            _cusumSim.InitForCusumRaw(_slacks, _thresholds);
        }

        protected override async Task SendAsync(ParameterData[] batch)
        {
            await _kalmanBlock.SendAsync(batch);
        }

        protected override void SignalCompletion()
        {
            _kalmanBlock.Complete();
        }


        public void InitPipeline(QcResult qc, R0Result r0, CusumSlacksResult slacks, CusumsPercentilesResult thresholds)
        {
            _r0 = r0;
            _qc = qc;
            _slacks = slacks;
            _thresholds = thresholds;

            // PercentilesCalculator is a singleton shared with MDCusumPipeline (MD distances).
            // Clear the MD data once, before CusumRaw starts collecting across all flights.
            _percentilesCalc.Reset();
        }

        public async Task<CusumRawPercentilesResult> GetCusumRawThresholds()
        {
            await Completion;
            return _percentilesCalc.GetRawPercentileValues();
        }
    }
}
