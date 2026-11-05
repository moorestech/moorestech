using System;
using System.Collections.Generic;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 画面ごとの性質。Web の ScreenTraits と同じ列
    /// Per-screen traits; same columns as the web ScreenTraits
    /// </summary>
    public sealed class UiScreenTraits
    {
        public bool Grab { get; }
        public bool PauseMenu { get; }
        public bool SkitInput { get; }
        public bool Backdrop { get; }
        public bool AlwaysOnHud { get; }
        public bool TrainHud { get; }

        public UiScreenTraits(bool grab, bool pauseMenu, bool skitInput, bool backdrop, bool alwaysOnHud, bool trainHud)
        {
            Grab = grab;
            PauseMenu = pauseMenu;
            SkitInput = skitInput;
            Backdrop = backdrop;
            AlwaysOnHud = alwaysOnHud;
            TrainHud = trainHud;
        }
    }

    /// <summary>
    /// 画面性質の表。Web の SCREEN_TRAITS を行ごとに写す
    /// Screen-trait table mirroring the web SCREEN_TRAITS row by row
    /// </summary>
    public static class UiScreenTraitsTable
    {
        // 列順: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud
        // Column order: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud
        private static readonly Dictionary<UiScreen, UiScreenTraits> Traits = BuildTraits();

        public static UiScreenTraits Get(UiScreen screen)
        {
            return Traits[screen];
        }

        private static Dictionary<UiScreen, UiScreenTraits> BuildTraits()
        {
            var traits = new Dictionary<UiScreen, UiScreenTraits>
            {
                { UiScreen.None, new UiScreenTraits(false, false, true, false, true, false) },
                { UiScreen.PlayerInventory, new UiScreenTraits(true, false, true, true, true, false) },
                { UiScreen.SubInventory, new UiScreenTraits(true, false, true, true, true, false) },
                { UiScreen.ResearchTree, new UiScreenTraits(true, false, true, true, false, false) },
                { UiScreen.BuildMenu, new UiScreenTraits(false, false, true, true, true, false) },
                { UiScreen.ChallengeList, new UiScreenTraits(false, false, true, true, true, false) },
                { UiScreen.PauseMenu, new UiScreenTraits(false, true, true, true, true, false) },
                { UiScreen.TrainHud, new UiScreenTraits(false, false, true, false, true, true) },
                { UiScreen.TrainPause, new UiScreenTraits(false, true, true, true, true, true) },
                // スキットのポーズ中だけ表示を続けたまま入力口を閉じる
                // Only the skit's pause keeps drawing while every input path closes
                { UiScreen.SkitPause, new UiScreenTraits(false, true, false, true, true, false) },
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
