// 研究ノードカードの状態ラベル（完了済み/研究可能/研究不可）の実表示を撮影する
// Capture the research node card state label (Completed/Available/Unavailable) live
using System;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var research1 = new Guid("837e9697-8586-406e-a0f6-16a010050218");
var research2 = new Guid("424be8c1-c40c-4644-8104-06934c59b147");
var research3 = new Guid("07d6226c-ed14-4a6f-aa2a-6fa085fce8ec");
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("research-node-state-label", options, async p =>
{
    var api = Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly;
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    for (var i = 0; i < 10; i++)
    {
        var s = skitStore.GetCurrent();
        if (skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok) break;
        await p.WaitSeconds(1f);
    }
    // 研究1=完了済み、研究2=素材充足で研究可能、研究3=前提未達で研究不可、の3状態を同時に作る
    // Build all three states at once: research1 completed, research2 researchable with items, research3 blocked by its prerequisite
    p.Note("研究1を完了させ、研究2の消費アイテムを付与して3状態を作る");
    api.CompleteResearch(research1);
    p.GiveItemDirect("木の板", 2);
    p.GiveItemDirect("木の棒", 2);
    p.GiveItemDirect("砕いた石材", 2);
    await p.WaitSeconds(2f);
    p.Note("Rキーで研究ツリーを開く");
    await p.PressKey(Key.R);
    await p.Until(() => p.CurrentUiState == UIStateEnum.ResearchTree, 10f, "研究ツリーへ遷移");

    // 3状態が実際にWeb UIへ描かれるまで待ってから撮る。出ていなければ落ちるので画像が証跡として機能する
    // Wait until all three labels actually render before capturing, so a missing state fails instead of yielding a hollow screenshot
    await AssertStateLabel(research1, "完了済み");
    await AssertStateLabel(research2, "研究可能");
    await AssertStateLabel(research3, "研究不可");
    await p.Screenshot("01_research_tree_state_labels");
    p.Assert(p.CurrentUiState == UIStateEnum.ResearchTree, "研究ツリー状態へ遷移した");

    #region Internal

    async UniTask AssertStateLabel(Guid researchGuid, string expectedLabel)
    {
        var dom = await Client.Playtest.WebUi.PlaytestDomQuery.Query($"research-node-state-{researchGuid}", 10f);
        p.Note($"[Web UI DOM] {researchGuid} found={dom.Found} text={dom.Text}");
        p.Assert(dom.Found && dom.Text == expectedLabel, $"{researchGuid} の状態ラベルが「{expectedLabel}」で描画されている");
    }

    #endregion
});
