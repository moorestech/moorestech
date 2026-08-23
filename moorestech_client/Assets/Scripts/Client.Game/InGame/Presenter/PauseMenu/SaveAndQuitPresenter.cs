using System;
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

            // 最終セーブの要求・書き出し完了待ち・アプリ終了は終了パイプラインが一括で担う
            // The shutdown pipeline owns the final save request, the flush wait, and the app exit
            GameShutdownEvent.QuitApplicationAsync().Forget(LogQuitFailure);
        }

        // 通信の切断は終了経路と破棄経路の双方から来る。終了通知は既発火なら無視される
        // Teardown reaches here from both the exit and destroy paths; an already-fired shutdown notice is ignored
        private void Disconnect()
        {
            ClientContext.VanillaApi.Disconnect();
            GameShutdownEvent.FireGameShutdown();
        }

        private void LogQuitFailure(Exception exception)
        {
            Debug.LogError($"セーブして終了に失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
