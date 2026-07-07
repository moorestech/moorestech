using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path
{
    /// <summary>
    /// 座標列の組み立てと向き解決を束ねる（立体交差・占有判定より前段の純粋な経路計算）
    /// Combines path coordinate building and direction resolution (pure path calculation before overpass/occupancy)
    /// </summary>
    public static class BeltConveyorPathBuilder
    {
        public static (List<PlaceInfo> placeInfos, int startToCornerDistance) Build(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection)
        {
            var (positions, startToCornerDistance) = BeltConveyorPositionListBuilder.Build(startPoint, endPoint, isStartDirectionZ);
            var placeInfos = BeltConveyorDirectionResolver.Resolve(positions, startPoint, endPoint, blockDirection, startToCornerDistance);
            return (placeInfos, startToCornerDistance);
        }
    }
}
