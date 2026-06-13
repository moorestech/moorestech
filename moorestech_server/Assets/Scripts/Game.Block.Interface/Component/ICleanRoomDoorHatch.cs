namespace Game.Block.Interface.Component
{
    // ドアバースト計量IF。CleanRoomDatastore が PeekPendingBurst() を非破壊で読む。
    // Door-burst metering IF; CleanRoomDatastore reads PeekPendingBurst() non-destructively.
    public interface ICleanRoomDoorHatch : IBlockComponent
    {
        double PeekPendingBurst();
    }
}
