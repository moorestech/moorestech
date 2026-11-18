using System.Collections.Generic;
using Client.Ui.State.Text;

namespace Client.Ui.State.Challenge
{
    /// <summary>
    /// 進行中チャレンジ HUD の目標（受信順）と出し分け（Web の CurrentChallengeHud の早期 return の写し）
    /// Objectives of the current challenge HUD in received order and whether it shows (mirrors the Web CurrentChallengeHud early returns)
    /// </summary>
    public sealed class CurrentChallengeHudViewModel
    {
        public IReadOnlyList<UiText> Objectives { get; }
        public bool Shown { get; }

        private CurrentChallengeHudViewModel(IReadOnlyList<UiText> objectives, bool shown)
        {
            Objectives = objectives;
            Shown = shown;
        }

        // blocking スキット中か目標が空なら描かない
        // Hidden during a blocking skit or when there are no objectives
        public static CurrentChallengeHudViewModel Create(IReadOnlyList<UiText> objectives, bool blockingSkitActive)
        {
            return new CurrentChallengeHudViewModel(objectives, !blockingSkitActive && 0 < objectives.Count);
        }
    }
}
