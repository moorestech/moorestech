// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using TMPro;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.UI.KeyControl
{
    public class KeyControlDescription : MonoBehaviour
    {
        public static KeyControlDescription Instance { get; private set; }

        [SerializeField] private TMP_Text keyControlText;
        private string _defaultText = "";

        private void Awake()
        {
            Instance = this;
        }

        public void SetText(string text)
        {
            _defaultText = text;
            RefreshText();
        }

        private void RefreshText()
        {
            var text = _defaultText;
            if (keyControlText != null)
            {
                keyControlText.text = text;
                keyControlText.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            }
        }
    }
}
