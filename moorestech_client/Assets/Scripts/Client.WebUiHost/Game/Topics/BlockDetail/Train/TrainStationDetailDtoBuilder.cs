using Client.Game.InGame.Block;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 駅ブロックの駅名をDTOへ充填する
    /// Fills the station block's name into the DTO
    /// </summary>
    public static class TrainStationDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            var nameState = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey);
            dto.TrainStation = BuildStationDetail(nameState, param);
        }

        /// <summary>
        /// 駅名は転送状態と独立に配信。駅マスタ以外は行を持たない
        /// Publishes the name independently of transfer state; non-station masters carry no name row
        /// </summary>
        public static TrainStationDetailDto BuildStationDetail(TrainStationNameStateDetail nameState, object param)
        {
            if (param is not TrainStationBlockParam) return null;
            return new TrainStationDetailDto { Name = nameState?.StationName ?? string.Empty };
        }
    }
}
