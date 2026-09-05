using System.Collections.Generic;
using Client.Game.InGame.Map.MapVein;
using Client.Network.API;
using Game.MapGeneration.Transfer;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.Map.Vein
{
    /// <summary>
    ///     鉱脈台帳を任意の鉱脈配置で組むテスト用ファクトリ。台帳が読むのはMapLayout.MapVeinsだけなので他の応答はdefaultで埋める
    ///     Test factory building the vein registry from an arbitrary vein layout; the registry only reads MapLayout.MapVeins, so every other response stays default
    /// </summary>
    public static class MapVeinAabbRegistryFixture
    {
        public static MapVeinAabbRegistry Create(params VeinLayoutMessagePack[] veinLayouts)
        {
            var mapLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(new Vector3MessagePack(Vector3.zero),
                new List<MapObjectLayoutMessagePack>(), new List<VeinLayoutMessagePack>(veinLayouts), TerrainTransferMeta.CreateWithoutWorldDirectory(), string.Empty);
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null, null, null);

            return new MapVeinAabbRegistry(new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, mapLayout)));
        }

        // 種別絞り込みは範囲表示テストが種別別マテリアルを検証するために使う
        // Kind filtering is used by the range view tests to verify per-kind materials
        public static List<MapVeinAabb> SelectVeinsOfKind(MapVeinAabbRegistry registry, MapVeinKind kind)
        {
            var veins = new List<MapVeinAabb>();
            foreach (var vein in registry.Veins)
                if (vein.Kind == kind)
                    veins.Add(vein);

            return veins;
        }
    }
}
