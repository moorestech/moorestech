using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class MachineRecipeUnlockStateInfo
    {
        public Guid MachineRecipeGuid { get; }
        public bool IsUnlocked { get; private set; }

        public MachineRecipeUnlockStateInfo(Guid machineRecipeGuid, bool isUnlocked)
        {
            MachineRecipeGuid = machineRecipeGuid;
            IsUnlocked = isUnlocked;
        }

        public MachineRecipeUnlockStateInfo(MachineRecipeUnlockStateInfoJsonObject jsonObject)
        {
            MachineRecipeGuid = Guid.Parse(jsonObject.MachineRecipeGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }


        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class MachineRecipeUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string MachineRecipeGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public MachineRecipeUnlockStateInfoJsonObject() { }

        public MachineRecipeUnlockStateInfoJsonObject(MachineRecipeUnlockStateInfo machineRecipeUnlockStateInfo)
        {
            MachineRecipeGuid = machineRecipeUnlockStateInfo.MachineRecipeGuid.ToString();
            IsUnlocked = machineRecipeUnlockStateInfo.IsUnlocked;
        }
    }
}
