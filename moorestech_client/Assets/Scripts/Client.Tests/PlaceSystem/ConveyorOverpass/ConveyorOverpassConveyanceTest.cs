using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ConveyorOverpass
{
    // 実際の自動立体交差配置(BeltConveyorPlacePointCalculator)で障害物を跨ぐベルト列を生成し、アイテムが流れることを検証する
    // Verify the real auto-overpass placement generates a belt run stepping over obstacles and items flow across it.
    public class ConveyorOverpassConveyanceTest
    {
        // 斜面を含む5マスは1つのsegmentになる。速度32・進入距離1で出口まで5*256-1=1279。39tick後に残り31、40tick目に渡る
        // The five cells including slopes form one segment; at speed 32 entering at length 1 leaves 5*256-1=1279, 31 away after 39 ticks, handed over on tick 40
        private const int ExpectedArrivalTick = 40;
        private const int MaxTransportTicks = 200;

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
            var placeInfos = BeltConveyorPlacePointCalculator.CalculateStraightPoint(
                start, end, false, BlockDirection.East, holding,
                (info, _) => !world.Exists(info.Position),
                cell => world.Exists(cell), out _, out _);

            // プロファイルを確認する（中央セルが想定の高さへ橋渡しされている）
            // Confirm the profile bridged the middle cell to the expected height.
            var plan = string.Join(" ", placeInfos.Select(p => $"{p.Position}:{p.VerticalDirection}"));
            Debug.Log($"overpass plan: {plan}");
            Assert.AreEqual(expectedMiddleY, placeInfos.First(p => p.Position.x == middleX).Position.y, $"中央セル高さが想定外 / unexpected middle cell height. {plan}");

            // 立体交差プロファイル通りに全ブロックを設置し、両端にチェストを置いてアイテムを流す
            // Place every block along the overpass profile, put chests at both ends and send an item through
            PlaceComputedBelts();
            var itemId = new ItemId(1);
            var source = PlaceChest(start - Vector3Int.right);
            var output = PlaceChest(end + Vector3Int.right);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 1));

            // 歯車ベルトは動力なしでもマスタ固定速度で流れる。到着tickを計測し、1segmentとしての到着tickと一致することを確認する
            // Gear belts run at the fixed master speed without power; measure the arrival tick and check it matches a single segment's arrival tick
            var arrivalTick = 0;
            for (var tick = 1; tick <= MaxTransportTicks && arrivalTick == 0; tick++)
            {
                GameUpdater.UpdateOneTick();
                if (CountOf(output, itemId) == 1) arrivalTick = tick;
            }
            Debug.Log($"overpass arrival tick: {arrivalTick}");
            Assert.AreNotEqual(0, arrivalTick, $"立体交差を越えて終端チェストへ届かない / item did not reach the far chest. {plan}");
            Assert.AreEqual(ExpectedArrivalTick, arrivalTick, $"立体交差が1segmentになっていない / the overpass is not a single segment. {plan}");
            Assert.AreEqual(0, CountOf(source, itemId));

            #region Internal

            void PlaceComputedBelts()
            {
                // 本番(PlaceBlockProtocol)のうち縦方向override→TryAddBlock部分を再現する（プロトコル全体は経由しない）
                // Reproduce production's (PlaceBlockProtocol) vertical-override -> TryAddBlock step (not the full protocol).
                foreach (var info in placeInfos)
                {
                    if (!info.Placeable) continue;
                    var blockId = ResolveVerticalBlockId(info.VerticalDirection);
                    Assert.IsTrue(world.TryAddBlock(blockId, info.Position, info.Direction, Array.Empty<BlockCreateParam>(), out _), $"設置失敗 / placement failed at {info.Position}");
                }
            }

            IOpenableBlockInventoryComponent PlaceChest(Vector3Int position)
            {
                Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ChestId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest));
                return chest.GetComponent<IOpenableBlockInventoryComponent>();
            }

            int CountOf(IOpenableBlockInventoryComponent inventory, ItemId targetItemId)
            {
                var count = 0;
                for (var i = 0; i < inventory.GetSlotSize(); i++)
                    if (inventory.GetItem(i).Id == targetItemId) count += inventory.GetItem(i).Count;
                return count;
            }

            // 斜面ブロックから向き別BlockIdを解決（水平は元のまま）
            // Resolve per-cell BlockId from slope blocks (horizontal keeps original)
            BlockId ResolveVerticalBlockId(BlockVerticalDirection verticalDirection)
            {
                var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holding.BlockGuid);
                if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(holdingBlockId, out var family)) return holdingBlockId;

                return verticalDirection switch
                {
                    BlockVerticalDirection.Up => family.UpBlockId.Value,
                    BlockVerticalDirection.Down => family.DownBlockId.Value,
                    _ => holdingBlockId,
                };
            }

            #endregion
        }
    }
}
