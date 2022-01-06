using System;
using System.IO;

namespace Core.ConfigPath
{
    public class ConfigPath
    {
        public static string RecipeConfigPath => GetConfigPath("macineRecipe.json");

        public static string BlockConfigPath => GetConfigPath("block.json");

        public static string ItemConfigPath => GetConfigPath("item.json");
        public static string OreConfigPath => GetConfigPath("ore.json");

        private static string GetConfigPath(string fileName)
        {
            DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
            DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            return Path.Combine(diParent.FullName, "Core.ConfigPath", "Json", fileName);
        }
    }
}