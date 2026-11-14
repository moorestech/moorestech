using System.Collections.Generic;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.EventMode;
using Client.Ui.State.EventMode;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Starter.EventMode
{
    /// <summary>
    /// ゲームの DI より前に言語ゲートを最前面の層へ出す宿主。倍率は DI 前の GameUi の土台（GameUiPreDiFoundation）が立てた 1 本を受ける
    /// Host that shows the language gate on the frontmost layer before the game DI; the scale is the single one the pre-DI GameUi foundation started
    /// </summary>
    /// <remarks>
    /// 層のパネルは土台の Initialize（StageScaleWatch.Start）が、言語の一覧は生成時のビルドが保証するので確かめない。入力規則と Pick の probe は付けない（DI 前は世界の入力が無く、ボタンはフォーカスを持たない）
    /// The layer's panel is guaranteed by the foundation's Initialize (StageScaleWatch.Start) and the language list by the generator's build, so neither is checked; no input rules or Pick probe are attached (no world input before DI and the buttons never take focus)
    /// </remarks>
    public sealed class EventLanguageGateBootHost : IEventLanguageGateScreen
    {
        private readonly VisualElement _layer;
        private readonly StageScaleSource _stageScale;
        private readonly IReadOnlyList<LanguageInfo> _languages;

        public EventLanguageGateBootHost(VisualElement layer, StageScaleSource stageScale, IReadOnlyList<LanguageInfo> languages)
        {
            _layer = layer;
            _stageScale = stageScale;
            _languages = languages;
        }

        public void Open(EventLanguageGate gate)
        {
            new EventLanguageGatePresenter(gate, _layer, _stageScale, _languages).Initialize();
        }
    }
}
