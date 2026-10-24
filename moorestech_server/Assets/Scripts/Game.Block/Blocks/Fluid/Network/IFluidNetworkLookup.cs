using System;
using Game.Fluid.Sync.State;
using UniRx;

namespace Game.Block.Blocks.Fluid
{
    // 流体網を読むだけの契約。可変な網そのものは出さず、全量の切り出しと現在のハッシュだけを渡す（ベルトのIBeltTransportLookupと同型）
    // Read-only contract for the fluid network; the mutable network stays hidden, only the full state and the current hash are handed out (same shape as the belt's IBeltTransportLookup)
    public interface IFluidNetworkLookup
    {
        // 網を作り直した直後に流れる。全量の送信がこれを購読する
        // Fires right after the network is rebuilt; the full-state send subscribes to it
        IObservable<Unit> OnTopologyRebuilt { get; }
        FluidNetworkFullState CaptureFullState();

        // 直前の Step(または再構築)時点のハッシュ。Stepper が走査のついでに足すので、ここでは計算しない
        // Hash as of the last Step (or rebuild); the stepper folds it in during its own walk, so nothing is computed here
        uint CurrentStateHash { get; }
    }
}
