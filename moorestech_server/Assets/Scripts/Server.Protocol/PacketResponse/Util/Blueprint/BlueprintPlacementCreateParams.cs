using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Block.Interface;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    public static class BlueprintPlacementCreateParams
    {
        public static BlockCreateParam[] From(Dictionary<string, string> settings)
        {
            // 保存設定をブロック生成入力へ変換
            // Convert saved settings to block creation parameters.
            if (settings == null) return Array.Empty<BlockCreateParam>();
            return settings.Select(setting => new BlockCreateParam(setting.Key, Encoding.UTF8.GetBytes(setting.Value))).ToArray();
        }
    }
}
