using System.Text.RegularExpressions;
using Client.Starter.Editor;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Starter
{
    // エディタの直Playが、無人起動でないときだけ常時記録を有効にすることの回帰ガード（ADR 0066）
    // Regression guard that an Editor direct play enables always-on capture only when the boot is attended (ADR 0066)
    public class DirectPlayAlwaysOnCaptureSettingsTest
    {
        [SetUp]
        public void SetUp()
        {
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [TearDown]
        public void TearDown()
        {
            // 静的な決定の残置は後続テストの起動を録り始めさせるため必ず戻す
            // A leftover static decision would make later test boots start recording, so always reset it
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [Test]
        public void 有人の直Playは常時記録を有効にする()
        {
            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason(null);

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.True);
        }

        [Test]
        public void 無人起動は常時記録を有効にせず理由をログへ出す()
        {
            LogAssert.Expect(LogType.Log, new Regex("無人起動のため直Playの常時記録を自動では有効にしません reason:unattendedBootMark"));

            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason("unattendedBootMark");

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False);
        }

        // 無人起動でも記録したいテストは起動前に明示的に有効化する。それを無効へ上書きしてはいけない
        // A test that wants capture on an unattended boot enables it up front; this must never overwrite that back to disabled
        [Test]
        public void 無人起動でも事前の明示的な有効化は潰さない()
        {
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());

            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason("batchMode");

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.True);
        }
    }
}
