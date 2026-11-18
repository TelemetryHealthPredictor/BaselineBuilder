using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Data.MongoOutputs;
using MongoLib.Documents;
using ParameterDataLib;

namespace BaselineBuilderNew.IO.Input.Abstractions
{
    internal interface IDatabaseReader
    {
        public Task InitAsync();

        public Task<IReadOnlyList<NamedSample>> GetInitialSamplesAsync();

        public Task<IReadOnlyList<NamedSample>?> GetNextSamplesAsync();

        public int GetTHPFieldCount();

        public FieldCatalogDocument[] GetTHPFields();

        public Task<FlightDocument[]> GetFlightsAsync(int amount);

        public Task<IReadOnlyList<ParameterData[]>> GetInitialPacketsAsync(FlightDocument flight);

        public Task<IReadOnlyList<ParameterData[]>> GetNextPacketsAsync(string flightID);
    }
}
