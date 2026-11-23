using System.Collections.Generic;
using UniRx;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.Save;
using Game.Block.Blocks.Service;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Rocket
{
    /// <summary>
    ///     - 資材・積荷・出力スロットを構成
    ///     - 積荷の選択と電力部品を構成
    ///     - Compose material, payload and output slots
    ///     - Compose the cargo selection and power components
    /// </summary>
    public class VanillaRocketLaunchPadTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryEvent;

        public VanillaRocketLaunchPadTemplate(BlockOpenableInventoryUpdateEvent blockInventoryEvent)
        {
            _blockInventoryEvent = blockInventoryEvent;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            // ブループリントの選択は BlockFactory が生成直後に IBlockBlueprintSettings へ適用する
            // BlockFactory applies the blueprint selection to IBlockBlueprintSettings right after creation
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Create(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (RocketLaunchPadBlockParam)blockMasterElement.BlockParam;
            var layout = new RocketLaunchPadSlotLayout(param);
            // 選択の実体は選択部品だけへ渡し、在庫・進行部品には読む口として渡す
            // Hand the concrete selection only to the selector; inventory and processor receive it as the read port
            var cargoSelection = new OrbitalCargoSelectionState();

            // 全スロットを1つの保管に持ち、入れ口と出力口の制限はインベントリ側で掛ける
            // Keep every slot in one store and apply input/output restrictions in the inventory component
            var inventory = new RocketLaunchPadInventoryComponent(blockInstanceId, layout, _blockInventoryEvent, RocketLaunchPadCargoRecovery.SlotCount(componentStates, layout), cargoSelection);
            var subscriptions = new CompositeDisposable();
            var slots = inventory.ItemDataStoreService;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var beltOutput = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector);
            var arrivalService = ServerContext.GetService<IOrbitalArrivalService>();
            var launchHistory = ServerContext.GetService<RocketLaunchHistory>();
            var cablePool = ServerContext.GetService<SpaceElevatorCablePool>();

            // 選択は段階の復元より先に読む（進行部品が選択の購読を始める前に値を確定する）
            // Read the selection before the stage restores, so the value is settled before the processor subscribes
            var inletSlots = new RocketLaunchPadCargoInletSlots(layout, slots);
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
            var selector = componentStates == null
                ? new OrbitalCargoSelectorComponent(cargoSelection, inletSlots, unlockState)
                : new OrbitalCargoSelectorComponent(componentStates, cargoSelection, inletSlots, unlockState);
            var processor = componentStates == null
                ? new RocketLaunchPadProcessorComponent(param, layout, slots, beltOutput, arrivalService, launchHistory, cablePool, inventory.OnInventoryChanged, cargoSelection, subscriptions)
                : new RocketLaunchPadProcessorComponent(componentStates, param, layout, slots, beltOutput, arrivalService, launchHistory, cablePool, inventory.OnInventoryChanged, cargoSelection, subscriptions);
            var electric = new RocketLaunchPadElectricComponent(blockInstanceId, processor);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            var components = new List<IBlockComponent>
            {
                connector,
                inventory,
                selector,
                new OrbitalCargoBlueprintSettingsComponent(selector),
                electric,
                wireConnector,
                processor,
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
