using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;

namespace Client.Ui.State.KeyHints
{
    /// <summary>
    /// 左下のキーヒントの表示内容と出し分け（Web の KeyHintHud の早期 return の写し）
    /// What the bottom-left key hints show and whether they show (mirrors the Web KeyHintHud early returns)
    /// </summary>
    public sealed class KeyHintHudViewModel
    {
        public IReadOnlyList<KeyHint> Hints { get; }
        public bool DeleteMode { get; }
        public bool Shown { get; }

        private KeyHintHudViewModel(IReadOnlyList<KeyHint> hints, bool deleteMode, bool shown)
        {
            Hints = hints;
            DeleteMode = deleteMode;
            Shown = shown;
        }

        // blocking スキット中かヒントが空なら描かない
        // Hidden during a blocking skit or when there are no hints
        public static KeyHintHudViewModel Create(IReadOnlyList<KeyHint> hints, bool deleteMode, bool blockingSkitActive)
        {
            return new KeyHintHudViewModel(hints, deleteMode, !blockingSkitActive && 0 < hints.Count);
        }
    }
}
