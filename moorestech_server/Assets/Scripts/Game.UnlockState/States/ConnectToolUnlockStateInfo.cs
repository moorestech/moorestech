using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class ConnectToolUnlockStateInfo
    {
        public Guid ConnectToolGuid { get; }
        public bool IsUnlocked { get; private set; }

        public ConnectToolUnlockStateInfo(Guid connectToolGuid, bool isUnlocked)
        {
            ConnectToolGuid = connectToolGuid;
            IsUnlocked = isUnlocked;
        }

        public ConnectToolUnlockStateInfo(ConnectToolUnlockStateInfoJsonObject jsonObject)
        {
            ConnectToolGuid = Guid.Parse(jsonObject.ConnectToolGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class ConnectToolUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string ConnectToolGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public ConnectToolUnlockStateInfoJsonObject() { }

        public ConnectToolUnlockStateInfoJsonObject(ConnectToolUnlockStateInfo connectToolUnlockStateInfo)
        {
            ConnectToolGuid = connectToolUnlockStateInfo.ConnectToolGuid.ToString();
            IsUnlocked = connectToolUnlockStateInfo.IsUnlocked;
        }
    }
}
