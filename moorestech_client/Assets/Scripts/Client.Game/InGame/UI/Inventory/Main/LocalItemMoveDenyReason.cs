namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// 検証つき移動（TryMoveItem）の拒否理由。足すと受け手の switch が網羅漏れで落ちる
    /// Deny reasons of the validated move (TryMoveItem); adding one breaks the receivers' exhaustive switches
    /// </summary>
    public enum LocalItemMoveDenyReason
    {
        SourceEmpty,
        SourceCountShort,
    }
}
