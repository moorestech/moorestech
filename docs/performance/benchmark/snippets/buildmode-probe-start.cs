// 設置モードの出入りを PlayerLoop の中で予定どおりに起こし、遷移の同期時間（Stopwatch）と前後のフレーム時間を記録する
// スニペットの実行中は Editor が止まって受信イベントが溜まるので、実行してから SETTLE フレーム待ってから最初の遷移を起こす
// 遷移は本番と同じ RequestTransition → UIStateControl.Update（反射で即時に呼ぶ）で起こし、その呼び出しだけを Stopwatch で囲む
// 遷移の前後の数フレームだけ Profiler を記録し、遷移の呼び出しは Profiler.BeginSample("Probe.*Transition") で囲む（buildmode-probe-collect.cs が読む）
// Drive build-mode enter/exit on a schedule from inside the PlayerLoop, recording the synchronous transition time (Stopwatch) and surrounding frame times
// Running a snippet stalls the Editor and queues incoming events, so the first transition fires SETTLE frames after this snippet returns
// The transition uses the production path RequestTransition -> UIStateControl.Update (invoked right away via reflection); only that call is timed
// The Profiler records only a few frames around each transition, and the call is wrapped in Profiler.BeginSample("Probe.*Transition") for buildmode-probe-collect.cs
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.LowLevel;
using VContainer;

const int Settle = 200;        // 最初の遷移までのフレーム数（約8秒） / frames before the first transition (~8 s)
const int Half = 75;           // 入ってから出るまで・出てから次に入るまで（約3秒） / frames between enter and exit, and exit and next enter (~3 s)
const int Cycles = 4;          // 出入りの回数。最後にもう1回入って設置モードのまま終える / enter/exit cycles; one more enter at the end leaves build mode on
const int ProfileBefore = 3, ProfileAfter = 10;

var probeKey = "moorestech.buildmodeProbe";
if (System.AppDomain.CurrentDomain.GetData(probeKey) != null) return "FAIL probe already installed; run buildmode-probe-collect.cs first";

var control = Object.FindFirstObjectByType<UIStateControl>();
if (control == null) return "FAIL UIStateControl not found (not in game)";
if (control.CurrentState != UIStateEnum.GameScreen) return "FAIL current state is " + control.CurrentState + ", expected GameScreen";
var updateMethod = typeof(UIStateControl).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
var blockId = MasterHolder.BlockMaster.GetBlockAllIds()[0];
var target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, null);
ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlaceSystemStateController>().SetTarget(target, PlacementOrigin.NonHotbar);

// 予定表：frame → 入る(true)／出る(false)
// Schedule: frame -> enter (true) / exit (false)
var schedule = new SortedDictionary<int, bool>();
for (var c = 0; c < Cycles; c++) { schedule[Settle + c * 2 * Half] = true; schedule[Settle + c * 2 * Half + Half] = false; }
var finalEnter = Settle + Cycles * 2 * Half;
schedule[finalEnter] = true;
var lastFrame = finalEnter + Half;

var state = new Dictionary<string, object>();
var frameStart = new List<double>();
var events = new List<string>();
state["frameStart"] = frameStart; state["events"] = events; state["done"] = false; state["error"] = "";
state["startFrameCount"] = Time.frameCount;
System.AppDomain.CurrentDomain.SetData(probeKey, state);

ProfilerDriver.ClearAllFrames();
ProfilerDriver.profileEditor = false;
ProfilerDriver.deepProfiling = false;
ProfilerDriver.enabled = false;

var frame = 0;
PlayerLoopSystem.UpdateFunction tick = () =>
{
    if ((bool)state["done"]) return;
    // 計測用の境界。例外で PlayerLoop を壊さないよう、ここで捕まえて結果に残す
    // Measurement boundary: catch so an exception cannot break the PlayerLoop, and keep it in the result
    try
    {
        frameStart.Add(Time.realtimeSinceStartupAsDouble);
        var profileOn = schedule.Keys.Any(k => frame >= k - ProfileBefore && frame <= k + ProfileAfter);
        if (ProfilerDriver.enabled != profileOn) ProfilerDriver.enabled = profileOn;
        if (schedule.TryGetValue(frame, out var enter))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            UnityEngine.Profiling.Profiler.BeginSample(enter ? "Probe.EnterTransition" : "Probe.ExitTransition");
            control.RequestTransition(enter ? UIStateEnum.PlaceBlock : UIStateEnum.GameScreen);
            updateMethod.Invoke(control, null);
            UnityEngine.Profiling.Profiler.EndSample();
            sw.Stop();
            events.Add((enter ? "enter" : "exit") + "@" + frame + "=" + sw.Elapsed.TotalMilliseconds.ToString("F2") + "ms state=" + control.CurrentState);
        }
        if (frame >= lastFrame) { ProfilerDriver.enabled = false; state["done"] = true; }
        frame++;
    }
    catch (System.Exception e)
    {
        state["error"] = e.ToString();
        state["done"] = true;
        ProfilerDriver.enabled = false;
    }
};

// Update フェーズの先頭（MonoBehaviour.Update より前）に差し込む。外すのは buildmode-probe-collect.cs
// Insert at the head of the Update phase (before MonoBehaviour.Update); buildmode-probe-collect.cs removes it
var loop = PlayerLoop.GetCurrentPlayerLoop();
var subs = loop.subSystemList;
var updateIndex = System.Array.FindIndex(subs, s => s.type == typeof(UnityEngine.PlayerLoop.Update));
var probeSystem = new PlayerLoopSystem { type = typeof(System.Diagnostics.Stopwatch), updateDelegate = tick };
var updateSubs = subs[updateIndex].subSystemList.ToList();
updateSubs.Insert(0, probeSystem);
subs[updateIndex].subSystemList = updateSubs.ToArray();
loop.subSystemList = subs;
PlayerLoop.SetPlayerLoop(loop);
return "probe installed frame=" + Time.frameCount + " lastFrame=" + lastFrame;
