using System;
using Client.Game.InGame.FluidNetwork;
using Game.Fluid;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 流体複製のキャッシュから節を読む本番の読み手
    /// The production reader that reads nodes from the fluid replica cache
    /// </summary>
    public sealed class FluidNetworkReplicaReader : IFluidReplicaReader
    {
        private readonly FluidNetworkClientCache _cache;

        // 複製の適用（順序待ちキューの Apply）の中で表示を走らせないよう通知をメインスレッドの次の配送へ回す
        // Defer to the main-thread dispatch so UI never runs inside the replica Apply (queue flush)
        public IObservable<Unit> OnReplicaChanged => _cache.OnReplicaVersionChanged.ObserveOnMainThread().AsUnitObservable();

        public FluidNetworkReplicaReader(FluidNetworkClientCache cache)
        {
            _cache = cache;
        }

        public bool TryReadNode(Vector3Int position, out int fluidId, out double amount, out double capacity)
        {
            fluidId = 0;
            amount = 0;
            capacity = 0;
            // silent-ok: 複製に節が無い座標はパイプでない（複製が届く前も含む） / 複製の通知で再読する
            if (!_cache.TryGetNodeIndex(position, out var nodeIndex)) return false;

            var network = _cache.Replica.Network;
            fluidId = network.NodeFluidIds[nodeIndex].AsPrimitive();
            amount = FluidUnits.ToAmount(network.NodeAmounts[nodeIndex]);
            capacity = FluidUnits.ToAmount(network.NodeCapacities[nodeIndex]);
            return true;
        }
    }
}
