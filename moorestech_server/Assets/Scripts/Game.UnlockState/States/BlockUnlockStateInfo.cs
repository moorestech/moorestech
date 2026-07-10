using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class BlockUnlockStateInfo
    {
        public Guid BlockGuid { get; }
        public bool IsUnlocked { get; private set; }

        public BlockUnlockStateInfo(Guid blockGuid, bool isUnlocked)
        {
            BlockGuid = blockGuid;
            IsUnlocked = isUnlocked;
        }

        public BlockUnlockStateInfo(BlockUnlockStateInfoJsonObject jsonObject)
        {
            BlockGuid = Guid.Parse(jsonObject.BlockGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class BlockUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string BlockGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public BlockUnlockStateInfoJsonObject() { }

        public BlockUnlockStateInfoJsonObject(BlockUnlockStateInfo blockUnlockStateInfo)
        {
            BlockGuid = blockUnlockStateInfo.BlockGuid.ToString();
            IsUnlocked = blockUnlockStateInfo.IsUnlocked;
        }
    }
}
