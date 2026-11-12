using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Client.Ui.Editor.Capture;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    /// Game View の解像度をシナリオから切替
    /// Switches the Game View resolution from a scenario
    /// </summary>
    public static class PlaytestGameViewOps
    {
        private const float ResolutionApplyTimeoutSeconds = 5f;

        /// <summary>
        /// 裏タブの Game View を表へ出す。裏だと描画されず Recorder が timeScale=0 に固定する
        /// Brings a Game View hidden behind another tab to front; a hidden one never renders and the Recorder pins timeScale=0
        /// </summary>
        public static void BringToFront()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var gameView = System.Array.Find(Resources.FindObjectsOfTypeAll<EditorWindow>(), w => w.GetType().Name == "GameView");
            if (gameView == null)
            {
                throw new InvalidOperationException("[PlaytestGameViewOps] GameView window not found; cannot bring it to front");
            }

            var dockArea = typeof(EditorWindow).GetField("m_Parent", flags)?.GetValue(gameView);
            if (dockArea == null)
            {
                throw new InvalidOperationException("[PlaytestGameViewOps] GameView is not docked (EditorWindow.m_Parent is null); cannot select its tab");
            }

            var panesField = dockArea.GetType().GetField("m_Panes", flags);
            if (panesField == null)
            {
                throw new InvalidOperationException("[PlaytestGameViewOps] DockArea.m_Panes not found; Unity internals changed, cannot select the GameView tab");
            }
            var selectedProperty = dockArea.GetType().GetProperty("selected", flags);
            if (selectedProperty == null)
            {
                throw new InvalidOperationException("[PlaytestGameViewOps] DockArea.selected not found; Unity internals changed, cannot select the GameView tab");
            }

            var panes = (IList)panesField.GetValue(dockArea);
            selectedProperty.SetValue(dockArea, panes.IndexOf(gameView));
        }

        public static async UniTask SetResolution(CancellationToken cancellationToken, int width, int height)
        {
            var selected = GameViewResolution.Set(width, height);
            Debug.Log($"[PlaytestGameViewOps] Game View {width}x{height}: {selected}");

            // 画面の寸法は次の描画から変わるので、Screen が追いつくまでフレームで待つ
            // The screen size changes from the next render, so wait per frame until Screen catches up
            var startTime = Time.realtimeSinceStartup;
            while (Screen.width != width || Screen.height != height)
            {
                if (ResolutionApplyTimeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"Game View did not reach {width}x{height} within {ResolutionApplyTimeoutSeconds}s (Screen {Screen.width}x{Screen.height})");
                }
                await UniTask.Yield(cancellationToken);
            }
        }
    }
}
