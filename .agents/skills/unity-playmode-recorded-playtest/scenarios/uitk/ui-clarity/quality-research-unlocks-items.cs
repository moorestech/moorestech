// 出荷マスタで品質モジュールLv1〜5 の研究を完了扱いにし、速度モジュールの研究をせずに ICチップLv2〜5・シリコンダイLv2〜5・品質モジュールLv1〜5 がクライアントで解放済みになることと、後から速度モジュールLv1〜5 を済ませても解放の状態が変わらず例外も出ないことを確かめる
// On the shipped master, complete quality module Lv1-5 research and confirm IC chip Lv2-5, silicon die Lv2-5 and quality module Lv1-5 unlock on the client without speed module research, then that completing speed module Lv1-5 afterwards keeps them unlocked without exceptions
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Research;
using Game.UnlockState;
using UnityEngine;
using VContainer;

var qualityResearch = new[]
{
    "01a167f3-bd6b-70c9-be6e-52e41907b21f", "01a173a4-cfe2-769c-9b35-eabb655f9a8f", "01a173a4-cfe2-769c-9b35-eabc81931e86", "01a173a4-cfe2-769c-9b35-eabdf0cceabb", "01a173a4-cfe2-769c-9b35-eabe85cfb575",
}.Select(Guid.Parse).ToArray();
var speedResearch = new[]
{
    "019e3ae2-5005-7439-b242-9b7b3191a2c3", "01a19cf1-4842-7064-82b6-5a6df2e2d583", "01a19cf1-484d-7116-99a9-6808c43f6199", "01a19cf1-4858-722c-8a40-a2ca854edee9", "01a19cf1-4861-70b0-90d8-7d6af161a8ab",
}.Select(Guid.Parse).ToArray();
// 品質モジュール研究が解放する 13 品目（シリコンダイLv2〜5・ICチップLv2〜5・品質モジュールLv1〜5）
// The 13 items the quality module research unlocks (silicon die Lv2-5, IC chip Lv2-5, quality module Lv1-5)
var unlockedItems = new[]
{
    "01a17356-2aff-701f-9a1c-174e55eb20f8", "01a17356-2aff-701f-9a1c-174f781ba191", "01a17356-2aff-701f-9a1c-1750b8ac3222", "01a17356-2aff-701f-9a1c-175178dfda4c",
    "878ea106-3689-4bcd-8d9a-e18b0e03072c", "d403eb63-f913-456f-842d-41980cdfc0bc", "2ef1886a-30b0-4792-8c20-0f39c370443a", "01a17356-2aff-701f-9a1c-17524cddb030",
    "01a167f3-bd96-73a2-a0ce-06ba261e109d", "01a17370-c063-7479-84da-2d07d02e2953", "01a17370-c063-7479-84da-2d0837ecdcb1", "01a17370-c063-7479-84da-2d09a793b11a", "01a17370-c063-7479-84da-2d0a09b79551",
}.Select(Guid.Parse).ToArray();
// 速度モジュールLv1 だけが解放する機械レシピ（速度モジュールLv1 の製作）。2 回目の段がクライアントへ届いた目印に使う
// The machine recipe only speed module Lv1 unlocks; marks that the second pass reached the client
var speedOnlyMachineRecipe = Guid.Parse("01a167f3-bd8b-7732-badd-8c1a011f7c26");

return PlaytestRunner.Run("quality-research-unlocks-items", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 120f }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");

    // 前提の閉包に速度モジュールの研究が入らないことを先に確かめる（入ると速度側の解放で通ってしまう）
    // First confirm the prerequisite closure contains no speed module research (otherwise the speed-side unlock would pass the check)
    p.Note("品質モジュール研究で13品目が解放されることを確かめる");
    var done = WithPrerequisites(qualityResearch).ToList();
    p.Assert(!done.Intersect(speedResearch).Any(), "品質モジュールLv1〜5 の前提に速度モジュールLv1〜5 が入っていない");
    var clientUnlock = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
    var itemIds = unlockedItems.Select(guid => MasterHolder.ItemMaster.GetItemId(guid)).ToArray();
    p.Assert(itemIds.All(id => !clientUnlock.ItemUnlockStateInfos[id].IsUnlocked), "研究の前は 13 品目が未解放");
    p.Assert(!clientUnlock.MachineRecipeUnlockStateInfos[speedOnlyMachineRecipe].IsUnlocked, "研究の前は速度モジュールLv1 の機械レシピが未解放");

    // 品質モジュールの研究を前提ごと完了扱いにする（解放アクションも再実行される。前例 tier9-2-modules）
    // Mark the quality module research done with prerequisites (unlock actions are replayed; precedent tier9-2-modules)
    await p.RunOnServer("品質モジュールLv1〜5 を前提ごと完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = done.Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(qualityResearch, speedResearch, 15f, "品質モジュールLv1〜5 の研究が完了し、速度モジュールLv1〜5 は未完了");
    await p.Until(() => itemIds.All(id => clientUnlock.ItemUnlockStateInfos[id].IsUnlocked), 15f, "ICチップ・シリコンダイ・品質モジュールの 13 品目がクライアントで解放済み");
    await p.WaitSeconds(0.5f);
    await p.Screenshot("quality-research-unlocks-items");

    // 速度モジュールLv1〜5 も前提ごと完了扱いにする。LoadResearchData は完了一覧を置き換えるので、品質側と合わせた閉包を渡す
    // Also mark speed module Lv1-5 done with prerequisites; LoadResearchData replaces the completed list, so pass the closure including the quality side
    var exceptions = new List<string>();
    Application.LogCallback onLog = (condition, _, type) => { if (type == LogType.Exception) exceptions.Add(condition); };
    p.Note("速度モジュール研究を追加し、13品目の解放状態を再確認する");
    Application.logMessageReceived += onLog;
    try
    {
        var doneWithSpeed = WithPrerequisites(qualityResearch.Concat(speedResearch)).ToList();
        await p.RunOnServer("速度モジュールLv1〜5 も前提ごと完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = doneWithSpeed.Select(g => g.ToString()).ToList() }));
        await p.WaitForResearch(qualityResearch.Concat(speedResearch).ToArray(), Array.Empty<Guid>(), 15f, "速度モジュールLv1〜5 の研究も完了");

        // 13 品目の再解放は Task E6 で通知が出ないので、速度側だけの機械レシピの解放がクライアントに届くのを待ってから見る（通知の回数は E6 の NUnit が見る）
        // Re-unlocking the 13 items sends nothing after Task E6, so wait for the speed-only machine recipe unlock to reach the client before checking (notification counts are E6's NUnit)
        await p.Until(() => clientUnlock.MachineRecipeUnlockStateInfos[speedOnlyMachineRecipe].IsUnlocked, 15f, "速度モジュールLv1 の機械レシピがクライアントで解放済み");
    }
    finally
    {
        Application.logMessageReceived -= onLog;
    }

    p.Assert(itemIds.All(id => clientUnlock.ItemUnlockStateInfos[id].IsUnlocked), "速度モジュールLv1〜5 の後も 13 品目は解放済みのまま");
    p.Assert(exceptions.Count == 0, $"速度モジュールLv1〜5 の重複解放で例外が出ない: {string.Join(" / ", exceptions)}");
    await p.WaitSeconds(0.5f);
    await p.Screenshot("quality-then-speed-research");

    #region Internal

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>(targets);
        while (pending.TryPop(out var guid))
        {
            if (visited.Add(guid))
            {
                foreach (var prerequisite in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids)
                    pending.Push(prerequisite);
            }
        }
        return visited;
    }

    #endregion
});
