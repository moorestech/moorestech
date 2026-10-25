using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkStage0Probe
{
    // S9の画面: 手順の説明、単行と複数行の入力欄、状態行、イベント履歴
    // The S9 screen: instructions, single- and multi-line fields, a state line and the event history
    public static class UitkImeProbeViews
    {
        public static VisualElement CreatePanel(bool cefRunning)
        {
            var panel = new VisualElement { name = "ime-panel" };
            panel.style.position = Position.Absolute;
            panel.style.left = 360;
            panel.style.top = 140;
            panel.style.width = 1200;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.backgroundColor = new Color(0.1f, 0.11f, 0.13f, 1f);
            panel.style.unityFontDefinition = ProbeAssets.JapaneseFontDefinition();

            panel.Add(CreateText($"UITK IME 確認（S9）  CEF: {(cefRunning ? "起動している" : "起動していない")}  F8 で表示切替", 30, Color.white));
            panel.Add(CreateText("手順: 単行と複数行それぞれで 変換→確定 / 変換中に取消(Esc・Backspace) / 確定後に Esc / 欄の外をクリックして戻る（フォーカス往復）", 20, new Color(0.8f, 0.8f, 0.8f)));

            panel.Add(CreateText("単行", 22, Color.white));
            var single = new TextField { name = "ime-single" };
            StyleField(single, 56);
            panel.Add(single);

            panel.Add(CreateText("複数行", 22, Color.white));
            var multi = new TextField { name = "ime-multi", multiline = true };
            StyleField(multi, 200);
            panel.Add(multi);

            var state = CreateText("", 20, new Color(1f, 0.85f, 0.3f));
            state.name = "ime-state";
            panel.Add(state);
            var events = CreateText("", 18, new Color(0.7f, 0.9f, 1f));
            events.name = "ime-events";
            events.style.minHeight = 240;
            panel.Add(events);
            return panel;
        }

        private static Label CreateText(string text, int fontSize, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 6;
            return label;
        }

        private static void StyleField(TextField field, float height)
        {
            field.style.height = height;
            field.style.fontSize = 28;
            field.style.marginTop = 4;
            field.style.marginBottom = 8;
        }
    }
}
