using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Catan3.Shared.Models;
using Catan3.Shared.Extensions;
using Catan3.Shared.Utility;
using Microsoft.UI.Xaml.Controls;
namespace Catan3.Models
{
    public partial class GameViewModel
    {
        public string StateMessage(GameModel gameModel, GameState gameState)
        {


            if (gameState == GameState.WaitingForNext || gameState == GameState.AllocateResourceForward || gameState == GameState.AllocateResourceReverse)
            {
                var player = gameModel.CurrentPlayer();
                int count = player.UnspentEntitlements.Count;
                if (count == 0)
                {
                    return $"{gameState.Description()}";
                }
                else if (count == 1)
                {
                    return $"[{count} Unspent Entitlement]";
                }
                else
                {
                    return $"[{count} Unspent Entitlements]";
                }
            }

            return $"{gameState.Description()}";
        }
        public string BIND_StarCount(int stars, List<TileModel> _tiles)
        {
            Debug.Assert(GameModel is not null);
            int count = 0;
            foreach (var building in GameModel.Buildings)
            {
                var tiles = TilesForBuildings(building.BuildingKey);
                if (tiles.Stars() == stars) count++;
            }
            return count.ToString();
        }


    }
}
