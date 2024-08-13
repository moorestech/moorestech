using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mooresmaster.SandBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var testModJsonsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMod");
        var jsonPaths = Directory.GetFiles(testModJsonsDirectoryPath);
        var jsons = jsonPaths.Select(path => (path, File.ReadAllText(path)));

        foreach (var (path, json) in jsons)
        {
            // Console.WriteLine(jsonFile);
            var jsonData = (JObject)JsonConvert.DeserializeObject(json);

            try
            {
                var value0 = jsonData["data"];
                var value1 = value0[0].ToString();
                Console.WriteLine(value1);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"throw from {path}\n{e}");
                Console.ResetColor();
            }
        }
    }
}
