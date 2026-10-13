using Game.Paths;
using UnityEngine;

namespace Client.Starter.EventMode
{
    internal sealed class EventModeAutoStartActions : IEventModeAutoStartActions
    {
        public void StartFreshWorld()
        {
            // 開始が確定してからワールドを消して既存の開始入口を呼ぶ
            // Wipe only after the start is settled, then use the existing launch entry
            GameSystemPaths.DeleteDefaultWorldDirectory();
            LocalGameLauncher.StartLocalGame();
        }

        public void QuitBecauseTitleGatesTimedOut(float deadlineSeconds)
        {
            // 期限切れはワールドを残し、理由を記録して出展ループへ終了を返す
            // Preserve the world on expiry, log the reason and quit for the exhibition loop
            Debug.LogError($"EventModeAutoStart: title gates did not pass within {deadlineSeconds}s (the title composition root may not have started the sequence); quitting without wiping the world so the loop script restarts the game");
            Application.Quit();
        }
    }
}
