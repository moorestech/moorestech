using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Extension;


namespace Tests.UnitTest.Game.SaveLoad
{
    public class RailGraphSaveLoadConsistencyTest
    {
        [Test]
        public void SmallRailGraphRemainsConsistentAfterSaveLoad()
        {
            RailGraphDatastore.ResetInstance();

            var environment = TrainTestHelper.CreateEnvironment();
            _ = environment.GetRailGraphDatastore();

            var positions = new[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(4, 0, 0),
                new Vector3Int(0, 0, 4)
            };

            var components = new List<RailComponent>
            {
                TrainTestHelper.PlaceRail(environment, positions[0], BlockDirection.North),
                TrainTestHelper.PlaceRail(environment, positions[1], BlockDirection.East),
                TrainTestHelper.PlaceRail(environment, positions[2], BlockDirection.South)
            };

            components[0].ConnectRailComponent(components[1], true, false);
            components[1].ConnectRailComponent(components[2], true, true);
            components[2].ConnectRailComponent(components[0], false, false);

            var expectedSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(components);

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);

            foreach (var position in positions)
            {
                environment.WorldBlockDatastore.RemoveBlock(position);
            }

            RailGraphDatastore.ResetInstance();

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            var loadedComponents = new List<RailComponent>();
            foreach (var position in positions)
            {
                var block = loadEnvironment.WorldBlockDatastore.GetBlock(position);
                Assert.IsNotNull(block, $"座標 {position} にレールブロックがロードされていません。");

                var saverComponent = block.GetComponent<RailSaverComponent>();
                Assert.IsNotNull(saverComponent, $"座標 {position} のRailSaverComponentを取得できませんでした。");
                Assert.IsNotEmpty(saverComponent.RailComponents,
                    $"座標 {position} のRailSaverComponentにRailComponentが含まれていません。");

                loadedComponents.Add(saverComponent.RailComponents[0]);
            }

            var actualSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(loadedComponents);

            RailGraphNetworkTestHelper.AssertEquivalent(expectedSnapshot, actualSnapshot);

            RailGraphDatastore.ResetInstance();
        }
    }
}
