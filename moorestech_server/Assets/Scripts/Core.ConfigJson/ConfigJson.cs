namespace Core.ConfigJson
{
    public class ConfigJson
    {
        public readonly string ModId;

        public readonly string BlockConfigJson;
        public readonly string CraftRecipeConfigJson;
        public readonly string ItemConfigJson;
        public readonly string MachineRecipeConfigJson;
        public readonly string MapObjectConfigJson;


        public ConfigJson(string modId, string itemJson, string blockConfigJson, string machineRecipeConfigJson, string craftRecipeConfigJson, string mapObjectConfigJson)
        {
            ModId = modId;

            ItemConfigJson = itemJson;
            BlockConfigJson = blockConfigJson;
            MachineRecipeConfigJson = machineRecipeConfigJson;
            CraftRecipeConfigJson = craftRecipeConfigJson;
            MapObjectConfigJson = mapObjectConfigJson;
        }
    }
}