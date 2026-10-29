using System;
using System.IO;
using Client.WebUiHost.Vite;
using NUnit.Framework;

namespace Client.Tests.WebUiHost.Vite
{
    public class ViteProcessCommandTest
    {
        [Test]
        public void NonzeroExitIsReturnedInsteadOfDiscarded()
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            Assert.AreEqual(1, ViteProcessCommand.Run("/usr/bin/false", ""));
#else
            Assert.Ignore("Unix fixture; Windows is unmeasured");
#endif
        }

        [Test]
        public void MissingCommandIsAnExplicitFailure()
        {
            Assert.Throws<FileNotFoundException>(() => ViteProcessCommand.Run("/no-such-owned-vite-command", ""));
        }

        [Test]
        public void TimeoutKillsOwnedHelperAndStillReportsFailure()
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            Assert.Throws<TimeoutException>(() => ViteProcessCommand.Run("/bin/sleep", "120"));
#else
            Assert.Ignore("Unix fixture; Windows is unmeasured");
#endif
        }
    }
}
