using Client.Game.InGame.UI.UIState.State.RemovePreview;
using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール1本の赤プレビューを要求者ごとに数える。赤の唯一の書き手
    ///     Counts red-preview requesters per rail; the sole writer of the red material
    /// </summary>
    public class RailChainRemovePreview : MonoBehaviour, IRemovePreviewable
    {
        private readonly RemovePreviewRequests _requests = new();
        private BezierRailChain _chain;

        // チェーンのGameObjectに1つだけ実行時付与
        // Attach one per chain GameObject at runtime, leaving the prefab untouched
        public static RailChainRemovePreview Of(BezierRailChain chain)
        {
            var preview = chain.GetComponent<RailChainRemovePreview>();
            if (preview != null) return preview;

            preview = chain.gameObject.AddComponent<RailChainRemovePreview>();
            preview._chain = chain;
            return preview;
        }

        public void RequestRemovePreview(object requester)
        {
            if (_requests.Add(requester)) ApplyRed();
        }

        public void ReleaseRemovePreview(object requester)
        {
            if (_requests.Remove(requester)) ResetRed();
        }

        // 材質の作り直し（Rebuild・設置アニメ終了）後、要求者が残っていれば赤を当て直す
        // Re-apply red after the materials are rebuilt (Rebuild, end of place animation) while requesters remain
        public void Reapply()
        {
            if (_requests.HasRequesters) ApplyRed();
        }

        private void ApplyRed()
        {
            if (_chain.IsRemoving)
            {
                Debug.Log("[BezierRailChain] preview skipped: rail is removing");
                return;
            }
            _chain.SetRemovePreviewing();
        }

        // 撤去アニメの材質を通常色で上書きしない（ApplyRed と対称）
        // Do not overwrite the removal animation's material with the normal one (symmetric with ApplyRed)
        private void ResetRed()
        {
            if (_chain.IsRemoving)
            {
                Debug.Log("[BezierRailChain] reset skipped: rail is removing");
                return;
            }
            _chain.ResetMaterial();
        }
    }
}
