using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Dataflow;
using BaselineBuilderNew.IO.Input.Abstractions;
using MongoLib.Documents;
using ParameterDataLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Pipelines
{
    internal abstract class SimulationPipeline
    {
        protected const int FlightsNeededForBaseline = 20;

        protected readonly IDatabaseReader _reader;

        protected readonly ExecutionDataflowBlockOptions _blockOptions;
        protected readonly DataflowLinkOptions _linkOptions;

        protected Task Completion { get; init; } = null!;

        protected SimulationPipeline(IDatabaseReader reader)
        {
            _reader = reader;

            _blockOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 1
            };

            _linkOptions = new DataflowLinkOptions()
            {
                PropagateCompletion = true
            };
        }

        public async Task BeginSimulationAsync()
        {
            FlightDocument[] flights = await _reader.GetFlightsAsync(FlightsNeededForBaseline);

            foreach (FlightDocument flight in flights)
            {
                InitSimulation();

                IReadOnlyList<ParameterData[]> batches = await _reader.GetInitialPacketsAsync(flight);

                foreach (ParameterData[] batch in batches)
                {
                    await SendAsync(batch);
                }

                batches = await _reader.GetNextPacketsAsync(flight.Id);

                while (batches.Count > 0)
                {
                    foreach (ParameterData[] batch in batches)
                    {
                        await SendAsync(batch);
                    }
                }
            }

            SignalCompletion();
        }

        protected abstract Task SendAsync(ParameterData[] batch);

        protected abstract void InitSimulation();

        protected abstract void SignalCompletion();
    }
}
