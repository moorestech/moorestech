// ラダー5 uitk-research-train: 研究を1つ完了→駅名を日本語で付ける（SetText）→時刻表を組んで自動運転を入れる（UITKだけ）
// Ladder 5 uitk-research-train: finish one research -> name a station in Japanese (SetText) -> build a timetable and turn auto-run on (UITK only)
// winunity play <branch> <このファイル> --ui-system GameUi で流す（全体スイッチは winunity が立てて Current を確かめる）
// Run with winunity play <branch> <this file> --ui-system GameUi (winunity sets the whole-UI switch and checks Current)
using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.WebUiHost.Boot;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-research-train", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 裏タブの Game View は描画されず timeScale=0 のままスキットの Skip 送りが止まるので表へ出す（前例 uitk-opening-hud）
    // A Game View behind a tab never renders and skit skip stalls at timeScale=0, so bring it to front (precedent uitk-opening-hud)
    PlaytestGameViewOps.BringToFront();
    await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 重い機械では1回の Skip で開幕スキットが終わらないことがあるので、Story のままなら飛ばし直し回数を Note に残す（前例 uitk-production-loop）
    // On a loaded machine one skip may not end the opening skit, so skip again while in Story and note the rounds (precedent uitk-production-loop)
    var skitSkipRounds = 1;
    await p.SkipOpeningSkitIfPlaying();
    while (skitSkipRounds < 4 && !await PlaytestUiOps.PollUiState(UIStateEnum.GameScreen, 5f)) { skitSkipRounds++; await p.SkipOpeningSkitIfPlaying(); }
    p.Note($"opening-skit-skip-rounds={skitSkipRounds}");
    await p.ExitToGameScreen();
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが GameUi（UITK側）");
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（UITK側）");

    // 研究素材を渡し、研究ツリーで原始研究1を完了する
    // Give research materials and finish primitive research 1 in the tree
    var research1 = Guid.Parse("858bcb10-b8ba-478e-9bc5-473ca61281a2");
    await p.GiveItem("木の板", 3);
    await p.GiveItem("木の棒", 3);
    await p.GiveItem("砕いた石材", 2);
    p.Note("研究ツリー→原始研究1→研究する");
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.ClickUi($"research-node-{research1}");
    await p.ClickUi($"research-button-{research1}");
    await p.WaitForResearch(new[] { research1 }, Array.Empty<Guid>(), 15f, "原始研究1が完了する（サーバー）");
    await p.Screenshot("01-research-done");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 駅 2 基と機関車の線を準備する（サーバー直。駅名は付けない）
    // Prepare two stations and a locomotive server-side (no names yet)
    await p.SetupFlatGround();
    var line = await PlaytestTrainLine.Build(p, new Vector3Int(10, 32, 4), new Vector3Int(10, 32, 64));

    // 駅 B を開き、駅名を日本語で入れて確定する
    // Open station B, type a Japanese name and confirm
    p.Note("駅名を日本語で付ける");
    await PlaytestTrainLine.OpenStation(p, line.StationBPosition);
    await p.SetText("train-station-name-input", "南駅");
    await p.ClickUi("train-station-name-apply");
    await p.WaitForStationName(line.StationB, "南駅", 10f, "サーバーの駅名が南駅になる");
    await p.Screenshot("02-station-named");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 機関車を開き、時刻表に 2 駅を足して自動運転を入れる
    // Open the locomotive, add both stations to the timetable and turn auto-run on
    p.Note("時刻表を組んで自動運転");
    await PlaytestTrainLine.OpenCar(p);
    await p.ClickUi("train-tab-timetable");
    await p.UntilUiElement("train-timetable-section", 10f);
    var a = line.StationAPosition;
    var b = line.StationBPosition;
    await p.ClickUi($"train-timetable-station-{a.x}_{a.y}_{a.z}-add");
    await p.WaitForTrainState(line.Train, 1, null, null, 10f, "サーバーの時刻表が1駅になる");
    await p.Until(() => ClientTimetableStopCount() == 1, 10f, "クライアントの時刻表が1駅になる（保留が解ける）");
    await p.ClickUi($"train-timetable-station-{b.x}_{b.y}_{b.z}-add");
    await p.WaitForTrainState(line.Train, 2, null, null, 10f, "サーバーの時刻表が2駅になる");
    await p.Until(() => ClientTimetableStopCount() == 2, 10f, "クライアントの時刻表が2駅になる（保留が解ける）");
    await p.UntilUiElement("train-timetable-stop-1", 5f);
    await p.ClickUi("train-timetable-auto-run-on");
    await p.WaitForTrainState(line.Train, null, true, null, 10f, "サーバーの自動運転がON");
    await p.Screenshot("03-timetable");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.WaitForTrainState(line.Train, null, null, true, 15f, "列車が走り出す");
    await p.WaitSeconds(2f);
    await p.Screenshot("04-running");

    #region Internal

    // クライアントの時刻表の停車駅数。公開が届くと保留が解ける（前例 train-timetable-via-ui。保留の印のクラスは I-fix6 R-10 で消える）
    // Stop count of the client timetable; the hold clears when the publish arrives (as train-timetable-via-ui; the hold class goes away in I-fix6 R-10)
    int ClientTimetableStopCount() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IClientTrainTimetableLookup>().TryGet(line.Train.TrainUnitInstanceId, out var snapshot) ? snapshot.Stops.Count : 0;

    #endregion
});
