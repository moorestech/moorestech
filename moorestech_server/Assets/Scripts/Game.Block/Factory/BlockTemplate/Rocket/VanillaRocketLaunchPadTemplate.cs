using System.Collections.Generic;
using UniRx;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.Service;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Rocket
{
    /// <summary>
    ///     - 資材・積荷・出力スロットを構成
    ///     - 電力部品を構成
    ///     - Compose material, payload and output slots
    ///     - Compose power components
    /// </summary>
    public class VanillaRocketLaunchPadTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static IBlock Create(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (RocketLaunchPadBlockParam)blockMasterElement.BlockParam;
            var layout = new RocketLaunchPadSlotLayout(param);

            // 全スロットを1つの保管に持ち、入れ口と出力口の制限はインベントリ側で掛ける
            // Keep every slot in one store and apply input/output restrictions in the inventory component
            var inventoryChanges = new Subject<Unit>();
            var subscriptions = new CompositeDisposable(inventoryChanges);
            var slots = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, layout.TotalSlotCount);
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var beltOutput = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector);
            var arrivalService = ServerContext.GetService<IOrbitalArrivalService>();
            var launchHistory = ServerContext.GetService<RocketLaunchHistory>();

            var processor = componentStates == null
                ? new RocketLaunchPadProcessorComponent(param, layout, slots, beltOutput, arrivalService, launchHistory, inventoryChanges, subscriptions)
                : new RocketLaunchPadProcessorComponent(componentStates, param, layout, slots, beltOutput, arrivalService, launchHistory, inventoryChanges, subscriptions);
            var electric = new RocketLaunchPadElectricComponent(blockInstanceId, processor);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            var components = new List<IBlockComponent>
            {
                connector,
                new RocketLaunchPadInventoryComponent(layout, slots),
                electric,
                wireConnector,
                processor,
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void InvokeEvent(int slot, IItemStack itemStack)
            {
                inventoryChanges.OnNext(Unit.Default);
                var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
                blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(blockInstanceId, slot, itemStack));
            }

            #endregion
        }
    }
}
