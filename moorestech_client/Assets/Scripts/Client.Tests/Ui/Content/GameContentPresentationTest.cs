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
    /// GameContentPresentation が ID から ID の文字・テクスチャ・GUID 由来の名前を作ることを確かめる
    /// Verifies GameContentPresentation builds ID text, texture and GUID-derived names from IDs
    /// </summary>
    public class GameContentPresentationTest
    {
        private static readonly Guid ItemGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Guid BlockGuid = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid FluidGuid = new("00000000-0000-0000-1234-000000000001");
        private Texture2D _texture;
        private bool _hadSavedLanguage;
        private string _savedLanguage;
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            // 端末の保存言語を退避し、テスト用 mod のマスタと英語の辞書を組む
            // Stash the device's saved language and load the test mod master with an English dictionary
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"content-names-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            MergeContentNames();
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
            UnityEngine.Object.DestroyImmediate(_texture);
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">item master のGuidから辞書名をtooltipと代替テキストへ使う</web-test>
        [Test]
        public void アイテムはIDの文字とテクスチャとGUIDから導いた辞書名を持つ()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, null, null));
            var itemId = MasterHolder.ItemMaster.GetItemId(ItemGuid);
            var icon = content.Item(itemId);

            Assert.AreEqual(itemId.AsPrimitive().ToString(), icon.IdText);
            Assert.AreSame(_texture, icon.Texture);
            Assert.AreEqual("Test Plate", icon.Label.Resolve());
            Assert.AreEqual("Test Plate", content.ItemName(itemId).Resolve());
        }

        [Test]
        public void ブロックと液体もGUIDから導いた辞書名を持つ()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, _texture, _texture));

            Assert.AreEqual("Test Machine", content.Block(MasterHolder.BlockMaster.GetBlockId(BlockGuid)).Label.Resolve());
            Assert.AreEqual("Test Water", content.Fluid(MasterHolder.FluidMaster.GetFluidId(FluidGuid)).Label.Resolve());
        }

        [Test]
        public void テクスチャの無いIDはnullのまま渡しGameIconに代わりを任せる()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));

            Assert.IsNull(content.Item(MasterHolder.ItemMaster.GetItemId(ItemGuid)).Texture);
        }

        [Test]
        public void 空のアイテムは表示素材にせず止める()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));

            Assert.Throws<ArgumentException>(() => content.Item(ItemMaster.EmptyItemId));
        }

        [Test]
        public void 空の液体は表示素材にせず止める()
        {
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));

            Assert.Throws<ArgumentException>(() => content.Fluid(FluidMaster.EmptyFluidId));
        }

        [Test]
        public void 本番の素材も空のアイテムと液体はテクスチャを引かずに止める()
        {
            // IGameContentTextureSource を直に使う呼び手にも、空の ID の無言の null を返さない
            // Even callers using IGameContentTextureSource directly never get a silent null for an empty ID
            var source = new ClientContextContentTextureSource();

            Assert.Throws<ArgumentException>(() => source.ItemTexture(ItemMaster.EmptyItemId));
            Assert.Throws<ArgumentException>(() => source.FluidTexture(FluidMaster.EmptyFluidId));
        }

        // 一時 mod の辞書へ導出キーを合成
        // Compose derived keys into a temporary mod dictionary
        private void MergeContentNames()
        {
            var modDirectory = Path.Combine(_temporaryDirectory, "content-names-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(
                Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"content-names\",\"name\":\"content-names\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(modDirectory, "localization", "localization.csv"),
                "key,Source,english,japanese\n" +
                $"{ContentLocalizationKeys.ItemName(ItemGuid).Key},Test Plate,Test Plate,テスト鉄板\n" +
                $"{ContentLocalizationKeys.BlockName(BlockGuid).Key},Test Machine,Test Machine,テスト機械\n" +
                $"{ContentLocalizationKeys.FluidName(FluidGuid).Key},Test Water,Test Water,テスト水\n");
            Localize.MergeGameDictionaries(new ModsResource(_temporaryDirectory), new[] { new ModId("author:content-names") }, MasterSourceTextCollector.Collect());
        }
    }
}
