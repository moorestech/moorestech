using System;

namespace Game.Block.Interface.Component
{
    public interface ITrainDockHandle
    {
        Guid TrainId { get; }
    }

    public interface ITrainDockingReceiver : IBlockComponent
    {
        void OnTrainDocked(ITrainDockHandle handle);
        void OnTrainDockedTick(ITrainDockHandle handle);
        void OnTrainUndocked(Guid trainId);
    }
}