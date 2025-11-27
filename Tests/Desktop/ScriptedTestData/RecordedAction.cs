using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Catan3.Shared.Models;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Represents a recorded action from GameRecorder that matches the exact JSON structure.
    /// This is different from TestAction which has additional testing properties.
    /// </summary>
    public class RecordedAction
    {
        [JsonPropertyName("type")]
        public ActionType Type { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}