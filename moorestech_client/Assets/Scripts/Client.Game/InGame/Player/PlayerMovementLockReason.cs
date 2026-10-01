namespace Client.Game.InGame.Player
{
    // 明示的に掛け外しする停止理由。理由ごとに独立し、1つでも残っていれば止まったまま
    // 乗車中の停止はここに無い。追従状態（PlayerRideFollow.IsFollowing）が正でApplyMovementLockが直接読む
    // Explicit stop reasons, each set and cleared independently; any remaining one keeps movement stopped
    // Riding is absent here: PlayerRideFollow.IsFollowing is its authority and ApplyMovementLock reads it directly
    public enum PlayerMovementLockReason
    {
        Ui,
        Debug,
        TextInput,
    }
}
