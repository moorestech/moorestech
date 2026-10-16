using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     通常設置の入口で地形めり込みと理由表示を検証する（ADR 0081）
    ///     Verifies terrain sinks and feedback through normal placement's entry (ADR 0081)
    /// </summary>
    public class NormalPlacementPreviewStepTest
    {
        private GameObject _ground;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // 地表30.3。底30は可、29のみ埋まり
            // Surface 30.3: base 30 is clear, only 29 is sunk
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(501.5f, 29.8f, 500.5f);
            _ground.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ground);
        }

        [Test]
        public void 一セル沈めたセルだけ不可でカーソルなら地形理由が出る()
        {
            var cells = BuildCells();
            cells[1].Position = new Vector3Int(501, 29, 500);
            var feedback = new PlacementFeedback();
            var preview = new RecordingPreviewController();

            var cursorIndex = Apply(preview, cells, PlacementBlockCause.None, feedback);

            Assert.AreEqual(1, cursorIndex);
            Assert.AreEqual(1, preview.SetPreviewCallCount);
            Assert.IsTrue(cells[0].Placeable);
            Assert.IsFalse(cells[1].Placeable);
            Assert.IsTrue(cells[2].Placeable);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 既定Yなら全セル設置可で地形理由を出さない()
        {
            var cells = BuildCells();
            var feedback = new PlacementFeedback();

            Apply(new RecordingPreviewController(), cells, PlacementBlockCause.None, feedback);

            foreach (var cell in cells) Assert.IsTrue(cell.Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 既存ブロックの理由は積む()
        {
            var feedback = new PlacementFeedback();
            Apply(new RecordingPreviewController(), BuildCells(), PlacementBlockCause.ExistingBlock, feedback);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        private static int Apply(RecordingPreviewController preview, List<PlaceInfo> cells, PlacementBlockCause cursorCause, PlacementFeedback feedback)
        {
            var causes = new[] { PlacementBlockCause.None, cursorCause, PlacementBlockCause.None };
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId);
            return NormalPlacementPreviewStep.Apply(preview, cells, causes, cells[1].Position, master, feedback);
        }

        private static List<PlaceInfo> BuildCells()
        {
            var cells = new List<PlaceInfo>();
            for (var i = 0; i < 3; i++)
            {
                cells.Add(new PlaceInfo { Position = new Vector3Int(500 + i, 30, 500), Direction = BlockDirection.North, BlockId = ForUnitTestModBlockId.BeltConveyorId, Placeable = true });
            }
            return cells;
        }

        private class RecordingPreviewController : IPlacementPreviewBlockGameObjectController
        {
            public int SetPreviewCallCount { get; private set; }
            public bool IsActive => true;
            public void SetPreview(List<PlaceInfo> cells, BlockMasterElement master) => SetPreviewCallCount++;
            public void UpdatePlaceableColors(List<PlaceInfo> placeInfos) { }
            public void SetActive(bool active) { }
            public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
            {
                previewBlock = null;
                return false;
            }
        }
    }
}
