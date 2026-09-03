using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;

namespace Server.Boot
{
    /// <summary>
    /// 既定seedのgeneratedワールドを用意してワールドIDを返す。生成システムの内部namespaceへの依存をサーバー起動側へ閉じ込める窓口
    /// Provisions the default-seed generated world and returns its id, keeping dependencies on the generation system's internal namespaces inside server boot
    /// </summary>
    public static class DefaultGeneratedWorldProvisioner
    {
        public static string EnsureWorld(WorldDataDirectory worldDataDirectory, string serverDataDirectory)
        {
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, serverDataDirectory, WorldMapMode.Generated, ServerInstanceManager.DefaultGeneratedSeed));
            return TerrainTransferMetaReader.Read(worldDataDirectory).WorldId;
        }
    }
}
