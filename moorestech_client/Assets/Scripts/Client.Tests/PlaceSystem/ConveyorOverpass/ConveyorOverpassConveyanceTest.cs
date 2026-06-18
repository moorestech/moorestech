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
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ConveyorOverpass
{
    // 実際の自動立体交差配置(CommonBlockPlacePointCalculator)で障害物を跨ぐベルト列を生成し、アイテムが流れることを検証する
    // Verify the real auto-overpass placement generates a belt run stepping over obstacles and items flow across it.
    public class ConveyorOverpassConveyanceTest
    {
        // 単一障害物(2,0,0)を跨いで終端まで搬送する
        // Step over a single obstacle at (2,0,0) and convey to the far belt.
        [Test]
        public void AutoOverpass_SingleObstacle_ConveysAcross()
        {
            AssertOverpassConveys(new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0),
                new[] { new Vector3Int(2, 0, 0) }, middleX: 2, expectedMiddleY: 1);
        }

        // 不具合1の実搬送: 1セル間隔の平行2ベルト(1,3)を橋渡しで跨ぎ、終端まで搬送する
        // Bug 1 end-to-end: bridge over two parallel belts one cell apart (cells 1 and 3) and convey to the far belt.
        [Test]
        public void AutoOverpass_TwoParallelBeltsGap1_ConveysAcross()
        {
            AssertOverpassConveys(new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0),
                new[] { new Vector3Int(1, 0, 0), new Vector3Int(3, 0, 0) }, middleX: 2, expectedMiddleY: 1);
        }

        private void AssertOverpassConveys(Vector3Int start, Vector3Int end, Vector3Int[] obstacles, int middleX, int expectedMiddleY)
        {
            // 自己完結したテスト用Modでサーバーを起動する（外部リポジトリ非依存）
            // Boot the server with the self-contained test mod (no external repo dependency).
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 障害物を設置する（立体交差はこの上を跨ぐ）
            // Place obstacles; the overpass must step over them.
            foreach (var cell in obstacles)
                Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.BlockId, cell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            // 歯車ベルトのドラッグ配置を本番の計算経路で求める
            // Compute the dragged gear-belt placement via the production calc path.
            var holding = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor);
            var placeInfos = CommonBlockPlacePointCalculator.CalculatePoint(
                start, end, false, BlockDirection.East, holding,
                (info, _) => !world.Exists(info.Position),
                cell => world.Exists(cell));

            // プロファイルを確認する（中央セルが想定の高さへ橋渡しされている）
            // Confirm the profile bridged the middle cell to the expected height.
            var plan = string.Join(" ", placeInfos.Select(p => $"{p.Position}:{p.VerticalDirection}"));
            Debug.Log($"overpass plan: {plan}");
            Assert.AreEqual(expectedMiddleY, placeInfos.First(p => p.Position.x == middleX).Position.y, $"中央セル高さが想定外 / unexpected middle cell height. {plan}");

            // 設置→先頭にアイテム挿入→毎tick全歯車ベルトへ動力供給して回す
            // Place -> insert an item on the first belt -> tick while powering every gear belt.
            var belts = PlaceComputedBelts();
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
                // 本番(PlaceBlockFromHotBarProtocol)のうち縦方向override→TryAddBlock部分を再現する（inventory/hotbar経由ではない）
                // Reproduce production's (PlaceBlockFromHotBarProtocol) vertical-override -> TryAddBlock step (not the full inventory/hotbar protocol).
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
