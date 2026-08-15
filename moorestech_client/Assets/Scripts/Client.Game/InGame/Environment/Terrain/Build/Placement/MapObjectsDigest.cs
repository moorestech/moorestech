using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Server.Protocol.PacketResponse.MapData;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     転送されたMapObjects一式を固定長のダイジェストへ畳む。見た目がmapObjectに依存するようになったため、
    ///     配置が1本でも動いたら見た目キャッシュを外すための入力になる
    ///     Folds the transferred MapObjects into a fixed-length digest; now that the visuals depend on the map objects,
    ///     it is the input that drops the visual cache the moment a single placement moves
    /// </summary>
    public static class MapObjectsDigest
    {
        public static byte[] Compute(IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)
        {
            // InstanceIdの昇順に固定してから畳む。転送順に任せるとサーバーの列挙順が揺れた回だけ全タイルが取り逃す
            // Sort by ascending InstanceId before folding; trusting the transfer order would miss every tile on any run the server's enumeration shifts
            var ordered = new List<MapObjectLayoutMessagePack>(mapObjects);
            ordered.Sort(CompareByInstanceId);

            var digestSource = new List<byte>();
            foreach (var mapObject in ordered)
            {
                // guidは可変長なので長さを先に書く。生で連結すると境界の違う別の並びが同じ列になる
                // The guid is variable-length so its length comes first; raw concatenation would let a differently split list form the same stream
                var guidBytes = Encoding.UTF8.GetBytes(mapObject.MapObjectGuid);
                digestSource.AddRange(BitConverter.GetBytes(guidBytes.Length));
                digestSource.AddRange(guidBytes);

                // 位置はビット列のまま入れる。10進表記に落とすと丸めた桁の差が消えて別の配置が同じキーになる
                // Positions enter as raw bits; a decimal rendering would drop digits and collapse different placements onto one key
                digestSource.AddRange(BitConverter.GetBytes(mapObject.X));
                digestSource.AddRange(BitConverter.GetBytes(mapObject.Y));
                digestSource.AddRange(BitConverter.GetBytes(mapObject.Z));
            }

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(digestSource.ToArray());
        }

        // List.Sortは不安定なので、同じInstanceIdが2本届いた瞬間に並びが揺れる。全項目まで比較して全順序にする
        // List.Sort is unstable and would shuffle the instant two objects share an InstanceId, so every field takes part in the total order
        private static int CompareByInstanceId(MapObjectLayoutMessagePack left, MapObjectLayoutMessagePack right)
        {
            var byInstanceId = left.InstanceId.CompareTo(right.InstanceId);
            if (byInstanceId != 0) return byInstanceId;

            var byGuid = string.CompareOrdinal(left.MapObjectGuid, right.MapObjectGuid);
            if (byGuid != 0) return byGuid;

            var byX = left.X.CompareTo(right.X);
            if (byX != 0) return byX;

            var byY = left.Y.CompareTo(right.Y);
            return byY != 0 ? byY : left.Z.CompareTo(right.Z);
        }
    }
}
