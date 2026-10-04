using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール1本の赤プレビューを要求者ごとに数え、最初の要求で赤く・最後の解除で戻す（電線・チェーンと同じ規則）
    ///     Counts red-preview requesters for one rail; reddens on the first request and resets on the last release (same rule as wires/chains)
    /// </summary>
    public class RailChainRemovePreview : MonoBehaviour, IRemovePreviewable
    {
        private readonly RemovePreviewRequests _requests = new();
        private BezierRailChain _chain;

        // チェーンのGameObjectに1つだけ付ける（プレハブを変えずに済むよう実行時に付与）
        // Attach exactly one per chain GameObject (added at runtime so the prefab stays untouched)
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
            if (_requests.Add(requester)) _chain.SetRemovePreviewing();
        }

        public void ReleaseRemovePreview(object requester)
        {
            if (_requests.Remove(requester)) _chain.ResetMaterial();
        }
    }
}
