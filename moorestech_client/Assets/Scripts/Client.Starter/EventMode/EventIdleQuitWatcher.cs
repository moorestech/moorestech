using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Starter.EventMode
{
    // イベント出展モード: 一定時間無入力ならアプリを終了する（ループスクリプトが新規ワールドで再起動する）
    // Event exhibition mode: quit after sustained input silence (the loop script relaunches with a fresh world)
    public class EventIdleQuitWatcher : MonoBehaviour
    {
        private float _idleSeconds;
        private Vector2 _lastMousePosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void CreateIfEventMode()
        {
            // イベントモード限定の常駐監視オブジェクトを起動時に生成する（シーンには置かない）
            // Spawn the event-mode-only resident watcher at boot instead of placing it in scenes
            if (!EventExhibitionMode.IsEnabled) return;
            var watcherObject = new GameObject(nameof(EventIdleQuitWatcher));
            DontDestroyOnLoad(watcherObject);
            watcherObject.AddComponent<EventIdleQuitWatcher>();
        }

        private void Update()
        {
            if (HasAnyInput())
            {
                _idleSeconds = 0f;
                return;
            }

            _idleSeconds += Time.unscaledDeltaTime;
            if (_idleSeconds < EventExhibitionMode.IdleTimeoutSeconds) return;

            // ワールドは次回起動時に削除されるためセーブ完了を待たず即終了してよい
            // The world is deleted on next boot, so quitting without waiting for a save flush is fine
            Application.Quit();
        }

        private bool HasAnyInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed) return true;

            var mouse = Mouse.current;
            if (mouse == null) return false;
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed) return true;

            // deltaはフレーム跨ぎで欠落しうるため位置比較で移動を検知する
            // Detect motion by comparing positions since delta can be missed across frames
            var position = mouse.position.ReadValue();
            var moved = (position - _lastMousePosition).sqrMagnitude > 0.01f;
            _lastMousePosition = position;
            return moved;
        }
    }
}
