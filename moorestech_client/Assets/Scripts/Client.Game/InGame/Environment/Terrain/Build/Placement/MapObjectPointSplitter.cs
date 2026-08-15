using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     halo込みでタイルローカル化されたMapObjectsを木の点群と岩の点群へ分ける。Detailの距離フィルタは両者を別の距離場として読むため、
    ///     混ざると岩の周りの草だけが木と同じ規則で消える
    ///     Splits the halo-inclusive tile-local MapObjects into tree points and rock points; the detail distance filters read the two as
    ///     separate fields, so mixing them clears the grass around rocks by the trees' rule
    /// </summary>
    public static class MapObjectPointSplitter
    {
        // 木か岩かはマスタのsoundEffectTypeが正本。転送レイアウトは種別を持たずGUIDだけを運ぶ
        // The master's soundEffectType is the source of truth; the transferred layout carries only a GUID, never a kind
        public static void Split(
            IReadOnlyList<MapObjectLayoutMessagePack> haloObjects,
            out List<Vector2> treePoints, out List<Vector2> objectPoints)
        {
            treePoints = new List<Vector2>();
            objectPoints = new List<Vector2>();

            foreach (var mapObject in haloObjects)
            {
                var masterElement = MasterHolder.MapObjectMaster.GetMapObjectElement(new Guid(mapObject.MapObjectGuid));
                var point = new Vector2(mapObject.X, mapObject.Z);

                // 振り分け先の無い種別を黙って片側へ寄せると、その種別の周りだけ草の規則が入れ替わる
                // Silently folding an unclassified kind into one side would swap the grass rule around that kind alone
                switch (masterElement.SoundEffectType)
                {
                    case MapObjectMasterElement.SoundEffectTypeConst.tree:
                        treePoints.Add(point);
                        break;
                    case MapObjectMasterElement.SoundEffectTypeConst.stone:
                        objectPoints.Add(point);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"[MapObjectPointSplitter] MapObject {masterElement.MapObjectName} declares an unknown soundEffectType {masterElement.SoundEffectType}.");
                }
            }
        }
    }
}
