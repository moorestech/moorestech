using System.IO;
using Client.Game.InGame.BugReport.Recording;

namespace Client.Editor.Build.Bundlers
{
    /// <summary>
    /// Mac成果物(.app)内の同梱先パスを組み立てる。ビルド工程だけが使う知識をランタイムへ置かないため分ける
    /// Builds destination paths inside the Mac .app; kept out of runtime code because only the build needs it
    /// </summary>
    internal static class MacPlayerAppBundle
    {
        public static string ResolveContentsDirectory(string appPath) => Path.Combine(appPath, "Contents");

        public static string ResolveBundledFfmpegPath(string appPath) =>
            Path.Combine(ResolveContentsDirectory(appPath), FfmpegLocator.BundledMacExecutableRelativePath);

        public static string ResolveBundledResourcePath(string appPath, string fileName) =>
            Path.Combine(ResolveContentsDirectory(appPath), "Resources", fileName);
    }
}
