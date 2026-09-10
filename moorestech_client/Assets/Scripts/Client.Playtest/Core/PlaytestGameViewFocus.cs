using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     Game Viewへ実際の入力フォーカスを取り戻す
    ///     Restores real input focus to the Game View
    /// </summary>
    public static class PlaytestGameViewFocus
    {
        // InputSystemの既定(PointersAndKeyboardsRespectGameViewFocus)はGame View非フォーカス時にキー注入を捨てるため、
        // Application.isFocusedがfalseなら別ウィンドウを経由して焦点遷移を起こし直す
        // The Input System default (PointersAndKeyboardsRespectGameViewFocus) drops injected keys while the Game View is unfocused,
        // so bounce focus through another window whenever Application.isFocused is false
        public static async UniTask Ensure()
        {
            if (Application.isFocused) return;

            var editorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var gameView = editorWindows.FirstOrDefault(window => window.GetType().Name == "GameView");
            if (gameView == null)
            {
                Debug.LogWarning("[Playtest] Game View window not found; injected input will be dropped");
                return;
            }

            // Game Viewが既にfocusedWindowでもランタイム側のフラグは立たないので、必ず一度他ウィンドウへ焦点を移す
            // The runtime flag stays down while the Game View already is focusedWindow, so always move focus away once
            var otherWindow = editorWindows.FirstOrDefault(window => window != gameView);
            if (otherWindow != null)
            {
                otherWindow.Focus();
                await UniTask.DelayFrame(5);
            }

            gameView.Focus();
            await UniTask.DelayFrame(10);

            if (!Application.isFocused) Debug.LogWarning("[Playtest] Game View did not take input focus; injected input will be dropped");
        }
    }
}
