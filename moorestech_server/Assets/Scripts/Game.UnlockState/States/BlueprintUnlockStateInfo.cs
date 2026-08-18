using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class BlueprintUnlockStateInfo
    {
        public bool IsUnlocked { get; private set; }

        public BlueprintUnlockStateInfo(bool isUnlocked)
        {
            IsUnlocked = isUnlocked;
        }

        public BlueprintUnlockStateInfo(BlueprintUnlockStateInfoJsonObject jsonObject)
        {
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class BlueprintUnlockStateInfoJsonObject
    {
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public BlueprintUnlockStateInfoJsonObject() { }

        public BlueprintUnlockStateInfoJsonObject(BlueprintUnlockStateInfo blueprintUnlockStateInfo)
        {
            IsUnlocked = blueprintUnlockStateInfo.IsUnlocked;
        }
    }
}
