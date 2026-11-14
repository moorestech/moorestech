using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Client.WebUiHost.Game.Actions;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 駅・列車PFの駅名・移送モード・スロット構成をDTOへ変換する。有無の判定は State の TrainPlatformDetailReader が持つ（DC-23 A）
    /// Converts a station/platform's name, transfer mode and slot layout into the DTO; State's TrainPlatformDetailReader owns the presence decisions (DC-23 A)
    /// </summary>
    public static class TrainPlatformDetailDtoBuilder
    {
        private static readonly SlotItem[] NoItemSlots = new SlotItem[0];
        private static readonly BlockFluidSlotState[] NoFluids = new BlockFluidSlotState[0];

        public static void Apply(BlockInventoryDto dto, BlockGameObject block)
        {
            // 本文の種類は UITK のパネルと同じ登録表で決める（blockType を並べない）
            // The body kind comes from the same registry as the UITK panel (no blockType list)
            var blockType = block.BlockMasterElement.BlockType;
            if (BlockComponentRegistry.Resolve(blockType).Body != BlockInventoryBodyKind.TrainPlatform) return;

            var stationName = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey);
            var transfer = block.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
            Fill(dto, blockType, block.BlockMasterElement.BlockParam, stationName, transfer);
        }

        // 駅名行・移送行の有無は Reader に任せ、ここでは wire の形へ写すだけ（スロットの中身は別の DTO が運ぶので数だけ）
        // The Reader decides the name and transfer rows; this only maps to the wire shape (slot contents travel in another DTO, so only the count)
        public static void Fill(BlockInventoryDto dto, string blockType, IBlockParam param, TrainStationNameStateDetail stationName, TrainPlatformTransferStateDetail transfer)
        {
            var body = TrainPlatformDetailReader.Read(blockType, param, stationName, transfer, NoItemSlots, NoFluids);
            dto.TrainStation = body.StationName.TryGetPresent(out var name) ? new TrainStationDetailDto { Name = name } : null;
            dto.TrainPlatform = body.Mode.TryGetPresent(out var mode)
                ? new TrainPlatformDetailDto { Mode = TrainPlatformTransferModeWire.ToWire(mode), ItemSlotCount = body.ItemSlots.IsPresent ? ReadItemSlotCount(param) : null }
                : null;

            #region Internal

            // スロット数はマスタの値。Reader がスロットありとするのは品物 PF と駅だけ
            // The slot count is the master value; the Reader reports slots only for item platforms and stations
            int? ReadItemSlotCount(IBlockParam blockParam)
            {
                return blockParam switch
                {
                    TrainItemPlatformBlockParam item => item.ItemSlotCount,
                    TrainStationBlockParam station => station.ItemSlotCount,
                    _ => null,
                };
            }

            #endregion
        }
    }
}
