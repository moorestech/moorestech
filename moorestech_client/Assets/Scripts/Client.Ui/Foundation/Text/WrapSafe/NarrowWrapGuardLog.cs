using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 狭い幅の回避が折り返しを止めた・戻したことを開発者へ知らせる（状態が替わったときだけ呼ばれる）
    /// Tells developers the narrow-width guard stopped or restored wrapping (called only when the state changes)
    /// </summary>
    internal static class NarrowWrapGuardLog
    {
        private const int TextExcerptLength = 24;

        // 止めている間の見た目は Chrome と違う（Chrome は狭くても折り返す）
        // While held the look differs from Chrome (which wraps at any width)
        public static void Held(TextElement element, float renderWidth, float minimumWidth)
        {
            Debug.LogWarning($"[NarrowWrapGuard] '{element.name}' stops wrapping: width {renderWidth:0.#}px < {minimumWidth:0.#}px needed to wrap \"{Excerpt(element.text)}\" without freezing UITK TextCore (bd moorestech-fcpzz, LX-184)"); // look-exception: LX-184
        }

        public static void Released(TextElement element, float renderWidth, float minimumWidth)
        {
            Debug.Log($"[NarrowWrapGuard] '{element.name}' wraps again: width {renderWidth:0.#}px, needs {minimumWidth:0.#}px for \"{Excerpt(element.text)}\"");
        }

        private static string Excerpt(string text)
        {
            return text.Length <= TextExcerptLength ? text : text.Substring(0, TextExcerptLength) + "…";
        }
    }
}
