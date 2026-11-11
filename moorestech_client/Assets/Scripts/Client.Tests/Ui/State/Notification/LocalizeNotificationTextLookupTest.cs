using System;
using Client.Localization;
using Client.Tests.Ui.Screens;
using Client.Ui.State.Notification.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 本番の口が辞書やマスタの欠けで理由つきの LogError を出し、Web と同じ見え方で返すことを英語の辞書で確かめる（UITK だけ）
    /// Verifies with the English dictionary that the production lookup logs a reasoned error on dictionary or master gaps and returns the Web look (UITK-only)
    /// </summary>
    public class LocalizeNotificationTextLookupTest : ScreenTestHost
    {
        // マスタ（ItemMaster）を読むのは既存の client テストと同じ土台（BlockSlotReaderTest）
        // Load the master through the same base as existing client tests (BlockSlotReaderTest)
        [SetUp]
        public void LoadMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 辞書に無い内容名は理由つきのエラーを出し欠けの印のまま見える()
        {
            var key = ContentLocalizationKeys.ResearchName(Guid.Empty);
            LogAssert.Expect(LogType.Error, $"[LocalizeNotificationTextLookup] content text missing in every language: {key.Key}");
            Assert.IsTrue(LocalizationMissingText.IsMissingText(new LocalizeNotificationTextLookup().ContentText(key), key.Key));
        }

        [Test]
        public void マスタに無いアイテムは理由つきのエラーを出しWebと同じ代わりの名前で見える()
        {
            const int missingItemId = 987654;
            LogAssert.Expect(LogType.Error, $"[LocalizeNotificationTextLookup] item {missingItemId} is not in the item master; showing the fallback name");
            Assert.AreEqual($"Item {missingItemId}", new LocalizeNotificationTextLookup().ItemDisplayName(new ItemId(missingItemId)));
        }
    }
}
