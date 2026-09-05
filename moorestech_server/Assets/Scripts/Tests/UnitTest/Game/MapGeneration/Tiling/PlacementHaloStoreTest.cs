using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    public class PlacementHaloStoreTest
    {
        [Test]
        public void 確定鉱脈snapshotは現在タイルの候補AABBへ届く履歴だけを返す()
        {
            var store = new PlacementHaloStore(10f);
            var placement = new VeinPlacementBatch();

            // タイルはX[100,150) Z[220,290) の非対称。X側とZ側の座標が一致しないので軸の取り違えが露見する。
            // The tile spans X[100,150) and Z[220,290); no X coordinate equals a Z one, so swapping the axes shows up.
            placement.Veins.Add(CreateVein("touch-left", new Vector3Int(97, 0, 230), new Vector3Int(99, 2, 232)));
            placement.Veins.Add(CreateVein("touch-right", new Vector3Int(150, 0, 240), new Vector3Int(152, 2, 242)));
            placement.Veins.Add(CreateVein("touch-lower-z", new Vector3Int(120, 0, 217), new Vector3Int(122, 2, 219)));
            placement.Veins.Add(CreateVein("touch-upper-z", new Vector3Int(120, 0, 290), new Vector3Int(122, 2, 292)));
            placement.Veins.Add(CreateVein("inside", new Vector3Int(120, 1000, 250), new Vector3Int(122, 1002, 252)));
            placement.Veins.Add(CreateVein("far-left", new Vector3Int(96, 0, 230), new Vector3Int(98, 2, 232)));
            placement.Veins.Add(CreateVein("far-right", new Vector3Int(151, 0, 240), new Vector3Int(153, 2, 242)));
            placement.Veins.Add(CreateVein("far-lower-z", new Vector3Int(120, 0, 216), new Vector3Int(122, 2, 218)));
            placement.Veins.Add(CreateVein("far-upper-z", new Vector3Int(120, 0, 291), new Vector3Int(122, 2, 293)));
            store.CommitVeins(store.ItemVeins, placement);

            // 候補範囲はXZの原点と幅だけで決まるので、他の寸法は0でよい。
            // The candidate bounds follow from the XZ origin and span alone, so the remaining dimensions can be zero.
            var dims = new TerrainDimensions(
                50f, 70f, 0f, 100f, 220f,
                0, 0, 0f, 0f, 0, 0f, 0f, 0, 0, 1, 1);
            var snapshot = store.CreateConfirmedVeinSnapshot(TileCandidateAabbBounds.From(dims));

            // snapshot は台帳の登録順を保つので順序ごと固定する。
            // The snapshot preserves the ledger's insertion order, so the order is pinned too.
            CollectionAssert.AreEqual(
                new[] { "touch-left", "touch-right", "touch-lower-z", "touch-upper-z", "inside" },
                snapshot.Select(vein => vein.VeinGuid));

            #region Internal

            PlacedVein CreateVein(string veinGuid, Vector3Int min, Vector3Int max)
            {
                return new PlacedVein(veinGuid, min, max);
            }

            #endregion
        }
    }
}
