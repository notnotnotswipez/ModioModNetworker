using HarmonyLib;
using LabFusion.Network;
using ModioModNetworker.Data;
using ModioModNetworker.Utilities;
using Il2CppSLZ.Marrow.SceneStreaming;

namespace ModioModNetworker.Patches
{
    public class LobbyCreatePatch
    {
        [HarmonyPatch(typeof(LobbyMetadataHelper), "WriteInfo")]
        public class LobbyMetaDataHelperPatch
        {
            public static string lobbyNumericalId = "null";

            public static void Postfix(INetworkLobby lobby)
            {
                lobby.SetMetadata("modionetworker", "true");
                lobby.SetMetadata("networkermap", lobbyNumericalId);
            }
        }
    }
}