// 採掘ホールド中に進捗バーの論理状態が立つことを確かめるスモーク
// Smoke check that the logical progress-bar state turns on while a mining hold is in progress
using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var stoneVeinGuid = new Guid("735633b7-7aac-4fb8-8b42-022f6bfb9e53");
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("mining-progress-bar-smoke", options, async p =>
{
    // 開幕スキットは全UI入力を塞ぐため、再生されていれば飛ばす
    // The opening skit blocks every UI input, so skip it when it plays
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 20f, "GameScreenに到達");

    p.Note("石の斧を装備枠1に装着する");
    p.GiveItemDirect("石の斧", 1);
    await p.WaitSeconds(1f);
    await p.EquipItem("石の斧", 0);

    var datastore = UnityEngine.Object.FindFirstObjectByType<OutcropGameObjectDatastore>();
    var outcrop = datastore.SearchNearestOutcrop(stoneVeinGuid, p.PlayerPosition);
    p.Assert(outcrop != null, "最寄りの石鉱脈露頭が見つかった");
    var outcropCollider = outcrop.GetComponentInChildren<Collider>(true);
    p.Assert(outcropCollider != null, "石鉱脈露頭に採掘用Colliderがある");

    await p.Until(() => Camera.main != null, 10f, "MainCameraの起動");
    var cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;
    p.WarpPlayer(outcrop.transform.position - cameraForward * 1.4f + Vector3.up * 0.5f);
    await p.WaitSeconds(1f);
    await p.AimAt(outcropCollider.bounds.center);

    var progressBar = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ProgressBarState>();
    p.Assert(!progressBar.IsShown, "採掘前は進捗バーが非表示");

    p.Note("Fを保持して石露頭を掘り、進捗バーの表示と進捗更新を確かめる");
    var shown = false;
    var progressed = false;
    SemanticInput.KeyDown(Key.F);
    var deadline = Time.realtimeSinceStartup + 6f;
    while (Time.realtimeSinceStartup < deadline)
    {
        if (progressBar.IsShown) shown = true;
        if (shown && 0f < progressBar.CurrentProgress) progressed = true;
        if (shown && progressed) break;
        await UniTask.Yield();
    }
    await p.Screenshot("01-mining-progress");
    SemanticInput.KeyUp(Key.F);

    p.Assert(shown, "採掘ホールド中に進捗バーが表示された");
    p.Assert(progressed, "採掘ホールド中に進捗が0より大きくなった");

    await p.Until(() => !progressBar.IsShown, 10f, "採掘終了で進捗バーが消える");
});
