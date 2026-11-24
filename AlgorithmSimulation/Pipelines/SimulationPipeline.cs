using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Dataflow;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.IO.Input.Abstractions;
using Microsoft.Extensions.Options;
using MongoLib.Documents;
using ParameterDataLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Pipelines
{
    internal abstract class SimulationPipeline
    {
        protected readonly int _requiredNumOfFlights;

        protected readonly IDatabaseReader _reader;

        protected readonly ExecutionDataflowBlockOptions _blockOptions;
        protected readonly DataflowLinkOptions _linkOptions;

        // Completion of the blocks built for the current flight; reassigned by BuildBlocks for every flight
        protected Task Completion { get; set; } = Task.CompletedTask;

        protected SimulationPipeline(IDatabaseReader reader, IOptions<CalcSettings> calcSettings)
        {
            _reader = reader;

            _requiredNumOfFlights = calcSettings.Value.RequiredNumOfFlights;

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
            FlightDocument[] flights = await _reader.GetFlightsAsync(_requiredNumOfFlights);

            foreach (FlightDocument flight in flights)
            {
                // A completed dataflow block can't be reused, so every flight gets new blocks
                BuildBlocks();

                // Safe to reset the algorithms here: the previous flight's blocks have fully drained
                InitSimulation();

                IReadOnlyList<ParameterData[]> batches = await _reader.GetInitialPacketsAsync(flight);

                while (batches.Count > 0)
                {
                    foreach (ParameterData[] batch in batches)
                    {
                        await SendAsync(batch);
                    }

                    batches = await _reader.GetNextPacketsAsync();
                }

                SignalCompletion();

                // Barrier: wait until every block in the chain has processed this flight,
                // so the next flight's InitSimulation can't reset state that is still in use
                try
                {
                    await Completion;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Simulation failed on flight {flight.Id}", ex);
                }
            }
        }

        /// <summary>
        /// Creates and links a new set of dataflow blocks and sets <see cref="Completion"/>.
        /// Called once per flight, before <see cref="InitSimulation"/>.
        /// </summary>
        protected abstract void BuildBlocks();

        protected abstract Task SendAsync(ParameterData[] batch);

        protected abstract void InitSimulation();

        protected abstract void SignalCompletion();
    }
}
