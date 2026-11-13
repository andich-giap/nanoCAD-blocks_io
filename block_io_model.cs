using System.Collections.Generic;
using Teigha.DatabaseServices;

namespace NanoCadIO.BlockIO
{
    public class BlockData
    {
        public string Handle { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new();
        public Dictionary<string, string> DynamicProperties { get; set; } = new();
    }
}
