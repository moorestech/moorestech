# BP設置プレビューの自己Raycastヒット修正 Implementation Plan

> **For the controller session:** 実装時は moores-subagent-driven-development スキルを使う。依頼範囲は計画作成であり、この計画の提示時点では実装セッションを起動しない。全ステップは未実施。

**Goal:** BPプレビュー内のColliderが設置レイを遮って表示と非表示を繰り返す経路を解消し、実BPの操作で解消を確認する。

**Architecture:** プレビューのCollider設定は `BlockPreviewObject` が所有し、共通生成入口 `PreviewBlockCreator.Create` が初期化後に一度適用する。全Colliderを非アクティブな子も含めてTrigger化し、既存の `LayerConst.BlockLayer` に揃える。設置レイ・位置解決・Hideの条件は既存機構を活かす。

**Tech Stack:** Unity / C# / NUnit EditMode / 独立PhysicsScene / Client.Playtest / uloop。

## Requirements

- R1: 共通生成入口から返るプレビューの全Colliderは設置レイの既存maskから除外される。鉄コンベア・分岐機の `Cube_4` を通るレイが奥の地面へ届く。
- R2: 非アクティブな子も生成時に設定する。後から表示した子、複数Colliderを持つ子、プール再利用でも自己ヒットが復活しない。
- R3: Colliderは無効化・削除せずTriggerとして残す。接触判定を使うベルト・レール・電柱・歯車ポールの `DetectGroundOverlaps()` と既存可否表示を維持する。通常ブロック設置はADR0047どおり地形接触だけでは拒否しない。
- R4: 元PrefabのCollider・レイヤーを変更しない。実設置したブロックのクリック・設置面、地面のTrigger問い合わせ設定を維持する。
- R5: 実Prefabを使う回帰テストと、675ブロック・13種類の原本 `koujyou2` を使うunityプレイ録画テストを含める。固定照準でプレビュー自己ヒット・意図しない非表示フレームがともに0。
- R6: 元報告のワールド・カメラ条件で修正前後を照合する。元条件が復元できない場合は不足入力を明記して未完了とし、チェスト64個や別の地形の成功を解消証拠に代用しない。
- R7: 自己ヒット以外の点滅が残った場合は別原因を調査し、報告症状の解消を確認するまで修正完了にしない。
- 範囲外: 設置位置のスナップ規則、素材不足の扱い、BP形式、報告運搬サービス、全Prefabのレイヤー改修、全Raycastの探索方式変更。

## Global Constraints

- 最新 `origin/master` から作成した使い捨てworktreeを使う。本計画の土台は `d5110abb042feddb79fcbb143e872c3f8ca91a40`。実装時にも差分を確認する。
- C#および実行スニペットは1ファイル200行未満、新設ディレクトリはコード10ファイル以下。partial・Funcは禁止。
- Unity作業は実装用worktreeのEditorで行う。C#変更後にコンパイルする。.metaはUnity生成のみ。
- Prefab・ProjectSettingsのYAMLを直接編集しない。今回はアセット変更を予定しない。
- `Physics.queriesHitTriggers` は変更しない。RaycastAll・非表示の遅延・前フレーム位置の固定で症状を隠さない。
- テストと録画は同じEditorで並走させない。自分が起動したプロセスのPIDを記録する。
- `.decisions/2026-10-08-BPコピーの始点終点と貼り付けアンカーは1x1設置と同じ面の外側セルを選ぶ.md`、`2026-10-09-BP貼り付けの素材不足は全体を置けなくする.md`、`2026-10-09-BP貼り付けプレビューは復元配線もゴースト表示する.md` を守る。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / Collider設定を既存のプレビュー所有者へ置き、入力・位置解決・可否評価には分岐を増やさない。
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱0・第3バケツ0 / ADR0076の旧部分設置記述との矛盾候補1件。ADR0077項目6が旧記述を明示的に置換しており、新規裁定不要。参照を追加した。

## 根拠と入力資料

原因の出所はユーザー提示の単離実測。今回の計画セッションではUnity実験を再実行していない。両Prefabで6ステップの `Cube_4 → ProbeGround` 交互ヒットが観測され、問題Colliderだけをmask対象外へ一時変更すると全6ステップで地面ヒットになった。元カメラ・元地形・675個全体での通し同一原因と修正後解消は未検証。

証拠: https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-13-moores-bp-postmortem-20261012/physics-evidence.txt

報告ID: `20261012_073351_fe31ba46`。原本から抽出済みの入力をlogs repoの `harness/playtest/reports/2026-10-13-bp-preview-plan/input/` に保存した。

| 入力 | SHA-256 |
|---|---|
| koujyou2.json | 36009ef3701b8aeb08d816c99a3d39e52cfc5b202a4536bc71fc75f896f61324 |
| manifest.json | 7ef29c0ed7cc45f3f842ff23dbd1bc052a733d6d9bb865d19cac828475241278 |
| tick_473801.json | 3d974779849951aebdac4b25782ef6f2413f5ef165c465659e8f1408dfa8fcba |

`koujyou2` は675ブロック・13種類・電線38本。鉄コンベア460個、分岐機37個。報告時コード `a1775eca5d9886470042ae7eebb359570f11aba5`（dirty=true）、master-data `47f79caa83c55b1c945dc3d86b1a91c5a0e5c3b3`。dirty差分が保存されていなければ完全な同一バイナリとは表明しない。

manifestの報告時点カメラ: position=(623.1587,118.412971,635.0399)、Euler=(62.2692833,113.473808,0.00000366964537)。プレイヤー=(623.2227,116.6802,635.012146)。これは報告時点の `GameScreen` 状態であり、動画内で点滅した全フレームの照準・カメラを表すものではない。

## 配置と前例

| 対象 | 所属・責務 | 機構・前例 |
|---|---|---|
| PreviewBlockCreator.Create | Client.Game / プレビュー生成 | BlockPlacePreviewObjectPool.GetObject の新規生成経路を使用 |
| BlockPreviewObject.ConfigurePlacementColliders | Client.Game / プレビューCollider設定 | 既存SetTriggerColliderを役割に一致する名前へ置換。BlockLayerと既存設置maskを参照 |
| PreviewBlockColliderIsolationTest | Client.Tests / 純粋な物理回帰 | AssetDatabase＋独立PhysicsScene。Editor専用コードはUNITY_EDITORで囲む |
| blueprint-koujyou2-self-hit.cs | スキルのbuildingシナリオ / 実プレイ検証 | BlueprintCopyPasteScenarioChecks・BlueprintPasteGhostProbe・SemanticInputを再使用 |

データフロー: 照準入力 → PlaceSystemUtil.TryRaycastPlacementSurface → BP位置・可否評価 → BlueprintPastePreviewController → プール → PreviewBlockCreator → BlockPreviewObject。変更箇所は末端の生成物設定であり、上流の入力と下流の表示をつなぐ第2経路は作らない。

Collider無効化の前例は `TrainCarPreviewController` にあるが、ブロック系のベルト・レール・電柱・歯車ポールでは `GroundCollisionDetector.OnTriggerEnter/Stay` の生通知を維持するため、その方法は採らない。既存の除外maskに合わせる案はレイ機構を維持でき、全面的な探索方式変更より今回の責務に合う。

### 実装後レビューでの技術契約補正（agent既定・新しいユーザー裁定ではない）

実controllerの同一プール個体で、clear→contactを同フレームに動かすと交差trueでも旧`DetectGroundOverlaps=false`、逆方向では交差falseでもtrueだった（session212010、frame1908/1920）。これは直近のFixedUpdate/Trigger通知を現フレームの可否に使用したC3の鮮度故障であり、UI確定送信の誤動作までは未観測。`SetPreview` は必須の `terrainContactEnabled` を受け、通常設置はfalse、接触を可否に使う4利用者はtrueとする。falseの初回では専用地形源を生成しない。true→falseはプレビュー所有の専用源だけ停止し、元Prefab由来のCollider/Detectorを維持する。false→trueは同一源を再利用する。

`GroundCollisionDetector.OnTriggerEnter/Stay` → `BlockPreviewObject.IsCollisionGround` は実物理通知の生値として残す。一方、`DetectGroundOverlaps` は現配置のColliderと`GroundGameObject`を同一物理シーンで同期照合した値を返す。したがって旧「通知値をそのまま利用者へ渡す」技術経路は変更される。通知源の回帰と、現配置→利用者可否の回帰を別々に検証する。マスター占有体積は各面を既存`Physics.defaultContactOffset`現在0.01の2倍（向かい合う2面の余白0.02）だけ内側へ入れ、平地・浅い境界・段差を検査する。

| 操作 | 計画後の期待 | 検証 |
|---|---|---|
| BPの地面・ブロック天面・側面への貼り付け | 通常のセル解決が続く | 録画と既存OriginResolverテスト |
| BP回転、Q/E、ドラッグ列、持ち替え | 再表示・再利用後も自己ヒット0 | 録画 |
| 素材不足の赤表示・不足tooltip | 全体拒否の既存規則が続く | blueprint-copy-paste-via-ui.cs |
| 保存配線のゴースト | 38本の電線を含め表示が続く | 原本BPの録画 |
| ベルト・レール・電柱・歯車ポールの接触判定 | Trigger接触と既存可否評価が続く | 各利用者の録画 |
| 通常ブロック設置 | ADR0047の地形接触で拒否しない規則が続く | NormalPlacementPreviewStepTestと通常設置録画 |
| 実ブロックのクリック・隣接設置 | 元Prefabと実ブロックを維持 | 同じPrefabの実設置で確認 |

## Task 1: 共通プレビューのCollider設定と物理回帰テスト

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PreviewBlockCreator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/PreviewBlockColliderIsolationTest.cs`

**Interfaces:**
- Consumes: `LayerConst.BlockLayer`、`LayerConst.Without_Player_MapObject_Block_LayerMask`。
- Produces: `public void BlockPreviewObject.ConfigurePlacementColliders()`。引数・戻り値なし。プレビュー全ColliderのTrigger化と設置レイ除外をまとめて行う。
- Maintains: `public static BlockPreviewObject PreviewBlockCreator.Create(BlockId blockId)`、プールのAPI。

- [ ] Step 1: `SetTriggerCollider(bool)` の呼び出しを再検索し、既存1箇所を次の処理へ置換する。`BlockPreviewObject.Initialize` はプレビュー限定オブジェクトを有効化するため、設定はその後、呼び出し元へ返す前に行う。

```csharp
// BlockPreviewObject: 既存SetTriggerColliderを置換
public void ConfigurePlacementColliders()
{
    // 地面接触を残し、非表示の子も設置照準から外す
    // Preserve ground contact and exclude inactive children from placement aiming.
    foreach (var collider in GetComponentsInChildren<Collider>(true))
    {
        collider.isTrigger = true;
        collider.gameObject.layer = LayerConst.BlockLayer;
    }
}
```

```csharp
// PreviewBlockCreator.Create: メソッド全体
public static BlockPreviewObject Create(BlockId blockId)
{
    // 実Prefabを複製して表示だけをプレビュー用に初期化
    // Clone the real prefab and initialize its preview appearance.
    var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
    block.SetActive(true);
    var preview = block.AddComponent<BlockPreviewObject>();
    preview.Initialize(blockId);
    // 初期化が有効化した子も含めて物理設定を確定
    // Finalize physics settings including children activated during initialization.
    preview.ConfigurePlacementColliders();
    return preview;
}
```

- [ ] Step 2: 次の回帰テストを追加する。表示中→非表示→再表示を含む60回の問い合わせで、実PrefabのColliderが地面を遮らないことを検査する。子レイヤー不一致の条件を実Prefabに加えて人工の非アクティブ子で固定する。

```csharp
#if UNITY_EDITOR
using System.Linq;
using Client.Common;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    public class PreviewBlockColliderIsolationTest
    {
        private Scene _scene;
        private PhysicsScene _physics;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _physics = _scene.GetPhysicsScene();
            Assert.AreNotEqual(Physics.defaultPhysicsScene, _physics);
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);

        [TestCase("Assets/AddressableResources/Block/metal_gear belt conveyor.prefab")]
        [TestCase("Assets/AddressableResources/Block/metal_gear belt conveyor splitter.prefab")]
        [TestCase("Assets/AddressableResources/Block/Old/wooden chest.prefab")]
        public void RealPrefabDoesNotBlockGroundAcrossHideAndReuse(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, path);
            var sourceLayers = asset.GetComponentsInChildren<Collider>(true).Select(c => c.gameObject.layer).ToArray();
            var ghost = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
            ghost.SetActive(true);
            _physics.Simulate(0.02f);
            var cube = ghost.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.name == "Cube_4");
            var center = cube == null ? new Vector3(0.5f, 0.5f, 0.5f) : cube.bounds.center;
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.transform.position = new Vector3(center.x, -1f, center.z);
            ground.AddComponent<GroundGameObject>();
            ground.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);

            var preview = ghost.AddComponent<BlockPreviewObject>();
            preview.ConfigurePlacementColliders();
            var origin = new Vector3(center.x, 10f, center.z);
            for (var frame = 0; frame < 60; frame++)
            {
                preview.SetActive(frame % 3 != 1);
                _physics.Simulate(0.02f);
                Assert.IsTrue(_physics.Raycast(origin, Vector3.down, out var hit, 100f,
                    LayerConst.Without_Player_MapObject_Block_LayerMask, QueryTriggerInteraction.Collide));
                Assert.AreSame(ground, hit.collider.gameObject, $"{path}: frame={frame}");
            }
            CollectionAssert.AreEqual(sourceLayers, asset.GetComponentsInChildren<Collider>(true).Select(c => c.gameObject.layer).ToArray());
        }

        [Test]
        public void InactiveNestedAndMultipleCollidersRemainTriggersAfterActivation()
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var child = new GameObject("InactiveDefaultChild");
            child.transform.SetParent(root.transform);
            child.AddComponent<BoxCollider>();
            child.AddComponent<SphereCollider>();
            child.SetActive(false);
            root.AddComponent<BlockPreviewObject>().ConfigurePlacementColliders();
            child.SetActive(true);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsTrue(collider.enabled);
                Assert.IsTrue(collider.isTrigger);
                Assert.AreEqual(0, LayerConst.Without_Player_MapObject_Block_LayerMask & (1 << collider.gameObject.layer));
            }
        }
    }
}
#endif
```

- [ ] Step 3: 旧版の単離実験で2種類の失敗を保存し、上記テストを修正版で実行する。比較時に `BlockPreviewObject` を複製実装せず、共通生成入口で設定が実行されることもTask 2で検査する。

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type class --filter-value Client.Tests.PlaceSystem.Common.Preview.PreviewBlockColliderIsolationTest
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'NormalPlacementPreviewStepTest|BlueprintPasteOriginResolverTest|BlueprintPasteVisualStateTest|PlaceSystemUtilCalcPlacePointTest'
```

Expected: コンパイルErrorCount=0、対象テスト全PASS。Collider.enabledを落とさず、Triggerのままレイが地面へ届く。

- [ ] Step 4: Unity生成の.metaを含め、Task 1のFilesだけコミットする。

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PreviewBlockCreator.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview.meta
git commit -m "fix: exclude preview colliders from placement rays"
```

## Task 2: 原本BPの実プレイ経路・元条件で修正前後を検証

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-koujyou2-self-hit.cs`
- Read: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-large-paste-flicker.cs`
- Read: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/BlueprintPasteGhostProbe.cs`
- Read: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs`

**Interfaces:**
- Consumes: `IBlueprintDatastore.Register(BlueprintJsonObject)`、`ClientBlueprintLibrary.Refresh(CancellationToken)`、`BlueprintCopyPasteScenarioChecks.SelectEntry(string,string)`、`BlueprintPasteGhostProbe.ActiveGhosts()`。
- Produces: 録画・result.json・フレーム単位の自己ヒット/プレビュー個数・アンカー位置の観測。新規本番APIは作らない。

- [ ] Step 1: 証拠の原本BPをハッシュ照合し、下記の録画シナリオを作る。`BP_FLICKER_INPUT` には上記inputディレクトリの絶対パスを渡す。環境変数不足やJSON欠落は例外を握り潰さずシナリオ失敗にする。

```csharp
using System;
using System.IO;
using System.Linq;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Blueprint;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

return PlaytestRunner.Run("blueprint-koujyou2-self-hit", new PlaytestRunOptions { Record = true }, async p =>
{
    SemanticInput.EnsureDevices();
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    p.Hotbar.UnlockBlueprint();
    var input = Environment.GetEnvironmentVariable("BP_FLICKER_INPUT");
    if (string.IsNullOrWhiteSpace(input)) throw new InvalidOperationException("BP_FLICKER_INPUT is required");
    var bp = JsonConvert.DeserializeObject<BlueprintJsonObject>(File.ReadAllText(Path.Combine(input, "koujyou2.json")));
    if (bp == null || bp.Blocks.Count != 675 || bp.Wires.Count != 38) throw new InvalidOperationException("Unexpected koujyou2 fixture");
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    p.ServerService<IBlueprintDatastore>().Register(bp);
    await resolver.Resolve<ClientBlueprintLibrary>().Refresh(Camera.main.GetCancellationTokenOnDestroy());
    var tooltip = resolver.Resolve<MouseCursorTooltipState>();
    var checks = new BlueprintCopyPasteScenarioChecks(p, tooltip);
    var probe = new BlueprintPasteGhostProbe(tooltip);
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000010", $"build-menu-entry-blueprint-{bp.BlueprintGuid:D}");
    p.WarpPlayer(new Vector3(-12f, 36f, -8f));
    await p.AimAt(new Vector3(-11.5f, 32f, 8.5f));
    await p.Until(() => probe.ActiveGhosts().Count == 675, 20f, "原本BPの675ゴースト表示");

    // 更新が終わった描画直前に自己ヒットと表示を観測
    // Observe self-hits and visibility just before rendering after updates.
    async UniTask CheckFrames(string label)
    {
        p.Note($"{label}: 固定照準で120フレーム計測");
        var missing = 0;
        var selfHits = 0;
        for (var frame = 0; frame < 120; frame++)
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            var ghosts = probe.ActiveGhosts();
            if (ghosts.Count != 675) missing++;
            var ray = Camera.main.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            var hit = Physics.Raycast(ray, out var info, float.PositiveInfinity,
                LayerConst.Without_Player_MapObject_Block_LayerMask, QueryTriggerInteraction.UseGlobal);
            if (hit && info.collider.GetComponentInParent<BlockPreviewObject>() != null) selfHits++;
            Debug.Log($"{label} frame={frame} ghosts={ghosts.Count} hit={(hit ? info.collider.name : "none")}");
        }
        p.Assert(missing == 0, $"{label}: 表示欠落={missing}/120");
        p.Assert(selfHits == 0, $"{label}: プレビュー自己ヒット={selfHits}/120");
        await p.Screenshot(label);
    }
    await CheckFrames("original-bp-controlled-ground");
    await p.PressKey(Key.R);
    await CheckFrames("rotated");
    await p.PressKey(Key.E);
    await CheckFrames("height-up");
    await p.PressKey(Key.Q);
    await CheckFrames("height-restored");
    await p.ExitToGameScreen();
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000010", $"build-menu-entry-blueprint-{bp.BlueprintGuid:D}");
    await p.AimAt(new Vector3(-11.5f, 32f, 8.5f));
    await CheckFrames("pool-reused");
});
```

このシナリオは元BPを使う制御された地面での検証。元報告のカメラ・地形での検証は次のStep 3で別に行う。`p.SetupDebugEnvironment` は平坦化を含むため元ワールドの検証には使わない。

- [ ] Step 2: `unity-playmode-recorded-playtest` スキルで前景実行する。新規シナリオのAPI/入力キーは現在の実装と照合してから投入する。

```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
BP_FLICKER_INPUT=/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/playtest/reports/2026-10-13-bp-preview-plan/input PLAYTEST_WORLD_DIRECTORY="$PWD/moorestech_client/PlaytestResults/worlds/bp-self-hit-fresh" PLAYTEST_MAP_MODE=generated PLAYTEST_SEED=12345 .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-koujyou2-self-hit.cs
```

Expected: 録画が0byteでなく、675個が全観測フレームで表示、自己ヒット0。新規生成・プール再利用の両方を通過。アバター・地面・HUDのある実プレイ視点で目視も行う。ワールドパスは再実行ごとに新しくする。

- [ ] Step 3: 元報告バンドルと原ワールドを読み取り専用で取り寄せ、実装worktreeの検証用ディレクトリへコピーして起動する。`tick_473801.json` 単体は地形定義を含まないため、原ワールドの定義・terrain入力/cacheを揃える。報告manifestのカメラ/プレイヤー座標と元動画を起点に照準を合わせ、平坦化せず `PlaytestRunner.Run` の録画付き実操作で120フレームを計測する。修正前も同条件で計測して自己ヒット経路と見た目の点滅を結びつける。報告時dirty差分が回収不能なら比較の制約として記す。
- [ ] Step 4: 原本BPのうち問題の2Prefabが照準を横切る地面・天面・側面の各位置で静止/移動、回転、Q/E、列ドラッグ、持ち替えを確認する。毎フレーム、最初のhitの階層・layer・解決成否・BPアンカー・active個数をlogs repoへ記録する。PostLateUpdateの再RaycastはHide後のColliderを観測するため、原因ヒットの計数には使わない。原因確認は `uloop-pause-point` スキルで `PlaceSystemUtil.TryRaycastPlacementSurface` のRaycast直後・return前を捕捉し、Hide前のhitを採取する。連続表示の計測は中断しない録画ランで行い、捕捉ランと分ける。静止中にアンカーが移動する/非表示になる別経路があれば調査して解消する。地面への接触がない空間では、従来の正当な非表示を欠落に数えない。
- [ ] Step 5: 通常設置でも同じ2Prefabを持ち、自己ヒット0を確認する。接触判定を使うベルト・レール・電柱・歯車ポールでは、物理ステップ後の `DetectGroundOverlaps()` が接触あり/なしを区別し、各利用者の可否評価が従来どおり働くことを実測する。通常ブロック設置は地形接触だけでは拒否せず、接触問い合わせもしない既存規則を維持する。実設置済みブロックのクリック/隣接設置、素材不足、38本の配線表示を確認する。既存チェストシナリオは比較用に残す。
- [ ] Step 6: 結果・入力ハッシュ・コード/master-dataのSHA・条件差・録画・代表フレームをlogs repoに保存する。元条件検証が未達ならR6/R7を完了にせず理由をbdに残す。シナリオをコミットする。

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-koujyou2-self-hit.cs
git commit -m "test: record original blueprint preview self-hit regression"
```

## Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] moores-code-reviewの正規台本を実行し、Collider設定時点・同型の残存・接触判定を使う各利用者・元症状の解消証拠を確認する。Codexホストでは同スキルの `references/codex-host.md` と `scripts/codex_workflow_runner/run.mjs` を使い、系統を自分で代行・縮退しない。
- [ ] 指摘を反映してC#を変更したらコンパイルと対象テストを行う。判定経路・条件・時点を変更したらTask 2のunityプレイ録画テストを反映後のバイナリで再実施する。
- [ ] Requirements R1〜R7と証拠の対応を確認する。未再現・未検証をPASSに置き換えない。

## Task 4: セッション終了可能状態にすること

- [ ] pr-createスキルでPRを作成する。masterとのコンフリクトは解消し、コンパイルを確認してpushする。
- [ ] 全作業がコミット・push済みで、PRがマージ可能な状態であることを確認する。元症状の未解消を「全Task完了」と併記して閉じない。
- [ ] 証拠をshare-putで提示し、判断・実測・条件差をwikiへ記録する。bdを結果とPRに結びつける。PR作成後に実装用worktreeをmoores-wt rmで片付ける。

## user-simulator review 結果

- 実行: Codexホストの既存runnerでopus/fableを対応するCodex推論設定へ変換。2系統が完了、失敗0。Claudeモデルの実行とは表明しない。
- 元々の想定: Triggerを保ち、プレビュー生成物を設置maskから除外する。
- 適用済み: ADR0047とNormalPlacementPreviewStepTestに従い、接触判定維持の対象を実利用者4経路へ修正。通常設置に新たな地形拒否条件を加えない。Hide前の原因ヒットと描画直前の表示観測を区別した。
- 要裁定: なし。既存裁定との照合で解決。
- 見なかった領域: Unity実行・元ワールドのterrain/cache復元・修正後の症状解消。実装工程で検証する。

## 判断記録（ADR）

| 判断 | 出所 | 根拠・比較 |
|---|---|---|
| 今回は計画提示まで | ユーザー依頼「この問題の修正計画を立てて」 | 実装・新規実装セッションの自動起動はこのターンの範囲外 |
| BlockPreviewObjectでCollider設定を所有する | agent前提 | 既存SetTriggerColliderの役割を拡張して名前を正す。BP側へアセット固有名の分岐を足さない |
| 全Colliderのlayerを既存BlockLayerへ揃える | agent前提 | 設置maskの既存除外を利用。Ignore Raycastは現在の明示maskに含まれるため、名前だけでは除外できない |
| Triggerを維持し、Initialize後に非アクティブ子も設定する | agent前提 | ベルト・レール・電柱・歯車ポールがGroundCollisionDetectorの接触を使う。Collider無効化はこの役割に適さない。初期化が有効化する子も対象 |
| 元Prefabの変更を本修正に含めない | agent前提 | 不具合はプレビュー生成物の物理契約として閉じる。元Prefabのレイヤーを直すだけでは別Prefab・非アクティブ子の再発を防げない |
| RaycastAll/Trigger全無視/前回位置保持は採らない | agent前提 | 最前面の不正物体を飛ばす設置仕様変更、正当な設置面の巻き添え、症状隠しを生む。所有者での設定が既存機構へ統合する案 |
| unityプレイ録画テストを含める | agent前提 | 問題が実Prefab・照準・表示/非表示のフレーム循環にあり、NUnitだけでは675個全体の解消証拠にならない |
| 元条件入力不足は未完了として扱う | ユーザー提示の検証限界＋agent前提 | 元報告の点滅全ての同一原因は未確定。回収不能ならそれを明示し、代理成功で完了扱いにしない |
| BPのアンカー・可否・復元配線仕様を維持する | 既存ユーザー裁定 | docs/adr/0076-blueprint-copy-paste-share-placement-cell-height-and-run.md、docs/adr/0077-blueprint-placement-as-one-large-block-with-wiring.md とGlobal Constraintsの.decisions。ADR0077項目6がADR0076の旧部分設置記述を明示的に置換している |

Self-Review: R1/R2/R4→Task 1＋Task 2、R3/R5/R6/R7→Task 2。新規サーバー状態・セーブ変更・公開DTO・DI登録はない。具体的な入力・観測・成功条件を各検証に指定した。
