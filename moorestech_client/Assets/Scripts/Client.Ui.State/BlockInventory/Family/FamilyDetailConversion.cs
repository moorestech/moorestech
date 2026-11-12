using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 採掘機と発電機の詳細を値へ写す
    /// Maps miner and generator state details onto values, as the Web host's builders
    /// </summary>
    public static class FamilyDetailConversion
    {
        public static MinerDetail Miner(CommonMinerBlockStateDetail miner, CommonMachineBlockStateDetail common, IMinerParam param, bool gearDriven)
        {
            // 1 個あたりの秒を毎分にし採掘中の品だけ並べる
            // Convert seconds per item to per-minute, mined items only
            var items = new List<MinerMiningItem>();
            if (0 < miner.MiningSeconds)
            {
                var currentIds = miner.GetCurrentMiningItemIds();
                foreach (var settings in param.MineSettings.items)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                    if (currentIds.Contains(itemId)) items.Add(new MinerMiningItem(itemId, BlockDetailMath.SecondsPerMinute / miner.MiningSeconds));
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
