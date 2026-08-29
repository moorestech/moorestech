using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    // 既存ブロックの重なり判定を検証する（実データストア経路のためDI起動とGameObjectを使う）
    // Verify the existing-block overlap check; the real datastore path needs the DI boot and a GameObject
    public class CommonBlockPlaceExistingBlockCauseTest
    {
        // 重なるセルだけが不可になり、原因はExistingBlockになる
        // Only the overlapping cell becomes unplaceable, with an ExistingBlock cause
        [Test]
        public void 重なるセルだけが不可になる()
        {
            var run = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, MakeMockBlock());

            CommonBlockPlacePointCalculator.EvaluateExistingBlockCauses(run, new StubExistingBlockQuery(new Vector3Int(1, 0, 0)));

            Assert.IsTrue(run.Cells[0].Placeable);
            Assert.IsFalse(run.Cells[1].Placeable);
            Assert.IsTrue(run.Cells[2].Placeable);

            // 不可原因の列はセル列と同じ添字で並走する
            // The block cause column runs alongside the cell list on the same index
            Assert.AreEqual(PlacementBlockCause.None, run.BlockCauses[0]);
            Assert.AreEqual(PlacementBlockCause.ExistingBlock, run.BlockCauses[1]);
            Assert.AreEqual(PlacementBlockCause.None, run.BlockCauses[2]);
        }

        // 地表欠落で既に不可のセルは重なり判定で原因を上書きされない
        // A cell already blocked by missing ground keeps its cause through the overlap check
        [Test]
        public void 先に立った原因は上書きされない()
        {
            var run = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, Vector3Int.zero, BlockDirection.North, MakeMockBlock());
            run.Cells[0].Placeable = false;
            run.BlockCauses[0] = PlacementBlockCause.GroundNotFound;

            CommonBlockPlacePointCalculator.EvaluateExistingBlockCauses(run, new StubExistingBlockQuery(Vector3Int.zero));

            Assert.AreEqual(PlacementBlockCause.GroundNotFound, run.BlockCauses[0]);
        }

        // 実データストアに登録済みのブロックへ重なると不可扱い
        // Overlapping a block registered in the real datastore is treated as blocked
        [Test]
        public void 実データストアの既存ブロックを検出する()
        {
            // 重なり判定がマスタを引くため先にロード
            // The overlap check reads MasterHolder, so load the masters first
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var dataStoreObject = new GameObject("BlockGameObjectDataStore");
            var dataStore = dataStoreObject.AddComponent<BlockGameObjectDataStore>();
            var calculator = new CommonBlockPlacePointCalculator(dataStore);

            var blockElement = MasterHolder.BlockMaster.Blocks.Data[0];
            var overlapPosition = new Vector3Int(0, 5, 0);

            // PlaceBlockもBlockGameObject.Initializeもプレハブロードとサーバー購読を要するため、辞書へ直接1件登録する
            // Both PlaceBlock and BlockGameObject.Initialize need a prefab load and a server subscription, so register one entry directly
            var existingBlockObject = new GameObject("ExistingBlock").AddComponent<BlockGameObject>();
            SetBlockPosInfo(existingBlockObject, new BlockPositionInfo(overlapPosition, BlockDirection.North, blockElement.BlockSize));
            var dictionary = (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(dataStore);
            dictionary.Add(overlapPosition, existingBlockObject);

            var run = CommonBlockPlacePointCalculator.CalculateRun(
                overlapPosition, overlapPosition, BlockDirection.North, blockElement);

            calculator.EvaluateExistingBlockCauses(run);

            Assert.IsFalse(run.Cells[0].Placeable);
            Assert.AreEqual(PlacementBlockCause.ExistingBlock, run.BlockCauses[0]);

            UnityEngine.Object.DestroyImmediate(existingBlockObject.gameObject);
            UnityEngine.Object.DestroyImmediate(dataStoreObject);
        }

        // 指定セルだけ重なっていると答える問い合わせ窓口のテストダブル
        // A test double of the query port that reports an overlap on one cell only
        private class StubExistingBlockQuery : IExistingBlockQuery
        {
            private readonly Vector3Int _overlapPosition;

            public StubExistingBlockQuery(Vector3Int overlapPosition)
            {
                _overlapPosition = overlapPosition;
            }

            public bool IsOverlapping(PlaceInfo placeInfo)
            {
                return placeInfo.Position == _overlapPosition;
            }
        }

        // 自動プロパティのバッキングフィールドへ直接書き、Initializeの外部依存を避ける
        // Writes the auto-property backing field directly, avoiding Initialize's external dependencies
        private static void SetBlockPosInfo(BlockGameObject blockGameObject, BlockPositionInfo posInfo)
        {
            typeof(BlockGameObject)
                .GetField($"<{nameof(BlockGameObject.BlockPosInfo)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, posInfo);
        }

        // ゼロGuidのモック要素はMasterHolderを引かないため実DI起動が要らない
        // A zero-Guid mock element never reads MasterHolder, so it needs no real DI boot
        private static BlockMasterElement MakeMockBlock()
        {
            return new BlockMasterElement(
                0,
                Guid.Empty,
                "TestBlock",
                "TestBlockType",
                null,
                1, // placementsPerCost
                null,
                "テスト",
                "テスト",
                0,
                false,
                Vector3Int.one,
                null,
                null,
                null
            );
        }
    }
}
