using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.EventMode.Parts;
using Client.Ui.State.Commands;
using Client.Ui.State.EventMode;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.EventMode
{
    /// <summary>
    /// 出展モードの全画面の言語ゲート（Web EventLanguageGate の写し）。面は画面いっぱい、中身は倍率を掛けない実画面 px（A-41）
    /// The exhibition full-screen language gate (mirrors Web EventLanguageGate); the face fills the screen and the contents sit in unscaled real-screen px (A-41)
    /// </summary>
    /// <remarks>
    /// 押下は View がゲートの選択へ直接渡し、拒否が返ればログに残す（設計書 A-8。前例 Screens/Settings/LanguageSelectView）。Presenter は載せる・外すだけ
    /// The view hands presses straight to the gate's selection and logs any rejection (design A-8; precedent Screens/Settings/LanguageSelectView); the presenter only mounts and removes
    /// </remarks>
    public sealed class EventLanguageGateView : VisualElement
    {
        public const string RootTestId = "event-language-gate";
        public const string TitleTestId = "event-language-gate-title";
        // ログのコマンド名は Web の action 名と同じ（UiCommandResultToaster の書式に揃える）
        // The logged command name is the Web action name (matching UiCommandResultToaster's format)
        private const string SelectLanguageCommandName = "event_mode.select_language";

        private readonly EventLanguageGate _gate;

        public EventLanguageGateView(EventLanguageGate gate, StageScaleSource stageScale, IReadOnlyList<LanguageInfo> languages)
        {
            _gate = gate;
            name = RootTestId;
            AddToClassList("event-language-gate");
            // 不透明の面が押下を受け、下へ通さない（Web Overlay と同じ）
            // The opaque face takes presses and lets none through (same as Web Overlay)
            pickingMode = PickingMode.Position;

            // Web は Portal の中身に --ui-scale を掛けないので、倍率を打ち消す箱に置く
            // The Web never applies --ui-scale inside the Portal, so the contents go in the scale-cancelling box
            var scope = new UiScreenPixelScope("event-language-gate-scope", stageScale);
            var overlay = new VisualElement { name = "event-language-gate-overlay", pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("event-language-gate__overlay");
            scope.Add(overlay);
            Add(scope);

            // 見出しと本体を中央で縦積み
            // Stack heading and body, centred
            var column = new UiStack(UiGap.Xl) { pickingMode = PickingMode.Ignore };
            column.AddToClassList(UiLayoutClass.StackAlignCenter);
            column.AddItem(CreateTitle());
            var body = new EventLanguageGateBody(languages);
            body.OnLanguageChosen.Subscribe(Choose);
            column.AddItem(body);
            overlay.Add(column);

            #region Internal

            LocalizedLabel CreateTitle()
            {
                var title = new LocalizedLabel { name = TitleTestId, pickingMode = PickingMode.Ignore };
                title.AddToClassList(UiTypographyClass.TitleOrder1);
                title.AddToClassList(UiTypographyClass.TextWhite);
                title.AddToClassList(UiTypographyClass.TextCenter);
                title.AddToClassList("event-language-gate__title");
                title.SetText(UiText.FromVerbatim(EventLanguageGateText.Title));
                return title;
            }

            void Choose(string languageCode)
            {
                // 受理なら Presenter が外す
                // On acceptance the presenter removes it
                var result = _gate.SelectLanguage(languageCode);

                // 本番の押下で拒否は起きない（D-6）が、返れば言語は変わっていないので黙らずに残す。DI 前で Toaster が無いので同じ書式でここに出す
                // Real presses never get rejected (D-6), but a rejection leaves the language unchanged, so log it; no toaster exists before DI, so use its format here
                if (result is UiCommandRejected rejected) Debug.LogWarning($"[EventLanguageGate] {SelectLanguageCommandName} failed: {rejected.ToLogText()}");
            }

            #endregion
        }
    }
}
