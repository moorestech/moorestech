using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Map.Outcrop;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse.MapData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// 鉱脈の露頭が全vein分だけAABB中心へ立つことと、設置プレビュー範囲表示が再入しても溜まらず消えることを実機検証する
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies that one outcrop per vein sits at its AABB center and that the placement range view never accumulates or lingers.
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class MapVeinOutcropAndRangeViewTest
    {
        // 本番の毎フレーム駆動を模して範囲表示をこのフレーム数だけ連続で叩く
        // Mimic production's per-frame driving by hitting the range view for this many consecutive frames
        private const int DrivenFrameCount = 3;

        [UnityTest]
        public IEnumerator OutcropsSitAtVeinAabbCenterAndRangeViewIsReleasedOnEveryPreviewExit()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var veinLayouts = resolver.Resolve<InitialHandshakeResponse>().MapLayout.MapVeins;

                // veinが0件のワールドでは以降の検証が全て素通りしてしまうので先に落とす
                // With zero veins every later assertion would pass vacuously, so fail here first
                Assert.IsNotEmpty(veinLayouts, "test world has no mapVeins");

                await AssertOutcropsSitAtVeinAabbCenter(veinLayouts);
                await AssertRangeViewAppearsAndIsReleased(resolver.Resolve<IMapVeinRangeView>(), resolver.Resolve<MapVeinAabbRegistry>());
            }

            async UniTask AssertOutcropsSitAtVeinAabbCenter(IReadOnlyList<VeinLayoutMessagePack> veinLayouts)
            {
                var datastore = Object.FindFirstObjectByType<OutcropGameObjectDatastore>(FindObjectsInactive.Include);
                Assert.IsNotNull(datastore, "OutcropGameObjectDatastore was not found in scene");

                // 初期化と同じawait経路を通し、生成例外がfire-and-forgetへ逃げないことも固定する
                // Use the same await path as startup, also pinning that generation exceptions cannot escape into fire-and-forget
                await datastore.WaitForInitialApplyAsync();
                Assert.AreEqual(veinLayouts.Count, datastore.transform.childCount, "outcrop count does not match the vein count");

                foreach (var layout in veinLayouts)
                {
                    // veinGuid名でひもづけ、どのveinの露頭がどこに立ったかを個別に突き合わせる
                    // Match by the veinGuid-based name so each vein's outcrop is checked against its own vein
                    var outcrop = datastore.transform.Find($"{OutcropGameObjectDatastore.OutcropObjectNamePrefix}{layout.VeinGuid}");
                    Assert.IsNotNull(outcrop, $"outcrop for veinGuid {layout.VeinGuid} was not instantiated");

                    // AABB中心XYZに立っていること。min/maxの取り違えやvein同士の取り違えはここで落ちる
                    // It sits at the AABB center XYZ; a min/max mix-up or a vein mix-up fails here
                    Assert.AreEqual((layout.MinX + layout.MaxX + 1) * 0.5f, outcrop.position.x, 0.001f, $"outcrop X for {layout.VeinGuid}");
                    Assert.AreEqual((layout.MinZ + layout.MaxZ + 1) * 0.5f, outcrop.position.z, 0.001f, $"outcrop Z for {layout.VeinGuid}");

                    // 地表Raycastを持たない裁定どおり、Yも地形ではなくAABB中心で決まる
                    // Per the ruling that drops the ground raycast, Y is decided by the AABB center too, not by terrain
                    Assert.AreEqual((layout.MinY + layout.MaxY + 1) * 0.5f, outcrop.position.y, 0.001f, $"outcrop Y for {layout.VeinGuid}");

                    // 索引配線そのものを通す。veinGuid別バケット→索引焼き込みが欠落すると恒久nullで落ちる
                    // Exercises the index wiring itself; a missing guid-bucket→index bake would fail here with a permanent null
                    var veinGuid = new Guid(layout.VeinGuid);
                    var nearestOutcrop = datastore.SearchNearestOutcrop(veinGuid, outcrop.position);
                    Assert.IsNotNull(nearestOutcrop, $"SearchNearestOutcrop returned null for veinGuid {layout.VeinGuid}");
                    Assert.AreEqual(outcrop.position, nearestOutcrop.transform.position, $"SearchNearestOutcrop resolved a different outcrop for veinGuid {layout.VeinGuid}");
                }

                // 別veinGuidの近い露頭を誤って返さないこと。veinGuid別バケットのキー分離まで死ぬmutation
                // Must not return a nearby outcrop belonging to a different veinGuid; kills mutations that collapse the per-veinGuid key separation
                if (1 < veinLayouts.Count)
                {
                    var first = veinLayouts[0];
                    var second = veinLayouts[1];
                    var firstOutcropPosition = datastore.transform
                        .Find($"{OutcropGameObjectDatastore.OutcropObjectNamePrefix}{first.VeinGuid}").position;

                    var resolvedForSecondGuid = datastore.SearchNearestOutcrop(new Guid(second.VeinGuid), firstOutcropPosition);
                    if (resolvedForSecondGuid != null)
                        Assert.AreNotEqual(firstOutcropPosition, resolvedForSecondGuid.transform.position,
                            $"SearchNearestOutcrop for veinGuid {second.VeinGuid} returned an outcrop belonging to veinGuid {first.VeinGuid}");
                }
            }

            async UniTask AssertRangeViewAppearsAndIsReleased(IMapVeinRangeView rangeView, MapVeinAabbRegistry veinAabbRegistry)
            {
                // アイテム鉱脈だけを表示対象にする。採掘機を持っている時の本番と同じ絞り込み
                // Target item veins only, the same filtering production does while holding a miner
                var itemVeins = new List<MapVeinAabb>();
                foreach (var vein in veinAabbRegistry.Veins)
                    if (vein.Kind == MapVeinKind.Item) itemVeins.Add(vein);

                // item veinが0件だと以降の検証が全て素通りしてしまうので先に落とす
                // With zero item veins every later assertion would pass vacuously, so fail here first
                Assert.IsNotEmpty(itemVeins, "test world has no item mapVeins");

                // 範囲表示はカメラ周辺のveinだけを対象にするので、駆動の直前にカメラを鉱脈群の中心へ置く
                // The range view only targets veins near the camera, so park the camera at the vein cluster right before driving it
                var nearVeins = AverageVeinCenter(itemVeins);
                var farAway = nearVeins + Vector3.right * 100000f;

                // ②採掘機の設置開始で範囲表示が現れ、item vein1本につき1個だけになる
                // (2) Starting a miner placement makes the range view appear, exactly one box per item vein
                await DriveRangeViewFrames(rangeView, nearVeins, VeinDisplay.OfVeins(itemVeins, false));
                Assert.AreEqual(itemVeins.Count, await CountVisibleRangeViewObjects(), "range view object count does not match the item vein count while previewing");

                // プール込みの総数を基準に取る。以降これが増えたら表示のたびに作り直している
                // Snapshot the pooled total as a baseline; any later growth means boxes are rebuilt on every show
                var pooledTotalBaseline = FindRangeViewRoot().childCount;

                // ③プレビュー終了で表示が消える
                // (3) Ending the preview hides them all
                await DriveRangeViewFrames(rangeView, nearVeins, VeinDisplay.Hidden);
                Assert.AreEqual(0, await CountVisibleRangeViewObjects(), "range view objects survived the preview exit");

                // ④開始と終了を3回繰り返しても1本1個のまま。二重表示も消し漏れもここで落ちる
                // (4) Three show/hide cycles keep one box per vein; both duplication and missed hides fail here
                for (var i = 0; i < 3; i++)
                {
                    await DriveRangeViewFrames(rangeView, nearVeins, VeinDisplay.OfVeins(itemVeins, false));
                    Assert.AreEqual(itemVeins.Count, await CountVisibleRangeViewObjects(), $"range view object count changed on cycle {i}");
                    Assert.AreEqual(pooledTotalBaseline, FindRangeViewRoot().childCount, $"range view boxes accumulated on cycle {i}");

                    await DriveRangeViewFrames(rangeView, nearVeins, VeinDisplay.Hidden);
                    Assert.AreEqual(0, await CountVisibleRangeViewObjects(), $"range view objects survived cycle {i}");
                }

                // プレビュー中でも遠ざかれば消える。表示条件がプレビュー有無だけに退化していないことを見る
                // Moving far away clears them even while previewing, proving the visibility rule is not just the preview flag
                await DriveRangeViewFrames(rangeView, nearVeins, VeinDisplay.OfVeins(itemVeins, false));
                Assert.AreEqual(itemVeins.Count, await CountVisibleRangeViewObjects(), "range view did not reappear near the veins");
                await DriveRangeViewFrames(rangeView, farAway, VeinDisplay.OfVeins(itemVeins, false));
                Assert.AreEqual(0, await CountVisibleRangeViewObjects(), "range view survived while the camera was far from every vein");
            }

            async UniTask DriveRangeViewFrames(IMapVeinRangeView rangeView, Vector3 cameraPosition, VeinDisplay display)
            {
                // 表示状態は本番同様、設置対象の変化時に1回だけプッシュする
                // Push the display state once on a target change, exactly as production does
                rangeView.SetVeinDisplay(display);

                // 本番はPlaceBlockStateが毎フレーム駆動する。1フレームだけ叩くと毎フレーム再生成の欠陥を見逃す
                // Production drives this every frame from PlaceBlockState; a single call would hide a per-frame regeneration defect
                for (var frame = 0; frame < DrivenFrameCount; frame++)
                {
                    // カメラ追従に上書きされる前に、同フレーム内で位置を置いてから駆動する
                    // Place the camera and drive within the same frame, before the camera follower overwrites it
                    Camera.main.transform.position = cameraPosition;
                    rangeView.ManualUpdate();
                    await UniTask.Yield();
                }
            }

            Vector3 AverageVeinCenter(IReadOnlyList<MapVeinAabb> veins)
            {
                var sum = Vector3.zero;
                foreach (var vein in veins) sum += vein.Bounds.center;
                return sum / veins.Count;
            }

            async UniTask<int> CountVisibleRangeViewObjects()
            {
                // 非表示ボックスは破棄せずプールへ戻るので、活性なものだけを数える
                // Hidden boxes are parked in a pool instead of destroyed, so count only the active ones
                await UniTask.Yield();

                var visibleCount = 0;
                foreach (Transform child in FindRangeViewRoot())
                    if (child.gameObject.activeSelf) visibleCount++;
                return visibleCount;
            }

            Transform FindRangeViewRoot()
            {
                var root = GameObject.Find(MapVeinRangeViewService.RootObjectName);
                Assert.IsNotNull(root, "MapVeinRangeViewRoot was not found in scene");
                return root.transform;
            }

            #endregion
        }
    }
}
