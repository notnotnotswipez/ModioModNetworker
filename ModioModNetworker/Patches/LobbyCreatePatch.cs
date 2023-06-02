using System;
using System.Linq;
using BoneLib.BoneMenu.Elements;
using HarmonyLib;
using LabFusion;
using LabFusion.BoneMenu;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;

namespace ModioModNetworker.Patches
{
    public class LobbyCreatePatch
    {
        [HarmonyPatch(typeof(LobbyMetadataHelper), "WriteInfo")]
        public class LobbyMetaDataHelperPatch
        {
            public static void Postfix(INetworkLobby lobby)
            {
                lobby.SetMetadata("modionetworker", "true");
            }
        }
    }
}