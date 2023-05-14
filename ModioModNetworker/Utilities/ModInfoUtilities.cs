using ModioModNetworker.Data;
using SLZ.Marrow.Pool;
using SLZ.Marrow.Warehouse;

namespace ModioModNetworker.Utilities
{
    public class ModInfoUtilities
    {
        public static ModInfo GetModInfoForPoolee(AssetPoolee assetPoolee)
        {
            string palletBarcode = assetPoolee.spawnableCrate._pallet._barcode;
            return GetModInfoForPalletBarcode(palletBarcode);
        }
        
        public static ModInfo GetModInfoForLevelBarcode(string barcode)
        {
            LevelCrate levelCrate =
                AssetWarehouse.Instance.GetCrate<LevelCrate>(barcode);
            if (levelCrate == null)
            {
                return null;
            }

            string palletBarcode = levelCrate._pallet._barcode;
            return GetModInfoForPalletBarcode(palletBarcode);
        }
        
        public static ModInfo GetModInfoForSpawnableBarcode(string barcode)
        {
            GameObjectCrate gameObjectCrate =
                AssetWarehouse.Instance.GetCrate<GameObjectCrate>(barcode);
            if (gameObjectCrate == null)
            {
                return null;
            }

            string palletBarcode = gameObjectCrate._pallet._barcode;
            return GetModInfoForPalletBarcode(palletBarcode);
        }

        public static ModInfo GetModInfoForPalletBarcode(string barcode)
        {
            ModInfo foundModInfo = null;
            foreach (var installedModInfo in MainClass.InstalledModInfos)
            {
                if (installedModInfo.palletBarcode == barcode)
                {
                    foundModInfo = installedModInfo.ModInfo;
                    break;
                }
            }
            return foundModInfo;
        }
    }
}