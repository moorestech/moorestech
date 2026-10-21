using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ベルト搬送の組を変える契約。tick順序を持つ側(MasterTickUpdater・搬送tick・再構築)と、機械の押し込み・返却を確定した撤去・ロード登録だけが使う
    // Mutation contract for the belt transport assembly; used only by the tick-order owners (MasterTickUpdater, transport tick, rebuild), machine pushes, refunded removals and load registration
    public interface IBeltTransportMutation
    {
        void BeginMachinePushWindow();
        void RebuildIfDirty();
        BeltTransportTickReport Tick();
        bool TrySupplyFromMachine(BlockInstanceId beltBlockInstanceId, in InsertItemContext context, ItemId itemId, ItemInstanceId itemInstanceId);
        void DiscardConnectionRemovalItems(IBlock block);
        void RegisterLoadedState(BlockInstanceId blockInstanceId, BeltConveyorSaveJsonObject state);
    }
}
