using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// HUD の表示状態を組む
    /// Builds the HUD state
    /// </summary>
    public static class ModeHudViewModelFactory
    {
        public static ModeHudViewModel Create(UIStateEnum state, IPlacementTarget target, int height)
        {
            // placeBlock は配置 HUD、deleteBar は帯と高さ
            // placeBlock shows the placement HUD, deleteBar the bands and height
            switch (state)
            {
                case UIStateEnum.PlaceBlock:
                    return new ModeHudPlacement(TargetName(), height);
                case UIStateEnum.DeleteBar:
                    // 削除箱は高さだけを出す
                    // The delete box shows only the height
                    return new ModeHudDelete(height);
                default:
                    return ModeHudViewModel.Hidden;
            }

            #region Internal

            ModeHudText TargetName()
            {
                // 設置モードで対象が未選択の瞬間は CurrentTarget が null になりうる。無いことは型で持つ
                // CurrentTarget can be null for a moment in placement mode; absence is carried by the type
                if (target == null) return ModeHudText.None;

                // 表示名はマスタ由来で空にならない前提。空なら開発者へ知らせ、表示は Web と同じくそのまま
                // Display names come from the master and should never be empty; tell developers and display as is, like Web
                if (target.DisplayName.Length == 0) Debug.LogError($"[ModeHud] empty DisplayName for placement target {target.Kind} {target.Id}");
                return ModeHudText.Of(target.DisplayName);
            }

            #endregion
        }
    }
}
