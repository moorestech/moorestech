using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Context;
using Game.Fluid;
using Game.Fluid.Simulation;
using MessagePack;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     流体パイプ本体。物理進行はFluidTickUpdaterが全パイプ一括で行うため、このコンポーネントはシミュレーションノードへの薄いアダプタに徹する。
    ///     設置時にFluidNetworkDatastoreへ登録し、破壊時に解除する。状態変更通知はtick末尾にdatastore経由でまとめて発火される。
    ///
    ///     The fluid pipe itself. FluidTickUpdater advances the physics of all pipes in one batch, so this component stays a thin adapter over its simulation node.
    ///     It registers itself to FluidNetworkDatastore on placement and unregisters on destruction; state notifications fire batched at the tick tail via the datastore.
    /// </summary>
    public class FluidPipeComponent : IFluidInventory, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        // シミュレーションが読む流体状態と、トポロジ構築に使う接続
        // Fluid state read by the simulation and connections used for topology building
        public readonly FluidSimNode Node;
        public readonly BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> Connector;

        public Vector3Int Position => Node.Position;

        private readonly Subject<Unit> _onChangeBlockState = new();

        // ロード復元用の面初期速度（正準側方向→速度）。最初のトポロジ構築で消費される
        // Loaded initial face velocities (canonical direction → velocity), consumed by the first topology rebuild
        private Dictionary<Vector3Int, double> _loadedFaceVelocities;

        private double _lastNotifiedAmount;
        private FluidId _lastNotifiedFluidId;

        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> connectorComponent, float capacity, Dictionary<string, string> componentStates)
        {
            Node = new FluidSimNode(blockPositionInfo.OriginalPos, capacity);
            Connector = connectorComponent;

            // セーブデータがある場合は内容量・流体ID・面速度を復元する
            // Restore amount, fluid id and face velocities when save data exists
            if (componentStates != null && componentStates.TryGetValue(FluidPipeSaveComponent.SaveKeyStatic, out var savedState))
            {
                var jsonObject = JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(savedState);
                Node.Amount = Math.Min(jsonObject.Amount, Node.Capacity);
                Node.FluidId = jsonObject.FluidId;
                _loadedFaceVelocities = jsonObject.ToFaceVelocityDictionary();
            }

            _lastNotifiedAmount = Node.Amount;
            _lastNotifiedFluidId = Node.FluidId;

            ServerContext.GetService<IFluidNetworkDatastore>().AddPipe(this);
        }

        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            return Node.AddExternal(fluidStack);
        }

        // パイプは単一流体しか保持しないため、残量があれば1要素、無ければ空のリストを返す
        // A pipe only ever holds a single fluid; return one stack when present, otherwise an empty list
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (0 < Node.Amount)
            {
                fluidStacks.Add(new FluidStack(Node.Amount, Node.FluidId));
            }
            return fluidStacks;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            var stateDetail = new FluidPipeStateDetail(Node.FluidId, (float)Node.Amount, (float)Node.Capacity);
            var serialized = MessagePackSerializer.Serialize(stateDetail);
            return new[] { new BlockStateDetail(FluidPipeStateDetail.BlockStateDetailKey, serialized) };
        }

        // tick末尾にFluidNetworkDatastoreから呼ばれ、前回通知から変化があった時だけ状態変更を発火する
        // Called by FluidNetworkDatastore at the tick tail; fires only when the state changed since the last notification
        internal void NotifyStateIfChanged()
        {
            var amountChanged = FluidSimulationConstants.AmountEpsilon < Math.Abs(Node.Amount - _lastNotifiedAmount);
            if (!amountChanged && Node.FluidId == _lastNotifiedFluidId) return;

            _lastNotifiedAmount = Node.Amount;
            _lastNotifiedFluidId = Node.FluidId;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        internal double TakeLoadedFaceVelocity(Vector3Int direction)
        {
            if (_loadedFaceVelocities == null) return 0;
            return _loadedFaceVelocities.TryGetValue(direction, out var velocity) ? velocity : 0;
        }

        internal void ClearLoadedFaceVelocities()
        {
            _loadedFaceVelocities = null;
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            ServerContext.GetService<IFluidNetworkDatastore>().RemovePipe(this);
            IsDestroy = true;
        }
    }
}
