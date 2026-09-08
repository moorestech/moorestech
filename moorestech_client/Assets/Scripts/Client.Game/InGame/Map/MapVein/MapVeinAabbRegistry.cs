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
                var vein = ResolveVeinOrNull(new Guid(layout.VeinGuid),
                    new Vector3Int(layout.MinX, layout.MinY, layout.MinZ),
                    new Vector3Int(layout.MaxX, layout.MaxY, layout.MaxZ));
                if (vein == null) continue;

                _veins.Add(vein);
            }

            #region Internal

            // マスタ欠損はサーバーのFluidMapVeinDatastoreと同じくログを出してスキップする。ここだけ例外にすると同じmodでワールドがロードできない
            // A missing master logs and skips just like the server's FluidMapVeinDatastore; throwing only here would leave the world unloadable for the very mod the server accepts
            MapVeinAabb ResolveVeinOrNull(Guid veinTypeGuid, Vector3Int minCell, Vector3Int maxCell)
            {
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinTypeGuid);
                if (element == null)
                {
                    Debug.LogError($"veinGuid:{veinTypeGuid}に対応するMapVeinマスタが存在しません。鉱脈の登録をスキップします。");
                    return null;
                }

                // 種別と産出アイテム/流体はマスタの判別共用体から1度で決める。逆極性の式に分けると片方だけ更新される
                // Kind and yielded item/fluid come from the master's discriminated union in one place; opposite-polarity expressions would drift apart
                switch (element.VeinParam)
                {
                    case ItemVeinParam itemVeinParam:
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemVeinParam.ItemGuid);
                        if (itemId == null)
                        {
                            Debug.LogError($"ItemGuid:{itemVeinParam.ItemGuid}に対応するItemIdが存在しません。鉱脈の登録をスキップします。");
                            return null;
                        }

                        return MapVeinAabb.OfItem(veinTypeGuid, minCell, maxCell, itemId.Value);
                    }
                    case FluidVeinParam fluidVeinParam:
                    {
                        var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(fluidVeinParam.FluidGuid);
                        if (fluidId == null)
                        {
                            Debug.LogError($"FluidGuid:{fluidVeinParam.FluidGuid}に対応するFluidIdが存在しません。鉱脈の登録をスキップします。");
                            return null;
                        }

                        return MapVeinAabb.OfFluid(veinTypeGuid, minCell, maxCell, fluidId.Value);
                    }
                    default:
                        throw new InvalidOperationException($"[MapVeinAabbRegistry] 未対応のVeinParam:{element.VeinParam.GetType().Name} veinGuid:{veinTypeGuid}");
                }
            }

            #endregion
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
