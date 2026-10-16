using System.Threading.Tasks;
using Client.Input;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUi.Settings
{
    public class SprintInputModeContractTest
    {
        private bool _hadSavedMode;
        private string _savedMode;

        [SetUp]
        public void SetUp()
        {
            // 開発者の保存値を退避し未保存から始める
            // Stash the developer preference and start unsaved
            _hadSavedMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            // 保存値を復元して他テストへ漏らさない
            // Restore persistence to avoid leaking into other tests
            if (_hadSavedMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.Save();
        }

        [TestCase("hold", SprintInputMode.Hold)]
        [TestCase("toggle", SprintInputMode.Toggle)]
        public async Task RegisteredActionPersistsModeAndTopicReturnsCurrentValue(string wire, SprintInputMode mode)
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new SprintInputModeTopic(hub);
            SprintInputModePreference.SetMode(mode == SprintInputMode.Toggle ? SprintInputMode.Hold : SprintInputMode.Toggle);
            var revisionBefore = hub.GetTopicRevision(SprintInputModeTopic.TopicName);
            var changes = 0;
            using var subscription = SprintInputModePreference.OnChanged.Subscribe(_ => changes++);

            // 登録済みactionとsnapshotを通してwire契約を検証する
            // Verify the wire contract through the registered action and snapshot
            var result = await hub.ResolveAction("settings.setSprintInputMode").ExecuteAsync(new JObject { ["mode"] = wire });
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(result.Ok);
            Assert.AreEqual(mode, SprintInputModePreference.Current);
            Assert.AreEqual(mode.ToString(), PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey));
            Assert.AreEqual(wire, snapshot["mode"]?.Value<string>());
            Assert.AreEqual(1, changes);
            Assert.AreEqual(revisionBefore + 1, hub.GetTopicRevision(SprintInputModeTopic.TopicName));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("run")]
        [TestCase("Toggle")]
        public async Task InvalidValueLeavesPreferenceUnchanged(string wire)
        {
            var changes = 0;
            using var subscription = SprintInputModePreference.OnChanged.Subscribe(_ => changes++);
            var result = await new SetSprintInputModeActionHandler().ExecuteAsync(new JObject { ["mode"] = wire });
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_sprint_mode", result.Error);
            Assert.AreEqual(SprintInputMode.Toggle, SprintInputModePreference.Current);
            Assert.IsFalse(PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public async Task NullPayloadReturnsFailure()
        {
            var result = await new SetSprintInputModeActionHandler().ExecuteAsync(null);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_sprint_mode", result.Error);
        }
    }
}
