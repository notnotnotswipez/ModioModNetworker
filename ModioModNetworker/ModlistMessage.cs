using System;
using System.Collections.Generic;
using System.Web.WebPages;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;

namespace ModioModNetworker
{
    public class ModlistData : IFusionSerializable, IDisposable
    {
        public bool isFinal = false;
        public string modId;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void Serialize(FusionWriter writer)
        {
            writer.Write(isFinal);
            writer.Write(modId);
        }

        public void Deserialize(FusionReader reader)
        {
            isFinal = reader.ReadBoolean();
            modId = reader.ReadString();
        }

        public static ModlistData Create(bool final, string modId)
        {
            return new ModlistData()
            {
                isFinal = final,
                modId = modId
            };
        }
    }
    
    public class ModlistMessage : ModuleMessageHandler
    {
        private static List<string> modlist = new List<string>();

        public override void HandleMessage(byte[] bytes, bool isServerHandled = false)
        {
            using (var reader = FusionReader.Create(bytes))
            {
                using (var data = reader.ReadFusionSerializable<ModlistData>())
                {
                    if (NetworkInfo.IsServer && isServerHandled)
                    {
                        return;
                    }

                    if (data != null)
                    {
                        modlist.Add(data.modId);
                        if (data.isFinal)
                        {
                            MelonLogger.Msg("Got modlist data");
                            ModlistMenu.PopulateModInfos(modlist);
                            modlist.Clear();
                        }
                    }
                }
            }
        }
    }
}