using System;
using System.Collections.Generic;
using System.Text;

namespace BaselineBuilderNew.Config.Settings
{
    internal class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
        public string GroupID { get; set; } = string.Empty;
    }
}
