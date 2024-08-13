using Mooresmaster.Loader.BlocksModule;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Loader.CraftRecipesModule;
using Mooresmaster.Loader.ItemsModule;
using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Loader.MapObjectsModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mooresmaster.SandBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        BlocksLoader.Load(GetJson("blocks"));
        ChallengesLoader.Load(GetJson("challenges"));
        CraftRecipesLoader.Load(GetJson("craftRecipes"));
        ItemsLoader.Load(GetJson("items"));
        MachineRecipesLoader.Load(GetJson("machineRecipes"));
        MapObjectsLoader.Load(GetJson("mapObjects"));

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
