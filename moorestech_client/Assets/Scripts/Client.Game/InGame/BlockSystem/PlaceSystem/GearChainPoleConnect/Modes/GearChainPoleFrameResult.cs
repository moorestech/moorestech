using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// Decideの出力。次の起点・プレビュー指示・送信指示のみで構成され、モードは状態や環境に一切書き込まない。
    /// Output of Decide: next source, preview command and send commands only. Modes never write to state or environment.
    /// </summary>
    public readonly struct GearChainPoleFrameResult
    {
        public readonly IGearChainPoleConnectAreaCollider NextSourcePole;
        public readonly bool InvalidatePendingRequest;
        public readonly GearChainPolePreviewCommand Preview;
        public readonly GearChainPoleExtendSendCommand? ExtendSend;
        public readonly GearChainConnectSendCommand? ChainConnectSend;

        /// <summary>
        /// 起点を維持（または送信なしで変更）してプレビューだけ更新する
        /// Keep (or change without sending) the source and update only the preview
        /// </summary>
        public static GearChainPoleFrameResult Show(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview)
        {
            return new GearChainPoleFrameResult(sourcePole, false, preview, null, null);
        }

        /// <summary>
        /// クリックされたポールを新しい起点として選択する。進行中の応答は常に無効化する
        /// Select the clicked pole as the new source, always invalidating pending responses
        /// </summary>
        public static GearChainPoleFrameResult SelectSource(IGearChainPoleConnectAreaCollider pole)
        {
            return new GearChainPoleFrameResult(pole, true, GearChainPolePreviewCommand.Hidden, null, null);
        }

        /// <summary>
        /// 設置・延長リクエストを送信する。起点はクリアし、引き継ぎは応答取り込みで行う
        /// Send a place/extend request. The source is cleared; hand-off happens via response consumption
        /// </summary>
        public static GearChainPoleFrameResult SendExtend(GearChainPoleExtendSendCommand command)
        {
            return new GearChainPoleFrameResult(null, false, GearChainPolePreviewCommand.Hidden, command, null);
        }

        /// <summary>
        /// チェーン接続を送信して起点をクリアする。進行中の延長応答も無効化する
        /// Send a chain connect and clear the source, also invalidating pending extend responses
        /// </summary>
        public static GearChainPoleFrameResult SendChainConnect(GearChainConnectSendCommand command)
        {
            return new GearChainPoleFrameResult(null, true, GearChainPolePreviewCommand.Hidden, null, command);
        }

        private GearChainPoleFrameResult(IGearChainPoleConnectAreaCollider nextSourcePole, bool invalidatePendingRequest, GearChainPolePreviewCommand preview, GearChainPoleExtendSendCommand? extendSend, GearChainConnectSendCommand? chainConnectSend)
        {
            NextSourcePole = nextSourcePole;
            InvalidatePendingRequest = invalidatePendingRequest;
            Preview = preview;
            ExtendSend = extendSend;
            ChainConnectSend = chainConnectSend;
        }
    }
}
