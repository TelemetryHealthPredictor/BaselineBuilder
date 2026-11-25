namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class CusumSlacksResult
    {
        /// <summary>
        /// Slack k per parameter: drift size × that parameter's z-score standard deviation.
        /// </summary>
        public required double[] Slacks { get; set; }
    }
}
