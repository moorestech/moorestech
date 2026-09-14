using System.IO;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 配布ビルドの印。plan E がビルド時に焼き、本アセンブリは在るかどうかしか見ない
    // The distribution-build marker; plan E bakes it at build time and this assembly only checks its presence
    public static class PlaytestBuildInfoFile
    {
        public const string FileName = "build-info.json";

        public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, FileName);

        public static bool Exists()
        {
            return File.Exists(Path);
        }
    }
}
