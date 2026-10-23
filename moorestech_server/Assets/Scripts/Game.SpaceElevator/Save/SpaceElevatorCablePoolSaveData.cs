using Newtonsoft.Json;

namespace Game.SpaceElevator.Save
{
    public class SpaceElevatorCablePoolSaveData
    {
        [JsonProperty("cableCount")] public int CableCount;
    }
}
