using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    // 選択ブロック→手持ちブロックの解決を検証する
    // - 分岐器が直線へ化けないことが本テストの主眼（設置ブロックの無言の取り違え防止）
    // Verifies how a build-menu selection resolves into the held block
    // - The main point is that a splitter never turns into a straight belt (silent placement mix-up)
    public class BeltConveyorHoldingBlockTest
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

        [Test]
        public void 分岐器を選ぶと手持ちは分岐器のままで坂の自動挿入は無効()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyorSplitter);

            Assert.AreEqual(BeltConveyorRole.Splitter, holding.Role);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, holding.BlockId);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, MasterHolderBlockId(holding));
            Assert.IsNull(holding.SlopeGrade);
            Assert.IsNull(holding.RunUpBlockId);
            Assert.IsNull(holding.RunDownBlockId);
        }

        // Resolve単体では取り違えを取り逃すため、経路構築まで通して設置ブロックを確認する
        // Resolve alone can miss the mix-up, so run through path building and check the placed block
        [Test]
        public void 分岐器のドラッグは全セルが分岐器になる()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyorSplitter);
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());

            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(0, 0, 2), BlockDirection.North, holding, out _, out _);

            Assert.AreEqual(3, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == ForUnitTestModBlockId.GearBeltConveyorSplitter));
        }

        [Test]
        public void 直線を選ぶと坂の自動挿入が有効()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);

            Assert.AreEqual(BeltConveyorRole.Straight, holding.Role);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, holding.BlockId);
            Assert.IsNull(holding.SlopeGrade);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, holding.RunUpBlockId);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorDown, holding.RunDownBlockId);
        }

        [Test]
        public void 坂を選ぶと勾配が立ち手持ちは坂自身()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.TestGearBeltConveyorUp);

            Assert.AreEqual(BeltConveyorRole.Up, holding.Role);
            Assert.AreEqual(BeltSlopeGrade.Up, holding.SlopeGrade);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, holding.BlockId);
        }

        // BlockMasterが手持ちブロックのものか（プレビュー形状が別ブロックへ化けないか）を確認する
        // Confirms BlockMaster belongs to the held block, so the preview shape cannot swap to another block
        private static BlockId MasterHolderBlockId(BeltConveyorHoldingBlock holding)
        {
            return MasterHolder.BlockMaster.GetBlockId(holding.BlockMaster.BlockGuid);
        }
    }
}
