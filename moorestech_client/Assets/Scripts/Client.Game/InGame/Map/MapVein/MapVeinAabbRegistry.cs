using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
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

                _veins.Add(new MapVeinAabb(veinGuid, minCell, maxCell, kind));
            }
        }

        /// <summary>
        ///     指定セルがその種別の鉱脈に入っているか。種別を跨いだ判定は採掘機/ポンプの掘れる条件とずれるため持たない
        ///     Whether the cell sits inside a vein of that kind; no cross-kind query exists because it would diverge from what miners/pumps can actually extract
        /// </summary>
        public bool IsInsideVein(Vector3Int cell, MapVeinKind kind)
        {
            foreach (var vein in _veins)
                if (vein.Kind == kind && vein.ContainsCell(cell))
                    return true;

            return false;
        }

        /// <summary>
        ///     指定セルがその鉱脈（GUID）に入っているか。チュートリアルの「この鉱脈にだけ置く」制限が使う
        ///     Whether the cell sits inside that specific vein; used by the tutorial's "place only on this vein" restriction
        /// </summary>
        public bool IsInsideVein(Vector3Int cell, Guid veinGuid)
        {
            foreach (var vein in _veins)
                if (vein.VeinGuid == veinGuid && vein.ContainsCell(cell))
                    return true;

            return false;
        }
    }
}
