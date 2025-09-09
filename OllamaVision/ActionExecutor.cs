using System.Text.Json.Serialization;

namespace OllamaVision
{
    /// <summary>
    /// Represents a single action to be performed, as described by the AI.
    /// This class is designed to be deserialized from a JSON object.
    /// </summary>
    public class AIAction
    {
        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("x")]
        public int? X { get; set; }

        [JsonPropertyName("y")]
        public int? Y { get; set; }
    }

    /// <summary>
    /// Executes an AI-generated action.
    /// </summary>
    public static class ActionExecutor
    {
        public static void Execute(AIAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Action))
            {
                // Invalid action
                return;
            }

            switch (action.Action.ToUpper())
            {
                case "TYPE":
                    if (action.Text != null)
                    {
                        InputSimulator.SendText(action.Text);
                    }
                    break;

                case "CLICK":
                    if (action.X.HasValue && action.Y.HasValue)
                    {
                        InputSimulator.ClickOnPoint(action.X.Value, action.Y.Value);
                    }
                    break;

                case "DONE":
                    // This action type signals that the task is complete.
                    // The execution loop will handle this.
                    break;

                default:
                    // Optionally, handle or log unknown action types.
                    break;
            }
        }
    }
}
