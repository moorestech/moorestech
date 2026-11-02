using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BlockInfoPolling;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    ///     抽選の中身の応答→役割ごとのビュー→DTO の詰め替えと、WireFixtures との一致を固定する
    ///     Pins the draw-detail response to per-role view to DTO conversion and its equality with WireFixtures
    /// </summary>
    public class WireContractCleanRoomYieldTest
    {
        [Test]
        public void DicingMatchesFixtureTest()
        {
            // ダイシングは損失率・品質Tier・レベル別割合・失敗ダイ割合を載せる
            // Dicing carries the loss rate, quality tier, per-level rates and failed-die rate
            var view = ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(new DicingYieldMessagePack(0.03d, 1, new[] { 0.7d, 0.1d, 0d }, 0.2d), null));
            AssertMatchesFixture(CreateMachineDto(CleanRoomYieldDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view)), "block_inventory_clean_room_yield_dicing.json");
        }

        [Test]
        public void DicingWithPendingDrawMatchesFixtureTest()
        {
            // 出力待ちの確定済み抽選は、次の抽選の現在値と別に同じ役割の値を子欄へ載せる
            // A fixed draw waiting for output carries the same role's values in a child, apart from the next draw's current values
            var pending = new DicingYieldMessagePack(0.03d, 1, new[] { 0.7d, 0.1d, 0d }, 0.2d);
            var view = ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(new DicingYieldMessagePack(0.15d, 0, new[] { 0.2d, 0d, 0d }, 0.8d), pending));
            AssertMatchesFixture(CreateMachineDto(CleanRoomYieldDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view)), "block_inventory_clean_room_yield_dicing_pending.json");
        }

        [Test]
        public void NoRoleBuildsNoSectionTest()
        {
            // 役割の無いレシピ・未選択では欄を出さない
            // No section for a role-less or unselected recipe
            var view = ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNoRole());
            Assert.IsNull(CleanRoomYieldDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view));
        }

        [Test]
        public void NotOperableBuildsNoSectionTest()
        {
            // 稼働できない機械では欄を出さない（停止理由は部屋欄が出す）
            // No section for a machine that cannot operate; the room section shows the halt reason
            var view = ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNotOperable());
            Assert.IsNull(CleanRoomYieldDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view));
        }

        [Test]
        public void BlockNotFoundAndUnsupportedTargetHaveNoViewTest()
        {
            // 消えたブロックと非対応の対象はビューを持たず、読み取り結果で区別される
            // A vanished block and an unsupported target have no view and are told apart by the read result
            Assert.AreEqual(BlockInfoReadResult.BlockNotFound, GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateBlockNotFound().TryReadView(out var vanished));
            Assert.IsNull(vanished);
            Assert.AreEqual(BlockInfoReadResult.UnsupportedTarget, GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateUnsupportedTarget().TryReadView(out var unsupported));
            Assert.IsNull(unsupported);
        }

        private static CleanRoomYieldView ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack response)
        {
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            return view;
        }

        private static BlockInventoryDto CreateMachineDto(CleanRoomYieldDetailDto cleanRoomYield)
        {
            return new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "CleanRoomMachine",
                Identifier = "(2, 1, 1)",
                BlockGuid = "66666666-6666-4666-8666-666666666666",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                CleanRoomYield = cleanRoomYield,
            };
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}
