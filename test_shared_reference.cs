// Temporary test file to verify Shared project reference works
using Catan3.Shared.Models;

namespace Test 
{
    public class TestClass 
    {
        public void TestMethod() 
        {
            // Test that we can access shared enums
            var gameState = GameState.PickingBoard;
            var resourceType = ResourceType.Wheat;
            
            // Test that we can create shared models
            var buildingKey = new BuildingKey();
        }
    }
}
