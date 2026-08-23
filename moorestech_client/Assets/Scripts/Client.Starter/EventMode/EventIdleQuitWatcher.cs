using System;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Client.Starter.EventMode
{
    // 一定時間無入力でアプリ終了（再起動は外部スクリプト）
    // Quit after input silence; the external script restarts it
    public class EventIdleQuitWatcher : MonoBehaviour
    {
        private float _idleSeconds;
        private int _idleTimeoutSeconds;

        // タイムアウト値の無い個体を作らせないため、生成はこの口だけに絞る
        // The only creation entry point, so no instance can exist without its timeout
        public static EventIdleQuitWatcher Create(int idleTimeoutSeconds)
        {
            var watcherObject = new GameObject(nameof(EventIdleQuitWatcher));
            DontDestroyOnLoad(watcherObject);
            var watcher = watcherObject.AddComponent<EventIdleQuitWatcher>();
            watcher._idleTimeoutSeconds = idleTimeoutSeconds;
            return watcher;
        }

        private void Start()
        {
            // 起動所要時間を無操作時間に数えない。ロード完了時点から計り直す
            // Boot time must not count as idle time, so restart the measurement when loading completes
            GameInitializedEvent.OnGameInitialized.Subscribe(_ => _idleSeconds = 0f).AddTo(this);
        }

        private void Update()
        {
            if (HasAnyInput())
            {
                _idleSeconds = 0f;
                return;
            }

            _idleSeconds += Time.unscaledDeltaTime;
            if (_idleSeconds < _idleTimeoutSeconds) return;

            // 以後は毎フレームQuitを呼び続けない（Editorではno-opのためPlayModeが終わらなくなる）
            // Stop re-triggering every frame (Application.Quit is a no-op in the Editor, which would hang PlayMode)
            enabled = false;
            GameShutdownEvent.QuitApplicationAsync().Forget(LogQuitFailure);

            #region Internal

            bool HasAnyInput()
            {
                var keyboard = Keyboard.current;
                // 押しっぱなしと、1フレーム内で完結した押下離しの両方を拾う
                // Catches both a held key and a press that started and ended inside one frame
                if (keyboard != null && (keyboard.anyKey.isPressed || keyboard.anyKey.wasPressedThisFrame || keyboard.anyKey.wasReleasedThisFrame)) return true;

                var mouse = Mouse.current;
                if (mouse == null) return false;
                if (IsButtonActive(mouse.leftButton) || IsButtonActive(mouse.rightButton) || IsButtonActive(mouse.middleButton)) return true;

                // ロック中はpositionが凍結するためdeltaとscrollで移動・ホイールを検知する
                // Position freezes while locked, so use delta and scroll to detect motion and wheel input
                if (0f < mouse.delta.ReadValue().sqrMagnitude) return true;
                return mouse.scroll.ReadValue() != Vector2.zero;
            }

            bool IsButtonActive(ButtonControl button)
            {
                return button.isPressed || button.wasPressedThisFrame || button.wasReleasedThisFrame;
            }

            #endregion
        }

        private void LogQuitFailure(Exception exception)
        {
            Debug.LogError($"無操作終了に失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
