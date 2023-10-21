#if NET6_0
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.WorldSetting
{
    /// <summary>
    ///     
    /// </summary>
    public class WorldSpawnPointTest
    {

        ///     OK

        [Test]
        public void WorldSpawnPointSearcherTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldSettings = serviceProvider.GetService<IWorldSettingsDatastore>();
            var vineGenerator = serviceProvider.GetService<VeinGenerator>();
            worldSettings.Initialize();

            var spawnPoint = worldSettings.WorldSpawnPoint;

            //ID
            var spawnPointOreId = vineGenerator.GetOreId(spawnPoint.X, spawnPoint.Y);

            Assert.AreEqual(vineGenerator.GetOreId(spawnPoint.X, spawnPoint.Y), spawnPointOreId);
            Assert.AreEqual(1, spawnPointOreId);
        }
    }
}
#endif