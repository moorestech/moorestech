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
                var veinTypeGuid = new Guid(layout.VeinGuid);
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinTypeGuid);
                if (element == null) throw new InvalidOperationException($"[MapVeinAabbRegistry] mapVeinsマスタにveinGuid:{veinTypeGuid}がありません");

                var minCell = new Vector3Int(layout.MinX, layout.MinY, layout.MinZ);
                var maxCell = new Vector3Int(layout.MaxX, layout.MaxY, layout.MaxZ);

                // 種別と産出アイテムはマスタの判別共用体から1度で決める。逆極性の2式に分けると片方だけ更新される
                // Kind and yielded item come from the master's discriminated union in one place; two opposite-polarity expressions would drift apart
                var (kind, veinItemId) = element.VeinParam switch
                {
                    ItemVeinParam itemVeinParam => (MapVeinKind.Item, (ItemId?)MasterHolder.ItemMaster.GetItemId(itemVeinParam.ItemGuid)),
                    FluidVeinParam => (MapVeinKind.Fluid, (ItemId?)null),
                    _ => throw new InvalidOperationException($"[MapVeinAabbRegistry] 未対応のVeinParam:{element.VeinParam.GetType().Name} veinGuid:{veinTypeGuid}"),
                };

                _veins.Add(new MapVeinAabb(veinTypeGuid, minCell, maxCell, kind, veinItemId));
            }
        }

        /// <summary>
        ///     その種別（アイテム/流体）の鉱脈を集める。ポンプのように「掘れる種別まるごと」を見たい側が使う
        ///     Collects every vein of that kind; used by callers such as the pump that want a whole extractable kind
        /// </summary>
        public List<MapVeinAabb> SelectVeinsOfKind(MapVeinKind kind)
        {
            var veins = new List<MapVeinAabb>();
            foreach (var vein in _veins)
                if (vein.Kind == kind)
                    veins.Add(vein);

            return veins;
        }

        /// <summary>
        ///     その鉱脈GUIDのインスタンスを集める。チュートリアルの「この鉱脈にだけ置く」制限が使う
        ///     Collects every instance of that vein type; used by the tutorial's "place only on this vein" restriction
        /// </summary>
        public List<MapVeinAabb> SelectVeinsOfType(Guid veinTypeGuid)
        {
            var veins = new List<MapVeinAabb>();
            foreach (var vein in _veins)
                if (vein.VeinTypeGuid == veinTypeGuid)
                    veins.Add(vein);

            return veins;
        }
    }
}
