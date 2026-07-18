using Common.Debug;
using Client.Input;
using UnityEngine;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.UI.UIState
{
    public class WebUiCefToggle : MonoBehaviour
    {
        [SerializeField] private GameObject cefUnityRoot;

        // デバッグスイッチのポーリング間隔（毎フレームのJSON3ファイル読込を避けるため間引く）
        // Poll interval for the debug switch (throttled to avoid reading 3 JSON files every frame)
        private const float DebugPollInterval = 0.5f;

        private bool _isCefActive;
        private bool _appliedCefActive;
        private float _lastDebugPollTime;

        private void Start()
        {
            // MouseCursorTooltip等、他コンポーネントのAwake完了後に適用するためStartで実行する
            // Apply in Start (not Awake) so other components (e.g. MouseCursorTooltip) finish Awake first
            // デフォルトはCEF有効。デバッグメニューでの変更はDebugParametersに永続化される
            // CEF is active by default; DebugSheet changes persist via DebugParameters
            _isCefActive = DebugParameters.GetValueOrDefaultBool(WebUiCefActiveKey, true);
            ApplyState();
        }

        private void Update()
        {
            // Ctrl+IでWeb UI(CEF)とuGUIの表示を排他的に切り替える（入力即応のため毎フレーム検知）
            // Toggle CEF vs uGUI exclusively with Ctrl+I (detected every frame for input responsiveness)
            if (HybridInput.GetKey(KeyCode.LeftControl) && HybridInput.GetKeyDown(KeyCode.I))
            {
                _isCefActive = !_isCefActive;
                DebugParameters.SaveBool(WebUiCefActiveKey, _isCefActive);
                ApplyState();
                return;
            }

            // デバッグスイッチのポーリングは0.5秒間隔に間引く
            // Throttle the debug-switch polling to 0.5s
            if (Time.unscaledTime - _lastDebugPollTime < DebugPollInterval) return;
            _lastDebugPollTime = Time.unscaledTime;

            // デバッグメニューのスイッチ変更をポーリングで反映する
            // Poll for DebugSheet switch changes and apply them
            var debugValue = DebugParameters.GetValueOrDefaultBool(WebUiCefActiveKey, true);
            if (debugValue != _isCefActive)
            {
                _isCefActive = debugValue;
                ApplyState();
            }
        }

        private void OnDestroy()
        {
            // シーンアンロード等でCEF表示中に破棄されるとゲートがtrueのまま残り、次シーンの遷移・カメラが止まるため解除する
            // If destroyed while CEF is active (e.g. scene unload) the gate would stay true and freeze the next scene's transitions/camera, so release it
            if (_appliedCefActive) WebUiScreenGate.SetWebUiMode(false);
        }

        private void ApplyState()
        {
            // webモード中はCEFルートを常時表示する（透明オーバーレイ。uGUIは隠さず共存）
            // While in web mode the CEF root stays always visible (transparent overlay; uGUI coexists unhidden)
            cefUnityRoot.SetActive(_isCefActive);

            // 入力・カーソル調停ゲートを更新する（一方通行: 書き込みはここのみ）
            // Update the input/cursor arbitration gate (one-way: written only here)
            WebUiScreenGate.SetWebUiMode(_isCefActive);

            _appliedCefActive = _isCefActive;
        }
    }
}
