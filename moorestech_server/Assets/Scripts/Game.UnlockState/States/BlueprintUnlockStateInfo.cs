using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class BlueprintUnlockStateInfoJsonObject
    {
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public BlueprintUnlockStateInfoJsonObject() { }

        public BlueprintUnlockStateInfoJsonObject(bool isUnlocked)
        {
            IsUnlocked = isUnlocked;
        }
    }
}
