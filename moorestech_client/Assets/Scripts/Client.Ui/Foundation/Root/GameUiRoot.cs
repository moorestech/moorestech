using System;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Root
{
    /// <summary>
    /// UITK の UIDocument 1 枚を持ち、層スタックを組む（トークンはテーマ TSS から当たる）
    /// Owns the single UITK UIDocument, builds the layer stack (tokens come from the theme TSS)
    /// </summary>
    public class GameUiRoot : MonoBehaviour
    {
        private static GameUiRoot _instance;
        public static GameUiRoot Instance => _instance;

        [SerializeField] private UIDocument uiDocument;

        private UiLayerStack _layers;

        private void Awake()
        {
            _instance = this;
            // 必須参照が欠けたら起動の段階で止める
            // Stop at startup when a required reference is missing
            if (uiDocument == null) throw new InvalidOperationException("[GameUiRoot] required references are not assigned on MainGameUI.prefab");
            var root = uiDocument.rootVisualElement;
            _layers = UiLayerStack.Build(root);
        }

        // パネルの描き先を差し替える口（sRGB 合成の RT。A-180）
        // Where the panel's render target is swapped (the sRGB composite RT, A-180)
        public UIDocument GetPanelDocument()
        {
            return uiDocument;
        }

        // ルート共通の入力規則とポインタ判定を付ける先
        // Where the root-wide input rules and the pointer probe attach
        public VisualElement GetRootElement()
        {
            return uiDocument.rootVisualElement;
        }

        // 画面は層と族（舞台か実画面）を選んで載る（設計書 C6）
        // Screens mount by choosing a layer and a family, stage or viewport (design C6)
        public VisualElement GetLayer(UiLayer layer, UiFamily family)
        {
            return _layers.GetLayer(layer, family);
        }

        // ロケールなど、パネル全体に効くクラスをルートへ付け外しする
        // Toggles panel-wide classes such as the locale on the root
        public void SetRootClassEnabled(string className, bool enabled)
        {
            uiDocument.rootVisualElement.EnableInClassList(className, enabled);
        }
    }
}
