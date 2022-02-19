using System;
using System.IO;

namespace Core.ConfigJson
{
    public static class ConfigJsonPath
    {
        public static string RecipeConfigPath => GetConfigPath("macineRecipe.json");

        public static string BlockConfigPath => GetConfigPath("block.json");

        public static string ItemConfigPath => GetConfigPath("item.json");
        public static string OreConfigPath => GetConfigPath("ore.json");
        public static string CraftRecipeConfigPath => GetConfigPath("craftRecipe.json");

        private static string GetConfigPath(string fileName)
        {
            DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
            DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            return Path.Combine(diParent.FullName, "Core.ConfigJson", "Json", fileName);
        }
    }
}