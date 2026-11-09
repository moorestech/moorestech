using System;
using System.Text.RegularExpressions;
using Client.Ui.State.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// 読めないチュートリアル GUID の LogError が GUID ごとに 1 回だけ出ることを確かめる（L0）
    /// Verifies an unreadable tutorial GUID logs its error only once per GUID (L0)
    /// </summary>
    public class TutorialTextFactoryTest
    {
        [Test]
        public void 読めないGUIDは文脈が変わってもLogErrorを1回だけ出す()
        {
            var factory = new TutorialTextFactory();
            LogAssert.Expect(LogType.Error, new Regex("tutorialGuid is not a GUID: 'not-a-guid'"));
            Assert.IsFalse(factory.TryCreate("not-a-guid", "keyControl s1:k1", out _));
            Assert.IsFalse(factory.TryCreate("not-a-guid", "keyControl s2:k1", out _));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 読めないGUIDの後でも正しいGUIDは文になる()
        {
            var factory = new TutorialTextFactory();
            LogAssert.Expect(LogType.Error, new Regex("tutorialGuid is not a GUID: 'not-a-guid'"));
            Assert.IsFalse(factory.TryCreate("not-a-guid", "worldPin p1", out _));
            Assert.IsTrue(factory.TryCreate(Guid.NewGuid().ToString(), "worldPin p1", out var text));
            Assert.IsNotNull(text);
        }
    }
}
