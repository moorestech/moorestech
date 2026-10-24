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
        public void DepositionMatchesFixtureTest()
        {
            // 成膜は上限グレードと昇格確率だけを載せる
            // Deposition carries only the max grade and the promotion chance
            var view = ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateWaferGrade(2, 0.9d));
            AssertMatchesFixture(CreateMachineDto(CleanRoomYieldDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view)), "block_inventory_clean_room_yield_deposition.json");
        }

        [Test]
        public void DicingMatchesFixtureTest()
        {
            // ダイシングはダイの損失率だけを載せる
            // Dicing carries only the die loss rate
            var view = ReadView(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(0.03d));
            AssertMatchesFixture(CreateMachineDto(CleanRoomYieldDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view)), "block_inventory_clean_room_yield_dicing.json");
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
