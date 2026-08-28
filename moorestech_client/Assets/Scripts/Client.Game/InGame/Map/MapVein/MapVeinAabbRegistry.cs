using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     ワールドの全鉱脈範囲の台帳。表示側と設置判定側が同じ範囲を見るための唯一の出所
    ///     Ledger of every vein range in the world; the single source both the view and the placement check read
    /// </summary>
    public class MapVeinAabbRegistry
    {
        public IReadOnlyList<MapVeinAabb> Veins => _veins;
        private readonly List<MapVeinAabb> _veins = new();

        public MapVeinAabbRegistry(InitialHandshakeResponse handshakeResponse)
        {
            // veinは動かないので初期ハンドシェイクの時点で範囲を確定させ、以後のmaster参照を無くす
            // Veins never move, so fix their ranges at the initial handshake and drop later master lookups
            foreach (var layout in handshakeResponse.MapLayout.MapVeins)
            {
                var veinGuid = new Guid(layout.VeinGuid);
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
                if (element == null) throw new InvalidOperationException($"[MapVeinAabbRegistry] mapVeinsマスタにveinGuid:{veinGuid}がありません");

                var minCell = new Vector3Int(layout.MinX, layout.MinY, layout.MinZ);
                var maxCell = new Vector3Int(layout.MaxX, layout.MaxY, layout.MaxZ);
                var kind = element.VeinParam is FluidVeinParam ? MapVeinKind.Fluid : MapVeinKind.Item;

                _veins.Add(new MapVeinAabb(minCell, maxCell, kind));
            }
        }

        /// <summary>
        ///     底面フットプリントがその種別の鉱脈とXZで重なるか。種別を跨いだ判定は採掘機/ポンプの掘れる条件とずれるため持たない
        ///     Whether the footprint overlaps a vein of that kind in XZ; no cross-kind query exists because it would diverge from what miners/pumps can actually extract
        /// </summary>
        public bool IsOverlappingFootprint(BlockPositionInfo footprint, MapVeinKind kind)
        {
            foreach (var vein in _veins)
                if (vein.Kind == kind && MinerVeinFootprintJudge.OverlapsXz(footprint, vein.MinCell, vein.MaxCell))
                    return true;

            return false;
        }
    }
}
