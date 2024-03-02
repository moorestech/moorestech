using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.WorldSetting
{
    /// <summary>
    ///     ワールドのスポーン地点が正しい位置にあるかどうかをテストする
    /// </summary>
    public class WorldSpawnPointTest
    {
        /// <summary>
        ///     スポーンポイントに鉄があればOKなので、それをテストする
        /// </summary>
        [Test]
        public void WorldSpawnPointSearcherTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldSettings = serviceProvider.GetService<IWorldSettingsDatastore>();
            var vineGenerator = serviceProvider.GetService<VeinGenerator>();
            worldSettings.Initialize();

            var spawnPoint = worldSettings.WorldSpawnPoint;

            //その座標の鉱石のIDを取得し、それが正しいかどうかをチェックする
            var spawnPointOreId = vineGenerator.GetOreId(spawnPoint.x, spawnPoint.y);

            Assert.AreEqual(vineGenerator.GetOreId(spawnPoint.x, spawnPoint.y), spawnPointOreId);
            Assert.AreEqual(1, spawnPointOreId);
        }
    }
}