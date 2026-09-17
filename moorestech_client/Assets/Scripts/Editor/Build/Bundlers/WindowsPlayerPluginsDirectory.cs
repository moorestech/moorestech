using System.IO;

namespace Client.Editor.Build.Bundlers
{
    /// <summary>
    /// Windows Player 成果物のネイティブプラグイン置き場（&lt;exe&gt;_Data/Plugins/x86_64）を解決する
    /// Resolves the Windows player artifact's native plugin directory (&lt;exe&gt;_Data/Plugins/x86_64)
    /// </summary>
    public static class WindowsPlayerPluginsDirectory
    {
        public static string Resolve(string playerOutputPath)
        {
            var dataDirectory = Path.Combine(Path.GetDirectoryName(playerOutputPath), Path.GetFileNameWithoutExtension(playerOutputPath) + "_Data");
            return Path.Combine(dataDirectory, "Plugins", "x86_64");
        }
    }
}
