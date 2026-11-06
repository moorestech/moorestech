using System;
using Client.Ui.Foundation.Layers;
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

        // ルート共通の入力規則とポインタ判定を付ける先
        // Where the root-wide input rules and the pointer probe attach
        public VisualElement GetRootElement()
        {
            return uiDocument.rootVisualElement;
        }

        public VisualElement GetLayer(UiLayer layer)
        {
            return _layers.GetLayer(layer);
        }

        // ロケールなど、パネル全体に効くクラスをルートへ付け外しする
        // Toggles panel-wide classes such as the locale on the root
        public void SetRootClassEnabled(string className, bool enabled)
        {
            uiDocument.rootVisualElement.EnableInClassList(className, enabled);
        }
    }
}
