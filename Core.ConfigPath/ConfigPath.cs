using System;
using System.IO;

namespace Core.ConfigPath
{
    public class ConfigPath
    {
        public static string RecipeConfigPath
        {
            get
            {
                if (recipeConfigPath == String.Empty)recipeConfigPath = GetConfigPath( "macineRecipe.json");
                return recipeConfigPath;
            }
        }
        private static string recipeConfigPath = String.Empty;
        public static string BlockConfigPath
        {
            get
            {
                if (blockConfigPath == String.Empty)blockConfigPath = GetConfigPath( "block.json");
                return blockConfigPath;
            }
        }

        private static string blockConfigPath = String.Empty;
        public static string ItemConfigPath 
        {
            get
            {
                if (itemConfigPath == String.Empty)itemConfigPath = GetConfigPath( "item.json");
                return itemConfigPath;
            }
        }
        
        private static string GetConfigPath(string fileName)
        {
            DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory); 
            DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            return Path.Combine(diParent.FullName, "Core.ConfigPath", "Json",fileName);
        }
        private static string itemConfigPath = String.Empty;
    }
}