using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // pass-1からpass-2へ渡す配置台帳
    // The ledger carried from pass-1 (placement) to pass-2 (visuals)
    public class PlacementLedger
    {
        // 浮動小数はTerrainVisualCacheKeyと同じ往復可能な"R"で書く。桁を落とすと1cmずれた配置を同じ台帳とみなす
        // Floats use the same round-trippable "R" as TerrainVisualCacheKey; truncating digits would treat a placement shifted by 1cm as the same ledger
        private const string RoundTripFormat = "R";

        // クラスタ無しは空欄で表す。IDの有無そのものが岩の裸地の畳み方を変えるので鍵に効かせる
        // "No cluster" is written as a blank; the mere presence of an id changes how the rocks' bare ground folds, so it must reach the key
        private const string NoClusterMark = "";

        private readonly List<LedgerPlacement> _placements = new();
        public IReadOnlyList<LedgerPlacement> Placements => _placements;

        public void Add(LedgerPlacement placement)
        {
            _placements.Add(placement);
        }

        // 見た目キャッシュ鍵へ混ぜる台帳の指紋。配置が1件でも動けば別の鍵になり、古いsplat/detailを引かない
        // The ledger fingerprint mixed into the visual cache key; one moved placement yields another key and never draws stale splat/detail
        public string ComputeDigest()
        {
            // 見た目は台帳の並びに依らない。昇順に揃えて順序非依存にし、配置ステージの都合で並びが動いても鍵を揺らさない
            // The visuals do not depend on the ledger's order, so sorting makes the digest order-independent and keeps stage-driven reordering out of the key
            var placementTexts = new List<string>(_placements.Count);
            foreach (var ledgerPlacement in _placements) placementTexts.Add(Describe(ledgerPlacement));
            placementTexts.Sort(StringComparer.Ordinal);

            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", placementTexts)));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

            #region Internal

            // 見た目ステージが読む項目だけを写す。重心はIDから決まるので、IDが同じなら重心も同じ
            // Copies only what the visual stages read; a centroid follows from its id, so an equal id means an equal centroid
            string Describe(LedgerPlacement placement)
            {
                return string.Join("|",
                    placement.Guid,
                    Format(placement.ScenePosition.x), Format(placement.ScenePosition.y), Format(placement.ScenePosition.z),
                    Format(placement.Scale.x), Format(placement.Scale.y), Format(placement.Scale.z),
                    placement.SurroundEffect.ToString(),
                    placement.Cluster.HasValue ? placement.Cluster.Value.Id.ToString(CultureInfo.InvariantCulture) : NoClusterMark);
            }

            string Format(float value)
            {
                return value.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
            }

            #endregion
        }
    }
}
