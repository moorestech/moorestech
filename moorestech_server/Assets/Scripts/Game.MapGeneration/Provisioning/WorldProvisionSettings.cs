using Game.Paths;

namespace Game.MapGeneration.Provisioning
{
    // WorldProvisioner.EnsureWorldへの入力。ワールド新規作成時のモード・シード・参照先を束ねる
    // Input to WorldProvisioner.EnsureWorld: bundles the mode, seed, and source paths for a fresh world
    public class WorldProvisionSettings
    {
        public readonly WorldDataDirectory WorldDataDirectory;
        public readonly string ServerDataDirectory;
        public readonly string MapMode; // "template" | "generated"
        public readonly int Seed;

        public WorldProvisionSettings(WorldDataDirectory worldDataDirectory, string serverDataDirectory, string mapMode, int seed)
        {
            WorldDataDirectory = worldDataDirectory;
            ServerDataDirectory = serverDataDirectory;
            MapMode = mapMode;
            Seed = seed;
        }
    }
}
