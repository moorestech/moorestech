using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mooresmaster.SandBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        // var blocks = BlocksLoader.Load(GetJson("blocks"));
        // var challenges = ChallengesLoader.Load(GetJson("challenges"));
        // var craftRecipes = CraftRecipesLoader.Load(GetJson("craftRecipes"));
        // var items = ItemsLoader.Load(GetJson("items"));
        // var machineRecipes = MachineRecipesLoader.Load(GetJson("machineRecipes"));
        // var mapObjects = MapObjectsLoader.Load(GetJson("mapObjects"));
        
        
        // Console.WriteLine(blocks);
        // Console.WriteLine(challenges);
        // Console.WriteLine(craftRecipes);
        // Console.WriteLine(items);
        // Console.WriteLine(machineRecipes);
        // Console.WriteLine(mapObjects);
        
        // var testModJsonsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMod");
        // var jsonPaths = Directory.GetFiles(testModJsonsDirectoryPath);
        // var jsons = jsonPaths.Select(path => (path, File.ReadAllText(path)));
        
        // foreach (var (path, json) in jsons)
        // {
        //     // Console.WriteLine(jsonFile);
        //     var jsonData = (JObject)JsonConvert.DeserializeObject(json);
        //
        //     try
        //     {
        //     }
        //     catch (Exception e)
        //     {
        //         Console.ForegroundColor = ConsoleColor.Red;
        //         Console.WriteLine($"throw from {path}\n{e}");
        //         Console.ResetColor();
        //     }
        // }
    }
    
    private static JToken GetJson(string name)
    {
        var blockJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMod", $"{name}.json");
        var blockJson = File.ReadAllText(blockJsonPath);
        return (JToken)JsonConvert.DeserializeObject(blockJson);
    }
}
