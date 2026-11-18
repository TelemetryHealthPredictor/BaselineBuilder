using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Data.MongoOutputs;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using MongoLib.Documents;
using StatsLib;
using ThpUtils.Naming;

namespace BaselineBuilderNew.KalmanFilterBaseline.Implementations
{
    internal class R0Calculator : IR0Calculator
    {
        private readonly IDatabaseReader _reader;
        private readonly NamingData _namingData;

        private const int windowSize = 10;
        private const int quantile = 15;

        private Dictionary<string, MovingStatistics> _variableStats;

        private Dictionary<string, TDigest> _quantileCalculators;

        private Dictionary<string, double> _variances;

        private Dictionary<string, int> _variancesAveraged;


        public R0Calculator(IDatabaseReader reader, NamingData namingData)
        {
            _reader = reader;
            _namingData = namingData;

            _quantileCalculators = [];

            _variableStats = [];

            _variances = [];
            _variancesAveraged = [];

            foreach (string fieldName in _namingData.ParameterNames)
            {
                _variableStats.Add(fieldName, new MovingStatistics(windowSize));
                _quantileCalculators.Add(fieldName, new TDigest());
            }
        }


        public async Task<R0Result> CalculateR0()
        {
            // Go over samples for every parameter,
            // calculating for each one the rolling STD of its most recent window.
            // pass these rolling STDs to TDigest, and extract the 15th percentile at the end.

            NamedSample[]? samples = (await _reader.GetInitialSamplesAsync()).ToArray();

            foreach (NamedSample sample in samples)
            {
                RecalculateQuantiles(sample);
            }

            samples = (await _reader.GetNextSamplesAsync())?.ToArray();
            while (samples != null)
            {
                foreach (NamedSample sample in samples)
                {
                    RecalculateQuantiles(sample);
                }

                samples = (await _reader.GetNextSamplesAsync())?.ToArray();
            }

            return new R0Result()
            {
                R0DiagonalVector = Vector<double>.Build.DenseOfArray(await CalculateVariances())
            };
        }

        private void RecalculateQuantiles(NamedSample sample)
        {
            _variableStats[sample.Name].Push(sample.Sample.Value);

            if (_variableStats[sample.Name].Count < windowSize)
            {
                return;
            }

            double populationStandardDeviation = _variableStats[sample.Name].PopulationStandardDeviation;
            if (Double.IsFinite(populationStandardDeviation))
            {
                _quantileCalculators[sample.Name].Add(populationStandardDeviation);
            }
        }

        private async Task<double[]> CalculateVariances()
        {
            foreach (string variable in _variableStats.Keys)
            {
                _variableStats[variable] = new MovingStatistics(windowSize);
            }

            NamedSample[]? samples = (NamedSample[])await _reader.GetInitialSamplesAsync();

            foreach (NamedSample sample in samples)
            {
                RecalculateVariances(sample);
            }

            samples = (NamedSample[]?)await _reader.GetNextSamplesAsync();
            while (samples != null)
            {
                foreach (NamedSample sample in samples)
                {
                    RecalculateVariances(sample);
                }

                samples = (NamedSample[]?)await _reader.GetNextSamplesAsync();
            }

            List<double> result = [];

            foreach (string variable in _namingData.ParameterNames)
            {
                result.Add(_variances[variable]);
            }

            return result.ToArray();
        }

        private void RecalculateVariances(NamedSample sample)
        {
            _variableStats[sample.Name].Push(sample.Sample.Value);

            if (_variableStats[sample.Name].Count < windowSize ||
                _variableStats[sample.Name].PopulationStandardDeviation > 
                    _quantileCalculators[sample.Name].Quantile(quantile))
            {
                return;
            }

            double variance = _variableStats[sample.Name].PopulationVariance;

            if (!_variances.ContainsKey(sample.Name))
            {
                _variances.Add(sample.Name, variance);
                _variancesAveraged.Add(sample.Name, 1);
            }
            else
            {
                _variances[sample.Name] += (variance - _variances[sample.Name]) / _variancesAveraged[sample.Name];
                _variancesAveraged[sample.Name]++;
            }
        }
    }
}
