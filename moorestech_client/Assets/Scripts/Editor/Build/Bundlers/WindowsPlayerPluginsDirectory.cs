using System.IO;
using Client.Game.InGame.BugReport.Recording;

namespace Client.Editor.Build.Bundlers
{
    /// <summary>
    /// Windows Player 成果物のネイティブプラグイン置き場（&lt;exe&gt;_Data/Plugins/x86_64）を解決する
    /// Resolves the Windows player artifact's native plugin directory (&lt;exe&gt;_Data/Plugins/x86_64)
    /// </summary>
    internal static class WindowsPlayerPluginsDirectory
    {
        public static string Resolve(string playerOutputPath)
        {
            return Path.Combine(ResolveDataDirectory(playerOutputPath), FfmpegLocator.BundledPluginsRelativeDirectory);
        }

        // Windows/Linux player の `<exe>_Data` を組み立てる。macは Contents 配下と別形なので、呼び出し側が分岐する
        // Builds a Windows/Linux player's `<exe>_Data`; mac uses a different Contents-based layout, so callers branch on target
        public static string ResolveDataDirectory(string playerOutputPath)
        {
            return Path.Combine(Path.GetDirectoryName(playerOutputPath), Path.GetFileNameWithoutExtension(playerOutputPath) + "_Data");
        }
    }
}
