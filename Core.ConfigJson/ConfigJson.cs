namespace Core.ConfigJson
{
    public class ConfigJson
    {
        public readonly string ModName;
            
        public readonly string ItemConfigJson;
        public readonly string BlockConfigJson;
        public readonly string MachineRecipeConfigJson;
        public readonly string CraftRecipeConfigJson;

        public ConfigJson(string modName,string itemJson, string blockConfigJson, string machineRecipeConfigJson, string craftRecipeConfigJson)
        {
            ItemConfigJson = itemJson;
            BlockConfigJson = blockConfigJson;
            MachineRecipeConfigJson = machineRecipeConfigJson;
            CraftRecipeConfigJson = craftRecipeConfigJson;
            ModName = modName;
        }
    }
}