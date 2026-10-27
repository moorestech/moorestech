// Tier8 終盤のセーブ（版6）が統合版でロードでき、コスト0で完了していた Tier9 研究とその解放が外れ、他の進行が残ることを確かめる
// Confirm the late-Tier8 save loads on the integrated build, zero-cost Tier9 research and its unlocks are reset, and other progress remains
// 固定world入口で起動する（PLAYTEST_WORLD_DIRECTORY=セーブの複製 / PLAYTEST_MAP_MODE=template / PLAYTEST_SEED=0）。ロードで原本の版が Backups へ退避されるので原本は渡さない
// Boot through the fixed-world entry (PLAYTEST_WORLD_DIRECTORY=a copy of the save / PLAYTEST_MAP_MODE=template / PLAYTEST_SEED=0); loading moves the original version into Backups, so never pass the original
using System;
using System.Linq;
using Client.Playtest;
using Core.Master;
using Game.Research;
using Game.UnlockState;
using UnityEngine;

var resetResearch = new[]
{
    "019e3aab-32c6-7166-a184-d761153c2498", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "019e3af5-f2e6-76de-a80f-5e66e79fa35d",
    "019e3af0-297f-726e-8a02-0a8518124e6d", "019e3aef-a149-746b-8fad-159e0434cb51", "01a16291-4094-7369-a87f-5dfe2197d25e",
}.Select(Guid.Parse).ToArray();
var resetBlocks = new[] { "01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a162aa-cec0-738c-93c5-f58adc87fa7f", "01a162b0-7769-7419-af24-bb35528b93d2" }.Select(Guid.Parse).ToArray();
// 元の完了38件から統合マスタ（744e2f10）に無い0件を引いた値。大きく減ったら移行の事故として止める
// The original 38 completions minus the 0 absent from the integrated master (744e2f10); a large drop stops it as a migration accident
var minimumKeptResearch = 38;
var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("tier9-0-tier8-save-smoke", options, async p =>
{
    // ロード直後の研究と解放を読み、リセットの効果と他の進行の残りを確かめる
    // Read research and unlocks right after loading to check the reset and that other progress remains
    await p.SkipOpeningSkitIfPlaying();
    var research = p.ServerService<IResearchDataStore>();
    var completed = MasterHolder.ResearchMaster.GetAllResearches().Count(r => research.IsResearchCompleted(r.ResearchNodeGuid));
    p.Note($"完了済みの研究 {completed} 件");
    foreach (var guid in resetResearch) p.Assert(!research.IsResearchCompleted(guid), $"リセット対象 {guid} は未完了");
    var unlocks = p.ServerService<IGameUnlockStateDataController>();
    foreach (var guid in resetBlocks) p.Assert(!unlocks.BlockUnlockStateInfos.TryGetValue(guid, out var info) || !info.IsUnlocked, $"本番ブロック {guid} は未解放");
    p.Assert(completed >= minimumKeptResearch, $"Tier8 までの研究が残っている（{completed} 件）");
    await p.Screenshot("01-loaded-tier8-save");
});
