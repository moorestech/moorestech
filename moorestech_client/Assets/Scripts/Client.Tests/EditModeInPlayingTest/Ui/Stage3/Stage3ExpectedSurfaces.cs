using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage3
{
    // 画面ごとに見えるべき面の表（Web の App.tsx と UiScreenTraitsTable の写し。BuildMenu・PlaceBlock も常駐 HUD の画面なのでホットバーが出る）
    // Which surfaces each screen shows (copied from the web App.tsx and UiScreenTraitsTable; BuildMenu and PlaceBlock keep the always-on HUD, so the hotbar shows)
    public static class Stage3ExpectedSurfaces
    {
        public const string BuildMenuPanel = "build-menu-panel";
        public const string HotbarHud = "hotbar-hud";
        public const string Modal = "modal";
        public const string ModalInput = "modal-input";
        public const string ModalButton = "modal-button";
        public const string Backdrop = "screen-backdrop";

        public static readonly IReadOnlyList<string> All = new[] { BuildMenuPanel, HotbarHud, Modal, Backdrop };

        public static IReadOnlyList<string> Expected(UIStateEnum state)
        {
            // 段 3 が通す 3 画面だけを持つ。モーダルは画面でなく保留の有無なので、どの画面の組にも入れない
            // Holds only the three screens stage 3 walks; the modal is a pending flag, not a screen, so no set includes it
            return state switch
            {
                UIStateEnum.GameScreen => new[] { HotbarHud },
                UIStateEnum.BuildMenu => new[] { BuildMenuPanel, Backdrop, HotbarHud },
                UIStateEnum.PlaceBlock => new[] { HotbarHud },
                _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, "S3 の同時に出る組の表に無い画面"),
            };
        }
    }
}
