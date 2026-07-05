using System.Collections.Generic;
using Client.Input;
using Common.Debug;
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

        private readonly List<GameObject> _uguiRoots = new();
        private readonly Dictionary<GameObject, bool> _uguiRootActiveSnapshot = new();
        private bool _isCefActive;
        private bool _hasAppliedOnce;
        private bool _appliedCefActive;
        private float _lastDebugPollTime;

        private void Awake()
        {
            // CefUnity以外の直下の子を全てuGUIルートとして収集する
            // Collect every direct child except CefUnity as an uGUI root
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child != cefUnityRoot) _uguiRoots.Add(child);
            }
        }

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
            // TODO InputManagerに移動
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl) && UnityEngine.Input.GetKeyDown(KeyCode.I))
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

            // CEF表示中はカーソル解放を再表明する（起動時の初期化順序競合への保険）
            // Re-assert cursor release while CEF is shown (guards against boot init-order races)
            if (_isCefActive) InputManager.MouseCursorVisible(true);
        }

        private void ApplyState()
        {
            // CEFルートの表示を切り替える
            // Toggle the CEF root visibility
            cefUnityRoot.SetActive(_isCefActive);

            // uGUIルートの表示状態を記録・復元する（一斉SetActive(true)による状態破壊を防ぐ）
            // Snapshot / restore uGUI roots' active state (avoids destroying state via a blanket SetActive(true))
            if (_isCefActive) SnapshotAndHideUguiRoots();
            else RestoreUguiRoots();

            // 入力・カーソル調停ゲートを更新する（一方通行: 書き込みはここのみ）
            // Update the input/cursor arbitration gate (one-way: written only here)
            WebUiScreenGate.SetCefActive(_isCefActive);
            if (_isCefActive) InputManager.MouseCursorVisible(true);

            _appliedCefActive = _isCefActive;
            _hasAppliedOnce = true;

            #region Internal

            void SnapshotAndHideUguiRoots()
            {
                // 既にCEF適用済みなら記録済みスナップショットを壊さないためスキップ
                // Skip when CEF is already applied so the existing snapshot is not overwritten
                if (_appliedCefActive) return;

                _uguiRootActiveSnapshot.Clear();
                foreach (var root in _uguiRoots)
                {
                    _uguiRootActiveSnapshot[root] = root.activeSelf;
                    root.SetActive(false);
                }
            }

            void RestoreUguiRoots()
            {
                // 初回(初期状態CEF OFF)は現状のactiveSelfを尊重しSetActiveを一切呼ばない
                // On the first apply (boot with CEF OFF) respect current activeSelf and don't call SetActive at all
                if (!_hasAppliedOnce || !_appliedCefActive) return;

                foreach (var root in _uguiRoots)
                    if (_uguiRootActiveSnapshot.TryGetValue(root, out var wasActive)) root.SetActive(wasActive);
            }

            #endregion
        }
    }
}
