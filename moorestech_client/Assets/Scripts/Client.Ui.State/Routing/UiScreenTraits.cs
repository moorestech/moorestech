using System;
using System.Collections.Generic;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 画面ごとの性質。Web の列に UITK 固有の 2 列を足す
    /// Per-screen traits; Web columns plus two UITK-only columns
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
        public bool CoversGameLayer { get; }

        public UiScreenTraits(bool grab, bool pauseMenu, bool skitInput, bool backdrop, bool alwaysOnHud, bool trainHud, bool collapsesStageLeftPadding, bool coversGameLayer)
        {
            Grab = grab;
            PauseMenu = pauseMenu;
            SkitInput = skitInput;
            Backdrop = backdrop;
            AlwaysOnHud = alwaysOnHud;
            TrainHud = trainHud;
            CollapsesStageLeftPadding = collapsesStageLeftPadding;
            CoversGameLayer = coversGameLayer;
        }
    }

    /// <summary>
    /// 画面性質の表。Web の SCREEN_TRAITS を行ごとに写す
    /// Screen-trait table mirroring the web SCREEN_TRAITS row by row
    /// </summary>
    public static class UiScreenTraitsTable
    {
        // 列順: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud, collapsesStageLeftPadding, coversGameLayer
        // Column order: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud, collapsesStageLeftPadding, coversGameLayer
        private static readonly Dictionary<UiScreen, UiScreenTraits> Traits = BuildTraits();

        public static UiScreenTraits Get(UiScreen screen)
        {
            return Traits[screen];
        }

        private static Dictionary<UiScreen, UiScreenTraits> BuildTraits()
        {
            // coversGameLayer は Web deriveActiveLayer の blockInventory（SubInventory）・research・buildMenu。modal は画面でなく保留の有無なので UiActiveLayer が足す
            // coversGameLayer is Web deriveActiveLayer's blockInventory (SubInventory), research and buildMenu; modal is a pending flag, not a screen, so UiActiveLayer adds it
            var traits = new Dictionary<UiScreen, UiScreenTraits>
            {
                { UiScreen.None, new UiScreenTraits(false, false, true, false, true, false, false, false) },
                { UiScreen.PlayerInventory, new UiScreenTraits(true, false, true, true, true, false, false, false) },
                { UiScreen.SubInventory, new UiScreenTraits(true, false, true, true, true, false, false, true) },
                { UiScreen.ResearchTree, new UiScreenTraits(true, false, true, true, false, false, true, true) },
                { UiScreen.BuildMenu, new UiScreenTraits(false, false, true, true, true, false, false, true) },
                { UiScreen.ChallengeList, new UiScreenTraits(false, false, true, true, true, false, false, false) },
                { UiScreen.PauseMenu, new UiScreenTraits(false, true, true, true, true, false, false, false) },
                { UiScreen.TrainHud, new UiScreenTraits(false, false, true, false, true, true, false, false) },
                { UiScreen.TrainPause, new UiScreenTraits(false, true, true, true, true, true, false, false) },
                // スキットのポーズ中だけ表示を続けたまま入力口を閉じる
                // Only the skit's pause keeps drawing while every input path closes
                { UiScreen.SkitPause, new UiScreenTraits(false, true, false, true, true, false, false, false) },
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
