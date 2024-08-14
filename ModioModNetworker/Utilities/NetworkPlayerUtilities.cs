using LabFusion.Entities;
using LabFusion.Player;
using LabFusion.Representation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModioModNetworker.Utilities
{
    public static class NetworkPlayerUtilities
    {
        public static List<NetworkPlayer> GetAllNetworkPlayers() {
            List<NetworkPlayer> networkPlayers = new List<NetworkPlayer>();
            foreach (var playerId in PlayerIdManager.PlayerIds)
            {
                if (NetworkPlayerManager.TryGetPlayer(playerId, out var player)) {
                    networkPlayers.Add(player);
                }
            }

            return networkPlayers;
        }
    }
}
