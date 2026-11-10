using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 採掘機と発電機の状態詳細を値へ写す（Web ホスト MinerDetailDtoBuilder・BlockDetailDtoBuilder の発電機部分の写し）
    /// Maps miner and generator state details onto values (port of the Web host's MinerDetailDtoBuilder and the generator part of BlockDetailDtoBuilder)
    /// </summary>
    public static class FamilyDetailConversion
    {
        public static MinerDetail Miner(CommonMinerBlockStateDetail miner, CommonMachineBlockStateDetail common, IMinerParam param, bool gearDriven)
        {
            // uGUI と同じく 60/秒で毎分に直し、いま採掘中の品だけを並べる
            // Like uGUI, convert per-second to per-minute and list only items currently mined
            var items = new List<MinerMiningItem>();
            if (0 < miner.MiningSeconds)
            {
                var currentIds = miner.GetCurrentMiningItemIds();
                foreach (var settings in param.MineSettings.items)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                    if (currentIds.Contains(itemId)) items.Add(new MinerMiningItem(itemId, 60 / miner.MiningSeconds));
                }
            }
            return new MinerDetail(common.CurrentPower, common.RequestPower, common.ProcessingRate, gearDriven, items);
        }

        public static GeneratorDetail Generator(PowerGeneratorStateDetail generator)
        {
            return new GeneratorDetail(generator.RemainingFuelTime, generator.CurrentFuelTime, generator.OperatingRate);
        }
    }
}
