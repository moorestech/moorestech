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
        private EventIdleTimer _idleTimer;

        // タイムアウト値の無い個体を作らせないため、生成はこの口だけに絞る
        // The only creation entry point, so no instance can exist without its timeout
        public static EventIdleQuitWatcher Create(int idleTimeoutSeconds)
        {
            var watcherObject = new GameObject(nameof(EventIdleQuitWatcher));
            DontDestroyOnLoad(watcherObject);
            var watcher = watcherObject.AddComponent<EventIdleQuitWatcher>();
            watcher._idleTimer = new EventIdleTimer(idleTimeoutSeconds);
            return watcher;
        }

        private void Start()
        {
            // 起動所要時間を無操作時間に数えない。ロード完了時点から計り直す
            // Boot time must not count as idle time, so restart the measurement when loading completes
            GameInitializedEvent.OnGameInitialized.Subscribe(_ => _idleTimer.Reset()).AddTo(this);
        }

        private void Update()
        {
            if (!_idleTimer.AdvanceAndCheckTimeout(HasInputChanged(), Time.unscaledDeltaTime)) return;

            // 以後は毎フレームQuitを呼び続けない（Editorではno-opのためPlayModeが終わらなくなる）
            // Stop re-triggering every frame (Application.Quit is a no-op in the Editor, which would hang PlayMode)
            enabled = false;
            GameShutdownEvent.QuitApplicationAsync().Forget(LogQuitFailure);

            #region Internal

            bool HasInputChanged()
            {
                var keyboard = Keyboard.current;
                // 押下と離しの遷移だけを拾う。isPressedを含めると押しっぱなしで無操作復帰が止まる
                // Only press and release transitions count; including isPressed lets a held key stop the idle reset
                if (keyboard != null && (keyboard.anyKey.wasPressedThisFrame || keyboard.anyKey.wasReleasedThisFrame)) return true;

                var mouse = Mouse.current;
                if (mouse == null) return false;
                if (IsButtonChanged(mouse.leftButton) || IsButtonChanged(mouse.rightButton) || IsButtonChanged(mouse.middleButton)) return true;

                // ロック中はpositionが凍結するためdeltaとscrollで移動・ホイールを検知する
                // Position freezes while locked, so use delta and scroll to detect motion and wheel input
                if (0f < mouse.delta.ReadValue().sqrMagnitude) return true;
                return mouse.scroll.ReadValue() != Vector2.zero;
            }

            bool IsButtonChanged(ButtonControl button)
            {
                return button.wasPressedThisFrame || button.wasReleasedThisFrame;
            }

            #endregion
        }

        private void LogQuitFailure(Exception exception)
        {
            Debug.LogError($"無操作終了に失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
