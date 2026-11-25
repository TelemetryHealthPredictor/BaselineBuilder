using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.MongoOutputs;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Options;
using MongoLib.Documents;
using StatsLib;
using ThpUtils.Naming;

namespace BaselineBuilderNew.KalmanFilterBaseline.Implementations
{
    internal class R0Calculator : IR0Calculator
    {
        private const double MinVariance = 1e-9;
        private const double QuietQuantile = 0.15;
        private const int MaxParallelFlights = 4;

        private readonly IDatabaseReader _reader;
        private readonly NamingData _namingData;

        private readonly int _requiredNumOfFlights;


        public R0Calculator(IDatabaseReader reader, NamingData namingData, IOptions<CalcSettings> calcSettings)
        {
            _reader = reader;
            _namingData = namingData;

            _requiredNumOfFlights = calcSettings.Value.RequiredNumOfFlights;
        }


        public async Task<R0Result> CalculateR0()
        {
            FlightDocument[] flights = await _reader.GetFlightsAsync(_requiredNumOfFlights);

            R0Pass1Result pass1Result = await RunPass1Async(flights);

            double[] r0 = await RunPass2Async(flights, pass1Result.QuietStdThresholds);

            for (int i = 0; i < r0.Length; i++)
            {
                string variable = _namingData.ParameterNames[i];

                r0[i] = Math.Max(r0[i], GetFloor(pass1Result.MinSteps[variable]));
            }

            return new R0Result()
            {
                R0DiagonalVector = Vector<double>.Build.DenseOfArray(r0)
            };
        }

        // Pass 1: window stds -> merged digest per variable -> p15, plus min step
        private async Task<R0Pass1Result> RunPass1Async(FlightDocument[] flights)
        {
            Dictionary<string, R0VariableState>[] pass1 =
                await ProcessFlightsAsync(flights, "R0 pass 1", _ => new R0VariableState());

            Dictionary<string, double> quietThresholds = [];
            Dictionary<string, double> minSteps = [];

            foreach (string variable in _namingData.ParameterNames)
            {
                TDigest[] digests = pass1
                    .Select(flight => flight[variable].StdDigest)
                    .Where(digest => digest.Count > 0)
                    .ToArray();

                if (digests.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"R0: variable '{variable}' has no complete window in any flight.");
                }

                TDigest merged = digests.Aggregate(TDigest.Merge);

                quietThresholds[variable] = merged.Quantile(QuietQuantile);
                minSteps[variable] = pass1.Min(flight => flight[variable].MinStep);
            }

            return new R0Pass1Result()
            {
                QuietStdThresholds = quietThresholds,
                MinSteps = minSteps
            };
        }

        // Pass 2: mean variance of windows with std <= p15, combined over flights (unfloored, NamingData order)
        private async Task<double[]> RunPass2Async(FlightDocument[] flights, Dictionary<string, double> quietThresholds)
        {
            Dictionary<string, R0VariableState>[] pass2 =
                await ProcessFlightsAsync(flights, "R0 pass 2", variable => new R0VariableState(quietThresholds[variable]));

            double[] r0 = new double[_namingData.ParameterNames.Length];

            for (int i = 0; i < r0.Length; i++)
            {
                string variable = _namingData.ParameterNames[i];

                long count = pass2.Sum(flight => (long)flight[variable].VarianceCount);

                if (count == 0)
                {
                    throw new InvalidOperationException(
                        $"R0: variable '{variable}' has no quiet window in any flight.");
                }

                r0[i] = pass2.Sum(flight => flight[variable].VarianceMean * flight[variable].VarianceCount) / count;
            }

            return r0;
        }

        // Quantization floor step²/12; fields that never change keep MinVariance
        private static double GetFloor(double minStep)
        {
            if (!double.IsFinite(minStep))
            {
                return MinVariance;
            }

            return Math.Max(MinVariance, minStep * minStep / 12.0);
        }

        private async Task<Dictionary<string, R0VariableState>[]> ProcessFlightsAsync
        (
            FlightDocument[] flights,
            string passName,
            Func<string, R0VariableState> createState
        )
        {
            Dictionary<string, R0VariableState>[] results = new Dictionary<string, R0VariableState>[flights.Length];
            int flightsDone = 0;

            ParallelOptions options = new() { MaxDegreeOfParallelism = MaxParallelFlights };

            await Parallel.ForEachAsync(Enumerable.Range(0, flights.Length), options, async (index, _) =>
            {
                Dictionary<string, R0VariableState> states =
                    _namingData.ParameterNames.ToDictionary(name => name, createState);

                await foreach (NamedSample[] batch in _reader.ReadSampleBatchesAsync(flights[index]))
                {
                    foreach (NamedSample sample in batch)
                    {
                        if (states.TryGetValue(sample.Name, out R0VariableState? state))
                        {
                            state.Add(sample.Sample);
                        }
                    }
                }

                results[index] = states;

                Console.WriteLine($"{passName}: flight {Interlocked.Increment(ref flightsDone)}/{flights.Length} done");
            });

            return results;
        }
    }
}
