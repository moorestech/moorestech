using System;
using Client.Ui.Foundation.Layers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Root
{
    /// <summary>
    /// UITK の UIDocument 1 枚を持ち、トークン USS を当てて層スタックを組む
    /// Owns the single UITK UIDocument, applies the token style sheets and builds the layer stack
    /// </summary>
    public class GameUiRoot : MonoBehaviour
    {
        private static GameUiRoot _instance;
        public static GameUiRoot Instance => _instance;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private StyleSheet[] tokenStyleSheets;
        [SerializeField] private RawImage cefRawImage;

        private UiLayerStack _layers;

        private void Awake()
        {
            _instance = this;
            // 必須参照が欠けたら起動の段階で止める
            // Stop at startup when a required reference is missing
            if (uiDocument == null || cefRawImage == null || tokenStyleSheets == null || tokenStyleSheets.Length == 0) throw new InvalidOperationException("[GameUiRoot] required references are not assigned on MainGameUI.prefab");
            var root = uiDocument.rootVisualElement;
            foreach (var sheet in tokenStyleSheets) root.styleSheets.Add(sheet);
            // 後続で UiAssetCatalog ができたらトークンの当て方もそこへ寄せる
            // Once UiAssetCatalog lands, route the token sheets through it as well
            _layers = UiLayerStack.Build(root);
        }

        public VisualElement GetLayer(UiLayer layer)
        {
            return _layers.GetLayer(layer);
        }

        // R1 の全面オフで CEF の絵を隠す
        // Hide the CEF picture during the R1 full-off
        public void SetCefVisible(bool visible)
        {
            cefRawImage.enabled = visible;
        }
    }
}
