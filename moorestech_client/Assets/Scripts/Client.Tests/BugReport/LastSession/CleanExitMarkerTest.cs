using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class CleanExitMarkerTest
    {
        [SetUp]
        [TearDown]
        public void RemoveMarker()
        {
            if (File.Exists(CleanExitMarker.FilePath)) File.Delete(CleanExitMarker.FilePath);
        }

        [Test]
        public void マーカーがあれば前回は正常終了と判定し消える()
        {
            CleanExitMarker.MarkCleanExit();
            Assert.IsTrue(File.Exists(CleanExitMarker.FilePath));
            Assert.IsTrue(CleanExitMarker.ConsumePreviousExitCleanFlag());
            Assert.IsFalse(File.Exists(CleanExitMarker.FilePath));
        }

        [Test]
        public void マーカーが無ければ前回は異常終了と判定する()
        {
            Assert.IsFalse(CleanExitMarker.ConsumePreviousExitCleanFlag());
            Assert.IsFalse(File.Exists(CleanExitMarker.FilePath));
        }
    }
}
