using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using MessagePack;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent, IFluidInventory, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        private readonly FluidContainer _fluidContainer;
        private readonly Dictionary<FluidContainer, FluidPipeSourceBucket> _pendingBySource = new();
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly FluidPipeTransferService _transferService;

        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity, int blockedRetryTicks, Dictionary<string, string> componentStates)
        {
            _connectorComponent = connectorComponent;
            _fluidContainer = LoadContainer(capacity, componentStates);
            _transferService = new FluidPipeTransferService(_fluidContainer, _pendingBySource, _connectorComponent, blockedRetryTicks);

            #region Internal

            FluidContainer LoadContainer(float loadCapacity, Dictionary<string, string> loadStates)
            {
                var container = new FluidContainer(loadCapacity);
                if (loadStates == null || !loadStates.TryGetValue(FluidPipeSaveComponent.SaveKeyStatic, out var savedState)) return container;

                // セーブ復元時はソース情報がないため Empty バケットへ戻す
                // On load, source identity is unknown, so restore into the Empty bucket.
                var jsonObject = JsonConvert.DeserializeObject<FluidContainerSaveJsonObject>(savedState);
                container = jsonObject.ToFluidContainer(loadCapacity);
                if (container.Amount > 0)
                {
                    _pendingBySource[FluidContainer.Empty] = new FluidPipeSourceBucket { Amount = container.Amount, BlockedTicks = 0 };
                }

                return container;
            }

            #endregion
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            var fluidStateDetail = GetFluidPipeStateDetail();
            var blockStateDetail = new BlockStateDetail(
                FluidPipeStateDetail.BlockStateDetailKey,
                MessagePackSerializer.Serialize(fluidStateDetail)
            );

            return new[] { blockStateDetail };
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            var beforeAmount = _fluidContainer.Amount;
            var remain = _fluidContainer.AddLiquid(fluidStack, FluidContainer.Empty);
            var accepted = _fluidContainer.Amount - beforeAmount;
            if (accepted <= 0) return remain;

            // 受け入れ量をソース別に蓄積し、次回 Update で配分する
            // Accumulate accepted fluid by source and distribute it on the next update.
            var key = source ?? FluidContainer.Empty;
            var bucket = _pendingBySource.GetValueOrDefault(key);
            bucket.Amount += accepted;
            bucket.BlockedTicks = 0;
            _pendingBySource[key] = bucket;

            _onChangeBlockState.OnNext(Unit.Default);
            return remain;
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public void Update()
        {
            var transferredAny = _transferService.Update();

            // 同 tick 内の逆流防止記録をクリアし、バケット合計を実コンテナへ反映する
            // Clear same-tick source records and reflect the bucket total into the container.
            _fluidContainer.ClearPreviousSources();
            _fluidContainer.Amount = _transferService.SumBuckets();
            if (_fluidContainer.Amount <= 0) _fluidContainer.FluidId = FluidMaster.EmptyFluidId;

            if (transferredAny) _onChangeBlockState.OnNext(Unit.Default);
        }

        public FluidPipeStateDetail GetFluidPipeStateDetail()
        {
            var fluidId = _fluidContainer.FluidId;
            var amount = _fluidContainer.Amount;
            var capacity = _fluidContainer.Capacity;
            return new FluidPipeStateDetail(fluidId, (float)amount, (float)capacity);
        }

        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (_fluidContainer.Amount > 0)
            {
                fluidStacks.Add(new FluidStack(_fluidContainer.Amount, _fluidContainer.FluidId));
            }

            return fluidStacks;
        }

        internal FluidContainer GetSourceIdentityContainer()
        {
            return _fluidContainer;
        }
    }
}
