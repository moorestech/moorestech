using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     拠点の接続・撤去時の返却・残量と接続できるかの配信を受け持つ。拠点はプールの残量の判定も1エレベーター分の引き算も知らない
    ///     Owns connecting, the refund on removal and pushing the pool amount and connectability; bases know neither the pool check nor the per-elevator subtraction
    /// </summary>
    public class SpaceElevatorConnectionService : IBootInitializable
    {
        private readonly SpaceElevatorCablePool _pool;
        private readonly Dictionary<BlockInstanceId, TrackedSpaceElevatorBase> _bases = new();

        public SpaceElevatorConnectionService(SpaceElevatorCablePool pool)
        {
            _pool = pool;
        }

        public void Load()
        {
            // ワールドロードより前に購読し、ロードで置かれる拠点も拾う
            // Subscribe before the world loads so bases placed by the load are tracked too
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties => OnBlockPlaced(properties.BlockData.Block));
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => OnBlockRemoved(properties.BlockData.Block));
            _pool.OnAmountChanged.Subscribe(_ => PushAvailabilityToAll());
        }

        public SpaceElevatorConnectResult Connect(IBlock block)
        {
            if (!_bases.TryGetValue(block.BlockInstanceId, out var tracked)) return SpaceElevatorConnectResult.NotSpaceElevatorBase;
            if (tracked.Base.IsConnected) return SpaceElevatorConnectResult.AlreadyConnected;

            // 1エレベーター分が残っていれば引いて接続する
            // Connect after taking one elevator's worth when it remains
            if (!_pool.TryTake(tracked.CablesPerElevator)) return SpaceElevatorConnectResult.InsufficientCable;
            tracked.Base.MarkConnected();
            return SpaceElevatorConnectResult.Connected;
        }

        private void OnBlockPlaced(IBlock block)
        {
            if (!block.ComponentManager.TryGetComponent<ISpaceElevatorBase>(out var elevatorBase)) return;
            var cablesPerElevator = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CablesPerElevator;
            var tracked = new TrackedSpaceElevatorBase(elevatorBase, cablesPerElevator);
            _bases[block.BlockInstanceId] = tracked;
            PushAvailability(tracked);
        }

        private void OnBlockRemoved(IBlock block)
        {
            if (!_bases.Remove(block.BlockInstanceId, out var tracked)) return;

            // 接続済みの拠点だけが1エレベーター分をプールへ戻す。撤去イベントはDestroyより前に来るので状態を読める
            // Only a connected base returns one elevator's worth; the remove event fires before Destroy, so its state is still readable
            if (tracked.Base.IsConnected) _pool.Add(tracked.CablesPerElevator);
        }

        private void PushAvailabilityToAll()
        {
            foreach (var tracked in _bases.Values) PushAvailability(tracked);
        }

        // 接続できるかはプールの HasEnough で決め、拠点へは結果だけを渡す
        // Connectability is decided by the pool's HasEnough; the base only receives the result
        private void PushAvailability(TrackedSpaceElevatorBase tracked)
        {
            tracked.Base.SetCablePoolAvailability(_pool.Amount, _pool.HasEnough(tracked.CablesPerElevator));
        }

        // 追跡中の拠点と、その拠点の1エレベーター分の量
        // A tracked base and its cables per elevator
        private class TrackedSpaceElevatorBase
        {
            public readonly ISpaceElevatorBase Base;
            public readonly int CablesPerElevator;

            public TrackedSpaceElevatorBase(ISpaceElevatorBase elevatorBase, int cablesPerElevator)
            {
                Base = elevatorBase;
                CablesPerElevator = cablesPerElevator;
            }
        }
    }
}
