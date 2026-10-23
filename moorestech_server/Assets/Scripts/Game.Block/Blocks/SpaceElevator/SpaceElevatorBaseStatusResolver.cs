using Game.Block.Interface.State;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点の今の状態を、待ちの理由の優先順に1つ選ぶ
    // Picks the base's current status by waiting-reason priority
    internal static class SpaceElevatorBaseStatusResolver
    {
        internal static SpaceElevatorBaseStatus Resolve(bool isConnected, bool hasEnoughCable, bool outputBlocked, bool isCarrying, bool isPowerShort)
        {
            if (!isConnected) return hasEnoughCable ? SpaceElevatorBaseStatus.ReadyToConnect : SpaceElevatorBaseStatus.InsufficientCable;
            if (outputBlocked) return SpaceElevatorBaseStatus.OutputBlocked;
            if (!isCarrying) return SpaceElevatorBaseStatus.WaitingSatellite;
            return isPowerShort ? SpaceElevatorBaseStatus.LowPower : SpaceElevatorBaseStatus.Climbing;
        }
    }
}
