using LabFusion.SDK.Modules;
using MelonLoader;

namespace ModioModNetworker
{
    public class ModlistModule : Module
    {
        public override string Name => "Modlist Module";
        public override string Author => "ModioModNetworker";
        public override Version Version => new Version(1, 0, 0);
        public override ConsoleColor Color => ConsoleColor.Green;

        protected override void OnModuleRegistered()
        {
            MelonLogger.Msg("Loaded Mod Io Modlist Module!");
        }
    }
}