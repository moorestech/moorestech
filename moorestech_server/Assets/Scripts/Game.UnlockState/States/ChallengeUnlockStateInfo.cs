using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class ChallengeUnlockStateInfo
    {
        public Guid ChallengeGuid { get; }
        public bool IsUnlocked { get; private set; }

        public ChallengeUnlockStateInfo(Guid challengeGuid, bool initialUnlocked)
        {
            ChallengeGuid = challengeGuid;
            IsUnlocked = initialUnlocked;
        }

        public ChallengeUnlockStateInfo(ChallengeUnlockStateInfoJsonObject jsonObject)
        {
            ChallengeGuid = Guid.Parse(jsonObject.ChallengeGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class ChallengeUnlockStateInfoJsonObject
    {
        [JsonProperty("challengeGuid")] public string ChallengeGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public ChallengeUnlockStateInfoJsonObject(ChallengeUnlockStateInfo info)
        {
            ChallengeGuid = info.ChallengeGuid.ToString();
            IsUnlocked = info.IsUnlocked;
        }

        // Parameterless constructor for JSON deserialization
        public ChallengeUnlockStateInfoJsonObject() { }
    }
}