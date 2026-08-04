using System;
using System.IO;
using System.Reflection;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class VeinPinTutorialTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000001");
        private const string MapPinId = "map-object-pin";
        private const string VeinPinId = "vein-pin";

        private ChallengeMaster _originalChallengeMaster;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
            SetChallengeMaster(CreateVeinPinChallengeMaster());
            _root = new GameObject("VeinPinTutorialTest");
            ClearWorldPins();
        }

        [TearDown]
        public void TearDown()
        {
            ClearWorldPins();
            UnityEngine.Object.DestroyImmediate(_root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        [Test]
        public void TutorialManagerはveinPinを専用managerへdispatchする()
        {
            var veinPin = new RecordingVeinPin();
            var manager = new TutorialManager(
                new RecordingMapObjectPin(),
                veinPin,
                _root.AddComponent<UIHighlightTutorialManager>(),
                _root.AddComponent<KeyControlTutorialManager>(),
                _root.AddComponent<ItemViewHighLightTutorialManager>(),
                _root.AddComponent<BlockPlacePreviewTutorialManager>());

            manager.ApplyTutorial(ChallengeGuid);

            Assert.AreEqual(1, veinPin.ApplyCount);
        }

        [Test]
        public void VeinPin完了時は自身のworldPinだけを消してMapObjectPinを残す()
        {
            var veinPin = _root.AddComponent<VeinPin>();
            var tutorial = MasterHolder.ChallengeMaster.GetChallenge(ChallengeGuid).Tutorials[0];
            WorldPinStateStore.Instance.SetPin(MapPinId, "map", new WorldPinProjection { OnScreen = true });
            WorldPinStateStore.Instance.SetPin(VeinPinId, "vein", new WorldPinProjection { OnScreen = true });

            veinPin.ApplyTutorial(tutorial);
            veinPin.CompleteTutorial();

            var pins = WorldPinStateStore.Instance.GetCurrent().Pins;
            Assert.AreEqual(1, pins.Length);
            Assert.AreEqual(MapPinId, pins[0].PinId);
            Assert.IsFalse(_root.activeSelf);
        }

        private static ChallengeMaster CreateVeinPinChallengeMaster()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
            var tutorial = (JObject)tutorials[0].DeepClone();
            tutorials.Clear();
            tutorials.Add(tutorial);
            tutorial["tutorialType"] = "veinPin";
            tutorial["tutorialParam"] = new JObject
            {
                ["veinGuid"] = "11111111-0000-0000-0000-000000000001",
                ["pinText"] = "nearest vein",
            };
            var master = new ChallengeMaster(json);
            master.Initialize();
            return master;
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster))
                .GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }

        private static void ClearWorldPins()
        {
            WorldPinStateStore.Instance.RemovePin(MapPinId);
            WorldPinStateStore.Instance.RemovePin(VeinPinId);
        }

        private sealed class RecordingMapObjectPin : IMapObjectPin
        {
            public void SetActive(bool active) { }
            public ITutorialView ApplyTutorial(TutorialsElement tutorial) => this;
            public void CompleteTutorial() { }
        }

        private sealed class RecordingVeinPin : IVeinPin
        {
            public int ApplyCount;
            public void SetActive(bool active) { }
            public ITutorialView ApplyTutorial(TutorialsElement tutorial)
            {
                ApplyCount++;
                return this;
            }
            public void CompleteTutorial()
            {
            }
        }
    }
}
