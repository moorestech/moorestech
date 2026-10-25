using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkStage0Probe
{
    // 前後判定用の色板と透かしラベルを組み立てる。位置は1920x1080基準のパネル座標
    // Builds the colored plates and watermark labels for order checks; positions are 1920x1080 panel coordinates
    public static class ProbeLayerViews
    {
        public static VisualElement CreateRoot()
        {
            var root = new VisualElement { name = "probe-root", pickingMode = PickingMode.Ignore };
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.style.unityFontDefinition = ProbeAssets.JapaneseFontDefinition();
            return root;
        }

        public static VisualElement CreatePlate(string name, Color color, float left, float top, float width, float height, string caption)
        {
            // 板は当たり判定を持たせない（S3の判定対象はボタンだけにする）
            // Plates carry no hit area, so S3 hits only the probe button
            var plate = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            plate.style.position = Position.Absolute;
            plate.style.left = left;
            plate.style.top = top;
            plate.style.width = width;
            plate.style.height = height;
            plate.style.backgroundColor = color;
            var label = new Label(caption) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 28;
            label.style.color = Color.black;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            plate.Add(label);
            return plate;
        }

        public static Label CreateStamp(string name, float left, float top, Color background)
        {
            var stamp = new Label { name = name, pickingMode = PickingMode.Ignore };
            stamp.style.position = Position.Absolute;
            stamp.style.left = left;
            stamp.style.top = top;
            stamp.style.fontSize = 30;
            stamp.style.color = Color.white;
            stamp.style.backgroundColor = background;
            stamp.style.paddingLeft = 8;
            stamp.style.paddingRight = 8;
            return stamp;
        }

        public static Button CreateProbeButton(float left, float top)
        {
            var button = new Button { name = "uitk-probe-button", text = "S3 PROBE" };
            button.style.position = Position.Absolute;
            button.style.left = left;
            button.style.top = top;
            button.style.width = 300;
            button.style.height = 120;
            button.style.fontSize = 30;
            button.style.backgroundColor = new Color(1f, 0.55f, 0f);
            button.style.color = Color.black;
            button.focusable = false;
            return button;
        }
    }
}
