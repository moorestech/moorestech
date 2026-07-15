using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class TrainCarUnlockStateInfo
    {
        public Guid TrainCarGuid { get; }
        public bool IsUnlocked { get; private set; }

        public TrainCarUnlockStateInfo(Guid trainCarGuid, bool isUnlocked)
        {
            TrainCarGuid = trainCarGuid;
            IsUnlocked = isUnlocked;
        }

        public TrainCarUnlockStateInfo(TrainCarUnlockStateInfoJsonObject jsonObject)
        {
            TrainCarGuid = Guid.Parse(jsonObject.TrainCarGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class TrainCarUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string TrainCarGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public TrainCarUnlockStateInfoJsonObject() { }

        public TrainCarUnlockStateInfoJsonObject(TrainCarUnlockStateInfo trainCarUnlockStateInfo)
        {
            TrainCarGuid = trainCarUnlockStateInfo.TrainCarGuid.ToString();
            IsUnlocked = trainCarUnlockStateInfo.IsUnlocked;
        }
    }
}
