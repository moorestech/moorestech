using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    // ドラッグ軸の寿命がドラッグ状態に縛られていることを、Buildの実経路で検証する
    // Verifies through the production Build path that the drag axis lives on the drag state
    public class BeltConveyorPlaceRunAxisTest
    {
        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;

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
            Object.DestroyImmediate(_dataStoreObject);
        }

        // 軸はドラッグ状態が持ち、起点セルへ戻すまで維持される
        // The drag state owns the axis and keeps it until the cursor returns to the start cell
        [Test]
        public void 起点セルへ戻すとBuildが軸を引き直す()
        {
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);
            var dragState = new CommonBlockPlaceDragState();
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, dragState);
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);

            // Z優勢の離脱で軸はZ先行に決まる
            // A Z-dominant departure fixes the axis to Z-first
            var zLead = runBuilder.Build(Vector3Int.zero, new Vector3Int(1, 0, 3), BlockDirection.North, holdingBlock, out _, out _);
            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, 2), new Vector3Int(0, 0, 3), new Vector3Int(1, 0, 3) },
                zLead.Select(info => info.Position).ToList());

            // X優勢へ動かしても決まった軸は変わらない
            // Moving into an X-dominant range does not flip the decided axis
            var stillZLead = runBuilder.Build(Vector3Int.zero, new Vector3Int(3, 0, 1), BlockDirection.North, holdingBlock, out _, out _);
            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(2, 0, 1), new Vector3Int(3, 0, 1) },
                stillZLead.Select(info => info.Position).ToList());

            // 起点セルへ戻すと軸は未決化し、次の離脱で引き直される
            // Returning to the start cell clears the axis, so the next departure redraws it
            runBuilder.Build(Vector3Int.zero, Vector3Int.zero, BlockDirection.North, holdingBlock, out _, out _);
            var xLead = runBuilder.Build(Vector3Int.zero, new Vector3Int(3, 0, 1), BlockDirection.North, holdingBlock, out _, out _);
            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0), new Vector3Int(3, 0, 0), new Vector3Int(3, 0, 1) },
                xLead.Select(info => info.Position).ToList());
        }

        // ドラッグ終了で軸も捨てられ、次のドラッグは軸未決から始まる
        // Ending a drag drops the axis too, so the next drag starts undecided
        [Test]
        public void ドラッグ終了で軸も一緒に捨てられる()
        {
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);
            var dragState = new CommonBlockPlaceDragState();
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, dragState);

            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);
            runBuilder.Build(Vector3Int.zero, new Vector3Int(1, 0, 3), BlockDirection.North, holdingBlock, out _, out _);
            dragState.EndDrag();

            // 前ドラッグのZ軸が残っていればZ先行になるが、捨てられているのでX先行になる
            // A leaked Z axis would lead with Z; since it is dropped, the run leads with X
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);
            var xLead = runBuilder.Build(Vector3Int.zero, new Vector3Int(3, 0, 1), BlockDirection.North, holdingBlock, out _, out _);
            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0), new Vector3Int(3, 0, 0), new Vector3Int(3, 0, 1) },
                xLead.Select(info => info.Position).ToList());
        }
    }
}
