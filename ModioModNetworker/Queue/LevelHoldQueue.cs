using System.Collections.Generic;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using UnityEngine;
using LabFusion.Scene;

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
            FusionNotifier.Send(new FusionNotification()
            {
                Title = new NotificationText($"The host tried loading a level you dont have. \"{data.missingBarcode}\""),
                Message = new NotificationText("Wait a bit, it may start downloading!"),
                PopupLength = 3f,
                SaveToMenu = false,
                ShowPopup = true,
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
            FusionSceneManager.SetTargetScene(data.levelBarcode, data.loadBarcode);
        }
        
        
        public class LevelHoldQueueData
        {
            public string missingBarcode;
            public SceneLoadData _data;
        }
    }
}