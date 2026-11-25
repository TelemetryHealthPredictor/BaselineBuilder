namespace BaselineBuilderNew.Data.ResultObjects
{
    internal class ZVectorPercentileResult
    {
        /// <summary>
        /// Cut-off on the identity-based squared distance ‖z‖²: z-vectors above it are excluded from the covariance.
        /// </summary>
        public required double Threshold { get; set; }
    }
}
