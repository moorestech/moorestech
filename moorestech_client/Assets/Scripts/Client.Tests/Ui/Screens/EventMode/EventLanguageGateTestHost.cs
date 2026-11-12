using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Screens;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.EventMode;
using Client.Ui.Screens.EventMode.Parts;
using Client.Ui.State.EventMode;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.EventMode
{
    /// <summary>
    /// 言語ゲートの L1 の土台。本番の層を組み、舞台の倍率 1.5 でゲートを最前面の層へ載せる
    /// L1 base for the language gate; builds the production layers and mounts the gate on the frontmost layer at stage scale 1.5
    /// </summary>
    public abstract class EventLanguageGateTestHost : ScreenTestHost
    {
        // 2560×1080 の舞台の倍率。テストのパネルの倍率は 1 なので、実画面 px = パネル px × この値
        // Stage scale of 2560x1080; the test panel has scale 1, so real-screen px = panel px x this value
        protected const float StageScale = 1.5f;

        protected VisualElement GateLayer { get; private set; }

        [SetUp]
        public void BuildGateLayer()
        {
            GateLayer = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.FullScreenGate, UiFamily.Viewport);
        }

        protected static StageScaleSource Scale()
        {
            return new StageScaleSource(new ReactiveProperty<float>(StageScale));
        }

        protected EventLanguageGate OpenWaitingGate()
        {
            var gate = new EventLanguageGate(true);
            new EventLanguageGatePresenter(gate, GateLayer, Scale(), LanguageCatalog.Languages).Initialize();
            simulate.FrameUpdate();
            return gate;
        }

        protected EventLanguageGateView MountView(EventLanguageGate gate)
        {
            var view = new EventLanguageGateView(gate, Scale(), LanguageCatalog.Languages);
            GateLayer.Add(view);
            simulate.FrameUpdate();
            return view;
        }

        protected List<FormButton> Options()
        {
            return GateLayer.Query<FormButton>().ToList();
        }

        protected FormButton Option(string languageCode)
        {
            return GateLayer.Q<FormButton>(EventLanguageGateBody.OptionTestIdPrefix + languageCode);
        }

        // ラベルの字だけを読む。行の高さの印（I-fix5 の <line-height=…px>）を外すので、取り込みの前後で同じ比べ方になる
        // Reads only a label's words; the line-height tag (I-fix5's <line-height=...px>) is dropped, so the comparison holds before and after that merge
        protected static string ReadPlainText(Label label)
        {
            return Regex.Replace(label.text, "<[^>]+>", string.Empty);
        }
    }
}
