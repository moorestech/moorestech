using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class WebUiCefToggle : MonoBehaviour
    {
        [SerializeField] private GameObject cefUnityRoot;

        private readonly List<GameObject> _uguiRoots = new();
        private bool _isCefActive;

        private void Awake()
        {
            // CefUnity以外の直下の子を全てuGUIルートとして収集する
            // Collect every direct child except CefUnity as an uGUI root
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child != cefUnityRoot) _uguiRoots.Add(child);
            }

            ApplyState();
        }

        private void Update()
        {
            // Ctrl+IでWeb UI(CEF)とuGUIの表示を排他的に切り替える
            // Toggle between Web UI (CEF) and uGUI display exclusively with Ctrl+I
            // TODO InputManagerに移動
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl) && UnityEngine.Input.GetKeyDown(KeyCode.I))
            {
                _isCefActive = !_isCefActive;
                ApplyState();
            }
        }

        private void ApplyState()
        {
            cefUnityRoot.SetActive(_isCefActive);
            foreach (var root in _uguiRoots) root.SetActive(!_isCefActive);
        }
    }
}
