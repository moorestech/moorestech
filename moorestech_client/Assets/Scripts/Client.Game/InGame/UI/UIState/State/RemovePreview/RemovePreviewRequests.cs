using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState.State.RemovePreview
{
    /// <summary>
    ///     1つの表示対象に赤プレビューを求めている要求者の集合。自分のホバー・選択と、撤去ブロックの巻き込み表示が同時に求め得る
    ///     Set of requesters wanting the red preview on one display target; own hover/selection and a removed block's cascade may request at once
    /// </summary>
    public class RemovePreviewRequests
    {
        private readonly HashSet<object> _requesters = new();

        // 最初の要求でだけtrue（赤を付ける合図）
        // True only on the first request (signal to apply red)
        public bool Add(object requester)
        {
            return _requesters.Add(requester) && _requesters.Count == 1;
        }

        // 最後の要求が外れたときだけtrue（赤を戻す合図）
        // True only when the last request is released (signal to reset)
        public bool Remove(object requester)
        {
            return _requesters.Remove(requester) && _requesters.Count == 0;
        }
    }
}
