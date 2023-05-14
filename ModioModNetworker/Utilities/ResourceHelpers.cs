using UnityEngine;

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
        public static GameObject AvatarDownloadBarPrefab;

        public static void LoadAssets(AssetBundle bundle)
        {
            AvatarDownloadBarPrefab =
                bundle.LoadPersistentAsset<GameObject>("assets/networkerui/avatarprogressbar.prefab");
        }
    }
}