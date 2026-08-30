using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 橋脚コストの予約と橋脚自身の可否を、production の入口をそのまま起動して検証する
    /// Verifies the pier cost reservation and the pier's own affordability by driving the production entry point itself
    /// </summary>
    public class TrainRailPierReservationTest
    {
        // lengthPerUnit=5、2素材(RailMaterial1×12・RailMaterial2×5)
        // TestRail: lengthPerUnit=5 with two materials, RailMaterial1 x12 and RailMaterial2 x5 per unit
        private static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid RailMaterial1Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        // 橋脚コスト側の素材。RailMaterial2と同一アイテムなので予約が必要数へ上乗せされる
        // The pier cost materials; the first is the same item as RailMaterial2, so the reservation stacks onto the requirement
        private static readonly Guid PierMaterial1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid PierMaterial2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        // 橋脚コストが予約として上乗せされるため、レール単体なら足りる所持でも不足になる
        // The pier cost is added on top as a reservation, so an inventory that covers the rail alone still falls short
        public void 橋脚コストの予約がレール必要数へ上乗せされる()
        {
            // レール分5＋橋脚分2で7必要なところを6しか持たない
            // 7 are needed (5 for the rail, 2 for the pier) against only 6 held
            var judgement = Evaluate(BuildInventory((RailMaterial1Guid, 50), (PierMaterial1Guid, 6), (PierMaterial2Guid, 5)), 1);

            Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem, judgement.Judgement.FailureReason);
            Assert.AreEqual(1, judgement.RailMaterialShortages.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(PierMaterial1Guid), judgement.RailMaterialShortages[0].ItemId);
            Assert.AreEqual(6, judgement.RailMaterialShortages[0].Held);
            Assert.AreEqual(7, judgement.RailMaterialShortages[0].Required);
        }

        [Test]
        // 財布が賄いコストセット0なら予約も消え、同じ所持で設置可になる
        // With zero cost sets (the wallet covers it) the reservation disappears and the same inventory becomes placeable
        public void コストセット0なら予約は消えて設置可になる()
        {
            var judgement = Evaluate(BuildInventory((RailMaterial1Guid, 50), (PierMaterial1Guid, 6), (PierMaterial2Guid, 5)), 0);

            Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, judgement.Judgement.FailureReason);
            Assert.IsEmpty(judgement.RailMaterialShortages);
            Assert.IsTrue(judgement.IsPierAffordable);
        }

        [Test]
        // 橋脚自身の建設コストが足りなければ、レール判定が通っていても可否ゲートで落ちる
        // An unaffordable pier fails the gate even when the rail judgement itself passes
        public void 橋脚自身の建設コスト不足は可否ゲートで落ちる()
        {
            var judgement = Evaluate(BuildInventory((RailMaterial1Guid, 50), (PierMaterial1Guid, 99), (PierMaterial2Guid, 0)), 1);

            Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, judgement.Judgement.FailureReason);
            Assert.IsFalse(judgement.IsPierAffordable);
            Assert.AreEqual(1, judgement.PierMaterialShortages.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(PierMaterial2Guid), judgement.PierMaterialShortages[0].ItemId);
            Assert.AreEqual(0, judgement.PierMaterialShortages[0].Held);
            Assert.AreEqual(1, judgement.PierMaterialShortages[0].Required);
        }

        // 距離5(=1単位)の接続を、橋脚ブロックのコストセット数を変えて判定する
        // Judges a distance-5 (one unit) connection while varying the pier block's cost set count
        private static TrainRailPierPlacementJudgement Evaluate(List<IItemStack> inventoryItems, int pierRequiredCostSets)
        {
            var pierRequiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            return TrainRailConnectPreviewCalculator.EvaluateWithPierReservation(5f, float.MaxValue, float.MaxValue, inventoryItems, RailConnectToolGuid, pierRequiredItems, pierRequiredCostSets);
        }

        private static List<IItemStack> BuildInventory(params (Guid itemGuid, int count)[] items)
        {
            var stacks = new List<IItemStack>(items.Length);
            foreach (var (itemGuid, count) in items) stacks.Add(ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
            return stacks;
        }
    }
}
