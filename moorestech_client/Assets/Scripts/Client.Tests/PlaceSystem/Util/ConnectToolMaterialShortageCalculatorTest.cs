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
        // TestRail: lengthPerUnit=5、2素材（1単位あたり×12と×5）
        // TestRail: lengthPerUnit=5 with two materials, x12 and x5 per unit
        private static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");

        // TestElectricWire: lengthPerUnit=1、単一素材×1
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
            // 距離6はlengthPerUnit=5で2単位。24個と10個が要る
            // A distance of 6 is 2 units at lengthPerUnit=5, requiring 24 and 10
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(RailConnectToolGuid, 6f, BuildInventory((RailMaterial1Guid, 0), (RailMaterial2Guid, 0)), null);

            Assert.AreEqual(2, shortages.Count);
            AssertShortage(shortages[0], RailMaterial1Guid, 0, 24);
            AssertShortage(shortages[1], RailMaterial2Guid, 0, 10);
        }

        [Test]
        public void 足りている素材は不足行に出ず足りない素材だけが残る()
        {
            // 1つ目の素材だけ十分に持たせると2つ目の不足1件だけが残る
            // Holding plenty of the first material leaves only the second as short
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
            // 所持3・必要1でも、同じアイテムの予約が3あれば不足になる
            // Holding 3 against a requirement of 1 still falls short once 3 of the same item are reserved
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
