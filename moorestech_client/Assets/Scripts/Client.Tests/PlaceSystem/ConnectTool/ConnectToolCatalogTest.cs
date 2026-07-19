using System;
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
