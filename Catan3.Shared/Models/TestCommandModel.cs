using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
