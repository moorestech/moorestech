// デバッグ用フライモードの録画検証（ADR 0075）: トグルオフでは4連打で飛ばない→オンで4連打→E上昇→ホバー→W+Shiftで高速水平移動→戻る→2連打で解除して足場へ落下
// Recorded check of the debug fly mode (ADR 0075): no flight while the toggle is off, then enter with 4 taps, rise with E, hover, fast horizontal flight, return, and leave with 2 taps to fall onto the scaffold
using Client.Game;
using Client.Playtest;
using Client.Playtest.Input;
using Common.Debug;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("debug-fly-mode", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    await UniTask.Delay(1000, ignoreTimeScale: true);
    var groundY = p.PlayerPosition.y;

    // 1: トグルオフでは4連打しても飛ばない
    // 1: Four taps do nothing while the toggle is off
    p.Note("トグルオフで4連打しても飛ばないことを確認する");
    DebugParameters.SaveBool(DebugConst.FlyModeKey, false);
    for (var i = 0; i < 4; i++) { SemanticInput.KeyDown(Key.Space); await UniTask.DelayFrame(1); SemanticInput.KeyUp(Key.Space); await UniTask.DelayFrame(1); }
    SemanticInput.KeyDown(Key.E); await UniTask.Delay(1000, ignoreTimeScale: true); SemanticInput.KeyUp(Key.E);
    await UniTask.Delay(1500, ignoreTimeScale: true);
    p.Assert(Mathf.Abs(p.PlayerPosition.y - groundY) < 0.5f, $"トグルオフでは地上のまま y={p.PlayerPosition.y} ground={groundY}");

    // 2: トグルオンで4連打→Eで上昇（期待15m/s）
    // 2: Toggle on, four taps, then rise with E (expected 15 m/s)
    p.Note("トグルオンで4連打してフライに入り、Eで1秒上昇する");
    DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
    await UniTask.Delay(1500, ignoreTimeScale: true);
    for (var i = 0; i < 4; i++) { SemanticInput.KeyDown(Key.Space); await UniTask.DelayFrame(1); SemanticInput.KeyUp(Key.Space); await UniTask.DelayFrame(1); }
    var startY = p.PlayerPosition.y;
    SemanticInput.KeyDown(Key.E); await UniTask.Delay(1000, ignoreTimeScale: true); SemanticInput.KeyUp(Key.E);
    p.Assert(p.PlayerPosition.y - startY > 10f, $"E 1秒で10m以上上昇（期待15m） rise={p.PlayerPosition.y - startY}");
    await p.Screenshot("01-flying-up");

    // 3: 入力なしでホバーする（重力が止まっている）
    // 3: Hover without input (gravity is off)
    var hoverY = p.PlayerPosition.y;
    await UniTask.Delay(1000, ignoreTimeScale: true);
    p.Assert(Mathf.Abs(p.PlayerPosition.y - hoverY) < 0.5f, $"入力なしで高度を保つ drift={p.PlayerPosition.y - hoverY}");

    // 4: W+Shiftで高速水平移動（期待45m/s）→S+Shiftで戻る
    // 4: Fast horizontal flight with W+Shift (expected 45 m/s), then come back with S+Shift
    p.Note("W+Shiftで1秒水平移動し、S+Shiftで戻る");
    var before = p.PlayerPosition;
    SemanticInput.KeyDown(Key.LeftShift); SemanticInput.KeyDown(Key.W);
    await UniTask.Delay(1000, ignoreTimeScale: true);
    SemanticInput.KeyUp(Key.W);
    var moved = Vector2.Distance(new Vector2(before.x, before.z), new Vector2(p.PlayerPosition.x, p.PlayerPosition.z));
    p.Assert(moved > 30f, $"W+Shift 1秒で30m以上（期待45m） moved={moved}");
    await p.Screenshot("02-flying-fast");
    SemanticInput.KeyDown(Key.S);
    await UniTask.Delay(1000, ignoreTimeScale: true);
    SemanticInput.KeyUp(Key.S); SemanticInput.KeyUp(Key.LeftShift);

    // 5: 2連打で解除し、足場へ落下する
    // 5: Leave with two taps and fall onto the scaffold
    p.Note("スペース2連打で解除し、足場へ落下する");
    for (var i = 0; i < 2; i++) { SemanticInput.KeyDown(Key.Space); await UniTask.DelayFrame(1); SemanticInput.KeyUp(Key.Space); await UniTask.DelayFrame(1); }
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - groundY) < 0.5f, 10f, "解除後に足場の高さへ戻る");
    await p.Screenshot("03-landed");

    DebugParameters.SaveBool(DebugConst.FlyModeKey, false);
});
