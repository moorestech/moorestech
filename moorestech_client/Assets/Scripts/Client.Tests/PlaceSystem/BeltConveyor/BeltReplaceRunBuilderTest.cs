using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 既設ライン追従の張替え経路（ロール対応・高さ追従・空セル飛ばし・no-op除外・起点解決）を検証する
    /// Verifies the existing-line-following replace run (role mapping, height follow, skipping empty cells, no-op exclusion, origin resolution)
    /// </summary>
    public class BeltReplaceRunBuilderTest
    {
        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;
        private readonly List<GameObject> _blockObjects = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _dataStoreObject = new GameObject("BlockGameObjectDataStore");
            _dataStore = _dataStoreObject.AddComponent<BlockGameObjectDataStore>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var blockObject in _blockObjects) Object.DestroyImmediate(blockObject);
            _blockObjects.Clear();
            Object.DestroyImmediate(_dataStoreObject);
        }

        [Test]
        public void 既設のロールと向きを保って手持ちファミリーの同ロールへ写し高さも追従する()
        {
            // 歯車ライン: 直線(y0) → 上り(y0) → 直線(y1) → 分岐器(y1)。手持ちは分岐器を持つが坂を持たない SmallGear
            // Gear line: straight(y0) -> up(y0) -> straight(y1) -> splitter(y1); held SmallGear has a splitter but no slopes
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 1), ForUnitTestModBlockId.TestGearBeltConveyorUp, BlockDirection.North);
            Register(new Vector3Int(0, 1, 2), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.East);
            Register(new Vector3Int(0, 1, 3), ForUnitTestModBlockId.GearBeltConveyorSplitter, BlockDirection.North);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);

            var result = new BeltReplaceRunBuilder(_dataStore).Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 3), true, holding, out var blockCauses, out var beltReasons);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(result.Count, blockCauses.Count);
            Assert.AreEqual(result.Count, beltReasons.Count);

            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, result[0].BlockId);
            Assert.IsTrue(result[0].Placeable && result[0].IsReplace);
            Assert.AreEqual(BlockVerticalDirection.Horizontal, result[0].VerticalDirection);

            // 坂を持たない手持ちファミリーでは上りセルが不可になり、その理由が張替えロール欠落として立つ
            // A held family without slopes blocks the up cell, raising the replace-role-missing reason
            Assert.IsFalse(result[1].Placeable);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, result[1].BlockId);
            Assert.AreEqual(BlockVerticalDirection.Up, result[1].VerticalDirection);
            Assert.AreEqual(BeltConveyorPlacementBlockReason.ReplaceRoleMissing, beltReasons[1]);

            Assert.AreEqual(new Vector3Int(0, 1, 2), result[2].Position);
            Assert.AreEqual(BlockDirection.East, result[2].Direction);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, result[2].BlockId);
            Assert.IsTrue(result[2].IsReplace);

            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, result[3].BlockId);
            Assert.AreEqual(new Vector3Int(0, 1, 3), result[3].Position);
            Assert.IsTrue(result[3].IsReplace);
        }

        [Test]
        public void 既設の無いセルは飛ばして続行し同ファミリー同ロールは出さない()
        {
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 2), ForUnitTestModBlockId.SmallGearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 3), ForUnitTestModBlockId.MachineId, BlockDirection.North);
            Register(new Vector3Int(0, 0, 4), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.South);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);

            var result = new BeltReplaceRunBuilder(_dataStore).Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 4), true, holding, out var blockCauses, out var beltReasons);

            // z=1 は空、z=2 は同ティア no-op、z=3 は機械、z=0/4 だけ張替え
            // z=1 is empty, z=2 is a same-tier no-op, z=3 is a machine; only z=0 and z=4 are replaced
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(result.Count, blockCauses.Count);
            Assert.AreEqual(result.Count, beltReasons.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), result[0].Position);
            Assert.AreEqual(new Vector3Int(0, 0, 4), result[1].Position);
            Assert.AreEqual(BlockDirection.South, result[1].Direction);
        }

        [Test]
        public void 天面ヒットで1段浮いた起点は直下の既設ベルトへ解決する()
        {
            Register(new Vector3Int(3, 0, 3), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(5, 1, 5), ForUnitTestModBlockId.MachineId, BlockDirection.North);
            Register(new Vector3Int(5, 0, 5), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);

            Assert.IsTrue(BeltReplaceRunBuilder.TryResolveOrigin(_dataStore, new Vector3Int(3, 1, 3), out var origin));
            Assert.AreEqual(new Vector3Int(3, 0, 3), origin);
            Assert.IsFalse(BeltReplaceRunBuilder.TryResolveOrigin(_dataStore, new Vector3Int(3, 0, 4), out _));

            // 非ベルトが埋まっているセルは、直下にベルトがあっても張替え起点にしない
            // A cell occupied by a non-belt block is never a replace origin, even with a belt directly below
            Assert.IsFalse(BeltReplaceRunBuilder.TryResolveOrigin(_dataStore, new Vector3Int(5, 1, 5), out _));
        }

        [Test]
        public void 空き地起点の通常経路は既設ベルトを張り替えない()
        {
            Register(new Vector3Int(0, 0, 2), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());

            var result = runBuilder.Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 3), BlockDirection.North, holding, out _, out _);

            // 既設セルは立体交差で跨がれるか不可になるかのどちらかで、張替えにはならない
            // The existing cell is either overpassed or blocked, never replaced
            Assert.AreEqual(4, result.Count);
            Assert.IsTrue(result.TrueForAll(info => !info.IsReplace));
            Assert.IsFalse(result.Exists(info => info.Position == new Vector3Int(0, 0, 2)));
        }

        [Test]
        public void 既設ベルト起点の通常経路呼び出しは張替え経路へ切り替わる()
        {
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 1), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());

            var result = runBuilder.Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 2), BlockDirection.North, holding, out _, out _);

            // 既設2セルだけが張替えとして出て、既設の無いz=2は経路から落ちる
            // Only the two existing cells come out as replacements; the empty z=2 drops out of the run
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.TrueForAll(info => info.IsReplace && info.Placeable));
            Assert.IsTrue(result.TrueForAll(info => info.BlockId == ForUnitTestModBlockId.SmallGearBeltConveyor));
        }

        [Test]
        public void 高さオフセットを上げていても既設ベルトの上なら張替えになる()
        {
            // R7: 張替え中は高さオフセットを使わない。+2でも既設ラインを掴む
            // R7: the replace run ignores the height offset, so +2 still grabs the existing line
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 1), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);
            var dragState = new CommonBlockPlaceDragState();
            dragState.AdjustHeightOffset(2);
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, dragState);

            // Q/Eで+2された座標がそのまま渡ってくる
            // The coordinates arrive already raised by +2 from Q/E
            var result = runBuilder.Build(new Vector3Int(0, 2, 0), new Vector3Int(0, 2, 1), BlockDirection.North, holding, out _, out _);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.TrueForAll(info => info.IsReplace && info.Placeable));
            Assert.AreEqual(new Vector3Int(0, 0, 0), result[0].Position);
            Assert.AreEqual(new Vector3Int(0, 0, 1), result[1].Position);
        }

        [Test]
        public void ドラッグ中に高さオフセットを上げていても既設ベルトの上なら張替えになる()
        {
            // 実プレイは押下でBeginDragしてから列を組むため、起点は押下時オフセット・カーソルは現在オフセットで引かれる非対称な経路を通る
            // Real play calls BeginDrag on press before building the run, so the origin strips the press-time offset and the cursor the current one
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 1), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 2), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            var dragState = new CommonBlockPlaceDragState();
            dragState.AdjustHeightOffset(2);
            dragState.BeginDrag(new Vector3Int(0, 2, 0), PlacementHitSurfaceKind.Ground);
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, dragState);

            AssertReplacesExistingLine(runBuilder, dragState, new Vector3Int(0, 2, 1), 2);

            // ドラッグ中にEでさらに+1しても、起点は押下時の+2・カーソルは現在の+3で引かれ既設列を掴み続ける
            // Pressing E mid-drag to +3 still grabs the line: the origin strips the press-time +2 and the cursor the current +3
            dragState.AdjustHeightOffset(1);
            AssertReplacesExistingLine(runBuilder, dragState, new Vector3Int(0, 3, 2), 3);
        }

        // ドラッグ状態が解決した起点とカーソルで列を組み、既設セルがそのまま張替えとして出ることを確かめる
        // Builds the run from the drag state's origin and cursor, and checks the existing cells come out as replacements
        private static void AssertReplacesExistingLine(BeltConveyorPlaceRunBuilder runBuilder, CommonBlockPlaceDragState dragState, Vector3Int cursorPoint, int expectedCellCount)
        {
            var result = runBuilder.Build(dragState.ResolveDragStartCell(cursorPoint), cursorPoint, BlockDirection.North, BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor), out _, out _);

            Assert.AreEqual(expectedCellCount, result.Count);
            Assert.IsTrue(result.TrueForAll(info => info.IsReplace && info.Placeable));
            for (var i = 0; i < expectedCellCount; i++) Assert.AreEqual(new Vector3Int(0, 0, i), result[i].Position);
        }

        private void Register(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            var blockObject = new GameObject($"Block_{position}");
            _blockObjects.Add(blockObject);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockPosInfo), new BlockPositionInfo(position, direction, Vector3Int.one));
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockId), blockId);

            var dictionary = (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_dataStore);
            dictionary.Add(position, blockGameObject);
        }

        private static void SetBackingField(BlockGameObject blockGameObject, string propertyName, object value)
        {
            typeof(BlockGameObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, value);
        }
    }
}
