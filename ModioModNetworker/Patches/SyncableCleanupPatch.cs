using HarmonyLib;
using LabFusion.Syncables;
using ModioModNetworker.Queue;
using ModioModNetworker.Utilities;

namespace ModioModNetworker.Patches
{
    public class SyncableCleanupPatch
    {
        [HarmonyPatch(typeof(SyncManager), "OnCleanup")]
        private static class CleanupPatchClass {
            public static void Prefix()
            {
                // Clear the spawnables which we are holding in the case of a potential catchup.
                SpawnableHoldQueue.ClearQueue();
            }
        }
    }
}