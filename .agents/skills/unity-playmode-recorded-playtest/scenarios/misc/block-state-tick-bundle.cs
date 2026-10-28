using Client.Game.InGame.Context;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

return PlaytestRunner.Run("block-state-tick-bundle", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    // 開幕スキットは画面を塞ぐので先に飛ばす
    // Skip the opening skit first since it covers the screen
    await p.SkipOpeningSkit();

    // 束イベントの受信回数を数える
    // Count received bundle events
    var bundleCount = 0;
    var bundleSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag, _ => bundleCount++);

    var origin = new Vector3Int(3, 32, 3);
    await PlaceGearAndCheckState(origin, "1回目");

    // 撤去して同じ座標に置き直しても状態が届く
    // State still arrives after removing and re-placing at the same position
    p.RemoveBlock(origin);
    await p.Until(() => p.GetBlock(origin) == null, 10f, "歯車撤去");
    await p.WaitSeconds(1f);
    await PlaceGearAndCheckState(origin, "再設置後");

    p.Assert(bundleCount > 0, $"束イベントを受信した count={bundleCount}");
    p.Note($"[BUNDLE] received={bundleCount}");
    bundleSubscription.Dispose();

    #region Internal
    async UniTask PlaceGearAndCheckState(Vector3Int pos, string label)
    {
        // 新しいBlockGameObjectは状態を持たず、束の振り分けでのみ歯車状態が入る
        // A fresh BlockGameObject holds no state; the gear state only arrives through bundle routing
        p.PlaceBlockDirect("鉄の歯車", pos, BlockDirection.East);
        var view = await p.WaitBlockGameObject(pos);
        await p.Until(() => view.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey) != null, 15f, $"{label} 歯車状態受信");
        await p.Screenshot($"gear-{label}");
    }
    #endregion
});
