using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通パイプハッチ。inflow面から受けた流体を内部コンテナに溜め、毎tick outflow面へpushする
    // Wall-piercing pipe hatch: buffers fluid from the inflow face and pushes it to the outflow side each tick
    public class CleanRoomPipeHatchComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        public string SaveKey => SaveKeyStatic;
        public static string SaveKeyStatic { get; } = typeof(CleanRoomPipeHatchComponent).FullName;

        private readonly FluidContainer _container;
        private readonly BlockConnectorComponent<IFluidInventory> _connector;

        public CleanRoomPipeHatchComponent(float capacity, BlockConnectorComponent<IFluidInventory> connector)
        {
            _container = new FluidContainer(capacity);
            _connector = connector;
        }

        // セーブからの復元: 内部流体のID/量を戻す（FluidPipeComponentと同方式）
        // Restore from save: fluid id/amount of the inner container (same as FluidPipeComponent)
        public CleanRoomPipeHatchComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory> connector)
            : this(capacity, connector)
        {
            if (componentStates == null) return;
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var json = JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(raw);
            if (json == null) return;
            _container.FluidId = json.FluidId;
            _container.Amount = json.Amount;
        }

        // inflow面から受ける。ソース帰属は単純化しEmptyで受ける
        // Accept on the inflow face; simplify source attribution by accepting with Empty
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            BlockException.CheckDestroy(this);
            return _container.AddLiquid(fluidStack, FluidContainer.Empty);
        }

        // パイプ同様、残量があれば1要素、無ければ空のリストを返す
        // Like a pipe: return one stack when present, otherwise an empty list
        public List<FluidStack> GetFluidInventory()
        {
            var list = new List<FluidStack>();
            if (_container.Amount > 0) list.Add(new FluidStack(_container.Amount, _container.FluidId));
            return list;
        }

        // 毎tick: 内部流体を接続先のIFluidInventoryへ流量上限までpushする
        // Each tick: push the buffered fluid to connected IFluidInventory up to the flow cap
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_container.Amount <= 0) { _container.ClearPreviousSources(); return; }

            DistributeToTargets();
            _container.ClearPreviousSources();
            if (_container.Amount <= 0) _container.FluidId = FluidMaster.EmptyFluidId;

            #region Internal

            void DistributeToTargets()
            {
                var targets = _connector.ConnectedTargets;
                if (targets.Count == 0) return;

                foreach (var kvp in targets)
                {
                    if (_container.Amount <= 0) break;
                    var maxFlow = GetMaxFlowRate(kvp.Value);
                    if (maxFlow <= 0) continue;

                    var sendAmount = Math.Min(_container.Amount, maxFlow);
                    var stack = new FluidStack(sendAmount, _container.FluidId);
                    var remain = kvp.Key.AddLiquid(stack, _container);
                    var accepted = sendAmount - remain.Amount;
                    _container.Amount -= accepted;
                }
            }

            // 自他のFlowCapacityの最小×1tick秒。FluidPipeComponentと同流儀
            // min(self,target FlowCapacity) * seconds-per-tick; same as FluidPipeComponent
            double GetMaxFlowRate(ConnectedInfo info)
            {
                var selfOption = info.SelfConnector?.ConnectOption as FluidConnectOption;
                var targetOption = info.TargetConnector?.ConnectOption as FluidConnectOption;
                if (selfOption == null || targetOption == null) return 0;
                return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.SecondsPerTick;
            }

            #endregion
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var json = new FluidPipeSaveJsonObject
            {
                FluidIdValue = _container.FluidId.AsPrimitive(),
                Amount = (float)_container.Amount,
                Capacity = (float)_container.Capacity,
            };
            return JsonConvert.SerializeObject(json);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
