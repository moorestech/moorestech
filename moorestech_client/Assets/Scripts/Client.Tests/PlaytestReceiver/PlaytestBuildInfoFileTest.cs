using System.IO;
using Client.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestBuildInfoFileTest
    {
        [Test]
        public void パスはStreamingAssets直下のbuildInfoJsonを指す()
        {
            StringAssert.EndsWith(Path.Combine("StreamingAssets", "build-info.json"), PlaytestBuildInfoFile.Path);
        }

        [Test]
        public void Editor実行では配布ビルドの印が無い()
        {
            // 配布ビルドだけがbuild-info.jsonを持つ。Editorに置かれていたら照合が誤発火する
            // Only distribution builds carry build-info.json; one left in the Editor would misfire the gate
            Assert.IsFalse(PlaytestBuildInfoFile.Exists(), $"Editorに {PlaytestBuildInfoFile.Path} が残っている");
        }
    }
}
