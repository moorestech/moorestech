using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 電気系ブロックのパラメータからワイヤー端点仕様を取り出す
    /// Extracts wire endpoint spec from an electric block param
    /// </summary>
    public static class ElectricWireBlockParamResolver
    {
        /// <summary>
        /// 電気系ブロックのパラメータから接続数上限・範囲プロファイル・電柱かどうかを取り出す
        /// Extract connection limit, range profile and pole-ness from an electric block param
        /// </summary>
        public static bool TryGetWireRangeParam(IBlockParam blockParam, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            switch (blockParam)
            {
                // 電柱は将来IElectricWireConnectParamを実装しても機械側へ落ちてはならないため必ず先頭
                // The pole case must stay first even if it ever implements IElectricWireConnectParam
                case ElectricPoleBlockParam pole:
                    maxWireConnectionCount = pole.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreatePole(pole);
                    isPole = true;
                    return true;
                case IElectricWireConnectParam wireConnectParam:
                    // 電柱以外はinterface経由で一括処理
                    // Non-pole electric params are handled via the schema interface
                    maxWireConnectionCount = wireConnectParam.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(wireConnectParam.ConnectionRange, wireConnectParam.ConnectionHeightRange);
                    isPole = false;
                    return true;
                default:
                    // 電気系以外のブロックパラメータには対応しない
                    // Not an electric block param
                    maxWireConnectionCount = 0;
                    rangeProfile = default;
                    isPole = false;
                    return false;
            }
        }
    }
}
