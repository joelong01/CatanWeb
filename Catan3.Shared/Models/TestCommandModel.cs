using System.Text.Json.Serialization;

namespace Catan3.Shared.Models
{
    //
    // each test command should have a type that tells the UI what to do
    public enum TestCommandType
    {
        UpdateUi
    }
    public class TestCommandModel
    {
        public TestCommandType Type { get; set; }

        [JsonConstructor]
        public TestCommandModel(TestCommandType type)
        {
            Type = type;
        }
    }
}
