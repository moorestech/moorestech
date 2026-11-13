using System;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// 移動の結果の写しをここ 1 か所にまとめる（プレイヤー側・ブロック側の口と行為が共有する）
    /// The single home of the move-result mappings (shared by the player-side and block-side ports and actions)
    /// </summary>
    internal static class PlayerItemMoveOutcomeConversion
    {
        // コントローラの結果を移動の結果へ。何もしない移動は送られないので開発者ログへ出して成功と区別する
        // Controller result to move outcome; a no-effect move is not sent, so it is logged for developers apart from success
        public static PlayerItemMoveOutcome FromController(LocalItemMoveResult result, string portName, string moveDescription)
        {
            if (result == LocalItemMoveResult.NoEffect) Debug.LogWarning($"[{portName}] move has no effect: {moveDescription}");
            return result switch
            {
                LocalItemMoveResult.Moved => PlayerItemMoveOutcome.Moved,
                LocalItemMoveResult.NoEffect => PlayerItemMoveOutcome.NoEffect,
                LocalItemMoveResult.SourceEmpty => PlayerItemMoveOutcome.SourceEmpty,
                LocalItemMoveResult.SourceCountShort => PlayerItemMoveOutcome.SourceCountShort,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "LocalItemMoveResult の未知の値。PlayerItemMoveOutcome に足すこと"),
            };
        }

        // 移動の結果を A-94 の拒否理由へ。何もしない移動は Web と同じく成功（警告ログは口が出す）
        // Move outcome to the A-94 reject reason; a no-effect move is accepted like Web (the port logs the warning)
        public static UiCommandResult ToCommandResult(PlayerItemMoveOutcome outcome)
        {
            return outcome switch
            {
                PlayerItemMoveOutcome.Moved => UiCommandAccepted.Instance,
                PlayerItemMoveOutcome.NoEffect => UiCommandAccepted.Instance,
                PlayerItemMoveOutcome.SourceEmpty => UiCommandRejected.Of(UiCommandRejectReason.ItemSourceSlotEmpty),
                PlayerItemMoveOutcome.SourceCountShort => UiCommandRejected.Of(UiCommandRejectReason.ItemSourceCountShort),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
            };
        }
    }
}
