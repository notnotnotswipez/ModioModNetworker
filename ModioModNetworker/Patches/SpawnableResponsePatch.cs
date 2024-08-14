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
        [HarmonyPatch(typeof(SpawnResponseMessage), "HandleMessage", typeof(byte[]), typeof(bool))]
        public static class PatchClass
        {
            public static bool Prefix(byte[] bytes, bool isServerHandled = false)
            {
                if (!isServerHandled && MainClass.autoDownloadSpawnables)
                {
                    using (var reader = FusionReader.Create(bytes))
                    {
                        SpawnResponseData data = reader.ReadFusionSerializable<SpawnResponseData>();

                        if (!MainClass.confirmedHostHasIt && !MainClass.useRepo)
                        {
                            return true;
                        }

                        if (!MainClass.overrideFusionDL)
                        {
                            return true;
                        }

                        if (!CrateFilterer.HasCrate<GameObjectCrate>(new Barcode(data.barcode)))
                        {
                            

                            SpawnableHoldQueue.AddToQueue(new SpawnableHoldQueueData()
                            {
                                missingBarcode = data.barcode,
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
                }

                return true;
            }
        }
    }
}