using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Interface;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ベルト搬送の組を読むだけの契約。可変な組そのものは出さず、全量・ハッシュ・保存内容・返却物の切り出しだけを渡す
    // Read-only contract for the belt transport assembly; the mutable assembly itself stays hidden, only the full state, hash, save content and refunds are handed out
    public interface IBeltTransportLookup
    {
        // 組を作り直した直後に流れる。全量の送信がこれを購読する
        // Fires right after the assembly is rebuilt; the full-state send subscribes to it
        IObservable<Unit> OnAssemblyRebuilt { get; }
        IReadOnlyList<IItemStack> GetOwnedRefundItems(BlockInstanceId blockInstanceId);
        IReadOnlyList<IItemStack> GetConnectionRemovalRefundItems(IBlock block);
        BeltTransportFullState CaptureFullState();
        uint ComputeStateHash();
        BeltConveyorSaveJsonObject CreateSaveState(BlockInstanceId blockInstanceId);
    }
}
