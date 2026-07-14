using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ConnectTool
{
    public class ConnectToolCatalogTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 表示順と表示名をツール種別から取得できる()
        {
            var displayOrder = ConnectToolCatalog.GetDisplayOrder();

            Assert.AreEqual(3, displayOrder.Count);
            Assert.AreEqual(ConnectToolType.TrainRailConnect, displayOrder[0]);
            Assert.AreEqual(ConnectToolType.GearChainPoleConnect, displayOrder[1]);
            Assert.AreEqual(ConnectToolType.ElectricWireConnect, displayOrder[2]);
            Assert.AreEqual("レール敷設", ConnectToolCatalog.GetDisplayName(displayOrder[0]));
            Assert.AreEqual("歯車チェーン接続", ConnectToolCatalog.GetDisplayName(displayOrder[1]));
            Assert.AreEqual("電線接続", ConnectToolCatalog.GetDisplayName(displayOrder[2]));
        }

        [Test]
        public void 表示順は全ツール種別を重複なく含む()
        {
            var displayOrder = ConnectToolCatalog.GetDisplayOrder();
            var allToolTypes = (ConnectToolType[])Enum.GetValues(typeof(ConnectToolType));

            CollectionAssert.AreEquivalent(allToolTypes, displayOrder);
            Assert.AreEqual(displayOrder.Count, displayOrder.Distinct().Count());
            foreach (var toolType in allToolTypes)
            {
                Assert.DoesNotThrow(() => ConnectToolCatalog.GetDisplayName(toolType));
                Assert.DoesNotThrow(() => ConnectToolCatalog.SelectIconItemGuid(toolType));
                Assert.DoesNotThrow(() => ConnectToolCatalog.TryGetPlaceBlock(toolType, out _, out _));
            }
        }

        [Test]
        public void 表示順は外部から変更できない()
        {
            var displayOrder = (IList<ConnectToolType>)ConnectToolCatalog.GetDisplayOrder();

            Assert.Throws<NotSupportedException>(() => displayOrder[0] = ConnectToolType.ElectricWireConnect);
        }

        [Test]
        public void 未知のツール種別は表示名取得時に失敗する()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ConnectToolCatalog.GetDisplayName((ConnectToolType)999));
        }

        [Test]
        public void 各ツールのアイコン素材を先頭マスタから取得する()
        {
            Assert.AreEqual(MasterHolder.TrainUnitMaster.GetRailItems()[0].ItemGuid, ConnectToolCatalog.SelectIconItemGuid(ConnectToolType.TrainRailConnect));
            Assert.AreEqual(MasterHolder.BlockMaster.Blocks.GearChainItems[0].ItemGuid, ConnectToolCatalog.SelectIconItemGuid(ConnectToolType.GearChainPoleConnect));
            Assert.AreEqual(MasterHolder.BlockMaster.Blocks.ElectricWireItems[0].ItemGuid, ConnectToolCatalog.SelectIconItemGuid(ConnectToolType.ElectricWireConnect));
        }

        [Test]
        public void 空きスペース延長用ブロックをツール種別ごとに取得する()
        {
            // 元配列を逆順にし、優先度と名前の整列を必須にする
            // Reverse source order so priority and name sorting are required
            Array.Reverse(MasterHolder.BlockMaster.Blocks.Data);

            Assert.IsTrue(ConnectToolCatalog.TryGetPlaceBlock(ConnectToolType.TrainRailConnect, out var railBlockId, out var railBlockMaster));
            Assert.AreEqual(ForUnitTestModBlockId.TestTrainRail, railBlockId);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(railBlockId).BlockGuid, railBlockMaster.BlockGuid);

            Assert.IsTrue(ConnectToolCatalog.TryGetPlaceBlock(ConnectToolType.ElectricWireConnect, out var poleBlockId, out var poleBlockMaster));
            Assert.AreEqual(ForUnitTestModBlockId.ElectricPoleId, poleBlockId);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(poleBlockId).BlockGuid, poleBlockMaster.BlockGuid);

            Assert.IsFalse(ConnectToolCatalog.TryGetPlaceBlock(ConnectToolType.GearChainPoleConnect, out _, out var gearBlockMaster));
            Assert.IsNull(gearBlockMaster);
        }
    }
}
