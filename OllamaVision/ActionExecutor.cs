using System.Text.Json.Serialization;

namespace OllamaVision
{
    /// <summary>
    /// Represents a single action to be performed, as described by the AI.
    /// This version is designed for UI Automation, targeting controls by properties.
    /// </summary>
    public class AIAction
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } // e.g., "INVOKE", "SET_VALUE"

        [JsonPropertyName("control")]
        public ControlIdentifier Control { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; } // Used for actions like SET_VALUE
    }

    /// <summary>
    /// Describes a UI control to be targeted by an AIAction.
    /// The AI should provide at least one of these properties.
    /// </summary>
    public class ControlIdentifier
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("automationId")]
        public string AutomationId { get; set; }
    }
}
