using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests
{
    // 実際の自動立体交差配置(CommonBlockPlacePointCalculator)で障害物を跨ぐベルト列を生成し、アイテムが流れることを検証する
    // Verify the real auto-overpass placement generates a belt run stepping over an obstacle and items flow across it.
    public class ConveyorOverpassConveyanceTest
    {
        [Test]
        public void AutoOverpassPlacementConveysItemOverObstacle()
        {
            // 自己完結したテスト用Modでサーバーを起動する（外部リポジトリ非依存）
            // Boot the server with the self-contained test mod (no external repo dependency).
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 経路中央(2,0,0)に障害物を設置する。立体交差はこの上を跨ぐ
            // Place an obstacle at the middle (2,0,0); the overpass must step over it.
            var obstacleCell = new Vector3Int(2, 0, 0);
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.BlockId, obstacleCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            // 歯車ベルトを (0,0,0)→(4,0,0) にドラッグした時の自動配置を計算する（本番と同じ計算経路）
            // Compute the auto-placement for dragging a gear belt (0,0,0)->(4,0,0) via the production calc path.
            var holding = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor);
            var placeInfos = CommonBlockPlacePointCalculator.CalculatePoint(
                new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0), false, BlockDirection.East, holding,
                (info, _) => !world.Exists(info.Position),
                cell => world.Exists(cell));

            // 立体交差プロファイルを確認する：中央セルがY1へ上昇し障害物の真上を通る
            // Confirm the overpass profile: the middle cell rose to Y1, passing directly above the obstacle.
            var plan = string.Join(" ", placeInfos.Select(p => $"{p.Position}:{p.VerticalDirection}"));
            Debug.Log($"overpass plan: {plan}");
            var middle = placeInfos.First(p => p.Position.x == 2);
            Assert.AreEqual(1, middle.Position.y, $"中央セルが障害物を跨いでいない / middle cell did not rise over obstacle. {plan}");

            // 計算結果を本番(PlaceBlockFromHotBarProtocol)と同じ手順でサーバーに設置する
            // Place the computed result with the same procedure as production (PlaceBlockFromHotBarProtocol).
            var belts = PlaceComputedBelts();

            // 先頭ベルトにアイテムを挿入し、毎tick全歯車ベルトへ動力を供給して回す
            // Insert an item on the first belt and tick while powering every gear belt.
            var itemId = MasterHolder.ItemMaster.GetItemAllIds().First();
            belts[0].belt.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 1), InsertItemContext.Empty);
            for (var i = 0; i < 600; i++)
            {
                foreach (var b in belts) b.gear.SupplyPower(new RPM(10f), new Torque(10f), true);
                GameUpdater.UpdateOneTick();
            }

            // 終端ベルトに到達していれば立体交差を搬送できている
            // Reaching the far belt proves the overpass conveys end-to-end.
            var diag = string.Join(" ", belts.Select((b, idx) => $"#{idx}={Count(b.belt)}"));
            Debug.Log($"items per belt: {diag}");
            Assert.Greater(Count(belts[^1].belt), 0, $"アイテムが立体交差を渡れなかった / item did not cross the overpass. {diag}");

            #region Internal

            List<(VanillaBeltConveyorComponent belt, GearBeltConveyorComponent gear)> PlaceComputedBelts()
            {
                var result = new List<(VanillaBeltConveyorComponent, GearBeltConveyorComponent)>();
                foreach (var info in placeInfos)
                {
                    if (!info.Placeable) continue;
                    var blockId = holding.BlockGuid.GetVerticalOverrideBlockId(info.VerticalDirection);
                    Assert.IsTrue(world.TryAddBlock(blockId, info.Position, info.Direction, Array.Empty<BlockCreateParam>(), out var block), $"設置失敗 / placement failed at {info.Position}");
                    result.Add((block.GetComponent<VanillaBeltConveyorComponent>(), block.GetComponent<GearBeltConveyorComponent>()));
                }
                return result;
            }

            int Count(VanillaBeltConveyorComponent belt)
            {
                return belt.BeltConveyorItems.Count(x => x != null);
            }

            #endregion
        }
    }
}
