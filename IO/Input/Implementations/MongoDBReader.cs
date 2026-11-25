using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Data.MongoOutputs;
using BaselineBuilderNew.IO.Input.Abstractions;
using MongoLib;
using MongoLib.Documents;
using ParameterDataLib;
using ParameterDataLib.Enums;

namespace BaselineBuilderNew.IO.Input.Implementations
{
    internal class MongoDBReader : IDatabaseReader
    {
        private const int BatchSizeInMinutes = 5;

        private readonly ITelemetryReader _reader;

        private IReadOnlyList<FieldCatalogDocument> _fieldCatalog = null!;


        private string? _currentFlightID;
        private DateTime _readPacketsUntil;

        private string? _currentSamplesFlightID;
        private DateTime _readSamplesUntil;


        public MongoDBReader(ITelemetryReader reader)
        {
            _reader = reader;
        }


        public async Task InitAsync()
        {
            _fieldCatalog = await _reader.GetFieldCatalogAsync();
        }

        public async Task<NamedSample[]> GetInitialSamplesAsync(FlightDocument flight)
        {
            DateTime from = flight.StartTime;
            DateTime to = from + TimeSpan.FromMinutes(BatchSizeInMinutes);

            _currentSamplesFlightID = flight.Id;
            _readSamplesUntil = to;

            return await GetSamplesAsync(flight.Id, from, to);
        }

        public async Task<NamedSample[]> GetNextSamplesAsync()
        {
            if (_currentSamplesFlightID is null)
            {
                throw new NullReferenceException("GetInitialSamples must be called before GetNext...");
            }

            DateTime from = _readSamplesUntil;
            DateTime to = _readSamplesUntil + TimeSpan.FromMinutes(BatchSizeInMinutes);
            _readSamplesUntil = to;

            return await GetSamplesAsync(_currentSamplesFlightID, from, to);
        }

        // Keeps its cursor in locals, so parallel callers can share this reader.
        public async IAsyncEnumerable<NamedSample[]> ReadSampleBatchesAsync(FlightDocument flight)
        {
            DateTime from = flight.StartTime;

            while (true)
            {
                DateTime to = from + TimeSpan.FromMinutes(BatchSizeInMinutes);

                NamedSample[] batch = await GetSamplesAsync(flight.Id, from, to);

                if (batch.Length == 0)
                {
                    yield break;
                }

                yield return batch;

                from = to;
            }
        }


        private async Task<NamedSample[]> GetSamplesAsync(string flightID, DateTime from, DateTime to)
        {
            List<NamedSample> result = [];

            foreach (FieldCatalogDocument field in _fieldCatalog)
            {
                if (field.UsedForTHP)
                {
                    IReadOnlyList<TelemetrySample> fieldSamples = await _reader.GetSamplesAsync
                    (
                        flightID,
                        field.Field,
                        from,
                        to
                    );

                    foreach (TelemetrySample sample in fieldSamples)
                    {
                        result.Add(new NamedSample(field.Field, sample));
                    }
                }
            }

            return result.ToArray();
        }

        public int GetTHPFieldCount()
        {
            return _fieldCatalog.Where(f => f.UsedForTHP).ToArray().Length;
        }

        public FieldCatalogDocument[] GetTHPFields()
        {
            return _fieldCatalog.Where(f => f.UsedForTHP).ToArray();
        }

        public async Task<FlightDocument[]> GetFlightsAsync(int amount)
        {
            FlightDocument[] flights = (await _reader.GetFlightsAsync()).ToArray();

            return [.. flights.Reverse().Take(amount)];
        }

        public async Task<IReadOnlyList<ParameterData[]>> GetInitialPacketsAsync(FlightDocument flight)
        {
            DateTime from = flight.StartTime;
            DateTime to = from + TimeSpan.FromMinutes(BatchSizeInMinutes);

            _currentFlightID = flight.Id;
            _readPacketsUntil = to;

            return await GetPacketsAsync(flight.Id, from, to);
        }

        public async Task<IReadOnlyList<ParameterData[]>> GetNextPacketsAsync()
        {
            if (_currentFlightID is null)
            {
                throw new NullReferenceException("GetInitialPackets must be called before GetNext...");
            }

            DateTime from = _readPacketsUntil;
            DateTime to = _readPacketsUntil + TimeSpan.FromMinutes(BatchSizeInMinutes);
            _readPacketsUntil += TimeSpan.FromMinutes(BatchSizeInMinutes);

            return await GetPacketsAsync(_currentFlightID, from, to);
        }

        private async Task<IReadOnlyList<ParameterData[]>> GetPacketsAsync(string flightID, DateTime from, DateTime to)
        {
            List<ParameterData[]> result = [];

            IReadOnlyList<TelemetryPacketDocument> packets = await _reader.GetPacketsAsync
            (
                flightID,
                from,
                to
            );

            foreach (TelemetryPacketDocument packet in packets)
            {
                ParameterData[] batch = new ParameterData[packet.Values.Length];

                for (int i = 0; i < packet.Values.Length; i++)
                {
                    string name = packet.Fields[i];
                    double value = packet.Values[i];
                    ulong timestampMS = (ulong)new DateTimeOffset(packet.Timestamp).ToUnixTimeMilliseconds();

                    batch[i] = new ParameterData
                    {
                        Name = name,
                        Value = value,
                        TimestampMS = timestampMS,
                        Units = TelemetryUnit.Unit
                    };
                }

                result.Add(batch);
            }

            return result;
        }
    }
}
