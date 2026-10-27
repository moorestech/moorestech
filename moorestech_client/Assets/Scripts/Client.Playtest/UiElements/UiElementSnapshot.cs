using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     1回の照会で得たUI要素の状態。Found以外では可視・矩形・文字列・状態フラグは空値
    ///     UI element state from one lookup; visibility, rect, text, and flags are empty unless Found
    /// </summary>
    public class UiElementSnapshot
    {
        public readonly string Testid;
        public readonly UiElementLookupStatus Status;
        public readonly UiRenderer Renderer;
        // 祖先のクリップと画面の内側に見える面積がある
        // Has visible area inside ancestor clips and the screen
        public readonly bool Visible;
        // 矩形中心へのクリックが自身か子孫に当たる（覆われていない）
        // A click at the rect center hits itself or a descendant (not covered)
        public readonly bool Clickable;
        // Unityスクリーン座標（左下原点・px）
        // Unity screen coordinates (bottom-left origin, pixels)
        public readonly Rect ScreenRect;
        public readonly string Text;
        // Webはdata-*属性、UITKはUSSクラスから作る状態名
        // State names built from data-* attributes on Web and USS classes on UITK
        public readonly IReadOnlyCollection<string> StateFlags;

        private UiElementSnapshot(string testid, UiElementLookupStatus status, UiRenderer renderer, bool visible, bool clickable, Rect screenRect, string text, IReadOnlyCollection<string> stateFlags)
        {
            Testid = testid;
            Status = status;
            Renderer = renderer;
            Visible = visible;
            Clickable = clickable;
            ScreenRect = screenRect;
            Text = text;
            StateFlags = stateFlags;
        }

        public bool IsFound => Status == UiElementLookupStatus.Found;

        public bool HasStateFlag(string flag)
        {
            foreach (var stateFlag in StateFlags)
            {
                if (stateFlag == flag) return true;
            }
            return false;
        }

        public static UiElementSnapshot Found(string testid, UiRenderer renderer, bool visible, bool clickable, Rect screenRect, string text, IReadOnlyCollection<string> stateFlags)
        {
            return new UiElementSnapshot(testid, UiElementLookupStatus.Found, renderer, visible, clickable, screenRect, text, stateFlags);
        }

        public static UiElementSnapshot NotFound(string testid, UiRenderer renderer)
        {
            return Empty(testid, UiElementLookupStatus.NotFound, renderer);
        }

        public static UiElementSnapshot Ambiguous(string testid, UiRenderer renderer)
        {
            return Empty(testid, UiElementLookupStatus.Ambiguous, renderer);
        }

        public static UiElementSnapshot Unavailable(string testid, UiRenderer renderer)
        {
            return Empty(testid, UiElementLookupStatus.RendererUnavailable, renderer);
        }

        private static UiElementSnapshot Empty(string testid, UiElementLookupStatus status, UiRenderer renderer)
        {
            return new UiElementSnapshot(testid, status, renderer, false, false, Rect.zero, string.Empty, Array.Empty<string>());
        }
    }
}
