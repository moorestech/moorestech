namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost
{
    /// <summary>
    /// 張替え1セルの支払い結果。送信可否と表示色はこれだけで決まる
    /// The payment outcome of one replace cell; it alone decides whether the cell is sent and how it is colored
    /// </summary>
    internal enum BeltReplaceCellPayment
    {
        // 自分の財布と所持素材だけで払い切れた
        // Paid entirely from the player's own wallet and holdings
        Paid,

        // 課金元を把握できない返却を見込めば払える。送信してサーバーに最終判定させる
        // Payable only if a refund whose payer is unknown arrives; it is sent and the server makes the final call
        PaidWithAssumedRefund,

        // どんな返却を見込んでも払えない
        // Unpayable under any refund assumption
        Unaffordable,
    }
}
