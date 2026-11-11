// S/Client.Ui/Screens/Skit/Parts/Window/SkitDialogueWindow.cs
using System;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Window
{
    /// <summary>
    /// 画面下の全幅の会話窓。話者 → FadeRule → 本文 → 送りの印（Web SkitPresentation の GamePanel skit と section.window）
    /// The full-width dialogue window at the screen bottom: speaker, FadeRule, body, advance marker (Web SkitPresentation GamePanel skit and section.window)
    /// </summary>
    public sealed class SkitDialogueWindow : VisualElement
    {
        private readonly GamePanelSurface _surface;
        private readonly VisualElement _section;
        private readonly LocalizedLabel _speaker;
        private readonly LocalizedLabel _body;
        private readonly SkitAdvanceMarker _advanceMarker;
        private readonly Subject<Unit> _onTextClicked = new();

        public IObservable<Unit> OnTextClicked => _onTextClicked;

        public SkitDialogueWindow(IUiOsCursor osCursor)
        {
            // 出し入れは自分の display で行う。L1 の IsShown は要素自身の display を見るので、名前もここに付ける
            // Showing and hiding uses this element's display; L1 IsShown reads the element's own display, so the name goes here too
            name = "skit-window";
            AddToClassList("skit-window");
            pickingMode = PickingMode.Ignore;

            // 面と本文の器は拾わず、クリックは文の区画だけが受ける
            // The face and body container never pick; only the text section takes clicks
            _surface = new GamePanelSurface("skit-panel", GamePanelSurfaceKind.Skit);
            _surface.pickingMode = PickingMode.Ignore;
            _surface.contentContainer.pickingMode = PickingMode.Ignore;
            Add(_surface);
            _section = new VisualElement { name = "blocking-skit" };
            _section.AddToClassList("skit-window__section");
            _section.RegisterCallback<ClickEvent>(_ => _onTextClicked.OnNext(Unit.Default));
            _surface.Add(_section);

            // 押せる印の指は文の区画で要求する（Web .window の cursor: pointer。ポーズで Ignore になると PointerLeave で外れる）
            // The pointer finger is requested on the text section (Web .window cursor: pointer; turning Ignore on pause withdraws it via PointerLeave)
            new UiOsCursorHoverRequest(_section, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);

            // 話者・罫線・本文・送りの印を Web と同じ順に積む
            // Stack speaker, rule, body and advance marker in the Web order
            _speaker = CreateText("skit-speaker", "skit-window__speaker");
            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.AddToClassList("skit-window__rule");
            rule.Add(new FadeRule());
            _body = CreateText("skit-body", "skit-window__body");
            _advanceMarker = new SkitAdvanceMarker();
            _section.Add(_speaker);
            _section.Add(rule);
            _section.Add(_body);
            _section.Add(_advanceMarker);
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetSpeaker(string speaker)
        {
            _speaker.SetText(UiText.FromVerbatim(SkitRichText.Literal(speaker)));
        }

        public void SetBody(string body, int visibleCodePoints)
        {
            _body.SetText(UiText.FromVerbatim(SkitRichText.TypewriterBody(body, visibleCodePoints)));
        }

        public void SetAdvanceMarkerShown(bool shown)
        {
            _advanceMarker.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ポーズ中は文の区画も拾わず、クリックは背後の何にも当たらない（Web の .inputBlocked）
        // While paused the text section stops picking too, so a click hits nothing behind (Web .inputBlocked)
        public void SetInteractive(bool interactive)
        {
            _section.pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
        }

        private static LocalizedLabel CreateText(string testid, string className)
        {
            var label = new LocalizedLabel { name = testid, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            label.SetText(UiText.FromVerbatim(string.Empty));
            return label;
        }
    }
}
