using System;
using System.IO;
using System.Linq;
using Client.Game.Localization;
using Client.Localization;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Tests.Ui.Tooltip;
using Client.Ui.Content;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryPanelTooltipTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private const string WoodName = "Test Wood";
        private Texture2D _texture;
        private bool _hadSavedLanguage;
        private string _savedLanguage;
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            // 端末の保存言語を退避し、テスト mod のマスタと英語の辞書を組む（前例 GameContentPresentationTest）
            // Stash the device's saved language and load the test mod master with an English dictionary (precedent GameContentPresentationTest)
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"inventory-tooltip-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            MergeWoodName();
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
        }

        /// <web-test file="e2e/tests/regression/itemSlot.spec.ts">インベントリスロットがマスタGuidの辞書名をtooltip表示する</web-test>
        [Test]
        public void 持ち物の枠の上でマスタGuidの辞書名が出る()
        {
            var wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));

            // 宿主の下に持ち物の View と拾わない層を置く（HoverTooltipTestStage と同じ組み方）
            // Put the inventory view and a non-picking layer under the host (same build as HoverTooltipTestStage)
            var pointerRoot = new VisualElement { name = "tooltip-pointer-root" };
            pointerRoot.StretchToParentSize();
            rootVisualElement.Add(pointerRoot);
            var view = new InventoryView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new RecordingInventorySortCommands(), content, Toaster);
            pointerRoot.Add(view);
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.StretchToParentSize();
            pointerRoot.Add(layer);
            var face = new TooltipFace("hover-tooltip", TooltipFaceLook.Hover);
            new HoverTooltipHost(face, new StageScaleSource(new ReactiveProperty<float>(1f))).Attach(pointerRoot, layer);

            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var main = Enumerable.Range(0, 36).Select(i => i == 0 ? new SlotItem(wood, 1) : empty).ToArray();
            view.SetShown(true);
            view.Render(new PlayerInventoryViewModel(main, new[] { empty }, 0, InventoryItemGuidTable.Build(main)));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            simulate.MouseMove(Find<VisualElement>(view, "main-slot-0").worldBound.center);
            simulate.FrameUpdate();

            // 期待は合成した木材の辞書名（マスタ GUID 由来のキー ContentLocalizationKeys.ItemName で引く）
            // Expect the merged dictionary name of wood (looked up by the GUID-derived key ContentLocalizationKeys.ItemName)
            Assert.AreEqual(WoodName, content.ItemName(wood).Resolve());
            StringAssert.EndsWith(WoodName, face.Q<Label>().text);
        }

        // 木材の名前を一時 mod の辞書へ合成
        // Compose the wood name into a temporary mod dictionary
        private void MergeWoodName()
        {
            var modDirectory = Path.Combine(_temporaryDirectory, "inventory-tooltip-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(
                Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"inventory-tooltip\",\"name\":\"inventory-tooltip\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(modDirectory, "localization", "localization.csv"),
                "key,Source,english,japanese\n" +
                $"{ContentLocalizationKeys.ItemName(WoodGuid).Key},{WoodName},{WoodName},テスト木材\n");
            Localize.MergeGameDictionaries(new ModsResource(_temporaryDirectory), new[] { new ModId("author:inventory-tooltip") }, MasterSourceTextCollector.Collect());
        }
    }
}
