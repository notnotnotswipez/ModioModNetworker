using System.Collections.Generic;
using BoneLib.Nullables;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;
using SLZ.Marrow.Data;
using SLZ.Marrow.Pool;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;
using UnityEngine;

namespace ModioModNetworker.Queue
{
    public class LevelHoldQueue
    {
        static LevelHoldQueueData queueData;
        
        public static bool waitingForLevel = false;
        public static bool waitingForLevelToLoad = false;
        public static bool finishedLoadingLevel = false;
        
        public static void ClearQueue()
        {
            queueData = null;
            waitingForLevel = false;
            waitingForLevelToLoad = false;
            finishedLoadingLevel = false;
        }
        
        public static bool LevelInQueue()
        {
            return queueData != null || waitingForLevel || waitingForLevelToLoad || finishedLoadingLevel;
        }
        
        public static void SetQueue(LevelHoldQueueData data)
        {
            MelonLogger.Msg("Got a level that isn't in the warehouse, adding to queue");
            MelonLogger.Msg("Barcode: " + data.missingBarcode);
            FusionNotifier.Send(new FusionNotification()
            {
                title = $"The host tried loading a level you dont have. \"{data.missingBarcode}\"",
                message = "Wait a bit, it may start downloading!",
                showTitleOnPopup = true,
                popupLength = 3f,
                isMenuItem = false,
                isPopup = true,
            });
            queueData = data;
        }

        public static void CheckValid(string barcode)
        {
            if (queueData != null)
            {
                if (queueData.missingBarcode == barcode)
                {
                    Handle(queueData._data);
                    waitingForLevel = true;
                    queueData = null;
                }
            }
        }

        public static void Update()
        {
            if (waitingForLevel)
            {
                if (SceneStreamer._session != null)
                {
                    if (SceneStreamer._session.Status == StreamStatus.LOADING)
                    {
                        waitingForLevelToLoad = true;
                        waitingForLevel = false;
                    }
                }
            }

            if (waitingForLevelToLoad)
            {
                if (SceneStreamer._session != null)
                {
                    if (SceneStreamer._session.Status != StreamStatus.LOADING)
                    {
                        waitingForLevelToLoad = false;
                        finishedLoadingLevel = true;
                    }
                }
            }

            if (finishedLoadingLevel)
            {
           
                SpawnableHoldQueue.HandleAllSpawnResponseDatas();
                finishedLoadingLevel = false;
            }

            if (SceneStreamer._session != null && !LevelInQueue())
            {
                if (SceneStreamer._session.Status == StreamStatus.DONE)
                {
        
                    SpawnableHoldQueue.ClearSpawnResponseDatas();
                }
            }
        }

        private static void Handle(SceneLoadData data)
        {
            FusionSceneManager.SetTargetScene(data.levelBarcode);
        }
        
        
        public class LevelHoldQueueData
        {
            public string missingBarcode;
            public SceneLoadData _data;
        }
    }
}