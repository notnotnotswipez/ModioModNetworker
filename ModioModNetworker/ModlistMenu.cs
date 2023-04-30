using System.Collections.Generic;
using BoneLib.BoneMenu;
using BoneLib.BoneMenu.Elements;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using UnityEngine;

namespace ModioModNetworker
{
    public class ModlistMenu
    {
        public static MenuCategory mainCategory;
        public static MenuCategory hostModsCategory;
        public static MenuCategory installedModsCategory;
        private static string lastSelectedCategory;
        public static List<ModInfo> _modInfos = new List<ModInfo>();
        
        public static ModInfo activeDownloadModInfo;

        public static void Initialize()
        {
            mainCategory = MenuManager.CreateCategory("ModIo Mod Networker", Color.cyan);
            MainClass.menuRefreshRequested = true;
        }
        
        public static void PopulateModInfos(List<string> modIds)
        {
            _modInfos.Clear();
            while (ModInfo.modInfoThreadRequests.TryDequeue(out var useless))
            {
            }
            ModInfo.requestSize = modIds.Count;
            ModInfo.SetFinishedAction(() =>
            {
                MainClass.menuRefreshRequested = true;
            });
            foreach (var id in modIds)
            {
                ModInfo.RequestModInfo(id, "menuinfos");
            }
        }

        public static void Clear()
        {
            _modInfos.Clear();
            Refresh(false);
        }

        public static void Refresh(bool openMenu)
        {
            mainCategory.Elements.Clear();

            if (activeDownloadModInfo != null)
            {
                mainCategory.CreateSubPanel("DOWNLOADING...", Color.yellow);
                mainCategory.CreateFunctionElement("Click to Update Percentage", Color.white, () =>
                {
                    Refresh(true);
                });
                mainCategory.CreateFunctionElement(activeDownloadModInfo.modId+$" ({activeDownloadModInfo.modDownloadPercentage}%)", Color.yellow, () => { });
                mainCategory.CreateSubPanel("==============", Color.yellow);
            }
            
            mainCategory.CreateFunctionElement("Refresh Mod.Io Subscriptions", Color.cyan, () =>
            {
                ModFileManager.QueueSubscriptions();
            });

            installedModsCategory = mainCategory.CreateCategory("Installed Mods", Color.white);
            CreateInstalledModsSection();
            
            if (NetworkInfo.HasServer && !NetworkInfo.IsServer)
            {
                hostModsCategory = mainCategory.CreateCategory("Host Mods", Color.white);
                CreateHostModsSection();
            }

            if (openMenu)
            {
                MenuCategory category = mainCategory;
                MenuManager.SelectCategory(category);
            }
        }

        private static void CreateInstalledModsSection()
        {
            installedModsCategory.CreateSubPanel("INSTALLED", Color.green);

            foreach (var info in MainClass.installedMods)
            {
                MakeModInfoButton(info, installedModsCategory, true);
            }
        }

        private static void CreateHostModsSection()
        {
            hostModsCategory.CreateSubPanel("HOST'S MODS", Color.white);
            
            if (_modInfos.Count == 0)
            {
                hostModsCategory.CreateFunctionElement("Host either has no mods in their modlist or does not have this mod.", Color.red, ()=>{});
                return;
            }

            foreach (var info in _modInfos)
            {
                MakeModInfoButton(info, hostModsCategory);
            }
        }

        private static ModInfo GetInstalledInfo(string modId)
        {
            foreach (var info in MainClass.installedMods)
            {
                if (info.modId == modId)
                {
                    return info;
                }
            }

            return null;
        }

        private static void MakeModInfoButton(ModInfo modInfo, MenuCategory category, bool displayUninstall = false)
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
            

            double kb = modInfo.fileSizeKB;
            double mb = kb / 1000000;
            double gb = mb / 1000;

            string display = "KB";
            double value = kb;
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
            
            value = System.Math.Round(value, 2);
            
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
                            modInfo.Download();
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
                            if (ModFileManager.Subscribe(modInfo.modId))
                            {
                                MainClass.subscribedModIoIds.Add(modInfo.modId);
                                Refresh(true);
                                FusionNotifier.Send(new FusionNotification()
                                {
                                    title = "Subscribed to "+modInfo.modId,
                                    showTitleOnPopup = true,
                                    message = "This is now in your mod.io subscribed list.",
                                    popupLength = 3f,
                                    isMenuItem = false,
                                    isPopup = true,
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
        }
    }
}