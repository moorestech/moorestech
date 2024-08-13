using Mooresmaster.Loader;
using Mooresmaster.Loader.BlocksModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mooresmaster.SandBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var blockJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMod", "blocks.json");
        var blockJson = File.ReadAllText(blockJsonPath);

        var json = (JToken)JsonConvert.DeserializeObject(blockJson);
        var blocks = GlobalLoader.Load(json);
        Console.WriteLine(blocks);

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
}
