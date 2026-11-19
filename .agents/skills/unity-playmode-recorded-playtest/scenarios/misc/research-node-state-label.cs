// 研究ノードカードの状態ラベル（完了済み/研究可能/研究不可）の実表示を撮影する。研究はマスタから選ぶ（根→その子→孫）
// Capture the research node card state label (Completed/Available/Unavailable) live; nodes are picked from the master (root -> child -> grandchild)
using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.ResearchModule;
using UnityEngine.InputSystem;

return PlaytestRunner.Run("research-node-state-label", new PlaytestRunOptions { Record = true }, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 根（前提なし）・前提が根だけの子・前提に子を含む孫をマスタから選ぶ
    // Pick a root (no prerequisites), a child requiring only the root, and a grandchild requiring the child
    var all = MasterHolder.ResearchMaster.GetAllResearches();
    var (research1, research2, research3) = PickChain();
    p.Note($"完了させる根 {research1.ResearchNodeGuid}／研究可能にする子 {research2.ResearchNodeGuid}／前提未達の孫 {research3.ResearchNodeGuid}");

    // 根を素材付きで完了させ、子の素材を持たせて 3 状態を同時に作る
    // Complete the root with its cost and hand over the child's cost to build all three states at once
    await GiveConsumeItems(research1);
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.CompleteResearch(research1.ResearchNodeGuid);
    await GiveConsumeItems(research2);
    await p.WaitSeconds(2f);

    // R で研究ツリーを開く
    // Open the research tree with R
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.UntilUiElement("research-tree", 5f);

    // 3 状態が実際に UITK へ描かれるまで待ってから撮る。出ていなければ落ちるので画像が証跡になる
    // Wait until all three labels render before capturing, so a missing state fails instead of yielding a hollow screenshot
    await AssertStateLabel(research1.ResearchNodeGuid, Localize.Get(LocalizationKeys.Ui.Research.Completed));
    await AssertStateLabel(research2.ResearchNodeGuid, Localize.Get(LocalizationKeys.Ui.Research.StateAvailable));
    await AssertStateLabel(research3.ResearchNodeGuid, Localize.Get(LocalizationKeys.Ui.Research.StateUnavailable));
    await p.Screenshot("01_research_tree_state_labels");
    p.Assert(p.CurrentUiState == UIStateEnum.ResearchTree, "研究ツリー状態へ遷移した");

    #region Internal

    (ResearchNodeMasterElement, ResearchNodeMasterElement, ResearchNodeMasterElement) PickChain()
    {
        foreach (var root in all.Where(r => r.PrevResearchNodeGuids.Length == 0 && r.ConsumeItems.Length > 0))
        foreach (var child in all.Where(r => r.PrevResearchNodeGuids.Length == 1 && r.PrevResearchNodeGuids[0] == root.ResearchNodeGuid && r.ConsumeItems.Length > 0))
        {
            var grandchild = all.FirstOrDefault(r => r.PrevResearchNodeGuids.Contains(child.ResearchNodeGuid));
            if (grandchild != null) return (root, child, grandchild);
        }
        throw new InvalidOperationException("根→子→孫の研究の連鎖がマスタに無い");
    }

    async UniTask GiveConsumeItems(ResearchNodeMasterElement node)
    {
        foreach (var consume in node.ConsumeItems)
        {
            var itemName = MasterHolder.ItemMaster.GetItemMaster(consume.ItemGuid).Name;
            var maxStack = Core.Item.ItemStackLevelDataStore.Instance.GetMaxStack(PlaytestItemOps.ResolveItemId(itemName));
            for (var given = 0; given < consume.ItemCount; given += maxStack) await p.GiveItem(itemName, Math.Min(maxStack, consume.ItemCount - given));
        }
    }

    async UniTask AssertStateLabel(Guid researchGuid, string expectedLabel)
    {
        var dom = await PlaytestUiElementOps.FindUi($"research-node-state-{researchGuid}");
        p.Note($"[UITK 要素] {researchGuid} found={dom.IsFound} text={dom.Text}");
        p.Assert(dom.IsFound && dom.Text == expectedLabel, $"{researchGuid} の状態ラベルが「{expectedLabel}」で描画されている");
    }

    #endregion
});
