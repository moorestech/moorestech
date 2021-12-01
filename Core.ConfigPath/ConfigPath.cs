using System;
using System.IO;

namespace Core.ConfigPath
{
    /// <summary>
    /// TODO ConfigをDI化する
    /// </summary>
    public class ConfigPath
    {
        public static string RecipeConfigPath
        {
            get
            {
                if (recipeConfigPath == String.Empty)
                {
                    DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory); 
                    DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            
                    recipeConfigPath = Path.Combine(diParent.FullName, "Core.ConfigPath", "Json","macineRecipe.json");
                }
                return recipeConfigPath;
            }
        }
        private static string recipeConfigPath = String.Empty;
        public static string BlockConfigPath
        {
            get
            {
                
                if (blockConfigPath == String.Empty)
                {
                    DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory); 
                    DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            
                    blockConfigPath = Path.Combine(diParent.FullName, "Core.ConfigPath", "Json","block.json");
                }
                return blockConfigPath;
            }
        }

        private static string blockConfigPath = String.Empty;
        public static string ItemConfigPath 
        {
            get
            {
                
                if (itemConfigPath == String.Empty)
                {
                    DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory); 
                    DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
            
                    itemConfigPath = Path.Combine(diParent.FullName, "Core.ConfigPath", "Json","item.json");
                }
                return itemConfigPath;
            }
        }
        private static string itemConfigPath = String.Empty;
    }
}