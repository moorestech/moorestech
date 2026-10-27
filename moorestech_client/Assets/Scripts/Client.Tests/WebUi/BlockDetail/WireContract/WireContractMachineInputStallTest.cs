using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BlockInfoPolling;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail.MachineInput;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    ///     止まった理由の応答→ビュー→DTO の詰め替えと、WireFixtures との一致を固定する
    ///     Pins the stall response to view to DTO conversion and its equality with WireFixtures
    /// </summary>
    public class WireContractMachineInputStallTest
    {
        [Test]
        public void AboveCraftCapMatchesFixtureTest()
        {
            // 上限超えは理由と待っている品のIDだけを載せる
            // Above-cap carries only the reason and the waiting item ID
            var view = ReadView(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateStalled(MachineInputStallReasonKind.AboveCraftCap, 12));
            AssertMatchesFixture(CreateMachineDto(MachineInputStallDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, view)), "block_inventory_machine_input_stall.json");

            #region Internal

            static BlockInventoryDto CreateMachineDto(MachineInputStallDetailDto machineInputStall)
            {
                return new BlockInventoryDto
                {
                    Open = true,
                    Source = "block",
                    BlockType = "GearMachine",
                    Identifier = "(0, 0, 0)",
                    BlockGuid = "a334d703-9442-4910-a8f8-b914df50df4e",
                    ItemSlots = new List<BlockItemSlotDto>(),
                    FluidSlots = new List<BlockFluidSlotDto>(),
                    MachineInputStall = machineInputStall,
                };
            }

            static void AssertMatchesFixture(object dto, string fixtureName)
            {
                var actual = JToken.Parse(WebUiJson.Serialize(dto));
                var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
                var expected = JToken.Parse(File.ReadAllText(path));
                Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
            }

            #endregion
        }

        [Test]
        public void MixedLevelMapsToWireNameTest()
        {
            // レベル違いは "mixedLevel" に写す
            // Mixed level maps to "mixedLevel"
            var dto = MachineInputStallDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, ReadView(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateStalled(MachineInputStallReasonKind.MixedLevel, 7)));
            Assert.AreEqual("mixedLevel", dto.Reason);
            Assert.AreEqual(7, dto.ItemId);
            Assert.AreEqual("fetched", dto.FetchState);
        }

        [Test]
        public void NotStalledBuildsNoSectionTest()
        {
            // 止まっていない機械では欄を出さない
            // No section for a machine that is not stalled
            Assert.IsNull(MachineInputStallDetailDtoBuilder.Build(BlockInfoFetchState.Fetched, ReadView(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateNotStalled())));
        }

        [Test]
        public void BlockNotFoundAndUnsupportedTargetHaveNoViewTest()
        {
            // 消えたブロックと非対応の対象はビューを持たず、読み取り結果で区別される
            // A vanished block and an unsupported target have no view and are told apart by the read result
            Assert.AreEqual(BlockInfoReadResult.BlockNotFound, GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateBlockNotFound().TryReadView(out var vanished));
            Assert.IsNull(vanished);
            Assert.AreEqual(BlockInfoReadResult.UnsupportedTarget, GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateUnsupportedTarget().TryReadView(out var unsupported));
            Assert.IsNull(unsupported);
        }

        private static MachineInputStallView ReadView(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack response)
        {
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            return view;
        }

    }
}
