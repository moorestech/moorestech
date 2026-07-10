using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Overlay
{
    /// <summary>
    ///     録画に焼き込まれるIMGUIオーバーレイ。左上=アクションログ、右上=入力状態、画面上=注入カーソル
    ///     IMGUI overlay baked into recordings: action log (top-left), input state (top-right), injected cursor
    /// </summary>
    public class PlaytestOverlayView : MonoBehaviour
    {
        private const float LineHeight = 24f;
        private const float ClickRippleDuration = 0.4f;
        // パネル上端。画面最上部に張り付くと見づらいため少し下げる
        // Top edge of the panels; nudged down so they don't hug the very top of the screen
        private const float PanelTop = 48f;
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            if (_labelStyle == null) _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            var state = PlaytestOverlay.State;
            DrawLogPanel(state);
            DrawInputPanel(state);
            DrawCursor(state);
            GUI.color = Color.white;
        }

        private void DrawLogPanel(PlaytestOverlayState state)
        {
            var entries = state.LogEntries;
            if (entries.Count == 0) return;

            // 半透明黒地にスタック式ログを描く（下が最新・古い行ほど薄く）
            // Stack log on a translucent black panel: newest at the bottom, older rows fade out
            DrawRect(new Rect(8, PanelTop, 640, entries.Count * LineHeight + 12), new Color(0f, 0f, 0f, 0.55f));
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var alpha = entries.Count <= 1 ? 1f : Mathf.Lerp(0.45f, 1f, (float)i / (entries.Count - 1));
                var time = TimeSpan.FromSeconds(entry.ElapsedSeconds);
                var text = $"[{(int)time.TotalMinutes}:{time.Seconds:00}] {entry.Text}";
                DrawShadowLabel(new Rect(16, PanelTop + 6 + i * LineHeight, 660, LineHeight), text, KindColor(entry.Kind, alpha));
            }
        }

        private const float InputPanelWidth = 640f;

        private void DrawInputPanel(PlaytestOverlayState state)
        {
            // 画面下部はゲーム自身の操作ヒント一覧（左）とホットバー（中央）が常設で占有するため、
            // 衝突しない右上に配置する
            // The bottom of the screen is permanently occupied by the game's own control hints (left)
            // and hotbar (center), so anchor this panel top-right where nothing else draws
            var panelX = Screen.width - InputPanelWidth - 8;
            DrawRect(new Rect(panelX, PanelTop, InputPanelWidth, 36), new Color(0f, 0f, 0f, 0.55f));

            // 押下中のキー・マウスボタンを常時表示する
            // Always show currently held keys and mouse buttons
            var held = new List<string>(state.HeldKeys);
            if (state.LeftMouseHeld) held.Add("LMB");
            if (state.RightMouseHeld) held.Add("RMB");
            var heldText = held.Count == 0 ? "-" : string.Join(" + ", held);
            DrawShadowLabel(new Rect(panelX + 8, PanelTop + 4, 300, 28), $"押下中: {heldText}", Color.white);

            // 直近入力をフェードアウト付きで右側に並べる
            // Lay out recent inputs to the right with a fade-out
            var x = panelX + 322f;
            foreach (var input in state.RecentInputs)
            {
                var age = Time.realtimeSinceStartup - input.PushedRealtime;
                if (PlaytestOverlayState.RecentInputLifetime < age) continue;
                var alpha = 1f - age / PlaytestOverlayState.RecentInputLifetime;
                var width = _labelStyle.CalcSize(new GUIContent(input.Label)).x + 8f;
                DrawShadowLabel(new Rect(x, PanelTop + 4, width, 28), input.Label, new Color(1f, 0.85f, 0.3f, alpha));
                x += width;
            }
        }

        private void DrawCursor(PlaytestOverlayState state)
        {
            if (Mouse.current == null) return;

            // 注入マウス位置に十字カーソルを描く（GUI座標はy反転）
            // Draw a crosshair at the injected mouse position (GUI y-axis is flipped)
            var position = Mouse.current.position.ReadValue();
            var center = new Vector2(position.x, Screen.height - position.y);
            var color = state.LeftMouseHeld ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.9f, 0.1f);
            DrawCrosshair(center, 14f, 3f, color);

            // クリック直後は拡大する枠（リップル）で強調する
            // Emphasize a click with an expanding outline (ripple) right after it happens
            var clickAge = Time.realtimeSinceStartup - state.LastClickRealtime;
            if (clickAge < ClickRippleDuration)
            {
                var progress = clickAge / ClickRippleDuration;
                DrawRectOutline(center, 16f + 44f * progress, 3f, new Color(1f, 0.5f, 0.1f, 1f - progress));
            }
        }

        private void DrawShadowLabel(Rect rect, string text, Color color)
        {
            // 視認性確保のため黒影を1pxずらして先に描く
            // Draw a 1px-offset black shadow first for readability
            GUI.color = new Color(0f, 0f, 0f, color.a);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _labelStyle);
            GUI.color = color;
            GUI.Label(rect, text, _labelStyle);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static void DrawCrosshair(Vector2 center, float armLength, float thickness, Color color)
        {
            // 黒縁→本体の順に十字を重ね、どんな背景でも視認できるようにする
            // Layer a black rim under the crosshair so it stays visible on any background
            DrawRect(new Rect(center.x - armLength - 1, center.y - thickness / 2 - 1, armLength * 2 + 2, thickness + 2), new Color(0f, 0f, 0f, 0.8f));
            DrawRect(new Rect(center.x - thickness / 2 - 1, center.y - armLength - 1, thickness + 2, armLength * 2 + 2), new Color(0f, 0f, 0f, 0.8f));
            DrawRect(new Rect(center.x - armLength, center.y - thickness / 2, armLength * 2, thickness), color);
            DrawRect(new Rect(center.x - thickness / 2, center.y - armLength, thickness, armLength * 2), color);
        }

        private static void DrawRectOutline(Vector2 center, float halfSize, float thickness, Color color)
        {
            DrawRect(new Rect(center.x - halfSize, center.y - halfSize, halfSize * 2, thickness), color);
            DrawRect(new Rect(center.x - halfSize, center.y + halfSize - thickness, halfSize * 2, thickness), color);
            DrawRect(new Rect(center.x - halfSize, center.y - halfSize, thickness, halfSize * 2), color);
            DrawRect(new Rect(center.x + halfSize - thickness, center.y - halfSize, thickness, halfSize * 2), color);
        }

        private static Color KindColor(PlaytestOverlayLogKind kind, float alpha)
        {
            var color = kind switch
            {
                PlaytestOverlayLogKind.Note => new Color(0.4f, 0.85f, 1f),
                PlaytestOverlayLogKind.Wait => new Color(0.75f, 0.75f, 0.75f),
                PlaytestOverlayLogKind.AssertPass => new Color(0.4f, 1f, 0.45f),
                PlaytestOverlayLogKind.AssertFail => new Color(1f, 0.35f, 0.3f),
                _ => Color.white,
            };
            color.a = alpha;
            return color;
        }
    }
}
