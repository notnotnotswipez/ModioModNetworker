using System;
using System.Security.Policy;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace ModioModNetworker.Utilities
{
    public static class AssetBundleExtension
    {
        public static T LoadPersistentAsset<T>(this AssetBundle bundle, string name) where T : UnityEngine.Object {
            var asset = bundle.LoadAsset(name);

            if (asset != null) {
                asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return asset.TryCast<T>();
            }

            return null;
        }
    }

    public class NetworkerAssets
    {
        public static GameObject avatarDownloadBarPrefab;
        public static GameObject uiMenuPrefab;
        public static GameObject modInfoDisplay;
        public static GameObject blacklistDisplayPrefab;
        public static GameObject checkboxSettingPrefab;
        public static GameObject numericalSettingPrefab;

        public static void LoadAssetsUI(AssetBundle bundle)
        {
            avatarDownloadBarPrefab =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerassets/avatarprogressbar.prefab");
            uiMenuPrefab =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerassets/rootmenu.prefab");
            modInfoDisplay =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerassets/modinfodisplay.prefab");
            blacklistDisplayPrefab =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerassets/blacklistelement.prefab");
            checkboxSettingPrefab =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerassets/checkboxelement.prefab");
            numericalSettingPrefab =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerassets/numberelement.prefab");
        }
    }
}