namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 設置1回が財布をどう使ったか。計画時に確定し確定処理はこれを網羅switchするだけ
    /// How one placement used the wallet; settled when planning so the commit only has to switch over it exhaustively
    /// </summary>
    public enum ConstructionWalletUsage
    {
        // 財布を通らない（1セット1個のブロック）
        // Bypasses the wallet entirely (one placement per cost set)
        NotUsed,

        // 残りで賄い素材を払わない
        // Covered by the remainder, no materials paid
        CoveredByWallet,

        // 素材1セットを払いN分を補充してから1消費する
        // Pays one material set, refills one set's worth, then consumes one
        PaidAndRefilled,
    }
}
