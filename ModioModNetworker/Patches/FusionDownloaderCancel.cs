using HarmonyLib;
using LabFusion.Downloading.ModIO;
using LabFusion.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModioModNetworker.Patches
{

    [HarmonyPatch(typeof(NetworkModRequester), "RequestMod")]
    public class FusionDownloaderCancel
    {
        public static bool Prefix() {
            return !MainClass.overrideFusionDL;
        }
    }

    [HarmonyPatch(typeof(NetworkModRequester), "RequestAndInstallMod")]
    public class FusionDownloaderCancelRequest
    {
        public static bool Prefix()
        {
            return !MainClass.overrideFusionDL;
        }
    }
}
