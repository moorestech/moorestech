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
    /// BP・配線ゴーストと不足素材を録画用に読む
    /// Read blueprint and wire ghosts, shortages and costs for recordings.
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
                // 可否色を持つ描画材質だけを読む
                // Read only renderer materials carrying judgement colors.
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
            // ブロック・配線の欠落フレームを集計
            // Count frames missing block or line ghosts.
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
            // 有効メッシュを持つ配線だけ数える
            // Count active lines with a rendering mesh.
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
            // 回転0・重複なしで財布込み素材計算
            // Calculate wallet-aware costs at zero rotation without overlaps.
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
