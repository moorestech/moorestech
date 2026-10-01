namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     開いた対象へ手が届くかの判定結果。届かない理由まで呼び出し側へ運ぶ
    ///     Result of the reach check for an opened target, carrying why it is out of reach
    /// </summary>
    public enum InteractReachResult
    {
        Reachable,
        OutOfRange,
        NotInteractable,
        TargetDestroyed,
    }
}
