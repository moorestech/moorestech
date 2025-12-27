using Game.Entity.Interface;

namespace Client.Game.InGame.Entity
{
    public interface IBeltConveyorItemEntityObject
    {
        void SetBeltConveyorItemPosition(BeltConveyorItemEntityStateMessagePack state, bool useLerp);
    }
}
