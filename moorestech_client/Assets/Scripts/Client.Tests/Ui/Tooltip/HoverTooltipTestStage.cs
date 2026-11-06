using Client.Ui.Components.Text;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Tooltip
{
    /// <summary>
    /// ホバーの宿主の L1 が使う舞台（印の付いた一覧・無関係なパネル・印の無い場所・最前の層と宿主）を組む
    /// Builds the stage for the hover host L1 tests (a marked list, an unrelated panel, an unmarked area, a front layer and the host)
    /// </summary>
    public sealed class HoverTooltipTestStage
    {
        private const string ShownClass = "hover-tooltip-host--shown";

        public ScrollView List { get; }
        public VisualElement Wood { get; }
        public ScrollView OtherPanel { get; }
        public VisualElement Plain { get; }
        public VisualElement Anchor { get; }
        public TooltipFace Face { get; }
        public ReactiveProperty<float> StageScale { get; }

        public HoverTooltipTestStage(VisualElement testRoot)
        {
            // 宿主の移動の受け口は testRoot（パネルの木の根そのもの）でなく全面の箱にする。根に登録した事象はテスト間の Clear で外れず、前のテストの宿主が次のテストの移動を受けて落ちるため
            // Use a full-size box, not testRoot (the panel tree root itself), as the host's pointer root; root callbacks survive the per-test Clear and an earlier host would receive the next test's moves
            var pointerRoot = new VisualElement { name = "tooltip-pointer-root" };
            FillParent(pointerRoot);
            testRoot.Add(pointerRoot);

            // 左に印の付いたセルの一覧、右に無関係なパネル、下に印の無い場所
            // A list of marked cells on the left, an unrelated panel on the right, an unmarked area below
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            List = Scroll("tooltip-list");
            Wood = Cell("item-wood");
            HoverTooltipMark.Mark(Wood, UiText.FromVerbatim("Wood"));
            List.Add(Wood);
            for (var i = 0; i < 20; i++) List.Add(Cell($"item-{i}"));
            OtherPanel = Scroll("tooltip-other-panel");
            for (var i = 0; i < 20; i++) OtherPanel.Add(Cell($"other-{i}"));
            row.Add(List);
            row.Add(OtherPanel);
            Plain = new VisualElement { name = "tooltip-plain" };
            Plain.style.height = 100;
            pointerRoot.Add(row);
            pointerRoot.Add(Plain);

            // 宿主は最前の全面の層に置く。テストのパネルは倍率 1 なので、舞台の倍率は StageScaleSource へ外から差し込む
            // The host sits on a front full-size layer; the test panel has scale 1, so the stage scale is injected into StageScaleSource
            var layer = new VisualElement { name = "tooltip-layer", pickingMode = PickingMode.Ignore };
            FillParent(layer);
            pointerRoot.Add(layer);
            StageScale = new ReactiveProperty<float>(1f);
            Face = new TooltipFace("hover-tooltip", TooltipFaceLook.Hover);
            new HoverTooltipHost(Face, new StageScaleSource(StageScale)).Attach(pointerRoot, layer);
            Anchor = layer.Q("hover-tooltip-host");
        }

        public bool IsShown()
        {
            return Anchor.ClassListContains(ShownClass);
        }

        private static void FillParent(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
        }

        private static ScrollView Scroll(string scrollName)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = scrollName };
            scroll.style.width = 200;
            scroll.style.height = 200;
            return scroll;
        }

        private static VisualElement Cell(string cellName)
        {
            var cell = new VisualElement { name = cellName };
            cell.style.height = 48;
            cell.style.flexShrink = 0;
            return cell;
        }
    }
}
