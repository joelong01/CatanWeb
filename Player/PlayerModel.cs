using System.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    ///     this should have all the data representing per player state that is bound to the UI 
    /// </summary>
    /// <param name="idx"></param>
    public partial class PlayerModel(string id)
    {
        public static PlayerModel Default { get; } = new PlayerModel("Nameless-001");

        public override string ToString()
        {
            return $"{Id}";
        }
       
    }
}
