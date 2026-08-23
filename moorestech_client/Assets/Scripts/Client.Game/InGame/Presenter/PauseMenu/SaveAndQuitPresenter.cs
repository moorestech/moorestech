using System;
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
            SaveAndQuitAsync().Forget();
        }

        private async UniTask SaveAndQuitAsync()
        {
            Disconnect();
            // サーバー側ShutdownAsyncのセーブflush完了を待ってからプロセスを終える
            // Wait for the server-side ShutdownAsync save flush before ending the process
            await UniTask.Delay(TimeSpan.FromSeconds(2), true);
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Disconnect()
        {
            ClientContext.VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            ClientContext.VanillaApi.Disconnect();
            // Web UI と内蔵サーバーへゲーム終了を通知する。内蔵サーバーは保存を消化してから自壊する
            // Notify the Web UI and the embedded server; the server folds itself after flushing pending saves
            GameShutdownEvent.FireGameShutdown();
        }
    }
}
