using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.PlaceSystemModule;

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
                    // ベルトファミリー→レール→歯車ポールの順に専用システムへ振り分け、残りは通常ブロック
                    // Route by belt family, then rail, then gear chain pole; fall back to common blocks
                    if (BeltConveyorPlaceFamilyUtil.TryGetFamily(block.BlockId, out _)) return _beltConveyorPlaceSystem;

                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                    if (blockMaster.BlockType == BlockMasterElement.BlockTypeConst.TrainRail) return _trainRailPlaceSystem;
                    if (blockMaster.BlockType == BlockMasterElement.BlockTypeConst.GearChainPole) return _gearChainPoleConnectSystem;
                    return _commonBlockPlaceSystem;
                }
                case TrainCarPlacementTarget:
                    return _trainCarPlaceSystem;
                case BlueprintPlacementTarget:
                    return _blueprintPasteSystem;
                case BlueprintCopyToolPlacementTarget:
                    return _blueprintCopySystem;
                case ConnectToolPlacementTarget connectTool:
                    // 接続ツールは接続モードで3系統へ振り分ける
                    // Route connect tools to the three connect systems by place mode
                    return connectTool.PlaceMode switch
                    {
                        PlaceSystemMasterElement.PlaceModeConst.TrainRailConnect => _trainRailConnectSystem,
                        PlaceSystemMasterElement.PlaceModeConst.GearChainPoleConnect => _gearChainPoleConnectSystem,
                        PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect => _electricWireConnectSystem,
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
