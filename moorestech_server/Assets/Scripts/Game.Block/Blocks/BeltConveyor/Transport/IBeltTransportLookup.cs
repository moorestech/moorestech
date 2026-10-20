using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ベルト搬送の組を読むだけの契約。可変な組そのものは出さず、全量・ハッシュ・保存内容の切り出しだけを渡す
    // Read-only contract for the belt transport assembly; the mutable assembly itself stays hidden, only the full state, hash and save content are handed out
    public interface IBeltTransportLookup
    {
        IReadOnlyList<IItemStack> GetRefundItems(BlockInstanceId blockInstanceId);
        BeltTransportFullState CaptureFullState();
        uint ComputeStateHash();
        BeltConveyorSaveJsonObject CreateSaveState(BlockInstanceId blockInstanceId);
    }
}
