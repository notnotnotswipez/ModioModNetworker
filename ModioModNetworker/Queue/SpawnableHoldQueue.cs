using System;
using System.Collections.Generic;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Extensions;
using UnityEngine;
using LabFusion.Marrow;
using LabFusion.Marrow.Pool;

namespace ModioModNetworker.Queue
{
    public class SpawnableHoldQueue
    {
        private static List<SpawnableHoldQueueData> queueDatas = new List<SpawnableHoldQueueData>();
        private static List<SpawnResponseData> spawnResponseDatas = new List<SpawnResponseData>();
        
        public static void ClearSpawnResponseDatas()
        {
            spawnResponseDatas.Clear();
        }
        
        public static void HandleAllSpawnResponseDatas()
        {
            foreach (var spawnResponseData in spawnResponseDatas)
            {
                Handle(spawnResponseData);
            }
            spawnResponseDatas.Clear();
        }
        
        public static void AddToQueue(SpawnResponseData data)
        {
            spawnResponseDatas.Add(data);
        }

        public static void ClearQueue()
        {
            queueDatas.Clear();
        }
        
        public static void AddToQueue(SpawnableHoldQueueData data)
        {
            queueDatas.Add(data);
        }

        public static void CheckValid(string barcode)
        {
            List<SpawnableHoldQueueData> toRemove = new List<SpawnableHoldQueueData>();
            foreach (var queueData in queueDatas)
            {
                if (queueData.missingBarcode == barcode)
                {
                    Handle(queueData._data);
                    toRemove.Add(queueData);
                }
            }
            queueDatas.RemoveAll((data) => toRemove.Contains(data));
        }

        private static void Handle(SpawnResponseData data)
        {
            var crateRef = new SpawnableCrateReference(data.Barcode);

            var spawnable = new Spawnable()
            {
                crateRef = crateRef,
                policyData = null
            };

            LocalAssetSpawner.Register(spawnable);

            byte owner = data.OwnerID;
            string barcode = data.Barcode;
            ushort syncId = data.EntityID;
            uint trackerId = data.TrackerID;

            LocalAssetSpawner.Spawn(spawnable, data.SerializedTransform.position, data.SerializedTransform.rotation, (go) => {
                SpawnResponseMessage.OnSpawnFinished(owner, barcode, syncId, go, trackerId);
            });
        }
    }
    
    public class SpawnableHoldQueueData
    {
        public string missingBarcode;
        public SpawnResponseData _data;
    }
}