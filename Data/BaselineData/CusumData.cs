namespace BaselineBuilderNew.Data.BaselineData
{
    internal class CusumData
    {
        /// <summary>
        /// Drift size δ to catch, in standard deviations. Each parameter's slack is k = δ × its z-score std.
        /// </summary>
        public double DriftSize { get; set; }
    }
}
