using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.Util
{
    /// <summary>
    ///     接続ツールの不足素材算出を検証する
    ///     Verifies the connect tool's material shortage calculation
    ///     必要数は ceil(距離 / lengthPerUnit) × 素材ごとのcount
    ///     The requirement is ceil(distance / lengthPerUnit) x each material's count
    /// </summary>
    public class ConnectToolMaterialShortageCalculatorTest
    {
        // lengthPerUnit=5、2素材(×12と×5)
        // TestRail: lengthPerUnit=5 with two materials, x12 and x5 per unit
        private static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");

        // lengthPerUnit=1、単一素材×1
        // TestElectricWire: lengthPerUnit=1 with a single material x1
        private static readonly Guid WireConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");

        private static readonly Guid WireMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid RailMaterial1Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private static readonly Guid RailMaterial2Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 距離を単位長で割り上げた単位数だけ素材ごとの必要数が増える()
        {
            // 距離6は2単位。24個と10個必要
            // A distance of 6 is 2 units, requiring 24 and 10
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(RailConnectToolGuid, 6f, BuildInventory((RailMaterial1Guid, 0), (RailMaterial2Guid, 0)), null);

            Assert.AreEqual(2, shortages.Count);
            AssertShortage(shortages[0], RailMaterial1Guid, 0, 24);
            AssertShortage(shortages[1], RailMaterial2Guid, 0, 10);
        }

        [Test]
        public void 足りている素材は不足行に出ず足りない素材だけが残る()
        {
            // 1つ目十分・2つ目だけ不足が残る
            // First plenty, only the second stays short
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(RailConnectToolGuid, 5f, BuildInventory((RailMaterial1Guid, 50), (RailMaterial2Guid, 2)), null);

            Assert.AreEqual(1, shortages.Count);
            AssertShortage(shortages[0], RailMaterial2Guid, 2, 5);
        }

        [Test]
        public void 全素材が足りていれば不足は0件になる()
        {
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(RailConnectToolGuid, 5f, BuildInventory((RailMaterial1Guid, 12), (RailMaterial2Guid, 5)), null);

            Assert.IsEmpty(shortages);
        }

        [Test]
        public void 予約素材は必要数へ上乗せされる()
        {
            // 予約3があれば所持3必要1でも不足
            // With 3 reserved it falls short even holding 3 against 1
            var reserved = new List<ConnectToolMaterialCost> { new(MasterHolder.ItemMaster.GetItemId(WireMaterialGuid), 3) };

            var shortages = ConnectToolMaterialShortageCalculator.Calculate(WireConnectToolGuid, 1f, BuildInventory((WireMaterialGuid, 3)), reserved);

            Assert.AreEqual(1, shortages.Count);
            AssertShortage(shortages[0], WireMaterialGuid, 3, 4);
        }

        [Test]
        public void マスタに無い接続ツールでは不足を作らず呼び出し元の落とし先に委ねる()
        {
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(Guid.Empty, 5f, BuildInventory((WireMaterialGuid, 0)), null);

            Assert.IsEmpty(shortages);
        }

        private static void AssertShortage(ConstructionMaterialShortage shortage, Guid expectedItemGuid, int expectedHeld, int expectedRequired)
        {
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(expectedItemGuid), shortage.ItemId);
            Assert.AreEqual(expectedHeld, shortage.Held);
            Assert.AreEqual(expectedRequired, shortage.Required);
        }

        private static List<IItemStack> BuildInventory(params (Guid itemGuid, int count)[] items)
        {
            var stacks = new List<IItemStack>(items.Length);
            foreach (var (itemGuid, count) in items) stacks.Add(ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
            return stacks;
        }
    }
}
