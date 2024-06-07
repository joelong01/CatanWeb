using System.Collections.Generic;
using System.Linq;
using Catan3.Models;
using Microsoft.UI;

namespace Catan3
{
    public static class PlayerDatabase
    {
        public static List<PlayerViewModel> AvailablePlayers { get; } =
            [
                new ("Dodgy",   "ms-appx:///Assets/DefaultPlayers/Dodgy.png", new PlayerColorViewModel(Colors.White, Colors.Red, Colors.Black)),
                new ("Joe",     "ms-appx:///Assets/DefaultPlayers/Joe.jpg", new PlayerColorViewModel(Colors.White, Colors.Blue, Colors.Black)),
                new ("Doug",    "ms-appx:///Assets/DefaultPlayers/Doug.jpg", new PlayerColorViewModel(Colors.White, Colors.Green, Colors.Black)),
                new ("Chris",   "ms-appx:///Assets/DefaultPlayers/Chris.jpg", new PlayerColorViewModel(Colors.White, Colors.Black, Colors.Black)),
                new ("Adrian",  "ms-appx:///Assets/DefaultPlayers/Adrian.jpg", new PlayerColorViewModel(Colors.White, Colors.Purple, Colors.Black)),
                new ("Ryan",    "ms-appx:///Assets/DefaultPlayers/Ryan.jpg", new PlayerColorViewModel(Colors.White, Colors.DarkGray, Colors.Black))
            ];

        public static PlayerViewModel? FromId(string id)
        {
            return AvailablePlayers.FirstOrDefault(x => x.Id == id);
        }
    }
}
