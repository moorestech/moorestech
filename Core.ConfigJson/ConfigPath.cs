using System.IO;

namespace Core.ConfigJson
{
    public class ConfigPath
    {
        private const string MachineRecipeConfigFileName = "machineRecipe.json";
        private const string BlockConfigFileName = "block.json";
        private const string ItemConfigFileName = "item.json";
        private const string OreConfigFileName = "ore.json";
        private const string CraftRecipeFileName = "craftRecipe.json";
        
        public string MachineRecipeConfigPath => Path.Combine(_jsonFolderPath, MachineRecipeConfigFileName);
        public string BlockConfigPath => Path.Combine(_jsonFolderPath, BlockConfigFileName);
        public string ItemConfigPath => Path.Combine(_jsonFolderPath, ItemConfigFileName);
        public string OreConfigPath => Path.Combine(_jsonFolderPath, OreConfigFileName);
        public string CraftRecipeConfigPath => Path.Combine(_jsonFolderPath, CraftRecipeFileName);

        private readonly string _jsonFolderPath;

        public ConfigPath(string jsonFolderPath)
        {
            _jsonFolderPath = jsonFolderPath;
        }
    }
}