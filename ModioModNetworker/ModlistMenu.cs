using System.Collections.Generic;
using BoneLib.BoneMenu;
using BoneLib.BoneMenu.UI;
using HarmonyLib;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Utilities;
using ModIoModNetworker.Ui;
using UnityEngine;

namespace ModioModNetworker
{
    public class ModlistMenu
    {
        public static Page mainCategory;
        private static string lastSelectedCategory;
        public static List<ModInfo> _modInfos = new List<ModInfo>();

        public static int installPage = 0;
        public static int hostPage = 0;

        private static int installPageCount = 0;
        private static int hostPageCount = 0;

        private static int modsPerPage = 4;

        public static ModInfo activeDownloadModInfo;
        public static GameObject customMenuObject;

        [HarmonyPatch(typeof(GUIMenu), "OnPageOpened")]
        public class CategoryUpdatePatch
        {
            public static void Postfix(GUIMenu __instance, Page page)
            {
                if (page == mainCategory)
                {
                    //__instance.gameObject.transform.Find("Page/Content").gameObject.SetActive(false);

                    if (!customMenuObject)
                    {
                        GameObject customMenu = GameObject.Instantiate(NetworkerAssets.uiMenuPrefab);
                        customMenu.transform.Find("MenuBase").gameObject.AddComponent<NetworkerMenuController>();
                        customMenu.transform.parent = __instance.gameObject.transform;
                        customMenu.transform.localPosition = Vector3.forward;
                        customMenu.transform.localRotation = Quaternion.identity;
                        customMenu.transform.localScale = Vector3.one;
                        customMenuObject = customMenu;
                    }
                    else
                    {
                        customMenuObject.SetActive(true);
                    }
                }
                else if (page == Page.Root)
                {
                    //__instance.gameObject.transform.Find("Page/Content").gameObject.SetActive(true);
                    if (customMenuObject)
                    {
                        customMenuObject.SetActive(false);
                        NetworkerMenuController.instance.Reset();
                    }
                }
            }
        }

        public static void Initialize()
        {
            mainCategory = Page.Root.CreatePage("ModIo Mod Networker", Color.cyan);
            NetworkerMenuController.AddCheckboxSetting("Override Fusion DL", MainClass.overrideFusionDL,
            (b) =>
            {
                MainClass.overrideFusionDL = b;
                MainClass.overrideFusionDLConfig.Value = b;
                MainClass.melonPreferencesCategory.SaveToFile();
            });

            NetworkerMenuController.AddCheckboxSetting("Auto Delete Lobby Mods", MainClass.tempLobbyMods,
            (b) =>
            {
                MainClass.tempLobbyMods = b;
                MainClass.tempLobbyModsConfig.Value = b;
                MainClass.melonPreferencesCategory.SaveToFile();
            });

            NetworkerMenuController.AddCheckboxSetting("Auto Download Avatars", MainClass.autoDownloadAvatars,
                (b) =>
                {
                    MainClass.autoDownloadAvatars = b;
                    MainClass.autoDownloadAvatarsConfig.Value = b;
                    MainClass.melonPreferencesCategory.SaveToFile();
                });

            NetworkerMenuController.AddCheckboxSetting("Auto Download Spawnables", MainClass.autoDownloadSpawnables,
                (b) =>
                {
                    MainClass.autoDownloadSpawnables = b;
                    MainClass.autoDownloadSpawnablesConfig.Value = b;
                    MainClass.melonPreferencesCategory.SaveToFile();
                });

            NetworkerMenuController.AddCheckboxSetting("Auto Download Levels", MainClass.autoDownloadLevels,
                (b) =>
                {
                    MainClass.autoDownloadLevels = b;
                    MainClass.autoDownloadLevelsConfig.Value = b;
                    MainClass.melonPreferencesCategory.SaveToFile();
                });

            NetworkerMenuController.AddNumericalSetting("(Spawnable/Avatar) Auto Download Max MB", (int) MainClass.maxAutoDownloadMb, 200, 2000, 100,
                (num) =>
                {
                    MainClass.maxAutoDownloadMb = num;
                    MainClass.maxAutoDownloadMbConfig.Value = num;
                    MainClass.melonPreferencesCategory.SaveToFile();
                });

            NetworkerMenuController.AddNumericalSetting("(Level) Auto Download Max GB", (int) MainClass.levelMaxGb, 1, 10, 1,
                (num) =>
                {
                    MainClass.levelMaxGb = num;
                    MainClass.maxLevelAutoDownloadGbConfig.Value = num;
                    MainClass.melonPreferencesCategory.SaveToFile();
                });

            NetworkerMenuController.AddCheckboxSetting("Auto Download 18+ Content", MainClass.downloadMatureContent,
                (b) =>
                {
                    MainClass.downloadMatureContent = b;
                    MainClass.downloadMatureContentConfig.Value = b;
                    MainClass.melonPreferencesCategory.SaveToFile();
                });
            MainClass.menuRefreshRequested = true;
        }

        public static void PopulateModInfos(List<ModInfo> modInfos)
        {
            hostPage = 0;
            _modInfos.Clear();
            _modInfos.AddRange(modInfos);
            MainClass.confirmedHostHasIt = true;
            Refresh(false);
        }

        public static void Clear()
        {
            _modInfos.Clear();
            Refresh(false);
        }

        public static void Refresh(bool openMenu)
        {
            installPageCount = Mathf.CeilToInt(MainClass.installedMods.Count / (float) modsPerPage);
            hostPageCount = Mathf.CeilToInt(_modInfos.Count / (float) modsPerPage);

            return;

        }

        private static void CreateSettingsSection()
        {
            Color desiredColor = Color.green;
            if (!MainClass.autoDownloadAvatars)
            {
                desiredColor = Color.yellow;
            }

            Color desiredColorSpawnable = Color.green;
            if (!MainClass.autoDownloadSpawnables)
            {
                desiredColorSpawnable = Color.yellow;
            }

            Color desiredColorLevel = Color.green;
            if (!MainClass.autoDownloadLevels)
            {
                desiredColorLevel = Color.yellow;

            }
        }

        /*private static void MakeModInfoButton(ModInfo modInfo, MenuCategory category, bool displayUninstall = false)
        {
            if (activeDownloadModInfo != null)
            {
                if (activeDownloadModInfo.modId == modInfo.modId)
                {
                    return;
                }
            }

            if (!modInfo.isValidMod)
            {
                return;
            }

            Color chosenColor = Color.white;


            float kb = modInfo.fileSizeKB;
            float mb = kb / 1000000;
            float gb = mb / 1000;

            string display = "KB";
            float value = kb;
            if (mb > 1)
            {
                value = mb;
                display = "MB";
            }
            if (gb > 1)
            {
                value = gb;
                display = "GB";
            }

            value = Mathf.Round(value * 100f) / 100f;

            ModInfo installedInfo = GetInstalledInfo(modInfo.modId);
            bool outOfDate = false;
            bool installed = false;
            Color installedColor = Color.green;

            if (installedInfo != null)
            {
                installed = true;
                chosenColor = Color.green;
                if (installedInfo.version != modInfo.version)
                {
                    installedColor = Color.yellow;
                    chosenColor = Color.yellow;
                    outOfDate = true;
                }
                else
                {
                    if (!modInfo.IsSubscribed())
                    {
                        chosenColor = Color.cyan;
                    }
                }
            }

            MenuCategory modInfoButton = category.CreateCategory(modInfo.modId, chosenColor);
            if (modInfo.isValidMod)
            {
                modInfoButton.CreateSubPanel("Filename: "+modInfo.fileName, Color.yellow);
                if (modInfo.mature)
                {
                    modInfoButton.CreateSubPanel("Mature Content", Color.red);
                }

                modInfoButton.CreateFunctionElement("File Size: "+value+" "+display, Color.yellow, ()=>{});
                // We just got this from the API, so it should be up to date.
                modInfoButton.CreateFunctionElement("Latest Version: "+modInfo.version, Color.yellow, ()=>{});

                if (installed)
                {
                    modInfoButton.CreateFunctionElement("Installed Version: "+installedInfo.version, installedColor, ()=>{});
                }

                if (!ModFileManager.isDownloading)
                {
                    bool allowDownload = false;
                    string buttonText = "INSTALL";
                    Color buttonColor = Color.green;
                    if (!installed)
                    {
                        allowDownload = true;
                    }
                    else
                    {
                        if (outOfDate)
                        {
                            allowDownload = true;
                            buttonText = "UPDATE";
                            buttonColor = Color.yellow;
                        }
                    }

                    if (allowDownload)
                    {
                        modInfoButton.CreateFunctionElement(buttonText, buttonColor, () =>
                        {
                            ModFileManager.AddToQueue(new DownloadQueueElement()
                            {
                                associatedPlayer = null,
                                info = modInfo,
                                notify = true
                            });
                            Refresh(true);
                        });
                    }
                    else
                    {
                        modInfoButton.CreateFunctionElement("UP TO DATE", Color.green, () =>
                        {
                        });
                    }

                    if (!modInfo.IsSubscribed())
                    {
                        modInfoButton.CreateFunctionElement("Subscribe", Color.cyan, () =>
                        {
                            if (ModFileManager.Subscribe(modInfo.numericalId))
                            {
                                MainClass.subscribedModIoNumericalIds.Add(modInfo.numericalId);
                                Refresh(true);
                                FusionNotifier.Send(new FusionNotification()
                                {
                                    title = new NotificationText("Subscribed to "+modInfo.modName),
                                    showTitleOnPopup = true,
                                    message = new NotificationText("This is now in your mod.io subscribed list."),
                                    popupLength = 3f,
                                    isMenuItem = false,
                                    isPopup = true
                                });
                            }
                        });
                    }

                    if (displayUninstall)
                    {
                        string uninstallText = "UNINSTALL";
                        if (modInfo.IsSubscribed())
                        {
                            uninstallText = "UNSUBSCRIBE AND UNINSTALL";
                        }

                        modInfoButton.CreateFunctionElement(uninstallText, Color.yellow, () =>
                        {
                            ModFileManager.UninstallAndUnsubscribe(modInfo.modId);
                        });
                    }
                }
                else
                {
                    modInfoButton.CreateFunctionElement("YOU ALREADY HAVE A DOWNLOAD RUNNING.", Color.red, () =>
                    {
                    });
                }
            }
        }*/
    }
}