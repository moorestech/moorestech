using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// CEF描画ルートを常時有効化する。uGUI廃止Phase1でCtrl+Iトグル・デバッグスイッチによるuGUIフォールバックを撤去した。
    /// Keeps the CEF render root always active; uGUI-retirement Phase1 removed the Ctrl+I toggle and debug-switch uGUI fallback.
    /// </summary>
    public class WebUiCefToggle : MonoBehaviour
    {
        [SerializeField] private GameObject cefUnityRoot;

        private void Start()
        {
            // MouseCursorTooltip等、他コンポーネントのAwake完了後に適用するためStartで実行する
            // Apply in Start (not Awake) so other components (e.g. MouseCursorTooltip) finish Awake first
            cefUnityRoot.SetActive(true);
        }
    }
}
