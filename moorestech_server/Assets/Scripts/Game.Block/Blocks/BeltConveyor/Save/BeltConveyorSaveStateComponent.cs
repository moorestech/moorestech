using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // ベルコンblockのセーブ入口。保存内容はワールド全体の搬送の組から切り出し、ロード内容は組へ預けて最初の再構築で載せ直す
    // The belt block's save entry; the saved content is cut out of the world-wide transport assembly, and loaded content is handed to it to be placed at the first rebuild
    public class BeltConveyorSaveStateComponent : IBlockSaveState
    {
        private readonly BlockInstanceId _blockInstanceId;
        private readonly BeltTransportDatastore _beltTransportDatastore;

        public string SaveKey { get; } = typeof(BeltConveyorSaveStateComponent).FullName;
        public bool IsDestroy { get; private set; }

        public BeltConveyorSaveStateComponent(BlockInstanceId blockInstanceId)
        {
            _blockInstanceId = blockInstanceId;
            _beltTransportDatastore = ServerContext.GetService<BeltTransportDatastore>();
        }

        public BeltConveyorSaveStateComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId) : this(blockInstanceId)
        {
            // 保存されていたアイテム・優先順は組へ預ける。次のtick先頭の再構築で復元手順に乗る
            // Saved items and priority orders are handed to the assembly and go through the restore procedure at the next tick-head rebuild
            if (!BlockComponentStateReader.TryRead<BeltConveyorSaveJsonObject>(componentStates, SaveKey, out var state)) return;
            _beltTransportDatastore.RegisterLoadedState(blockInstanceId, state);
        }

        public object GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return _beltTransportDatastore.CreateSaveState(_blockInstanceId);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
