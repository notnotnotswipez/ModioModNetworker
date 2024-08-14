using ModioModNetworker.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Marrow;

namespace ModioModNetworker.Utilities
{
    public class ModInfoUtilities
    {
        public static ModInfo GetModInfoForPoolee(Poolee assetPoolee)
        {
            string palletBarcode = assetPoolee.SpawnableCrate._pallet._barcode._id;
            return GetModInfoForPalletBarcode(palletBarcode);
        }
        
        public static ModInfo GetModInfoForLevelBarcode(string barcode)
        {
            LevelCrate levelCrate =
                CrateFilterer.GetCrate<LevelCrate>(new Barcode(barcode));
            if (levelCrate == null)
            {
                return null;
            }

            string palletBarcode = levelCrate._pallet._barcode._id;
            return GetModInfoForPalletBarcode(palletBarcode);
        }
        
        public static ModInfo GetModInfoForSpawnableBarcode(string barcode)
        {
            GameObjectCrate gameObjectCrate =
                CrateFilterer.GetCrate<GameObjectCrate>(new Barcode(barcode));
            if (gameObjectCrate == null)
            {
                return null;
            }

            string palletBarcode = gameObjectCrate._pallet._barcode._id;
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