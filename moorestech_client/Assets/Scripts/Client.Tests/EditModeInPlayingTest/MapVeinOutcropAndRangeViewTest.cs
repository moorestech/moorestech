using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapVein;
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
    /// 鉱脈の露頭が全vein分だけ地表に立つことと、設置プレビュー範囲表示が再入しても溜まらず消えることを実機検証する
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies that one outcrop per vein stands on the surface and that the placement range view never accumulates or lingers.
    /// </summary>
    public class MapVeinOutcropAndRangeViewTest
    {
        // 露頭が地表に接しているとみなす許容差。真上から短いレイを落として接地を確かめる
        // Tolerance for treating an outcrop as standing on the surface, checked by a short ray dropped from just above it
        private const float GroundContactTolerance = 0.05f;

        // 本番の毎フレーム駆動を模して範囲表示をこのフレーム数だけ連続で叩く
        // Mimic production's per-frame driving by hitting the range view for this many consecutive frames
        private const int DrivenFrameCount = 3;

        [UnityTest]
        public IEnumerator OutcropsStandOnSurfaceAndRangeViewIsReleasedOnEveryPreviewExit()
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

                await AssertOutcropsStandOnSurface(veinLayouts);
                await AssertRangeViewAppearsAndIsReleased(resolver.Resolve<IMapVeinRangeView>(), veinLayouts);
            }

            async UniTask AssertOutcropsStandOnSurface(IReadOnlyList<VeinLayoutMessagePack> veinLayouts)
            {
                var datastore = Object.FindFirstObjectByType<MapVeinObjectDatastore>(FindObjectsInactive.Include);
                Assert.IsNotNull(datastore, "MapVeinObjectDatastore was not found in scene");

                // 初期化と同じawait経路を通し、生成例外がfire-and-forgetへ逃げないことも固定する
                // Use the same await path as startup, also pinning that generation exceptions cannot escape into fire-and-forget
                await datastore.WaitForInitializationAsync();
                Assert.AreEqual(veinLayouts.Count, datastore.transform.childCount, "outcrop count does not match the vein count");

                foreach (var layout in veinLayouts)
                {
                    // veinGuid名でひもづけ、どのveinの露頭がどこに立ったかを個別に突き合わせる
                    // Match by the veinGuid-based name so each vein's outcrop is checked against its own vein
                    var outcrop = datastore.transform.Find($"{MapVeinObjectDatastore.OutcropObjectNamePrefix}{layout.VeinGuid}");
                    Assert.IsNotNull(outcrop, $"outcrop for veinGuid {layout.VeinGuid} was not instantiated");

                    // ①AABB中心XZに立っていること。min/maxの取り違えやvein同士の取り違えはここで落ちる
                    // (1) It stands at the AABB center XZ; a min/max mix-up or a vein mix-up fails here
                    Assert.AreEqual((layout.MinX + layout.MaxX + 1) * 0.5f, outcrop.position.x, 0.001f, $"outcrop X for {layout.VeinGuid}");
                    Assert.AreEqual((layout.MinZ + layout.MaxZ + 1) * 0.5f, outcrop.position.z, 0.001f, $"outcrop Z for {layout.VeinGuid}");

                    // 真上の短いレイで接地を確かめる。Y=0や鉱脈Yへのフォールバックはここで落ちる
                    // A short ray from just above confirms ground contact; a Y=0 or vein-Y fallback fails here
                    var probeOrigin = outcrop.position + Vector3.up;
                    var isHit = Physics.Raycast(probeOrigin, Vector3.down, out var hit, 2f);
                    Assert.IsTrue(isHit && hit.transform.TryGetComponent<GroundGameObject>(out _), $"outcrop for {layout.VeinGuid} is not standing on ground");
                    Assert.AreEqual(outcrop.position.y, hit.point.y, GroundContactTolerance, $"outcrop Y for {layout.VeinGuid} is off the surface");
                }
            }

            async UniTask AssertRangeViewAppearsAndIsReleased(IMapVeinRangeView rangeView, IReadOnlyList<VeinLayoutMessagePack> veinLayouts)
            {
                // 範囲表示はカメラ周辺のveinだけを対象にするので、駆動の直前にカメラを鉱脈群の中心へ置く
                // The range view only targets veins near the camera, so park the camera at the vein cluster right before driving it
                var nearVeins = AverageVeinCenter(veinLayouts);
                var farAway = nearVeins + Vector3.right * 100000f;

                // ②プレビュー開始で範囲表示が現れ、vein1本につき1個だけになる
                // (2) Starting a preview makes the range view appear, exactly one box per vein
                await DriveRangeViewFrames(rangeView, nearVeins, true);
                Assert.AreEqual(veinLayouts.Count, await CountVisibleRangeViewObjects(), "range view object count does not match the vein count while previewing");

                // プール込みの総数を基準に取る。以降これが増えたら表示のたびに作り直している
                // Snapshot the pooled total as a baseline; any later growth means boxes are rebuilt on every show
                var pooledTotalBaseline = FindRangeViewRoot().childCount;

                // ③プレビュー終了で表示が消える
                // (3) Ending the preview hides them all
                await DriveRangeViewFrames(rangeView, nearVeins, false);
                Assert.AreEqual(0, await CountVisibleRangeViewObjects(), "range view objects survived the preview exit");

                // ④開始と終了を3回繰り返しても1本1個のまま。二重表示も消し漏れもここで落ちる
                // (4) Three show/hide cycles keep one box per vein; both duplication and missed hides fail here
                for (var i = 0; i < 3; i++)
                {
                    await DriveRangeViewFrames(rangeView, nearVeins, true);
                    Assert.AreEqual(veinLayouts.Count, await CountVisibleRangeViewObjects(), $"range view object count changed on cycle {i}");
                    Assert.AreEqual(pooledTotalBaseline, FindRangeViewRoot().childCount, $"range view boxes accumulated on cycle {i}");

                    await DriveRangeViewFrames(rangeView, nearVeins, false);
                    Assert.AreEqual(0, await CountVisibleRangeViewObjects(), $"range view objects survived cycle {i}");
                }

                // プレビュー中でも遠ざかれば消える。表示条件がプレビュー有無だけに退化していないことを見る
                // Moving far away clears them even while previewing, proving the visibility rule is not just the preview flag
                await DriveRangeViewFrames(rangeView, nearVeins, true);
                Assert.AreEqual(veinLayouts.Count, await CountVisibleRangeViewObjects(), "range view did not reappear near the veins");
                await DriveRangeViewFrames(rangeView, farAway, true);
                Assert.AreEqual(0, await CountVisibleRangeViewObjects(), "range view survived while the camera was far from every vein");
            }

            async UniTask DriveRangeViewFrames(IMapVeinRangeView rangeView, Vector3 cameraPosition, bool isPreviewing)
            {
                // 表示状態は本番同様、遷移時に1回だけプッシュする
                // Push the visibility once on transition, exactly as production does
                rangeView.Show(isPreviewing);

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

            Vector3 AverageVeinCenter(IReadOnlyList<VeinLayoutMessagePack> veinLayouts)
            {
                var sum = Vector3.zero;
                foreach (var layout in veinLayouts)
                    sum += new Vector3(layout.MinX + layout.MaxX + 1, layout.MinY + layout.MaxY + 1, layout.MinZ + layout.MaxZ + 1) * 0.5f;
                return sum / veinLayouts.Count;
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
