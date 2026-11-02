using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.SpaceElevator
{
    public class SpaceElevatorClimberSaveJsonObject
    {
        [JsonProperty("isConnected", Required = Required.Always)] public bool IsConnected;
        // 昇降中の衛星。昇っていなければ空スタック
        // The satellite being lifted; an empty stack while not climbing
        [JsonProperty("climbingSatellite")] public ItemStackSaveJsonObject ClimbingSatellite;
        [JsonProperty("remainingClimbSeconds", Required = Required.Always)] public double RemainingClimbSeconds;
    }
}
