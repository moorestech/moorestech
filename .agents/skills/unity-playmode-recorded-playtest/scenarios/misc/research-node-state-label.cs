// 研究ノードカードの状態ラベル（完了済み/研究可能/研究不可）の実表示を撮影する
// Capture the research node card state label (Completed/Available/Unavailable) live
using System;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var research1 = new Guid("837e9697-8586-406e-a0f6-16a010050218");
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
    p.Note("研究1を完了させ、完了済み/研究可能/研究不可の3状態を作る");
    api.CompleteResearch(research1);
    await p.WaitSeconds(2f);
    p.Note("Rキーで研究ツリーを開き、カードの状態ラベルを撮影する");
    await p.PressKey(Key.R);
    await p.Until(() => p.CurrentUiState == UIStateEnum.ResearchTree, 10f, "研究ツリーへ遷移");
    await p.WaitSeconds(3f);
    await p.Screenshot("01_research_tree_state_labels");
    p.Assert(p.CurrentUiState == UIStateEnum.ResearchTree, "研究ツリー状態へ遷移した");
});
