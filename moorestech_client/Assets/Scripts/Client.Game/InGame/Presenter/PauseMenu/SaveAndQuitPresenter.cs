using System.Threading;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    // ゲーム内の正規終了経路。メインメニューへは戻らずセーブ後にアプリを終了する（AGENTS.md既知の制約に整合）
    // Canonical in-game exit path: saves and quits the app without returning to the main menu
    public class SaveAndQuitPresenter : MonoBehaviour
    {
        private bool _quitRequested;

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        public void SaveAndQuit()
        {
            // 連打時は2本目以降を無視
            // Ignore repeated presses after the first
            if (_quitRequested) return;
            _quitRequested = true;
            SaveAndQuitAsync().Forget();

            #region Internal

            async UniTask SaveAndQuitAsync()
            {
                // 最終セーブの要求と書き出し完了待ちを終了イベントに任せ、完了後に切断してから終了する
                // Delegate the final save request and its flush wait to the shutdown event, then disconnect and exit
                await GameShutdownEvent.FireGameShutdownAsync();
                Disconnect();
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            #endregion
        }

        private void Disconnect()
        {
            ClientContext.VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            ClientContext.VanillaApi.Disconnect();
            // 終了通知（サーバーは保存後自壊）
            // Notify shutdown (server self-destructs after saving)
            GameShutdownEvent.FireGameShutdown();
        }
    }
}
