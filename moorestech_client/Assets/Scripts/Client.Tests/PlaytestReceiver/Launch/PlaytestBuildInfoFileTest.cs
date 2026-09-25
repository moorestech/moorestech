using System.IO;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestBuildInfoFileTest
    {
        [Test]
        public void パスはStreamingAssets直下のbuildInfoJsonを指す()
        {
            StringAssert.EndsWith(Path.Combine("StreamingAssets", "build-info.json"), GameSystemPaths.BuildInfoFilePath);
        }

        [Test]
        public void Editor実行では配布ビルドの印が無い()
        {
            // 配布ビルドだけがbuild-info.jsonを持つ。Editorに置かれていたら配布版と誤判定する
            // Only distribution builds carry build-info.json; one left in the Editor would misidentify a distribution build
            Assert.IsFalse(File.Exists(GameSystemPaths.BuildInfoFilePath), $"Editorに {GameSystemPaths.BuildInfoFilePath} が残っている");
        }
    }
}
