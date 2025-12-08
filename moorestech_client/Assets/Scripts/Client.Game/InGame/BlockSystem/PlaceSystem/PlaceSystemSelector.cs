using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Core.Master;
using Mooresmaster.Model.PlaceSystemModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemSelector
    {
        public readonly EmptyPlaceSystem EmptyPlaceSystem;
        private readonly CommonBlockPlaceSystem _commonBlockPlaceSystem;
        private readonly TrainRailPlaceSystem _trainRailPlaceSystem;
        private readonly TrainCarPlaceSystem _trainCarPlaceSystem;
        private readonly TrainRailConnectSystem _trainRailConnectSystem;
        private readonly GearChainPoleConnectSystem _gearChainPoleConnectSystem;
        
        public PlaceSystemSelector(
            CommonBlockPlaceSystem commonBlockPlaceSystem,
            TrainCarPlaceSystem trainCarPlaceSystem,
            TrainRailPlaceSystem trainRailPlaceSystem,
            TrainRailConnectSystem trainRailConnectSystem,
            GearChainPoleConnectSystem gearChainPoleConnectSystem)
        {
            EmptyPlaceSystem = new EmptyPlaceSystem();
            _commonBlockPlaceSystem = commonBlockPlaceSystem;
            _trainCarPlaceSystem = trainCarPlaceSystem;
            _trainRailPlaceSystem = trainRailPlaceSystem;
            _trainRailConnectSystem = trainRailConnectSystem;
            _gearChainPoleConnectSystem = gearChainPoleConnectSystem;
        }
        
        public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
        {
            // マスターデータからPlaceSystemを検索
            // Search PlaceSystem from master data
            var placeSystemElement = GetPlaceSystemElement(context.HoldingItemId);
            if (placeSystemElement != null)
            {
                // PlaceModeに基づいて適切なシステムを返す
                // Return appropriate system based on PlaceMode
                return placeSystemElement.PlaceMode switch
                {
                    PlaceSystemMasterElement.PlaceModeConst.TrainRail => _trainRailPlaceSystem,
                    PlaceSystemMasterElement.PlaceModeConst.TrainCar => _trainCarPlaceSystem,
                    PlaceSystemMasterElement.PlaceModeConst.TrainRailConnect => _trainRailConnectSystem,
                    PlaceSystemMasterElement.PlaceModeConst.GearChainPoleConnect => _gearChainPoleConnectSystem,
                    _ => throw new System.Exception($"Unsupported PlaceMode: {placeSystemElement.PlaceMode}"),
                };
            }
            
            // ブロックアイテムの場合は共通ブロック設置システムを返す
            // For block items, return common block place system
            if (MasterHolder.BlockMaster.IsBlock(context.HoldingItemId))
            {
                return _commonBlockPlaceSystem;
            }
            
            return EmptyPlaceSystem;
            
            #region Internal
            
            PlaceSystemMasterElement GetPlaceSystemElement(ItemId itemId)
            {
                if (itemId == ItemMaster.EmptyItemId)
                {
                    return null;
                }
                
                // アイテムIDからGuidを取得
                // Get Guid from ItemId
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var itemGuid = itemMaster.ItemGuid;
                
                // UsePlaceItemsに現在のアイテムGuidが含まれている要素を検索
                // Search elements that contain current item Guid in UsePlaceItems
                var matchingElements = MasterHolder.PlaceSystemMaster.PlaceSystem.Data
                    .Where(element => element.UsePlaceItems.Contains(itemGuid))
                    .ToList();
                
                if (matchingElements.Count == 0)
                {
                    return null;
                }
                
                // Priorityが最も高いものを返す（Priorityは大きいほど優先度が高い）
                // Return the one with highest Priority (larger Priority value means higher priority)
                return matchingElements.OrderByDescending(element => element.Priority).First();
            }
            
            #endregion
        }
    }
}