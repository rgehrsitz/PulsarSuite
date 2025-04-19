// Auto-generated metadata file
// Generated: 2025-04-17T20:51:00.0437041Z

using System;
using System.Collections.Generic;

namespace Generated
{
    public static class RuleMetadata
    {
        public static readonly Dictionary<string, RuleInfo> Rules = new()
        {
        };

        public class RuleInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int Layer { get; set; }
            public string SourceFile { get; set; }
            public int LineNumber { get; set; }
            public string[] InputSensors { get; set; }
            public string[] OutputSensors { get; set; }
            public bool HasTemporalConditions { get; set; }
        }
    }
}
