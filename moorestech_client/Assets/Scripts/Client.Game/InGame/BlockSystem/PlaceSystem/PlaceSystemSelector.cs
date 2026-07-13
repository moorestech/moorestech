using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemSelector
    {
        public readonly EmptyPlaceSystem EmptyPlaceSystem;
        private readonly CommonBlockPlaceSystem _commonBlockPlaceSystem;
        private readonly BeltConveyorPlaceSystem _beltConveyorPlaceSystem;
        private readonly TrainRailPlaceSystem _trainRailPlaceSystem;
        private readonly TrainCarPlaceSystem _trainCarPlaceSystem;
        private readonly TrainRailConnectSystem _trainRailConnectSystem;
        private readonly GearChainPoleConnectSystem _gearChainPoleConnectSystem;
        private readonly ElectricWireConnectSystem _electricWireConnectSystem;
        private readonly BlueprintPasteSystem _blueprintPasteSystem;
        private readonly BlueprintCopySystem _blueprintCopySystem;

        public PlaceSystemSelector(
            CommonBlockPlaceSystem commonBlockPlaceSystem,
            BeltConveyorPlaceSystem beltConveyorPlaceSystem,
            TrainCarPlaceSystem trainCarPlaceSystem,
            TrainRailPlaceSystem trainRailPlaceSystem,
            TrainRailConnectSystem trainRailConnectSystem,
            GearChainPoleConnectSystem gearChainPoleConnectSystem,
            ElectricWireConnectSystem electricWireConnectSystem,
            BlueprintPasteSystem blueprintPasteSystem,
            BlueprintCopySystem blueprintCopySystem)
        {
            EmptyPlaceSystem = new EmptyPlaceSystem();
            _commonBlockPlaceSystem = commonBlockPlaceSystem;
            _beltConveyorPlaceSystem = beltConveyorPlaceSystem;
            _trainCarPlaceSystem = trainCarPlaceSystem;
            _trainRailPlaceSystem = trainRailPlaceSystem;
            _trainRailConnectSystem = trainRailConnectSystem;
            _gearChainPoleConnectSystem = gearChainPoleConnectSystem;
            _electricWireConnectSystem = electricWireConnectSystem;
            _blueprintPasteSystem = blueprintPasteSystem;
            _blueprintCopySystem = blueprintCopySystem;
        }

        public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
        {
            switch (context.Target)
            {
                case BlockPlacementTarget block:
                {
                    // ブロック種別で専用システムへ振り分け、残りは通常ブロック
                    // Route to the dedicated systems by block type; everything else falls back to common blocks
                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                    return blockMaster.BlockType switch
                    {
                        BlockMasterElement.BlockTypeConst.BeltConveyor => _beltConveyorPlaceSystem,
                        BlockMasterElement.BlockTypeConst.GearBeltConveyor => _beltConveyorPlaceSystem,
                        BlockMasterElement.BlockTypeConst.TrainRail => _trainRailPlaceSystem,
                        BlockMasterElement.BlockTypeConst.GearChainPole => _gearChainPoleConnectSystem,
                        _ => _commonBlockPlaceSystem,
                    };
                }
                case TrainCarPlacementTarget:
                    return _trainCarPlaceSystem;
                case BlueprintPlacementTarget:
                    return _blueprintPasteSystem;
                case BlueprintCopyToolPlacementTarget:
                    return _blueprintCopySystem;
                case ConnectToolPlacementTarget connectTool:
                    // 接続ツールはツール種別で3系統へ振り分ける
                    // Route connect tools to the three connect systems by tool type
                    return connectTool.ToolType switch
                    {
                        ConnectToolType.TrainRailConnect => _trainRailConnectSystem,
                        ConnectToolType.GearChainPoleConnect => _gearChainPoleConnectSystem,
                        ConnectToolType.ElectricWireConnect => _electricWireConnectSystem,
                        _ => EmptyPlaceSystem,
                    };
                default:
                    // null（未選択）や未知の型はEmptyへ
                    // Route null (nothing selected) and unknown types to Empty
                    return EmptyPlaceSystem;
            }
        }
    }
}
