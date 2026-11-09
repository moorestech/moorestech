using System;
using Client.Localization;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.ModeHud;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 題の辞書の解決と取得失敗の文（L1。Web BlockInventoryPanel.localization.test.ts の写し）
    /// Title dictionary resolution and the fetch-failure text (L1; port of Web BlockInventoryPanel.localization.test.ts)
    /// </summary>
    public class BlockInventoryLocalizationTest : BlockInventoryScreenTestHost
    {
        // マスタに無い Guid（マスタの原文が Source を上書きしないように）
        // A Guid absent from the master (so the master text never overrides Source)
        private static readonly Guid MachineGuid = new("5b1c0a00-0000-4000-8000-0000000000a1");
        private PlacementNameTestDictionary _dictionary;

        [SetUp]
        public void SetUpDictionary()
        {
            _dictionary = new PlacementNameTestDictionary();
        }

        [TearDown]
        public void TearDownDictionary()
        {
            _dictionary.Restore();
        }

        /// <web-test file="src/features/blockInventory/BlockInventoryPanel.localization.test.ts">blockGuidの導出キーをfallback解決し、辞書世代の変更でタイトルを再描画する</web-test>
        [Test]
        public void 導出キーは対象言語から英語と原文へ戻り辞書の差し替えで題が描き直される()
        {
            // 言語を先に固定する（言語の初期化は合成した辞書を捨てる）
            // Pin the language first (language initialization drops merged dictionaries)
            _dictionary.UseLanguage("japanese");
            var key = ContentLocalizationKeys.BlockName(MachineGuid).Key;
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(Machine()));

            _dictionary.Merge($"{key},,Fallback Machine,\n");
            simulate.FrameUpdate();
            Assert.AreEqual("Fallback Machine", shell.TitleText());

            _dictionary.Merge($"{key},,Fallback Machine,対象言語の機械\n");
            simulate.FrameUpdate();
            Assert.AreEqual("対象言語の機械", shell.TitleText());

            _dictionary.Merge($"{key},Source Machine,,\n");
            simulate.FrameUpdate();
            Assert.AreEqual("Source Machine", shell.TitleText());
        }

        [Test]
        public void 言語を切り替えると題が描き直される()
        {
            _dictionary.UseLanguage("english");
            var key = ContentLocalizationKeys.BlockName(MachineGuid).Key;
            _dictionary.Merge($"{key},Source Machine,English Machine,日本語の機械\n");
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(Machine()));
            simulate.FrameUpdate();
            Assert.AreEqual("English Machine", shell.TitleText());

            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            Assert.AreEqual("日本語の機械", shell.TitleText());
        }

        /// <web-test file="src/features/blockInventory/BlockInventoryPanel.localization.test.ts">取得失敗はブロック本文を隠し、失敗文を表示する</web-test>
        [Test]
        public void 取得失敗は本文と題の操作を出さず失敗文を出す()
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OpenFailedChest()));
            simulate.FrameUpdate();

            var error = Find<Label>(shell.View, "block-inventory-error");
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed).Resolve(), error.text);
            Assert.IsNull(shell.View.Q("chest-grid"));
            Assert.AreEqual(0, Find<VisualElement>(shell.View, "block-inventory-title-action").childCount);
        }

        private static BlockInventoryPanelState Machine()
        {
            return new OpenBlockInventoryPanel(BlockMasterElement.BlockTypeConst.Chest, MachineGuid, default, new BlockInstanceId(1), new Vector3Int(1, 0, 0), Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
        }
    }
}
