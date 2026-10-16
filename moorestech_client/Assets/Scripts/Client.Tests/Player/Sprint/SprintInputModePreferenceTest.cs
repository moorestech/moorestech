using System.Text.RegularExpressions;
using Client.Input;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Player
{
    public class SprintInputModePreferenceTest
    {
        private bool _hadSavedMode;
        private string _savedMode;

        [SetUp]
        public void SetUp()
        {
            // 端末の設定を退避してテストを隔離する
            // Preserve the device preference to isolate each test
            _hadSavedMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            // テスト前の保存状態を戻す
            // Restore the persisted state from before the test
            if (_hadSavedMode)
                PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else
                PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void 未保存ならトグルを返す()
        {
            Assert.AreEqual(SprintInputMode.Toggle, SprintInputModePreference.Current);
        }

        [Test]
        public void 保存済みホールドを読み込む()
        {
            PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, "Hold");
            Assert.AreEqual(SprintInputMode.Hold, SprintInputModePreference.Current);
        }

        [Test]
        public void 未定義の保存値は警告してトグルを返す()
        {
            PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, "2");
            LogAssert.Expect(LogType.Warning, new Regex("Unknown saved sprint mode"));
            Assert.AreEqual(SprintInputMode.Toggle, SprintInputModePreference.Current);
        }

        [Test]
        public void 方式変更は保存後に一度通知して同値では通知しない()
        {
            var count = 0;
            using var subscription = SprintInputModePreference.OnSprintInputModeChanged.Subscribe(mode =>
            {
                Assert.AreEqual(SprintInputMode.Hold, mode);
                Assert.AreEqual("Hold", PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey));
                count++;
            });
            SprintInputModePreference.SetMode(SprintInputMode.Hold);
            SprintInputModePreference.SetMode(SprintInputMode.Hold);
            Assert.AreEqual(1, count);
            Assert.AreEqual(SprintInputMode.Hold, SprintInputModePreference.Current);
        }
    }
}
