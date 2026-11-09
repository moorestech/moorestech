using System;
using System.Collections.Generic;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 画面ごとの性質。Web の ScreenTraits と同じ列に、UITK だけの列 CollapsesStageLeftPadding（Web App.tsx の .stageResearch）を足す
    /// Per-screen traits; the web ScreenTraits columns plus the UITK-only CollapsesStageLeftPadding (Web App.tsx .stageResearch)
    /// </summary>
    public sealed class UiScreenTraits
    {
        public bool Grab { get; }
        public bool PauseMenu { get; }
        public bool SkitInput { get; }
        public bool Backdrop { get; }
        public bool AlwaysOnHud { get; }
        public bool TrainHud { get; }
        public bool CollapsesStageLeftPadding { get; }

        public UiScreenTraits(bool grab, bool pauseMenu, bool skitInput, bool backdrop, bool alwaysOnHud, bool trainHud, bool collapsesStageLeftPadding)
        {
            Grab = grab;
            PauseMenu = pauseMenu;
            SkitInput = skitInput;
            Backdrop = backdrop;
            AlwaysOnHud = alwaysOnHud;
            TrainHud = trainHud;
            CollapsesStageLeftPadding = collapsesStageLeftPadding;
        }
    }

    /// <summary>
    /// 画面性質の表。Web の SCREEN_TRAITS を行ごとに写す
    /// Screen-trait table mirroring the web SCREEN_TRAITS row by row
    /// </summary>
    public static class UiScreenTraitsTable
    {
        // 列順: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud, collapsesStageLeftPadding
        // Column order: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud, collapsesStageLeftPadding
        private static readonly Dictionary<UiScreen, UiScreenTraits> Traits = BuildTraits();

        public static UiScreenTraits Get(UiScreen screen)
        {
            return Traits[screen];
        }

        private static Dictionary<UiScreen, UiScreenTraits> BuildTraits()
        {
            var traits = new Dictionary<UiScreen, UiScreenTraits>
            {
                { UiScreen.None, new UiScreenTraits(false, false, true, false, true, false, false) },
                { UiScreen.PlayerInventory, new UiScreenTraits(true, false, true, true, true, false, false) },
                { UiScreen.SubInventory, new UiScreenTraits(true, false, true, true, true, false, false) },
                { UiScreen.ResearchTree, new UiScreenTraits(true, false, true, true, false, false, true) },
                { UiScreen.BuildMenu, new UiScreenTraits(false, false, true, true, true, false, false) },
                { UiScreen.ChallengeList, new UiScreenTraits(false, false, true, true, true, false, false) },
                { UiScreen.PauseMenu, new UiScreenTraits(false, true, true, true, true, false, false) },
                { UiScreen.TrainHud, new UiScreenTraits(false, false, true, false, true, true, false) },
                { UiScreen.TrainPause, new UiScreenTraits(false, true, true, true, true, true, false) },
                // スキットのポーズ中だけ表示を続けたまま入力口を閉じる
                // Only the skit's pause keeps drawing while every input path closes
                { UiScreen.SkitPause, new UiScreenTraits(false, true, false, true, true, false, false) },
            };

            // 画面を足して行を書き忘れたら静的初期化で止める
            // A new screen without a row stops at static initialization
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                if (!traits.ContainsKey(screen)) throw new InvalidOperationException($"[UiScreenTraitsTable] missing traits row {screen}");
            }
            return traits;
        }
    }
}
