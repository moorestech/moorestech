using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar.Cost
{
    /// <summary>
    /// 車両1両分の建設コスト不足を返す
    /// Returns the construction cost shortage for a single train car
    /// </summary>
    public static class TrainCarConstructionCostShortage
    {
        // サーバーの車両設置は財布を通さずRequiredItems 1セットを直接検証するため、同じ基準で突き合わせる
        // The server validates one RequiredItems set directly without the wallet, so match against the same gate
        public static List<ConstructionMaterialShortage> Calculate(Guid trainCarGuid, IEnumerable<IItemStack> inventoryItems)
        {
            var trainCarMaster = MasterHolder.TrainUnitMaster.GetTrainCarMaster(trainCarGuid);
            var costItems = ConstructionCostItems.ToItemCounts(trainCarMaster.RequiredItems);
            var heldByItem = ConstructionMaterialAccounting.TallyHeld(inventoryItems);
            var requirements = ConstructionCostShortageCalculator.CalculateRequirements(costItems, heldByItem);
            return ConstructionCostShortageCalculator.ToShortages(requirements);
        }
    }
}
