using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catan3.Models;
using Microsoft.UI;

namespace Catan3
{
    public static class PlayerDatabase
    {
        public static List<PlayerViewModel> AvailablePlayers { get; } =
            [
                new ("Dodgy","https://i.redd.it/5yivdr9vxy161.jpg", new PlayerColorViewModel(Colors.White, Colors.Red, Colors.Black)),
                new ("Joe", "https://www.facebook.com/photo/?fbid=10160459911485939&set=a.427028510938", new PlayerColorViewModel(Colors.White, Colors.Blue, Colors.Black)),
                new ("Doug", "https://www.facebook.com/photo/?fbid=100839586607787&set=a.154471181244627", new PlayerColorViewModel(Colors.White, Colors.Green, Colors.Black)),
                new ("Chris", "https://www.facebook.com/photo/?fbid=10158309840577127&set=a.424812752126", new PlayerColorViewModel(Colors.White, Colors.Black, Colors.Black)),
                new ("Adrian","https://www.facebook.com/photo/?fbid=883633928380&set=pob.1902864", new PlayerColorViewModel(Colors.White, Colors.Purple, Colors.Black)),
                new ("Ryan","https://www.facebook.com/photo/?fbid=4029871567127703&set=pob.1264294343", new PlayerColorViewModel(Colors.White, Colors.DarkGray, Colors.Black))
            ];

        public static PlayerViewModel? FromId(string id)
        {
            return AvailablePlayers.FirstOrDefault(x => x.Id == id);
        }
    }
}
