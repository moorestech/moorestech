using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ベルト搬送の組を変える契約。tick順序を持つ側(MasterTickUpdater・搬送tick)と、機械の押し込み・ロード登録だけが使う
    // Mutation contract for the belt transport assembly; used only by the tick-order owners (MasterTickUpdater, transport tick), machine pushes and load registration
    public interface IBeltTransportMutation
    {
        void RebuildIfDirty();
        BeltTransportTickReport Tick();
        bool TrySupplyFromMachine(BlockInstanceId beltBlockInstanceId, in InsertItemContext context, ItemId itemId, ItemInstanceId itemInstanceId);
        void RegisterLoadedState(BlockInstanceId blockInstanceId, BeltConveyorSaveJsonObject state);
    }
}
