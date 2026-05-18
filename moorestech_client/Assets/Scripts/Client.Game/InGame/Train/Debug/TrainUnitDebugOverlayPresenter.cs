using System;
using Client.Game.InGame.Train.Unit;
using Common.Debug;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Client.Game.InGame.Train.DebugView
{
    public sealed class TrainUnitDebugOverlayPresenter : ITickable, IDisposable
    {
        private const float RefreshIntervalSeconds = 0.1f;
        private const int SortingOrder = 32700;

        private readonly TrainUnitClientCache _trainCache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainUnitDebugStatusFormatter _formatter = new();

        private GameObject _root;
        private TextMeshProUGUI _text;
        private float _elapsedSeconds = RefreshIntervalSeconds;
        private bool _wasEnabled;

        public TrainUnitDebugOverlayPresenter(TrainUnitClientCache trainCache, TrainUnitTickState tickState)
        {
            _trainCache = trainCache;
            _tickState = tickState;
        }

        public void Tick()
        {
            _elapsedSeconds += Time.unscaledDeltaTime;
            if (_elapsedSeconds < RefreshIntervalSeconds)
            {
                return;
            }

            _elapsedSeconds = 0f;
            var isEnabled = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainUnitDebugOverlayKey);
            if (!isEnabled)
            {
                if (_wasEnabled)
                {
                    HideOverlay();
                }

                _wasEnabled = false;
                return;
            }

            // ONになったタイミングで表示を生成し、以後は一定間隔で文字列を更新する
            // Create the overlay when enabled, then refresh text at a fixed interval.
            EnsureOverlay();
            _wasEnabled = true;
            _text.text = _formatter.Format(_trainCache, _tickState);
        }

        public void Dispose()
        {
            HideOverlay();
        }

        private void EnsureOverlay()
        {
            if (_root != null)
            {
                return;
            }

            // PrefabやSceneを触らず、デバッグ用Canvasを実行時だけ生成する
            // Create a runtime-only debug Canvas without touching prefabs or scenes.
            _root = new GameObject("TrainUnitDebugOverlay");
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 背景パネルでゲーム画面上でも文字の視認性を確保する
            // Add a background panel so text stays readable on the game view.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(12f, -12f);
            panelRect.sizeDelta = new Vector2(900f, 760f);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.68f);
            image.raycastTarget = false;

            // TextMeshProUGUIを全面に敷き、固定幅フォント相当の配置で状態を並べる
            // Fill the panel with TextMeshProUGUI and lay out status in a fixed text block.
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(panel.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);

            _text = textObject.AddComponent<TextMeshProUGUI>();
            _text.raycastTarget = false;
            _text.fontSize = 14f;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.text = _formatter.Format(_trainCache, _tickState);
        }

        private void HideOverlay()
        {
            if (_root == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_root);
            _root = null;
            _text = null;
            _elapsedSeconds = RefreshIntervalSeconds;
        }
    }
}
