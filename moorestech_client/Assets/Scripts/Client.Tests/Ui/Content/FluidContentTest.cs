using System;
using System.IO;
using Client.Game.Localization;
using Client.Localization;
using Client.Ui.Content;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Content
{
    /// <summary>
    /// GameContentPresentation が液体の背面の色をマスタから解き、液体名が言語に追従することを確かめる
    /// Verifies GameContentPresentation resolves the fluid fill color from the master and fluid names follow the language
    /// </summary>
    public class FluidContentTest
    {
        private static readonly Guid WaterGuid = new("00000000-0000-0000-1234-000000000001");
        private bool _hadSavedLanguage;
        private string _savedLanguage;
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            // 端末の保存言語を退避し、テスト用 mod のマスタと英語の辞書を組む
            // Stash the device's saved language and load the test mod master with an English dictionary
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"fluid-content-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            MergeFluidNames();
        }

        [TearDown]
        public void TearDown()
        {
            // 言語の復元を先に済ませ、検証が失敗しても英語固定を残さない
            // Restore the language first so a failed assertion never leaves English pinned
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
            Localize.Initialize();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 液体の背面の色はマスタの色()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));

            var face = content.FluidFace(MasterHolder.FluidMaster.GetFluidId(WaterGuid));

            Assert.AreEqual((Color)new Color32(0x33, 0x99, 0xFF, 0xFF), face);
        }

        [Test]
        public void 空の液体は色を引かずに止める()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));

            Assert.Throws<ArgumentException>(() => content.FluidFace(FluidMaster.EmptyFluidId));
        }

        [Test]
        public void 液体名は言語を切り替えると同じ値のまま訳し直す()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));
            var label = content.Fluid(MasterHolder.FluidMaster.GetFluidId(WaterGuid)).Label;

            Assert.AreEqual("Test Water", label.Resolve());
            Localize.TrySetChosenLanguage("japanese");
            Assert.AreEqual("テスト水", label.Resolve());
        }

        // 一時 mod の辞書へ液体名の導出キーを合成する（C3-a の GameContentPresentationTest と同じ形）
        // Compose the fluid-name derived key into a temporary mod dictionary (same shape as C3-a GameContentPresentationTest)
        private void MergeFluidNames()
        {
            var modDirectory = Path.Combine(_temporaryDirectory, "fluid-names-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(
                Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"fluid-names\",\"name\":\"fluid-names\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(modDirectory, "localization", "localization.csv"),
                "key,Source,english,japanese\n" +
                $"{ContentLocalizationKeys.FluidName(WaterGuid).Key},Test Water,Test Water,テスト水\n");
            Localize.MergeGameDictionaries(new ModsResource(_temporaryDirectory), new[] { new ModId("author:fluid-names") }, MasterSourceTextCollector.Collect());
        }
    }
}
