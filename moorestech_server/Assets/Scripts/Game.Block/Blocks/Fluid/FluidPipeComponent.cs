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
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     流体パイプ本体。物理進行はFluidTickUpdaterが網全体で行うため、このコンポーネントは網のノード番号を握る薄いアダプタに徹する。
    ///     設置時にFluidNetworkDatastoreへ登録し、破壊時に解除する。中身は網の配列にあり、網に入る前(ロード直後・設置直後)だけ自分で持ち越す。
    ///     量は網と同じ FluidUnits の整数単位で持ち、IFluidInventory(double)との変換はここで行う。中身はブロック状態としては配らず、網全体のtick差分同期でクライアントへ届く。
    ///
    ///     The fluid pipe itself. FluidTickUpdater advances the physics of the whole network, so this component stays a thin adapter holding its node index.
    ///     It registers itself to FluidNetworkDatastore on placement and unregisters on destruction; contents live in the network arrays and are held over here only before the pipe is built in (right after load or placement).
    ///     Amounts are FluidUnits integers like the network; conversion to the IFluidInventory doubles happens here. Contents are not published as block state but reach the client through the network-wide tick diff sync.
    /// </summary>
    public class FluidPipeComponent : IFluidInventory
    {
        public readonly Vector3Int Position;
        public readonly long CapacityUnits;
        public readonly BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> Connector;

        // 網の配列の添字。再構築ごとに振り直され、網に入る前は -1
        // Index into the network arrays, reassigned on every rebuild; -1 before the pipe is built in
        public int NodeIndex { get; private set; } = -1;

        private readonly IFluidNetworkDatastore _fluidNetworkDatastore;

        // 網に入る前の中身(単位)。最初の再構築で網へ移る
        // Contents (units) before the pipe is built in; moved into the network by the first rebuild
        private long _holdoverUnits;
        private FluidId _holdoverFluidId = FluidMaster.EmptyFluidId;

        // ロード復元用の面初期速度（正準側方向→速度、単位）。最初のトポロジ構築で消費される
        // Loaded initial face velocities (canonical direction → velocity in units), consumed by the first topology rebuild
        private Dictionary<Vector3Int, long> _loadedFaceVelocities;

        public long AmountUnits => NodeIndex < 0 ? _holdoverUnits : _fluidNetworkDatastore.Network.NodeAmounts[NodeIndex];
        public FluidId FluidId => NodeIndex < 0 ? _holdoverFluidId : _fluidNetworkDatastore.Network.NodeFluidIds[NodeIndex];

        // 表示向けの double。計算は単位で行う
        // Double for display; computation uses units
        public double Amount => FluidUnits.ToAmount(AmountUnits);

        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> connectorComponent, float capacity, Dictionary<string, object> componentStates)
        {
            Position = blockPositionInfo.OriginalPos;
            CapacityUnits = FluidUnits.ToUnitsNearest(capacity);
            Connector = connectorComponent;

            // 新規設置はcomponentStatesがnullで来る。復元を試みるのはロード経路だけに限る
            // Fresh placement passes a null componentStates, so only the load path attempts a restore
            if (componentStates != null && BlockComponentStateReader.TryRead<FluidPipeSaveJsonObject>(componentStates, FluidPipeSaveComponent.SaveKeyStatic, out var jsonObject))
            {
                _holdoverUnits = Math.Min(jsonObject.AmountUnits, CapacityUnits);
                _holdoverFluidId = jsonObject.FluidId;
                _loadedFaceVelocities = jsonObject.ToFaceVelocityDictionary();
            }

            _fluidNetworkDatastore = ServerContext.GetService<IFluidNetworkDatastore>();
            _fluidNetworkDatastore.AddPipe(this);
        }

        // 機械からの流入。網に入っていれば網へ足して受け入れた量を差分に残し、網に入る前は持ち越しへ同じ受け入れ規則で足す。単位への切り捨てで端数は機械側に残る
        // Inflow from a machine; once built in, adds to the network and records the accepted amount in the diff, before that, adds to the holdover under the same acceptance rule. Flooring to units leaves the fraction with the machine
        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            if (0 <= NodeIndex) return _fluidNetworkDatastore.AddInflow(this, fluidStack);
            return AddToHoldover();

            #region Internal

            FluidStack AddToHoldover()
            {
                // 網に入る前は面が無いので、圧力スケールは自分の容量
                // Before joining the network there are no faces, so the pressure scale is the pipe capacity itself
                var accepted = FluidSimNetwork.ResolveAcceptedUnits(_holdoverUnits, CapacityUnits, CapacityUnits, _holdoverFluidId, fluidStack.FluidId, FluidUnits.ToUnitsFloor(fluidStack.Amount));
                if (accepted <= 0) return fluidStack;

                // 受け入れた以上、名は入ってきた流体(空・痕跡は置き換わり、同じ流体なら変わらない)
                // Once accepted, the id is the incoming fluid (empty and trace are replaced; the same fluid is unchanged)
                _holdoverFluidId = fluidStack.FluidId;
                _holdoverUnits += accepted;
                return new FluidStack(fluidStack.Amount - FluidUnits.ToAmount(accepted), fluidStack.FluidId);
            }

            #endregion
        }

        // パイプは単一流体しか保持しないため、残量があれば1要素、無ければ空のリストを返す
        // A pipe only ever holds a single fluid; return one stack when present, otherwise an empty list
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (0 < AmountUnits)
            {
                fluidStacks.Add(new FluidStack(Amount, FluidId));
            }
            return fluidStacks;
        }

        // 再構築が中身を新しい網へ移すために読む。旧網に入っていればその番号から、入っていなければ持ち越しから取り、持ち越しは空にする
        // Read by a rebuild to move the contents into the new network: from the old network's index when built in, else from the holdover, which is then cleared
        internal (long units, FluidId fluidId) TakeContents(FluidSimNetwork previous)
        {
            if (0 <= NodeIndex) return (previous.NodeAmounts[NodeIndex], previous.NodeFluidIds[NodeIndex]);
            var contents = (_holdoverUnits, _holdoverFluidId);
            _holdoverUnits = 0;
            _holdoverFluidId = FluidMaster.EmptyFluidId;
            return contents;
        }

        internal void SetNodeIndex(int nodeIndex)
        {
            NodeIndex = nodeIndex;
        }

        internal long TakeLoadedFaceVelocity(Vector3Int direction)
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

            // 旧網の添字を持ち越さない。以後の読み書きは持ち越し(空)に落ちる
            // Do not carry the old network's index; later reads and writes fall through to the (empty) holdover
            NodeIndex = -1;
            IsDestroy = true;
        }
    }
}
