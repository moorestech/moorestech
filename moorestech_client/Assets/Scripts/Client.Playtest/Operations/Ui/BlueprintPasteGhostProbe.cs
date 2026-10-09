using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Blueprint;
using Game.Context;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.Construction;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     BP貼り付けゴースト・配線ゴースト・不足行・必要素材を録画シナリオから読む
    ///     Reads paste ghosts, wire ghosts, shortage lines and required materials for recorded scenarios
    /// </summary>
    public class BlueprintPasteGhostProbe
    {
        private readonly MouseCursorTooltipState _tooltip;

        public BlueprintPasteGhostProbe(MouseCursorTooltipState tooltip)
        {
            _tooltip = tooltip;
        }

        public List<BlueprintPasteGhostSnapshot> ActiveGhosts()
        {
            var result = new List<BlueprintPasteGhostSnapshot>();
            var root = GameObject.Find("BlueprintPastePreview");
            if (root == null) return result;
            foreach (Transform child in root.transform)
            {
                if (child.gameObject.activeSelf) result.Add(new BlueprintPasteGhostSnapshot(child.position, ReadPreviewColor(child.gameObject)));
            }
            return result;

            #region Internal

            Color ReadPreviewColor(GameObject ghost)
            {
                // 置換済みマテリアルの可否色を読む（可否色を持たないレンダラーは飛ばす）
                // Read the placeability color from replaced materials, skipping renderers without it
                foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.HasProperty(MaterialConst.PreviewColorPropertyName)) return material.GetColor(MaterialConst.PreviewColorPropertyName);
                }
                return Color.clear;
            }

            #endregion
        }

        public bool AreAllGhostsColored(int count, bool placeable)
        {
            var ghosts = ActiveGhosts();
            return ghosts.Count == count && ghosts.All(g => placeable ? IsPlaceableColor(g.Color) : IsNotPlaceableColor(g.Color));
        }

        public async UniTask<int> CountFramesMissingGhosts(int ghostCount, int wireCount, int frames)
        {
            // 連続フレームでゴーストか配線ゴーストが欠けたフレーム数を数える
            // Count consecutive frames where any block or wire ghost is missing
            var missing = 0;
            for (var frame = 0; frame < frames; frame++)
            {
                await UniTask.DelayFrame(1);
                if (ActiveGhosts().Count != ghostCount || ActiveWireLineCount() != wireCount) missing++;
            }
            return missing;
        }

        public static bool IsPlaceableColor(Color color) => Approximately(color, MaterialConst.PlaceableColor);
        public static bool IsNotPlaceableColor(Color color) => Approximately(color, MaterialConst.NotPlaceableColor);

        public static Vector3 MinPosition(IReadOnlyList<BlueprintPasteGhostSnapshot> ghosts)
        {
            return ghosts.Aggregate(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), (min, ghost) => Vector3.Min(min, ghost.Position));
        }

        public int ActiveWireLineCount()
        {
            // 配線ゴーストは描画用メッシュを持つ有効な線だけ数える
            // Count only active wire ghosts that carry a drawable mesh
            var root = GameObject.Find("BlueprintPasteLines");
            if (root == null) return 0;
            var count = 0;
            foreach (Transform child in root.transform)
            {
                if (child.name != "PreviewWireLine" || !child.gameObject.activeInHierarchy) continue;
                var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                if (mesh != null && 0 < mesh.vertexCount) count++;
            }
            return count;
        }

        public List<IReadOnlyList<string>> MaterialShortageLines()
        {
            return _tooltip.GetPresentation().Lines
                .Where(line => line.Key.Key == LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key)
                .Select(line => line.TextParams).ToList();
        }

        public static List<(ItemId itemId, int count)> RequiredItems(BlueprintJsonObject blueprint, int copies)
        {
            // ビルドメニューと同じく回転0・重なりなしのBPを財布込みで数える
            // Count unobstructed rotation-0 copies through the wallet, as the build menu does
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var wallet = ServerContext.GetService<ConstructionWalletService>().GetQuery(playerId);
            var drafts = Enumerable.Range(0, copies).Select(_ => BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint)).ToArray();
            return BlueprintPasteCostCalculator.CalcRequiredItems(drafts, wallet, false);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }

    public readonly struct BlueprintPasteGhostSnapshot
    {
        public readonly Vector3 Position;
        public readonly Color Color;

        public BlueprintPasteGhostSnapshot(Vector3 position, Color color)
        {
            Position = position;
            Color = color;
        }
    }
}
