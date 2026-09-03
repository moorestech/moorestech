namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     設置系すべてで共有するセルの設置不可原因。判定した側が立て、表示側が文言へ写像する
    ///     The cell block cause shared by every placement system; set by whoever judged it and mapped to wording by the presentation side
    ///     ある設置系だけが持つ原因はここに足さず、その系の専用enumへ置く
    ///     A cause that only one placement system has does not belong here; it goes to that system's own enum
    /// </summary>
    public enum PlacementBlockCause
    {
        None,
        ExistingBlock,
        GroundNotFound,
    }
}
