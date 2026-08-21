// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。参照元はuGUI側のみ（WebUIブリッジ経路は削除済み）（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained; only referenced from the uGUI side now (the Web UI bridge path was removed) (docs/webui/ugui-retirement-plan.md)
using TMPro;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.UI.KeyControl
{
    public class KeyControlDescription : MonoBehaviour
    {
        public static KeyControlDescription Instance { get; private set; }

        [SerializeField] private TMP_Text keyControlText;

        private void Awake()
        {
            Instance = this;
        }

        public void SetText(string text)
        {
            if (keyControlText != null)
            {
                keyControlText.text = text;
                keyControlText.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            }
        }
    }
}
