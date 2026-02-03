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
    public class TrainRailGraphSaveLoadConsistencyTest
    {
        [Test]
        public void SmallRailGraphRemainsConsistentAfterSaveLoad()
        {
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

            //components[0].ConnectRailComponent(components[1], true, false);
            components[0].FrontNode.ConnectNode(components[1].BackNode);
            components[1].FrontNode.ConnectNode(components[0].BackNode);
            //components[1].ConnectRailComponent(components[2], true, true);
            components[1].FrontNode.ConnectNode(components[2].FrontNode);
            components[2].BackNode.ConnectNode(components[1].BackNode);
            //components[2].ConnectRailComponent(components[0], false, false);
            components[2].BackNode.ConnectNode(components[0].BackNode);
            components[0].FrontNode.ConnectNode(components[2].FrontNode);

            var expectedSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(components);
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);

            foreach (var position in positions)
            {
                environment.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            }

            environment.GetRailGraphDatastore().Reset();

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            var loadedComponents = new List<RailComponent>();
            foreach (var position in positions)
            {
                var block = loadEnvironment.WorldBlockDatastore.GetBlock(position);
                Assert.IsNotNull(block, $"座標 {position} にレールブロックがロードされていません。");

                var railComponents = block.GetComponents<RailComponent>();
                Assert.IsNotNull(railComponents, $"座標 {position} のRailComponentを取得できませんでした。");
                Assert.IsNotEmpty(railComponents,
                    $"座標 {position} のRailComponentにRailComponentが含まれていません。");

                loadedComponents.Add(railComponents[0]);
            }

            var actualSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(loadedComponents);

            RailGraphNetworkTestHelper.AssertEquivalent(expectedSnapshot, actualSnapshot);

            loadEnvironment.GetRailGraphDatastore().Reset();
        }

        [Test]
        public void LargeRailGraphWithHubRemainsConsistentAfterSaveLoad()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            _ = environment.GetRailGraphDatastore();

            const int gridSize = 10;
            const int spacing = 3;
            var positions = new List<Vector3Int>();
            var components = new List<RailComponent>(gridSize * gridSize);

            for (var x = 0; x < gridSize; x++)
            {
                for (var z = 0; z < gridSize; z++)
                {
                    var position = new Vector3Int(x * spacing, 0, z * spacing);
                    positions.Add(position);

                    var direction = BlockDirection.North;
                    switch ((x + z) % 4)
                    {
                        case 0:
                            direction = BlockDirection.North;
                            break;
                        case 1:
                            direction = BlockDirection.East;
                            break;
                        case 2:
                            direction = BlockDirection.South;
                            break;
                        default:
                            direction = BlockDirection.West;
                            break;
                    }

                    components.Add(TrainTestHelper.PlaceRail(environment, position, direction));
                }
            }

            Assert.AreEqual(gridSize * gridSize, components.Count, "レールコンポーネントの生成数が期待値と一致しません。");

            int Index(int x, int z) => x * gridSize + z;

            for (var x = 0; x < gridSize; x++)
            {
                for (var z = 0; z < gridSize; z++)
                {
                    var index = Index(x, z);

                    if (x < gridSize - 1)
                    {
                        var eastIndex = Index(x + 1, z);
                        var useFrontCurrent = (x + z) % 2 == 0;
                        var useFrontEast = (x + 1 + z) % 2 == 0;
                        //components[index].ConnectRailComponent(components[eastIndex], useFrontCurrent, useFrontEast);
                        var tmpn0 = useFrontCurrent ? components[index].FrontNode : components[index].BackNode;
                        var tmpm1 = useFrontEast ? components[eastIndex].FrontNode : components[eastIndex].BackNode;
                        tmpn0.ConnectNode(tmpm1);
                        tmpm1.OppositeRailNode.ConnectNode(tmpn0.OppositeRailNode);
                    }

                    if (z < gridSize - 1)
                    {
                        var southIndex = Index(x, z + 1);
                        var useFrontCurrent = (x + z) % 2 != 0;
                        var useFrontSouth = (x + z + 1) % 2 != 0;
                        //components[index].ConnectRailComponent(components[southIndex], useFrontCurrent, useFrontSouth);
                        var tmpn0 = useFrontCurrent ? components[index].FrontNode : components[index].BackNode;
                        var tmpm1 = useFrontSouth ? components[southIndex].FrontNode : components[southIndex].BackNode;
                        tmpn0.ConnectNode(tmpm1);
                        tmpm1.OppositeRailNode.ConnectNode(tmpn0.OppositeRailNode);
                    }
                }
            }

            var hubX = gridSize / 2;
            var hubZ = gridSize / 2;
            var hubComponent = components[Index(hubX, hubZ)];
            var additionalConnections = 0;

            for (var x = 0; x < gridSize && additionalConnections < 12; x++)
            {
                for (var z = 0; z < gridSize && additionalConnections < 12; z++)
                {
                    if (x == hubX && z == hubZ)
                    {
                        continue;
                    }

                    if (System.Math.Abs(x - hubX) <= 1 && System.Math.Abs(z - hubZ) <= 1)
                    {
                        continue;
                    }

                    var targetIndex = Index(x, z);
                    var useFrontTarget = (x + z) % 2 == 0;
                    //hubComponent.ConnectRailComponent(components[targetIndex], true, useFrontTarget);
                    var tmpn0 = useFrontTarget ? components[targetIndex].FrontNode : components[targetIndex].BackNode;
                    hubComponent.FrontNode.ConnectNode(tmpn0);
                    tmpn0.OppositeRailNode.ConnectNode(hubComponent.BackNode);
                    additionalConnections++;
                }
            }

            Assert.GreaterOrEqual(additionalConnections, 10, "ハブノードには10個以上の追加接続が必要です。");

            var hubFrontConnections = 0;
            foreach (var _ in hubComponent.FrontNode.ConnectedNodes)
            {
                hubFrontConnections++;
            }

            Assert.GreaterOrEqual(hubFrontConnections, 10, "ハブのFrontNodeが十分な接続を保持していません。");

            var expectedSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(components);

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(environment.ServiceProvider);

            foreach (var position in positions)
            {
                environment.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            }

            environment.GetRailGraphDatastore().Reset();

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            var loadedComponents = new List<RailComponent>(positions.Count);
            foreach (var position in positions)
            {
                var block = loadEnvironment.WorldBlockDatastore.GetBlock(position);
                Assert.IsNotNull(block, $"座標 {position} にレールブロックがロードされていません。");

                var railComponents = block.GetComponents<RailComponent>();
                Assert.IsNotNull(railComponents, $"座標 {position} のRailComponentを取得できませんでした。");
                Assert.IsNotEmpty(railComponents,
                    $"座標 {position} のRailComponentにRailComponentが含まれていません。");

                loadedComponents.Add(railComponents[0]);
            }

            var actualSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(loadedComponents);

            RailGraphNetworkTestHelper.AssertEquivalent(expectedSnapshot, actualSnapshot);

            loadEnvironment.GetRailGraphDatastore().Reset();
        }
    }
}
