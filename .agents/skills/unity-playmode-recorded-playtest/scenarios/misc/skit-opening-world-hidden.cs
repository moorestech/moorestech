// 開幕スキットの世界非表示カットで露頭がmapObjectと一緒に消えることを実走確認する
// Verify at runtime that outcrops hide together with map objects during the opening skit's world-hidden cut
using Client.Game.InGame.Map.Outcrop;
using Client.Playtest;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("skit-opening-world-hidden", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 検証対象が開幕スキットそのものなのでSkipOpeningSkitは呼ばない
    // The opening skit itself is under test, so SkipOpeningSkit is deliberately not called
    p.Note("開幕スキットをオート送りで宇宙カットまで進める");

    var skitStore = SkitPresentationStateStore.Instance;
    await p.Until(() => skitStore.GetCurrent().PresentationState.Mode != "none", 60f, "スキット開始待ち");

    var started = skitStore.GetCurrent();
    skitStore.TrySetAuto(started.SessionId, started.SceneRevision, true);

    var outcropDatastore = UnityEngine.Object.FindFirstObjectByType<OutcropGameObjectDatastore>(FindObjectsInactive.Include);
    p.Assert(outcropDatastore != null, "露頭datastoreがシーンに存在する");

    // 露頭0体だと非表示assertが素通りするので、実体があることを先に固定する
    // With zero outcrops the hide assert would pass vacuously, so pin down that they actually exist
    p.Assert(outcropDatastore.transform.childCount > 0, "露頭が1体以上生成されている");

    // オートが効かない場合に備え、poll毎にAdvanceインテントも打つ
    // Also fire an advance intent on every poll in case auto mode does not take
    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        skitStore.TryAdvance(current.SessionId, current.SceneRevision);
        return !outcropDatastore.gameObject.activeSelf;
    }, 180f, "世界非表示カットで露頭rootが非表示になる");

    await p.Screenshot("01-space-cut-without-outcrop");

    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        skitStore.TryAdvance(current.SessionId, current.SceneRevision);
        return outcropDatastore.gameObject.activeSelf;
    }, 180f, "復帰カットで露頭rootが再表示される");

    await p.Screenshot("02-world-restored-with-outcrop");
});
