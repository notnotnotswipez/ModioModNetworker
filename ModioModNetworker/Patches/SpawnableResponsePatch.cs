using HarmonyLib;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Marrow;
using LabFusion.Network;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Queue;
using ModioModNetworker.Utilities;

namespace ModioModNetworker.Patches
{
    public class SpawnableResponsePatch 
    {
        [HarmonyPatch(typeof(SpawnResponseMessage), "OnHandleMessage", typeof(ReceivedMessage))]
        public static class PatchClass
        {
            public static bool Prefix(ReceivedMessage received)
            {
                if (!received.IsServerHandled && MainClass.autoDownloadSpawnables)
                {

                    SpawnResponseData data = received.ReadData<SpawnResponseData>();

                    if (!MainClass.confirmedHostHasIt && !MainClass.useRepo)
                    {
                        return true;
                    }

                    if (!MainClass.overrideFusionDL)
                    {
                        return true;
                    }

                    if (!CrateFilterer.HasCrate<GameObjectCrate>(new Barcode(data.Barcode)))
                    {


                        SpawnableHoldQueue.AddToQueue(new SpawnableHoldQueueData()
                        {
                            missingBarcode = data.Barcode,
                            _data = data
                        });
                        return false;
                    }

                    if (LevelHoldQueue.LevelInQueue())
                    {
                        SpawnableHoldQueue.AddToQueue(data);
                        return false;
                    }

                }


                return true;
            }
        }
    }
}