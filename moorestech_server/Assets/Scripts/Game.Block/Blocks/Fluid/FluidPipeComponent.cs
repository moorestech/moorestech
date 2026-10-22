using System;
using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Context;
using Game.Fluid;
using Game.Fluid.Simulation;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     流体パイプ本体。物理進行はFluidTickUpdaterが全パイプ一括で行うため、このコンポーネントはシミュレーションノードへの薄いアダプタに徹する。
    ///     設置時にFluidNetworkDatastoreへ登録し、破壊時に解除する。中身はブロック状態としては配らず、網全体のtick差分同期でクライアントへ届く。
    ///
    ///     The fluid pipe itself. FluidTickUpdater advances the physics of all pipes in one batch, so this component stays a thin adapter over its simulation node.
    ///     It registers itself to FluidNetworkDatastore on placement and unregisters on destruction; contents are not published as block state but reach the client through the network-wide tick diff sync.
    /// </summary>
    public class FluidPipeComponent : IFluidInventory
    {
        // シミュレーションが読む流体状態と、トポロジ構築に使う接続
        // Fluid state read by the simulation and connections used for topology building
        public readonly FluidSimNode Node;
        public readonly BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> Connector;

        public Vector3Int Position => Node.Position;

        private readonly IFluidNetworkDatastore _fluidNetworkDatastore;

        // ロード復元用の面初期速度（正準側方向→速度）。最初のトポロジ構築で消費される
        // Loaded initial face velocities (canonical direction → velocity), consumed by the first topology rebuild
        private Dictionary<Vector3Int, double> _loadedFaceVelocities;

        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> connectorComponent, float capacity, Dictionary<string, object> componentStates)
        {
            Node = new FluidSimNode(blockPositionInfo.OriginalPos, capacity);
            Connector = connectorComponent;

            // 新規設置はcomponentStatesがnullで来る。復元を試みるのはロード経路だけに限る
            // Fresh placement passes a null componentStates, so only the load path attempts a restore
            if (componentStates != null && BlockComponentStateReader.TryRead<FluidPipeSaveJsonObject>(componentStates, FluidPipeSaveComponent.SaveKeyStatic, out var jsonObject))
            {
                Node.Amount = Math.Min(jsonObject.Amount, Node.Capacity);
                Node.FluidId = jsonObject.FluidId;
                _loadedFaceVelocities = jsonObject.ToFaceVelocityDictionary();
            }

            _fluidNetworkDatastore = ServerContext.GetService<IFluidNetworkDatastore>();
            _fluidNetworkDatastore.AddPipe(this);
        }

        // 機械からの流入。ノードが受け入れた量だけをtick差分に残し、クライアントの複製が同じ量を足す
        // Inflow from a machine; only the amount the node accepted goes into the tick diff, and the client replica adds the same amount
        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            var remainder = Node.AddExternal(fluidStack);
            var accepted = fluidStack.Amount - remainder.Amount;
            if (0 < accepted) _fluidNetworkDatastore.RecordInflow(this, fluidStack.FluidId, accepted);
            return remainder;
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
            _fluidNetworkDatastore.RemovePipe(this);
            IsDestroy = true;
        }
    }
}
