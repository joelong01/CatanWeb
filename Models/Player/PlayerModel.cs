using System.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    ///     this should have all the data representing per player state that is bound to the UI 
    /// </summary>
    /// <param name="idx"></param>
    public partial class PlayerModel(int idx) 
    {
        public static PlayerModel Default { get; } = new PlayerModel(-1);

        public override string ToString()
        {
            return $"{Index}";
        }
       
    }
}
