using LabFusion;
using LabFusion.Marrow;
using LabFusion.SDK.Modules;
using LabFusion.Utilities;
using MelonLoader;

namespace ModioModNetworker
{
    public class ModlistModule : Module
    {
        public override string Name => "ModIoModNetworkerModule";
        public override string Author => "notnotnotswipez";
        public override Version Version => new Version(ModioModNetworkerUpdaterVersion.versionString);

        public override ConsoleColor Color => ConsoleColor.Cyan;

        protected override void OnModuleRegistered()
        {
            ModuleMessageHandler.RegisterHandler<ModlistMessage>();
        }

        protected override void OnModuleUnregistered()
        {

        }
    }
}