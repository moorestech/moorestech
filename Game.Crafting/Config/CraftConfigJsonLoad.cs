using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Core.ConfigPath;
using Game.Crafting.Interface;

namespace Game.Crafting.Config
{
    public class CraftConfigJsonLoad
    {
        public CraftConfigJsonData Load()
        {
            //JSONをロードする
            var json = File.ReadAllText(ConfigPath.CraftRecipeConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(CraftConfigJsonData));
            var data = serializer.ReadObject(ms) as CraftConfigJsonData;

            return data;
        }
    }
}