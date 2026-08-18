using Client.Game.InGame.Block;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線延長要求の確定結果。成功したかと、次起点になる終点ブロックを運ぶ。
    /// 成功していても終点の生成待ちが間に合わなければ Endpoint は null になり、その場合は起点を解除する必要がある
    /// （古い起点を残すと、サーバー側では張られている線をもう一度張ってしまい電線を二重消費するため）。
    /// Settled outcome of an extend request: whether it succeeded and the endpoint block that becomes the next origin.
    /// Endpoint is null when the spawn wait times out even on success, and the origin must then be released
    /// (keeping the stale origin would re-draw a wire the server already made, consuming wire twice).
    /// </summary>
    public readonly struct ElectricWireExtendOutcome
    {
        public readonly bool IsSuccess;
        public readonly BlockGameObject Endpoint;

        public ElectricWireExtendOutcome(bool isSuccess, BlockGameObject endpoint)
        {
            IsSuccess = isSuccess;
            Endpoint = endpoint;
        }
    }
}
