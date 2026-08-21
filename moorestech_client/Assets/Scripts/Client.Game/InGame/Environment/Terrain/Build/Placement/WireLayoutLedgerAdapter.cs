using System;
using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    // 移設期間だけの橋渡し。ワイヤの配置＋マスタの種別から台帳を組む。Task 6 でファサードが台帳を内製したら削除する
    // A bridge for the migration only: builds the ledger from the wire layout plus the master's kind; deleted in Task 6 once the facade owns the ledger
    public static class WireLayoutLedgerAdapter
    {
        public static PlacementLedger Build(IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)
        {
            var ledger = new PlacementLedger();
            foreach (var mapObject in mapObjects)
            {
                var element = MasterHolder.MapObjectMaster.GetMapObjectElement(new Guid(mapObject.MapObjectGuid));
                var kind = element.TerrainSurroundEffectType switch
                {
                    MapObjectMasterElement.TerrainSurroundEffectTypeConst.treeRootPatch => TerrainSurroundEffectType.treeRootPatch,
                    MapObjectMasterElement.TerrainSurroundEffectTypeConst.rockBareGround => TerrainSurroundEffectType.rockBareGround,
                    MapObjectMasterElement.TerrainSurroundEffectTypeConst.rockNoBareGround => TerrainSurroundEffectType.rockNoBareGround,
                    _ => throw new InvalidOperationException($"unknown terrainSurroundEffectType {element.TerrainSurroundEffectType}"),
                };
                ledger.Add(new LedgerPlacement(mapObject.MapObjectGuid,
                    new Vector3(mapObject.X, mapObject.Y, mapObject.Z),
                    new Quaternion(mapObject.RotationX, mapObject.RotationY, mapObject.RotationZ, mapObject.RotationW),
                    new Vector3(mapObject.ScaleX, mapObject.ScaleY, mapObject.ScaleZ),
                    kind, mapObject.ClusterId, new Vector2(mapObject.ClusterCenterX, mapObject.ClusterCenterZ)));
            }
            return ledger;
        }
    }
}
