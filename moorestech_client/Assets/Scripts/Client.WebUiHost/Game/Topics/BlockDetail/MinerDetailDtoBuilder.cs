using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 採掘機の詳細DTOを構築する
    /// Composes the miner's power and per-minute mining-rate state into its capability DTO
    /// </summary>
    public static class MinerDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param, CommonMachineBlockStateDetail common)
        {
            // 採掘機: CommonMiner StateDetail + マスタ MineSettings から分間採掘数を算出
            // Miners: the CommonMiner state detail plus per-minute rates derived from master MineSettings
            var miner = block.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            if (miner == null || common == null || param is not IMinerParam minerParam) return;

            dto.Progress = common.ProcessingRate;
            dto.Miner = new MinerDetailDto
            {
                CurrentPower = common.CurrentPower,
                RequestPower = common.RequestPower,
                MiningItems = BuildMiningItems(miner, minerParam),
            };
        }

        private static List<MiningItemDto> BuildMiningItems(CommonMinerBlockStateDetail miner, IMinerParam minerParam)
        {
            // uGUI MinerBlockInventoryView と同じ算出（サーバーの実効採掘時間から 60/秒 を分間数に）
            // Same derivation as uGUI MinerBlockInventoryView (60 / the server's effective seconds, per minute)
            var result = new List<MiningItemDto>();
            var currentIds = miner.GetCurrentMiningItemIds();
            if (miner.MiningSeconds <= 0) return result;

            foreach (var settings in minerParam.MineSettings.items)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                if (!currentIds.Contains(itemId)) continue;
                result.Add(new MiningItemDto { ItemId = itemId.AsPrimitive(), ItemsPerMinute = (float)(60 / miner.MiningSeconds) });
            }
            return result;
        }
    }
}
