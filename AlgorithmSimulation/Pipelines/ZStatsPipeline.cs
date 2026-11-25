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
    /// <summary>
    /// Runs the Kalman Filter over all flights and collects statistics of its z-scores:
    /// the ‖z‖² cut-off for trimming the covariance, and the per-parameter CUSUM slacks.
    /// </summary>
    internal class ZStatsPipeline : SimulationPipeline
    {
        private readonly KalmanSim _kalmanSim;
        private readonly ZVectorPercentileCalculator _zVectorPercentileCalc;
        private readonly CusumSlackCalculator _cusumSlackCalc;

        private TransformManyBlock<ParameterData[], KalmanFilterResult> _kalmanBlock = null!;
        private BroadcastBlock<KalmanFilterResult> _broadcastBlock = null!;
        private ActionBlock<KalmanFilterResult> _zVectorPercentileBlock = null!;
        private ActionBlock<KalmanFilterResult> _cusumSlackBlock = null!;

        private QcResult? _qc;
        private R0Result? _r0;

        public ZStatsPipeline
        (
            IDatabaseReader reader,
            IOptions<CalcSettings> settings,
            KalmanSim kalmanSim,
            ZVectorPercentileCalculator zVectorPercentileCalc,
            CusumSlackCalculator cusumSlackCalc
        ) : base(reader, settings)
        {
            _kalmanSim = kalmanSim;
            _zVectorPercentileCalc = zVectorPercentileCalc;
            _cusumSlackCalc = cusumSlackCalc;
        }

        protected override void BuildBlocks()
        {
            _kalmanBlock = new TransformManyBlock<ParameterData[], KalmanFilterResult>
            (
                _kalmanSim.KalmanDelegate,
                _blockOptions
            );

            // Both calculators only read the result, and its z-vector is a clone, so sharing it is safe
            _broadcastBlock = new BroadcastBlock<KalmanFilterResult>(res => res, _blockOptions);

            _zVectorPercentileBlock = new ActionBlock<KalmanFilterResult>
            (
                _zVectorPercentileCalc.AddZVector,
                _blockOptions
            );

            _cusumSlackBlock = new ActionBlock<KalmanFilterResult>
            (
                _cusumSlackCalc.AddZScores,
                _blockOptions
            );

            _kalmanBlock.LinkTo(_broadcastBlock, _linkOptions);
            _broadcastBlock.LinkTo(_zVectorPercentileBlock, _linkOptions);
            _broadcastBlock.LinkTo(_cusumSlackBlock, _linkOptions);

            Completion = Task.WhenAll(_zVectorPercentileBlock.Completion, _cusumSlackBlock.Completion);
        }

        protected override void InitSimulation()
        {
            if (_qc is null || _r0 is null)
            {
                throw new NullReferenceException("Pipeline wasn't initialized");
            }

            _kalmanSim.Init(_qc, _r0);
        }

        protected override async Task SendAsync(ParameterData[] batch)
        {
            await _kalmanBlock.SendAsync(batch);
        }

        protected override void SignalCompletion()
        {
            _kalmanBlock.Complete();
        }


        public void InitPipeline(QcResult qc, R0Result r0)
        {
            _qc = qc;
            _r0 = r0;
        }

        public async Task<ZVectorPercentileResult> GetZVectorPercentile()
        {
            await Completion;
            return _zVectorPercentileCalc.GetResult();
        }

        public async Task<CusumSlacksResult> GetCusumSlacks()
        {
            await Completion;
            return _cusumSlackCalc.GetResult();
        }
    }
}
