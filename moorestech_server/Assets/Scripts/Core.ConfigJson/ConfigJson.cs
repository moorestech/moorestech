namespace Core.ConfigJson
{
    public class ConfigJson
    {
        public readonly string BlockConfigJson;
        public readonly string ChallengeConfigJson;
        public readonly string CraftRecipeConfigJson;
        public readonly string ItemConfigJson;
        public readonly string MachineRecipeConfigJson;
        public readonly string MapObjectConfigJson;
        public readonly string ModId;
        
        
        public ConfigJson(string modId, string itemJson, string blockConfigJson, string machineRecipeConfigJson, string craftRecipeConfigJson, string mapObjectConfigJson, string challengeConfigJson)
        {
            ModId = modId;
            
            ItemConfigJson = itemJson;
            BlockConfigJson = blockConfigJson;
            MachineRecipeConfigJson = machineRecipeConfigJson;
            CraftRecipeConfigJson = craftRecipeConfigJson;
            MapObjectConfigJson = mapObjectConfigJson;
            ChallengeConfigJson = challengeConfigJson;
        }
    }
}