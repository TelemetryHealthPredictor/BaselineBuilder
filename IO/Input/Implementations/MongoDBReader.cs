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
        private readonly ITelemetryReader _reader;

        private IReadOnlyList<FlightDocument> _flights = null!;
        private IReadOnlyList<FieldCatalogDocument> _fieldCatalog = null!;

        private int _flightIndex;
        private DateTime _readUntil;


        private string? _currentFlightID;
        private DateTime _readPacketsUntil;


        public MongoDBReader(ITelemetryReader reader)
        {
            _reader = reader;

            _flightIndex = 0;
        }


        public async Task InitAsync()
        {
            _flights = await _reader.GetFlightsAsync();
            _fieldCatalog = await _reader.GetFieldCatalogAsync();
        }

        public async Task<IReadOnlyList<NamedSample>> GetInitialSamplesAsync()
        {
            DateTime firstStartTime = FlightStartTime(_flightIndex);
            DateTime? firstEndTime = FlightEndTime(_flightIndex);

            if (firstEndTime is null)
            {
                if (_flightIndex >= _flights.Count)
                {
                    throw new Exception("Every flight in the database is open");
                }

                _flightIndex++;
                return await GetInitialSamplesAsync();
            }
            
            _readUntil = firstStartTime + TimeSpan.FromMinutes(1);

            if (_readUntil > firstEndTime)
            {
                _flightIndex++;
            }

            return await GetMinuteOfSamples(firstStartTime);
        }

        public async Task<IReadOnlyList<NamedSample>?> GetNextSamplesAsync()
        {
            List<NamedSample> result = await GetMinuteOfSamples(_readUntil);

            if (result.Count == 0)
            {
                _flightIndex = 0;
                return null;
            }

            _readUntil += TimeSpan.FromMinutes(1);

            if (NextFlight())
            {
                _flightIndex++;
                _readUntil = _flights[_flightIndex].StartTime;
            }

            return result;
        }

        private DateTime FlightStartTime(int flightIndex)
        {
            return _flights[flightIndex].StartTime;
        }

        private DateTime? FlightEndTime(int flightIndex)
        {
            return _flights[flightIndex].EndTime;
        }

        private async Task<List<NamedSample>> GetMinuteOfSamples(DateTime from)
        {
            List<NamedSample> result = [];

            foreach (FieldCatalogDocument field in _fieldCatalog)
            {
                if (field.UsedForTHP)
                {
                    IReadOnlyList<TelemetrySample> fieldSamples = await _reader.GetSamplesAsync
                    (
                        _flights[_flightIndex].Id,
                        field.Field,
                        from,
                        from + TimeSpan.FromMinutes(1)
                    );

                    foreach (TelemetrySample sample in fieldSamples)
                    {
                        result.Add(new NamedSample(field.Field, sample));
                    }
                }
            }

            return result;
        }

        private bool NextFlight()
        {
            if (_readUntil > FlightEndTime(_flightIndex))
            {
                return true;
            }

            return false;
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
            FlightDocument[] flights = (FlightDocument[]) await _reader.GetFlightsAsync();

            return [.. flights.Take(amount)];
        }

        public async Task<IReadOnlyList<ParameterData[]>> GetInitialPacketsAsync(FlightDocument flight)
        {
            DateTime from = flight.StartTime;
            DateTime to = from + TimeSpan.FromMinutes(1);

            _currentFlightID = flight.Id;
            _readPacketsUntil = to;

            return await GetPacketsAsync(flight.Id, from, to);
        }

        public async Task<IReadOnlyList<ParameterData[]>> GetNextPacketsAsync(string flightID)
        {
            DateTime from = _readPacketsUntil;
            DateTime to = _readPacketsUntil += TimeSpan.FromMinutes(1);

            return await GetPacketsAsync(flightID, from, to);
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
