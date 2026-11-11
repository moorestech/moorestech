using Client.Tests.Ui.Screens.Recipe.ItemList;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.Recipe;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Overlay
{
    /// <summary>
    /// ビューアの素材スロットと一覧のセルのツールチップを、本番と同じホバー宿主の下で確かめる（L1。組み方は HoverTooltipTestStage と同じ）
    /// Verifies tooltips of viewer material slots and list cells under the production hover host (L1; built like HoverTooltipTestStage)
    /// </summary>
    public class RecipeSlotTooltipTest : ItemListTestHost
    {
        private const string ShownClass = "hover-tooltip-host--shown";
        private TooltipFace _face;
        private VisualElement _anchor;
        private RecipeViewerView _viewer;
        private ItemListView _list;

        [SetUp]
        public void MountStage()
        {
            // 宿主の受け口は全面の箱（根に登録するとテスト間で外れない）。左にビューア・右に一覧、最前に拾わない層
            // The host's pointer root is a full-size box (root callbacks survive between tests); viewer left, list right, a non-picking layer in front
            var pointerRoot = new VisualElement { name = "tooltip-pointer-root" };
            pointerRoot.StretchToParentSize();
            rootVisualElement.Add(pointerRoot);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _viewer = new RecipeViewerView(Context(Toaster, new RecordingRecipeCommands(), Selection));
            _list = new ItemListView(ContentPresentation, Selection);
            row.Add(_viewer);
            row.Add(_list);
            pointerRoot.Add(row);
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.StretchToParentSize();
            pointerRoot.Add(layer);
            _face = new TooltipFace("hover-tooltip", TooltipFaceLook.Hover);
            new HoverTooltipHost(_face, new StageScaleSource(new ReactiveProperty<float>(1f))).Attach(pointerRoot, layer);
            _anchor = layer.Q("hover-tooltip-host");

            _viewer.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, true, Material(1, 2, 1))));
            _list.Render(ListOf(MaxItemId));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/recipe/slotTooltip.spec.ts">ツールチップはMantine既定の白い角丸ではなくCursorTooltipと同じ面を使う</web-test>
        [Test]
        public void 素材スロットのツールチップはHoverの面で出る()
        {
            var slot = Find<VisualElement>(_viewer, $"craft-recipe-box-{RecipeFixtures.RecipeGuid(1)}-material-0");
            Hover(slot.worldBound.center);
            Assert.IsTrue(IsTooltipShown());
            Assert.IsTrue(_face.ClassListContains("tooltip-face--hover"));
            Assert.IsFalse(string.IsNullOrEmpty(_face.Q<Label>().text));

            // 面・角丸は共有トークン由来（Mantine 既定の白い角丸にならない）
            // Face and radius come from the shared tokens (never Mantine's default white rounded box)
            Assert.AreEqual(0f, _face.resolvedStyle.borderTopLeftRadius, 0.01f);
            Assert.AreEqual(0f, _face.resolvedStyle.borderBottomRightRadius, 0.01f);
            var face = _face.resolvedStyle.backgroundColor;
            Assert.AreEqual(16f / 255f, face.r, 0.01f);
            Assert.AreEqual(20f / 255f, face.g, 0.01f);
            Assert.AreEqual(28f / 255f, face.b, 0.01f);
            Assert.AreEqual(0.94f, face.a, 0.01f);
        }

        /// <web-test file="e2e/tests/recipe/slotTooltip.spec.ts">祖先のスクロールでツールチップを引っ込め、ポインタを動かせば開き直す</web-test>
        [Test]
        public void 祖先のスクロールで引っ込みポインタを動かすと開き直す()
        {
            var point = Find<VisualElement>(_list, "item-list-cell-1").worldBound.center;
            Hover(point);
            Assert.IsTrue(IsTooltipShown());

            // 一覧が動いたら引っ込む（内容と一緒に滑らせない）
            // A list move retracts it instead of letting it slide with the content
            var scroll = _list.Q<GameScrollView>("item-list-scroll");
            scroll.SetScrollOffset(40f);
            simulate.FrameUpdate();
            Assert.IsFalse(IsTooltipShown());

            // 先頭へ戻して同じセルの上でポインタを動かせば開き直す
            // Back at the top, moving the pointer over the same cell reopens it
            scroll.SetScrollOffset(0f);
            simulate.FrameUpdate();
            Hover(point + new Vector2(5f, 5f));
            Assert.IsTrue(IsTooltipShown());
        }

        private bool IsTooltipShown()
        {
            return _anchor.ClassListContains(ShownClass);
        }

        private void Hover(Vector2 point)
        {
            simulate.MouseMove(point);
            simulate.FrameUpdate();
        }
    }
}
