using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using static Client.Tests.UnitTest.Tutorial.PlacementGuide.PlacementGuideTutorialTestFixture;

namespace Client.Tests.UnitTest.Tutorial.PlacementGuide
{
    /// <summary>
    ///     相対座標ゴーストの完了判定とエントリ独立性を検証する
    ///     Verifies the relative ghost's completion check and the independence of its entries
    /// </summary>
    public class RelativeBlockPlacePreviewTutorialManagerTest
    {
        private PlacementGuideTutorialTestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new PlacementGuideTutorialTestFixture("RelativeBlockPlacePreviewTutorialManagerTest");
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.TearDown();
        }

        [Test]
        public void 相対ゴーストは目標セルでも向きが違えば完了しない()
        {
            _fixture.SetTutorial("relativeBlockPlacePreview", CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-00000000000e", 0, 0, 1));
            var manager = _fixture.CreateTutorialManager(new VeinRestrictedPlacementState(), new List<ITutorialViewManager>());
            var relative = _fixture.Relative;

            manager.ApplyTutorial(ChallengeGuid);
            var entry = (RelativeBlockPlacePreviewEntry)GetAppliedView(manager);
            var targetCell = new Vector3Int(3, 0, 4);
            entry.SetTarget(targetCell, BlockDirection.East);

            // 繋がらない向きで置いても案内は残る
            // A direction that never connects leaves the guide up
            InvokeOnBlockPlaced(relative, CreatePlacedBlock(entry.TargetBlockId, targetCell, BlockDirection.North));
            Assert.IsTrue(HasActiveEntry(relative, entry.TutorialGuid), "a mismatched direction completed the guide");

            InvokeOnBlockPlaced(relative, CreatePlacedBlock(entry.TargetBlockId, targetCell, BlockDirection.East));
            Assert.IsFalse(HasActiveEntry(relative, entry.TutorialGuid), "the matching direction did not complete the guide");

            #region Internal

            // Initializeはプレハブを要するため値だけ注入する
            // BlockGameObject.Initialize needs a prefab load and a server subscription, so only the placed-block values are injected
            BlockGameObject CreatePlacedBlock(BlockId blockId, Vector3Int cell, BlockDirection direction)
            {
                var block = new GameObject("PlacedBlock").AddComponent<BlockGameObject>();
                block.transform.SetParent(_fixture.Root.transform);

                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockId)).GetSetMethod(true).Invoke(block, new object[] { blockId });
                typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true).Invoke(block, new object[] { new BlockPositionInfo(cell, direction, blockSize) });
                return block;
            }

            // 設置検知は購読経由なのでprivateハンドラを直接呼ぶ
            // The placement hook is only reachable through the datastore subscription, so the private handler is invoked directly
            void InvokeOnBlockPlaced(RelativeBlockPlacePreviewTutorialManager targetRelative, BlockGameObject block)
            {
                typeof(RelativeBlockPlacePreviewTutorialManager)
                    .GetMethod("OnBlockPlaced", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(targetRelative, new object[] { block });
            }

            #endregion
        }

        [Test]
        public void 同一チャレンジ内の相対ゴースト2件は上書きされず両方生きる()
        {
            _fixture.SetRelativeTutorials(
                CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-00000000000e", 0, 0, 1),
                CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-000000000006", 0, 0, 2));
            var manager = _fixture.CreateTutorialManager(new VeinRestrictedPlacementState(), new List<ITutorialViewManager>());
            var relative = _fixture.Relative;

            manager.ApplyTutorial(ChallengeGuid);

            // 2エントリが独立し片方完了で他方は残る
            // Both entries stay alive as independent views; completing one never folds the other
            var views = GetAppliedViews(manager);
            Assert.AreEqual(2, views.Count);
            var first = (RelativeBlockPlacePreviewEntry)views[0];
            var second = (RelativeBlockPlacePreviewEntry)views[1];
            Assert.AreNotSame(first, second);
            Assert.IsTrue(HasActiveEntry(relative, first.TutorialGuid));
            Assert.IsTrue(HasActiveEntry(relative, second.TutorialGuid));

            first.CompleteTutorial();
            Assert.IsFalse(HasActiveEntry(relative, first.TutorialGuid));
            Assert.IsTrue(HasActiveEntry(relative, second.TutorialGuid));
        }

        // manager内部の保持中エントリはproductionに公開しないため、reflectionで読み出して突き合わせる
        // The manager's active entries are not exposed in production, so reflection reads them back for comparison
        private static bool HasActiveEntry(RelativeBlockPlacePreviewTutorialManager relative, Guid tutorialGuid)
        {
            var entries = (Dictionary<Guid, RelativeBlockPlacePreviewEntry>)typeof(RelativeBlockPlacePreviewTutorialManager)
                .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(relative);
            return entries.ContainsKey(tutorialGuid);
        }
    }
}
