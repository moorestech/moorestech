using Newtonsoft.Json;

namespace mooresmaster.SandBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var testModJsonsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMod");
        var jsonPaths = Directory.GetFiles(testModJsonsDirectoryPath);
        var jsons = jsonPaths.Select(File.ReadAllText);

        foreach (var jsonFile in jsons)
        {
            // Console.WriteLine(jsonFile);
            dynamic jsonData = JsonConvert.DeserializeObject(jsonFile);

            Console.WriteLine(jsonData);
        }
    }
}
