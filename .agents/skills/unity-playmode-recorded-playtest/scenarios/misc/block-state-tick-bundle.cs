using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Playtest;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Server.Event.EventReceive;
using UniRx;
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
    await PlacePipeAndCheckWater(origin, 100f, "1回目");

    // 撤去して同じ座標に置き直しても状態が届く
    // State still arrives after removing and re-placing at the same position
    p.RemoveBlock(origin);
    await p.Until(() => p.GetBlock(origin) == null, 10f, "パイプ撤去");
    await p.WaitSeconds(1f);
    await PlacePipeAndCheckWater(origin, 50f, "再設置後");

    p.Assert(bundleCount > 0, $"束イベントを受信した count={bundleCount}");
    p.Note($"[BUNDLE] received={bundleCount}");
    bundleSubscription.Dispose();

    #region Internal
    async UniTask PlacePipeAndCheckWater(Vector3Int pos, float amount, string label)
    {
        var block = p.PlaceBlockDirect("鉄のパイプ", pos, BlockDirection.East);
        var view = await p.WaitBlockGameObject(pos);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, $"{label} 設置アニメ完了");
        var changedCount = 0;
        var changedSubscription = view.OnBlockStateChanged.Subscribe(_ => changedCount++);
        var processor = view.GetComponentInChildren<FluidPipeStateChangeProcessor>();
        var water = MasterHolder.FluidMaster.GetFluidId(new System.Guid("9eae6979-d56a-4991-9107-b8161acec430"));
        block.GetComponent<FluidPipeComponent>().Node.AddExternal(new FluidStack(amount, water));
        await p.Until(() => processor.DebugCurrentFluidPipeState != null && processor.DebugCurrentFluidPipeState.Amount == amount, 15f, $"{label} 水位受信 {amount}");
        p.Assert(changedCount > 0, $"{label} OnBlockStateChangedが発火 count={changedCount}");
        await p.Screenshot($"water-{label}");
        changedSubscription.Dispose();
    }
    #endregion
});
