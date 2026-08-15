using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     タイルローカル化されたMapObjectsを木と岩へ分ける唯一の場所。Detailの距離フィルタは両者を別の距離場として読み、
    ///     岩周辺の裸地テクスチャは岩側だけを読むため、混ざるとどちらの規則も相手側へ漏れる
    ///     The single place splitting tile-local MapObjects into trees and rocks; the detail distance filters read the two as
    ///     separate fields and the bare-ground texture reads only the rocks, so mixing them leaks each rule onto the other
    /// </summary>
    public static class MapObjectKindSplitter
    {
        // 木か岩かはマスタのsoundEffectTypeが正本。転送レイアウトは種別を持たずGUIDだけを運ぶ
        // The master's soundEffectType is the source of truth; the transferred layout carries only a GUID, never a kind
        public static void Split(
            IReadOnlyList<MapObjectLayoutMessagePack> mapObjects,
            out List<MapObjectLayoutMessagePack> trees, out List<MapObjectLayoutMessagePack> stones)
        {
            trees = new List<MapObjectLayoutMessagePack>();
            stones = new List<MapObjectLayoutMessagePack>();

            foreach (var mapObject in mapObjects)
            {
                var masterElement = MasterHolder.MapObjectMaster.GetMapObjectElement(new Guid(mapObject.MapObjectGuid));

                // 振り分け先の無い種別を黙って片側へ寄せると、その種別の周りだけ草と裸地の規則が入れ替わる
                // Silently folding an unclassified kind into one side would swap the grass and bare-ground rules around that kind alone
                switch (masterElement.SoundEffectType)
                {
                    case MapObjectMasterElement.SoundEffectTypeConst.tree:
                        trees.Add(mapObject);
                        break;
                    case MapObjectMasterElement.SoundEffectTypeConst.stone:
                        stones.Add(mapObject);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"[MapObjectKindSplitter] MapObject {masterElement.MapObjectName} declares an unknown soundEffectType {masterElement.SoundEffectType}.");
                }
            }
        }
    }
}
