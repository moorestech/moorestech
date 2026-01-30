//using Mooresmaster.Loader.BlocksModule;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mooresmaster.SandBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        var blockJson = GetJson("blocks");
//        BlocksLoader.Load(blockJson);
    }
    
    private static JToken GetJson(string name)
    {
        var blockJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMod", $"{name}.json");
        var blockJson = File.ReadAllText(blockJsonPath);
        return (JToken)JsonConvert.DeserializeObject(blockJson);
    }
}