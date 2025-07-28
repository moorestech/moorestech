using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class ChallengeCategoryUnlockStateInfo
    {
        public Guid ChallengeCategoryGuid { get; }
        public bool IsUnlocked { get; private set; }

        public ChallengeCategoryUnlockStateInfo(Guid challengeCategoryGuid, bool initialUnlocked)
        {
            ChallengeCategoryGuid = challengeCategoryGuid;
            IsUnlocked = initialUnlocked;
        }

        public ChallengeCategoryUnlockStateInfo(ChallengeUnlockStateInfoJsonObject jsonObject)
        {
            ChallengeCategoryGuid = Guid.Parse(jsonObject.CategoryGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class ChallengeUnlockStateInfoJsonObject
    {
        [JsonProperty("categoryGuid")] public string CategoryGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public ChallengeUnlockStateInfoJsonObject(ChallengeCategoryUnlockStateInfo info)
        {
            CategoryGuid = info.ChallengeCategoryGuid.ToString();
            IsUnlocked = info.IsUnlocked;
        }

        // Parameterless constructor for JSON deserialization
        public ChallengeUnlockStateInfoJsonObject() { }
    }
}