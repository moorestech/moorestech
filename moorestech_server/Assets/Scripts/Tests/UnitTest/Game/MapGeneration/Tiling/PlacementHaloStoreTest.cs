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

            // 四辺の接触を残し遠隔履歴を除く。
            // Keeps touching history on every edge while excluding history one cell farther away.
            placement.Veins.Add(CreateVein("touch-left", new Vector3Int(97, 0, 110), new Vector3Int(99, 2, 112)));
            placement.Veins.Add(CreateVein("touch-right", new Vector3Int(150, 0, 120), new Vector3Int(152, 2, 122)));
            placement.Veins.Add(CreateVein("touch-lower-z", new Vector3Int(120, 0, 97), new Vector3Int(122, 2, 99)));
            placement.Veins.Add(CreateVein("touch-upper-z", new Vector3Int(120, 0, 150), new Vector3Int(122, 2, 152)));
            placement.Veins.Add(CreateVein("inside", new Vector3Int(120, 1000, 130), new Vector3Int(122, 1002, 132)));
            placement.Veins.Add(CreateVein("far-left", new Vector3Int(96, 0, 110), new Vector3Int(98, 2, 112)));
            placement.Veins.Add(CreateVein("far-right", new Vector3Int(151, 0, 120), new Vector3Int(153, 2, 122)));
            placement.Veins.Add(CreateVein("far-lower-z", new Vector3Int(120, 0, 96), new Vector3Int(122, 2, 98)));
            placement.Veins.Add(CreateVein("far-upper-z", new Vector3Int(120, 0, 151), new Vector3Int(122, 2, 153)));
            store.CommitItemVeins(placement);

            var snapshot = store.CreateConfirmedVeinSnapshot(100f, 100f, 50f, 50f);

            CollectionAssert.AreEquivalent(
                new[] { "touch-left", "touch-right", "touch-lower-z", "touch-upper-z", "inside" },
                snapshot.Select(vein => vein.VeinGuid));

            #region Internal

            PlacedVein CreateVein(string veinGuid, Vector3Int min, Vector3Int max)
            {
                return new PlacedVein
                {
                    VeinGuid = veinGuid,
                    Min = min,
                    Max = max,
                };
            }

            #endregion
        }
    }
}
