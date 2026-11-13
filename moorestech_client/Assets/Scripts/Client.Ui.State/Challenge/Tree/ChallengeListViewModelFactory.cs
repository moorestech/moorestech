using System;
using System.Collections.Generic;
using Client.Network.API;
using Client.Ui.State.Challenge.Tree.Selection;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// 応答の列から一覧の表示を組む（規則は Web の ChallengeDtoBuilder.BuildTree と同じ）
    /// Builds the list display from responses (same rules as Web ChallengeDtoBuilder.BuildTree)
    /// </summary>
    public static class ChallengeListViewModelFactory
    {
        public static ChallengeListViewModel Create(IReadOnlyList<ChallengeCategoryResponse> responses, ChallengeCategorySelection selection)
        {
            // 解放済みカテゴリを受信順に写す
            // Copy unlocked categories in received order
            var categories = new List<ChallengeCategoryViewModel>();
            foreach (var response in responses)
            {
                // silent-ok: 未解放のカテゴリを出さないのは Web の橋と同じ仕様 / L0 ChallengeListViewModelFactoryTest が固定する
                if (!response.IsUnlocked) continue;
                categories.Add(ChallengeCategoryViewModel.Create(response.Category.CategoryGuid, BuildNodes(response)));
            }
            return ChallengeListViewModel.Create(categories, selection);

            #region Internal

            // マスタ順に状態・座標・倍率・前提を写す
            // Copy state, position, scale and prerequisites in master order
            List<ChallengeNodeCardViewModel> BuildNodes(ChallengeCategoryResponse response)
            {
                var completed = new HashSet<Guid>();
                foreach (var challenge in response.CompletedChallenges) completed.Add(challenge.ChallengeGuid);
                var current = new HashSet<Guid>();
                foreach (var challenge in response.CurrentChallenges) current.Add(challenge.ChallengeGuid);

                var nodes = new List<ChallengeNodeCardViewModel>();
                foreach (var challenge in response.Category.Challenges)
                {
                    var guid = challenge.ChallengeGuid;
                    var state = completed.Contains(guid) ? ChallengeNodeState.Completed : current.Contains(guid) ? ChallengeNodeState.Current : ChallengeNodeState.Locked;
                    var param = challenge.DisplayListParam;
                    var position = new Vector2(param.UIPosition.x, param.UIPosition.y);
                    var scale = new Vector2(param.UIScale.x, param.UIScale.y);

                    // prevChallengeGuids はスキーマで optional で、省略は null（マスタ JSON は外部データ）。前提なしとして空にする（ChallengeMaster.GetCategoryInitialChallenges と同じ扱い）
                    // prevChallengeGuids is optional in the schema and null when omitted (master JSON is external data); treat it as no prerequisites (same as ChallengeMaster.GetCategoryInitialChallenges)
                    // silent-ok: 省略は前提なしというマスタの仕様 / 線が無いことは並べ比べで見え、L0 が前提の写しを固定する
                    var previous = challenge.PrevChallengeGuids ?? Array.Empty<Guid>();
                    nodes.Add(new ChallengeNodeCardViewModel(guid, position, scale, previous, state));
                }
                return nodes;
            }

            #endregion
        }
    }
}
