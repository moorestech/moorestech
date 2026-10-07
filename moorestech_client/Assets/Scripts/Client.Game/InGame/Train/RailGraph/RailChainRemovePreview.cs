using Client.Game.InGame.UI.UIState.State.RemovePreview;
using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール1本の赤プレビューを要求者ごとに数える
    ///     Counts red-preview requesters per rail; red on first, reset on last
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
            if (_requests.Add(requester)) _chain.SetRemovePreviewing();
        }

        public void ReleaseRemovePreview(object requester)
        {
            if (_requests.Remove(requester)) _chain.ResetMaterial();
        }
    }
}
