namespace Game.PlayerRiding.Interface
{
    // 乗り物実体が実装するサーバー側インターフェース。
    // サーバーは座席数のみを使う（座席のワールド座標は計算しない。仕様書セクション3・4.5）。
    // Server-side interface implemented by a ridable. The server only needs seat count.
    public interface IRidable
    {
        IRidableIdentifier Identifier { get; }
        int SeatCount { get; }
    }
}
