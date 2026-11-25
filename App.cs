using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.AlgorithmSimulation.Pipelines;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.IO.Output;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using Microsoft.Extensions.Hosting;

namespace BaselineBuilderNew
{
    internal class App : BackgroundService
    {
        private readonly IR0Calculator _R0Calculator;
        private readonly IQcCalculator _QcCalculator;
        private readonly IDatabaseReader _dbReader;
        private readonly ZStatsPipeline _zStatsPipeline;
        private readonly CovPipeline _covPipeline;
        private readonly MDCusumPipeline _mdCusumPipeline;
        private readonly CusumRawPipeline _cusumRawPipeline;

        private readonly BaselineCSVWriter _writer;

        private readonly IHostApplicationLifetime _lifetime;

        public App
        (
            IR0Calculator r0Calculator,
            IQcCalculator qcCalculator,
            IDatabaseReader dbReader,
            ZStatsPipeline zStatsPipeline,
            CovPipeline covPipeline,
            MDCusumPipeline mdCusumPipeline,
            CusumRawPipeline cusumRawPipeline,
            BaselineCSVWriter csvWriter,
            IHostApplicationLifetime lifetime
        )
        {
            _R0Calculator = r0Calculator;
            _QcCalculator = qcCalculator;
            _dbReader = dbReader;
            _zStatsPipeline = zStatsPipeline;
            _covPipeline = covPipeline;
            _mdCusumPipeline = mdCusumPipeline;
            _cusumRawPipeline = cusumRawPipeline;
            _writer = csvWriter;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _dbReader.InitAsync();

            Console.WriteLine("Initialized DB.");

            R0Result r0 = await _R0Calculator.CalculateR0();

            Console.WriteLine("Calculated R0:");
            foreach (double variance in r0.R0DiagonalVector)
            {
                Console.WriteLine(variance);
            }

            _QcCalculator.Init(r0);
            QcResult qc = await _QcCalculator.CalculateQc();

            Console.WriteLine("Calculated Qc:");
            foreach (double variableProcessNoise in qc.QcVector)
            {
                Console.WriteLine(variableProcessNoise);
            }

            _zStatsPipeline.InitPipeline(qc, r0);
            await _zStatsPipeline.BeginSimulationAsync();
            ZVectorPercentileResult zCutOff = await _zStatsPipeline.GetZVectorPercentile();
            CusumSlacksResult cusumSlacks = await _zStatsPipeline.GetCusumSlacks();

            Console.WriteLine($"Calculated z-vector cut-off: {zCutOff.Threshold}");
            Console.WriteLine("Calculated CUSUM slacks:");
            foreach (double slack in cusumSlacks.Slacks)
            {
                Console.WriteLine(slack);
            }

            await _zStatsPipeline.LogZDiagnostics();

            _covPipeline.InitPipeline(qc, r0, zCutOff);
            await _covPipeline.BeginSimulationAsync();
            CovResult cov = await _covPipeline.GetCovarianceMatrix();

            Console.WriteLine("Calculated COV.");

            _mdCusumPipeline.InitPipeline(qc, r0, cov, cusumSlacks);
            await _mdCusumPipeline.BeginSimulationAsync();
            MDPercentilesResult mdThresholds = await _mdCusumPipeline.GetMDPercentiles();
            CusumsPercentilesResult cusumThresholds = await _mdCusumPipeline.GetCusumPercentiles();

            Console.WriteLine("Calculated MD & CUSUM Thresholds.");

            _cusumRawPipeline.InitPipeline(qc, r0, cusumSlacks, cusumThresholds);
            await _cusumRawPipeline.BeginSimulationAsync();
            CusumRawPercentilesResult cusumRawThresholds = await _cusumRawPipeline.GetCusumRawThresholds();

            Console.WriteLine("Calculated CusumRaw Thresholds.");

            _writer
                .AddR0(r0)
                .AddQc(qc)
                .AddCov(cov)
                .AddCusumSlacks(cusumSlacks)
                .AddMDThresholds(mdThresholds)
                .AddCusumThresholds(cusumThresholds)
                .AddCusumRawThresholds(cusumRawThresholds);

            Console.WriteLine("Writing CSV files.");

            _writer.WriteBaselineCSVFiles();

            Console.WriteLine("Baseline written. Shutting down.");

            _lifetime.StopApplication();
        }
    }
}
