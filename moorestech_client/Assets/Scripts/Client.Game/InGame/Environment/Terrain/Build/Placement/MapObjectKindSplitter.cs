using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.MapModule;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     タイルローカル化されたMapObjectsを地形への見た目の効き方で分ける唯一の場所。Detailの距離フィルタは両者を別の距離場として読み、
    ///     岩周辺の裸地テクスチャは岩側だけを読むため、混ざるとどちらの規則も相手側へ漏れる
    ///     The single place splitting tile-local MapObjects by how they affect the terrain's look; the detail distance filters read
    ///     the two as separate fields and the bare-ground texture reads only the rocks, so mixing them leaks each rule onto the other
    /// </summary>
    public static class MapObjectKindSplitter
    {
        // 分類はマスタのterrainSurroundEffectTypeが正本。転送レイアウトは種別を持たずGUIDだけを運ぶ
        // The master's terrainSurroundEffectType is the source of truth; the transferred layout carries only a GUID, never a kind
        // stonesは岩用距離場を担う全岩、bareGroundStonesはその中で裸地を塗る岩だけ（移植元はBoulder/Cliff名の岩のみ裸地化する）
        // stones carries every rock for the rock distance field; bareGroundStones is the subset that paints bare ground (the source repaints only Boulder/Cliff rocks)
        public static void Split(
            IReadOnlyList<TileLocalMapObject> mapObjects,
            out List<TileLocalMapObject> trees, out List<TileLocalMapObject> stones,
            out List<TileLocalMapObject> bareGroundStones)
        {
            trees = new List<TileLocalMapObject>();
            stones = new List<TileLocalMapObject>();
            bareGroundStones = new List<TileLocalMapObject>();

            foreach (var mapObject in mapObjects)
            {
                var masterElement = MasterHolder.MapObjectMaster.GetMapObjectElement(new Guid(mapObject.Guid));

                // 振り分け先の無い効果を黙って片側へ寄せると、そのMapObjectの周りだけ草と裸地の規則が入れ替わる
                // Silently folding an unclassified effect into one side would swap the grass and bare-ground rules around that object alone
                switch (masterElement.TerrainSurroundEffectType)
                {
                    case MapObjectMasterElement.TerrainSurroundEffectTypeConst.treeRootPatch:
                        trees.Add(mapObject);
                        break;
                    case MapObjectMasterElement.TerrainSurroundEffectTypeConst.rockBareGround:
                        stones.Add(mapObject);
                        bareGroundStones.Add(mapObject);
                        break;
                    case MapObjectMasterElement.TerrainSurroundEffectTypeConst.rockNoBareGround:
                        stones.Add(mapObject);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"[MapObjectKindSplitter] MapObject {masterElement.MapObjectName} declares an unknown terrainSurroundEffectType {masterElement.TerrainSurroundEffectType}.");
                }
            }
        }
    }
}
