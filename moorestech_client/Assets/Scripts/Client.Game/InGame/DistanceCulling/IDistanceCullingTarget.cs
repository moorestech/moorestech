namespace Client.Game.InGame.DistanceCulling
{
    /// <summary>
    /// 距離カリングの対象。半径内/外の指示だけ受け取り、具体処理は実装側が行う
    /// A distance-culling target. Receives only within/outside-range instructions; concrete work is done by the implementation
    /// </summary>
    public interface IDistanceCullingTarget
    {
        // 半径内ならtrue、外ならfalse。状態が変わった時だけ呼ばれる
        // true = within range, false = outside. Called only when the state changes
        void SetWithinRange(bool withinRange);
    }
}
