namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// 距離カリングの対象。オンオフ指示だけ受け取り、具体的なコライダー操作は実装側が行う
    /// A distance-culling target. Receives only on/off instructions; concrete collider work is done by the implementation
    /// </summary>
    public interface IColliderDistanceCullingTarget
    {
        // 近ければon(有効)、遠ければoff(無効)。状態が変わった時だけ呼ばれる
        // on = near (enable), off = far (disable). Called only when the state changes
        void SetCollider(bool on);
    }
}
