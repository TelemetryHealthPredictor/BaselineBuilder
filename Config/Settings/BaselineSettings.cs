using System;
using System.Collections.Generic;
using System.Text;

namespace BaselineBuilderNew.Config.Settings
{
    internal class BaselineSettings
    {
        public string BaselineFolderPath { get; set; } = string.Empty;
        public string ScalarsFileName {  get; set; } = string.Empty;
        public string ForgettingFactorKey {  get; set; } = string.Empty;
        public string CusumDriftSizeKey { get; set; } = string.Empty;
        public string NameForQc { get; set; } = string.Empty;
        public string NameForR0 { get; set; } = string.Empty;
        public string NameForCov { get; set; } = string.Empty;
        public string NameForCusumSlacks { get; set; } = string.Empty;
        public string NameForMDThresholds { get; set; } = string.Empty;
        public string NameForCusumThresholds { get; set; } = string.Empty;
        public string NameForCusumRawThresholds { get; set; } = string.Empty;
    }
}
