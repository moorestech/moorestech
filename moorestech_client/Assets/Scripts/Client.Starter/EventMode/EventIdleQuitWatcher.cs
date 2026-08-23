using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Starter.EventMode
{
    // 一定時間無入力でアプリ終了（再起動は外部スクリプト）
    // Quit after input silence; the external script restarts it
    public class EventIdleQuitWatcher : MonoBehaviour
    {
        private float _idleSeconds;
        private int _idleTimeoutSeconds;

        // 毎フレームの環境変数読出し回避のため
        // Avoids reading the env var every frame
        public void SetIdleTimeoutSeconds(int idleTimeoutSeconds)
        {
            _idleTimeoutSeconds = idleTimeoutSeconds;
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
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif

            #region Internal

            bool HasAnyInput()
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.anyKey.isPressed) return true;

                var mouse = Mouse.current;
                if (mouse == null) return false;
                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed) return true;

                // ロック中はpositionが凍結するためdeltaとscrollで移動・ホイールを検知する
                // Position freezes while locked, so use delta and scroll to detect motion and wheel input
                if (0f < mouse.delta.ReadValue().sqrMagnitude) return true;
                return mouse.scroll.ReadValue() != Vector2.zero;
            }

            #endregion
        }
    }
}
