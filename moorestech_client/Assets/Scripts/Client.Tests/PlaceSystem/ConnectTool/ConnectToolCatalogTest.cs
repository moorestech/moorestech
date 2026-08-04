using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BuildMenuModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ConnectTool
{
    public class ConnectToolCatalogTest
    {
        private ServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            (_, _serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 未解放のときはデフォルト接続ツールを解決しない()
        {
            var unlockState = _serviceProvider.GetService<IGameUnlockStateDataController>();

            // テストmodのconnectToolは全てinitialUnlocked=falseで、初期状態は全未解放
            // Every connectTool in the test mod is initialUnlocked=false, so all start locked
            Assert.IsTrue(unlockState.ConnectToolUnlockStateInfos.Values.All(info => !info.IsUnlocked));

            Assert.IsFalse(ConnectToolCatalog.TryResolveDefaultConnectToolGuid(ConnectToolType.ElectricWireConnect, unlockState, out var connectToolGuid));
            Assert.AreEqual(Guid.Empty, connectToolGuid);
        }

        [Test]
        public void 解放済みなら種別からデフォルト接続ツールを解決する()
        {
            var unlockState = _serviceProvider.GetService<IGameUnlockStateDataController>();
            var electricWireGuid = MasterHolder.ConnectToolMaster.All.First(element => element.ToolType == ConnectToolMasterElement.ToolTypeConst.electricWire).ConnectToolGuid;
            unlockState.UnlockConnectTool(electricWireGuid);

            Assert.IsTrue(ConnectToolCatalog.TryResolveDefaultConnectToolGuid(ConnectToolType.ElectricWireConnect, unlockState, out var connectToolGuid));
            Assert.AreEqual(electricWireGuid, connectToolGuid);
        }

        [Test]
        public void 空きスペース延長用ブロックをツール種別ごとに取得する()
        {
            // 元配列を逆順にし、優先度とGuidの整列を必須にする
            // Reverse source order so priority and Guid sorting are required
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
