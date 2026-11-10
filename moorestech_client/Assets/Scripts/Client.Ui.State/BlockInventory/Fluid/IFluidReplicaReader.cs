using System;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// パイプの網の複製の読み口（本番は FluidNetworkClientCache。テストは偽で差し替える）
    /// Read port of the pipe network replica (production is FluidNetworkClientCache; tests swap in a fake)
    /// </summary>
    public interface IFluidReplicaReader
    {
        IObservable<Unit> OnReplicaChanged { get; }

        // 複製に節がある座標だけ真。量と容量は FluidUnits.ToAmount 済み
        // True only at a position with a replica node; amount and capacity are already FluidUnits.ToAmount
        bool TryReadNode(Vector3Int position, out int fluidId, out double amount, out double capacity);
    }
}
