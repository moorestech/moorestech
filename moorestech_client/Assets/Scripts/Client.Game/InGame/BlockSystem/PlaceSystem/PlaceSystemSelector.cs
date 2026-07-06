using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Core.Master;
using Game.Block.Interface.Extension;
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

        public PlaceSystemSelector(
            CommonBlockPlaceSystem commonBlockPlaceSystem,
            BeltConveyorPlaceSystem beltConveyorPlaceSystem,
            TrainCarPlaceSystem trainCarPlaceSystem,
            TrainRailPlaceSystem trainRailPlaceSystem,
            TrainRailConnectSystem trainRailConnectSystem,
            GearChainPoleConnectSystem gearChainPoleConnectSystem,
            ElectricWireConnectSystem electricWireConnectSystem)
        {
            EmptyPlaceSystem = new EmptyPlaceSystem();
            _commonBlockPlaceSystem = commonBlockPlaceSystem;
            _beltConveyorPlaceSystem = beltConveyorPlaceSystem;
            _trainCarPlaceSystem = trainCarPlaceSystem;
            _trainRailPlaceSystem = trainRailPlaceSystem;
            _trainRailConnectSystem = trainRailConnectSystem;
            _gearChainPoleConnectSystem = gearChainPoleConnectSystem;
            _electricWireConnectSystem = electricWireConnectSystem;
        }

        public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
        {
            // ビルドメニュー選択がベルトファミリーなら専用設置システムを使う
            // Route belt-family build menu selections to the dedicated place system
            if (context.SelectedBlockId.HasValue && BeltConveyorPlaceFamilyUtil.TryGetFamily(context.SelectedBlockId.Value, out _))
            {
                return _beltConveyorPlaceSystem;
            }

            // ビルドメニューで選択中なら通常ブロック設置システムを使う
            // Use the common placement system while a build-menu selection exists
            if (context.SelectedBlockId.HasValue)
            {
                return _commonBlockPlaceSystem;
            }

            // 接続ツール系（レール/歯車/電線/車両）はTask 8で選択駆動として復活予定
            // Connect tools (rail/gear/wire/car) will return as selection-driven in Task 8
            return EmptyPlaceSystem;
        }
    }
}