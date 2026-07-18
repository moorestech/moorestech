using Client.Game.InGame.Block;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 列車PFの状態と構成をDTOへ変換する
    /// Converts train-platform transfer state and master-defined slot layout into the DTO
    /// </summary>
    public static class TrainPlatformDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            var state = block.GetStateDetail<TrainPlatformTransferStateDetail>(
                TrainPlatformTransferStateDetail.BlockStateDetailKey);
            if (state == null) return;

            // 対象マスタ型だけをDTO化する
            // Emit the capability only for supported master types to avoid showing it on ordinary blocks
            dto.TrainPlatform = param switch
            {
                TrainItemPlatformBlockParam item => CreateItemDetail(state, item.ItemSlotCount),
                TrainStationBlockParam station => CreateItemDetail(state, station.ItemSlotCount),
                TrainFluidPlatformBlockParam fluid => CreateFluidDetail(state, (double)fluid.Capacity),
                _ => null,
            };
        }

        private static TrainPlatformDetailDto CreateItemDetail(
            TrainPlatformTransferStateDetail state,
            int itemSlotCount)
        {
            return new TrainPlatformDetailDto
            {
                Mode = ToWireMode(state.Mode),
                ItemSlotCount = itemSlotCount,
            };
        }

        private static TrainPlatformDetailDto CreateFluidDetail(
            TrainPlatformTransferStateDetail state,
            double fluidCapacity)
        {
            return new TrainPlatformDetailDto
            {
                Mode = ToWireMode(state.Mode),
                FluidCapacity = fluidCapacity,
            };
        }

        private static string ToWireMode(TrainPlatformTransferComponent.TransferMode mode)
        {
            return mode == TrainPlatformTransferComponent.TransferMode.LoadToTrain
                ? "loadToTrain"
                : "unloadToPlatform";
        }
    }
}
