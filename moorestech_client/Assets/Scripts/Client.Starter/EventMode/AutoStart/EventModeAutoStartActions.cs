using Game.Paths;
using UnityEngine;

namespace Client.Starter.EventMode
{
    internal sealed class EventModeAutoStartActions : IEventModeAutoStartActions
    {
        public void StartFreshWorld()
        {
            // 開始拒否でワールドだけが消える状態を防ぐため、ゲート通過が確定してから削除する
            // Wipe only after the gates pass so a refused start never leaves the world deleted without a game starting
            GameSystemPaths.DeleteDefaultWorldDirectory();
            LocalGameLauncher.StartLocalGame();
        }

        public void QuitBecauseTitleGatesTimedOut(float deadlineSeconds)
        {
            // 期限切れでメニューに残ると前の来場者のワールドを再開できブースも止まるため、ワールドを残して終了し出展ループに再起動を委ねる
            // Quit without wiping on expiry so staying on the menu cannot expose the previous visitor's world or stall the booth; the exhibition loop restarts the game
            Debug.LogError($"EventModeAutoStart: title gates did not pass within {deadlineSeconds}s (the title composition root may not have started the sequence); quitting without wiping the world so the loop script restarts the game");
            Application.Quit();
        }
    }
}
