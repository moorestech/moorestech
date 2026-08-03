---
spec: docs/plans/map-autogen-world-design.md
---

# PR #1104 最終ブランチレビュー Critical 是正 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** PR #1104 裁定反映ブランチの最終レビューで確定したCritical 6件（C1〜C5＋Codex High②）と事実誤りコメント1件を、挙動の裏取りテスト付きで解消する。

**Architecture:** C1はユーザー裁定(b)に従い「新しい起動順序（`InitializeDispatch` の地形構築前倒し）は保ったまま、プレイヤーの物理開始だけを地形構築後へ遅らせる」。本PRが既に採用した `StartOutcropInstantiation()` / `Show(bool)` と同じ「DI生成の副作用をやめ、必要なタイミングで明示的にプッシュする」形に揃える。C2〜C5・Codex High② は方針が一意に定まっており設計判断を含まない。

**Tech Stack:** Unity 6 / C# / UniTask / VContainer / MessagePack / NUnit（EditMode）

## Global Constraints

- 1ファイル200行以下・`partial` 絶対禁止・1ディレクトリ10ファイルまで
- `try-catch` は外部境界（ネットワーク受信payload・外部入力パース）限定。使う場合は「なぜ境界か」を根拠コメントで明記する
- デフォルト引数禁止（オーバーロードで表現する）・`Func<>` 禁止・イベントはUniRx
- 単純なgetter/setterプロパティ禁止。値のSetは `public void SetHoge` メソッド
- コメントは日本語1行 → 英語1行の2行セット。3〜10行ごとに主要セクションへ。日本語は処理・変数20字/メソッド30字が目安（根拠コメントは長くて可）
- `#region Internal` はメソッド内ローカル関数用。クラス直下のprivateメソッド群を囲うのは禁止
- サーバーのゲーム内経過時間は `GameUpdater` のティックのみ（本planの対象コードは該当なし）
- `.meta` は手動作成禁止。Prefab/シーン/ScriptableObjectをテキスト編集しない
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行しエラー0を確認する
- 後方互換性・パフォーマンス最適化・将来の拡張性は考慮不要（AGENTS.md）

---

### Task 1: C1 — プレイヤーの物理開始を地形構築後へ遅らせる（ユーザー裁定(b)）

**背景（実装者向け1行）:** 本PRで `InitializeDispatch()` を地形構築より前へ動かした結果、`StartGame()` 内のDI解決で `PlayerObjectController.Initialize` が走り、地形コライダーの無い空間へプレイヤーがWarpされて落下する。落下復帰が保存座標 `PlayerPos` を握り潰し、落下中の座標が `PlayerPositionSender` 経由でサーバーへ即時反映される。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerSystemContainer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/Player/PlayerPositionSender.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerRuntimeStartGateTest.cs`

**Interfaces:**
- Produces: `PlayerObjectController.Initialize(Vector3 initialPlayerPosition, Vector3 worldSpawnPosition)` / `PlayerObjectController.StartPlayerRuntime()` / `PlayerSystemContainer.StartPlayerRuntime()` / `PlayerPositionSender.StartSending()`
- Consumes: なし（本タスクが先頭）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerRuntimeStartGateTest.cs` を新規作成する。
`PlayerObjectController` はシリアライズ参照を持つため、テスト側でGameObjectを組み立てて private field へリフレクション注入する（`Client.Tests/UIState/UIStateCameraInteractionTest.cs` の `SetField` と同じ手法）。

```csharp
using System.Reflection;
using Client.Common;
using Client.Game.InGame.Player;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;

namespace Client.Tests.Player
{
    public class PlayerRuntimeStartGateTest
    {
        private GameObject _playerRoot;
        private GameObject _ground;

        [TearDown]
        public void TearDown()
        {
            if (_playerRoot != null) Object.DestroyImmediate(_playerRoot);
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        [Test]
        public void StartPlayerRuntimeまでWarpも落下復帰も起きない()
        {
            var controller = CreatePlayerObjectController();

            // 地形構築前を模す。Initializeだけでは保存座標へ動かないこと
            // Emulate the pre-terrain window: Initialize alone must not move the player to the saved position
            controller.Initialize(new Vector3(30f, 70f, 40f), new Vector3(5f, 15f, 5f));
            Assert.AreNotEqual(30f, controller.Position.x, 0.001f, "Initializeの時点でWarpされている");

            // 落下復帰が武装していないこと。武装していればGetGroundPointのLogErrorでテストが落ちる
            // Fall recovery must be disarmed; if armed, GetGroundPoint's LogError fails this test
            controller.transform.position = new Vector3(0f, -100f, 0f);
            InvokeLateUpdate(controller);
            Assert.AreEqual(-100f, controller.transform.position.y, 0.001f, "開始前に落下復帰が動いた");
        }

        [Test]
        public void StartPlayerRuntimeで保存座標へWarpし落下復帰が武装する()
        {
            var controller = CreatePlayerObjectController();
            controller.Initialize(new Vector3(30f, 70f, 40f), new Vector3(5f, 15f, 5f));

            controller.StartPlayerRuntime();
            Assert.AreEqual(30f, controller.Position.x, 0.001f, "保存座標Xへ復帰していない");
            Assert.AreEqual(70f, controller.Position.y, 0.001f, "保存座標Yへ復帰していない");
            Assert.AreEqual(40f, controller.Position.z, 0.001f, "保存座標Zへ復帰していない");

            // 開始後は落下復帰が効く。地表を用意し、そのYへ戻ることで武装を確かめる
            // After the start, fall recovery works; prepare ground and confirm the recovery Y
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(30f, 20f, 40f);
            _ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();

            controller.transform.position = new Vector3(30f, -100f, 40f);
            InvokeLateUpdate(controller);
            Assert.AreEqual(20.5f, controller.transform.position.y, 0.001f, "開始後に落下復帰が動かない");
        }

        [Test]
        public void StartPlayerRuntimeの二重呼び出しは無視される()
        {
            var controller = CreatePlayerObjectController();
            controller.Initialize(new Vector3(30f, 70f, 40f), new Vector3(5f, 15f, 5f));
            controller.StartPlayerRuntime();

            controller.transform.position = new Vector3(1f, 2f, 3f);
            controller.StartPlayerRuntime();
            Assert.AreEqual(1f, controller.Position.x, 0.001f, "2回目の開始でWarpし直された");
        }

        private PlayerObjectController CreatePlayerObjectController()
        {
            _playerRoot = new GameObject("PlayerRuntimeStartGateTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            var playerObjectController = _playerRoot.AddComponent<PlayerObjectController>();
            SetField(playerObjectController, "controller", thirdPersonController);
            return playerObjectController;
        }

        private static void InvokeLateUpdate(PlayerObjectController controller)
        {
            var method = typeof(PlayerObjectController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
```

補足（実装者へ）: `Client.Tests` の asmdef に `StarterAssets` 系の参照が無ければ追加する。`Client.Common` の `LayerConst.GroundLayer` は `PlayerFallRecoveryPositionTest.cs` が既に使っている前例なのでそのまま使ってよい。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRuntimeStartGateTest"`
Expected: コンパイルエラー（`Initialize(Vector3, Vector3)` と `StartPlayerRuntime` が未定義）

- [ ] **Step 3: PlayerObjectController を「初期化」と「実行開始」の2段階へ分ける**

`PlayerObjectController.cs` の該当箇所を次のとおり置き換える（`isInitialized` は意味が「実行開始済み」へ変わるので `isRuntimeStarted` へ改名し、全参照を追従させる）。

```csharp
        private Vector3 worldSpawnPosition;
        private Vector3 initialPlayerPosition;
        private bool isRuntimeStarted;

        // 参照解決と復帰先の確定まで。地形コライダーがまだ無いのでWarpも重力も始めない
        // Resolve references and settle the recovery target only; terrain colliders do not exist yet, so no warp and no gravity
        public void Initialize(Vector3 initialPlayerPosition, Vector3 worldSpawnPosition)
        {
            controller.Initialize();
            characterController = GetComponent<CharacterController>();

            // 落下復帰先はワールドのスポーン地点。地形はランタイム構築なのでシーン配置のマーカーは当てにできない
            // Fall recovery targets the world spawn; terrain is built at runtime so a scene-authored marker cannot be trusted
            this.worldSpawnPosition = worldSpawnPosition;
            this.initialPlayerPosition = initialPlayerPosition;

            // 地形の無い空間で落下させない。重力はStartPlayerRuntimeで解禁する
            // Never let the player fall through a terrain-less space; gravity is released in StartPlayerRuntime
            controller.enabled = false;
        }

        // 地形構築の完了後にFinalizerが呼ぶ。保存座標へ置いてから重力と落下復帰を解禁する
        // Called by the finalizer once terrain is built: place at the saved position, then release gravity and fall recovery
        public void StartPlayerRuntime()
        {
            if (isRuntimeStarted) return;

            SetPlayerPosition(initialPlayerPosition);
            controller.enabled = true;
            isRuntimeStarted = true;
        }
```

`LateUpdate` の先頭ガードも改名に合わせ、コメントを実態へ直す。

```csharp
            // 地形構築前は復帰先の地表が無く、復帰させると保存座標を捨ててしまう
            // Before the terrain is built there is no ground to recover onto, and recovering would discard the saved position
            if (!isRuntimeStarted) return;
```

- [ ] **Step 4: PlayerSystemContainer に開始プッシュ口を足す**

`PlayerSystemContainer.cs`:

```csharp
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            playerObjectController.Initialize(initialHandshakeResponse.PlayerPos, initialHandshakeResponse.MapLayout.Spawn);
        }

        // 地形構築の完了をFinalizerから受けて、自機の実行を開始する
        // Receives terrain-build completion from the finalizer and starts the player runtime
        public void StartPlayerRuntime()
        {
            playerObjectController.StartPlayerRuntime();
        }
```

`IPlayerObjectController` は変更しない（`playerObjectController` フィールドは具象型のため）。

- [ ] **Step 5: PlayerPositionSender を明示開始にする**

`PlayerPositionSender.cs`:

```csharp
    public class PlayerPositionSender : ITickable
    {
        private float _timer;
        private bool _isSending;

        // 地形構築の完了後にFinalizerが呼ぶ。落下中の座標をサーバーへ書き込ませない
        // Called by the finalizer after the terrain is built so falling coordinates never reach the server
        public void StartSending()
        {
            _isSending = true;
        }

        /// <summary>
        ///     Updateと同じタイミングで呼ばれる
        /// </summary>
        public void Tick()
        {
            if (!_isSending) return;

            _timer += Time.deltaTime;
            ...
```

`MainGameStarter.cs` の登録を `.AsSelf()` 付きへ変える（同ファイルの `TrainFullSnapshotEventNetworkHandler` が既に取っている形）。

```csharp
            builder.RegisterEntryPoint<PlayerPositionSender>().AsSelf();
```

- [ ] **Step 6: Finalizerから開始をプッシュする**

`MainGameInitializationFinalizer.cs` の `StartOutcropInstantiation()` の直後へ追加する。

```csharp
            // 露頭生成はTerrain完成後に明示開始する。完了待ちは下のWhenAllが一括で担う（ADR#15）
            // Outcrop instantiation starts explicitly after the terrain is ready; the WhenAll below waits for it with the rest (ADR#15)
            resolver.Resolve<MapVeinObjectDatastore>().StartOutcropInstantiation();

            // 地形コライダーが揃ってから自機を保存座標へ置き、重力と座標送信を解禁する（落下と座標汚染の窓を作らない・ADR#16）
            // Release the player onto the finished terrain before gravity and position reporting start, leaving no fall or coordinate-pollution window (ADR#16)
            resolver.Resolve<PlayerSystemContainer>().StartPlayerRuntime();
            resolver.Resolve<PlayerPositionSender>().StartSending();
```

`using Client.Game.InGame.Player;` と `using Client.Game.InGame.Presenter.Player;` を追加する。

- [ ] **Step 7: MainGameStarter の陳腐化した設計意図コメントを直す**

`MainGameStarter.cs` の `EnvironmentRoot` プロパティ上のコメントは「地形の実行時構築が StartGame より前にマウント先を要るため」と書いてあるが、本PRで地形構築は StartGame の後になった。事実へ合わせる。

```csharp
        // 地形の実行時構築はDIの外（Finalizer）で走るため、マウント先だけを読み取り専用で公開する
        // Runtime terrain construction runs outside DI in the finalizer, so only read access to the mount point is exposed
        public EnvironmentRoot EnvironmentRoot => environmentRoot;
```

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRuntimeStartGateTest|PlayerFallRecoveryPositionTest|PlayerObjectModelVisibilityTest"`
Expected: 全PASS

- [ ] **Step 9: mutation で有効性を確かめる**

`StartPlayerRuntime()` の `SetPlayerPosition(initialPlayerPosition);` を一時的に削除 → `StartPlayerRuntimeで保存座標へWarpし落下復帰が武装する` が落ちることを確認して戻す。
`Initialize` の `controller.enabled = false;` を一時的に削除しても落ちないのが正（この行は落下の見た目を止めるだけで、データ被害は `isRuntimeStarted` ガードが担う）。ただしこの事実を報告に1行残すこと。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Player moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/Player moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/Player
git commit -m "fix: プレイヤーの実行開始を地形構築後へ遅らせる(C1・ADR#16)"
```

---

### Task 2: C2 — 地形モード解釈を単一入口へ寄せる

**背景:** `TerrainTransferMetaMessagePack.ToTerrainTransferMeta()` は「モード解釈を各所へ散らさない唯一の入口」と宣言しているが、`TerrainRuntimeBuilder` が template/generated/未知の3分岐を二重実装し、`TerrainDataFetcher` も template を独自判定している。結果として `ToTerrainTransferMeta()` の template 分岐は production から到達不能になっている。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/TerrainDataFetcher.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/MapGeneration/TerrainTransferMetaModeTest.cs`（配置先は実装者が既存の地形転送テストの隣に合わせること）

**Interfaces:**
- Produces: `TerrainTransferMeta.IsTemplate`（readonly bool）
- Consumes: なし

- [ ] **Step 1: 失敗するテストを書く**

```csharp
        [Test]
        public void ワイヤメタからのモード解釈は単一入口で完結する()
        {
            var template = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-a", 42), string.Empty);
            Assert.IsTrue(template.ToTerrainTransferMeta().IsTemplate);

            var generated = new TerrainTransferMetaMessagePack(
                TerrainTransferMeta.CreateGenerated("world-b", 513, 4, 3, 42, new TerrainOrigins(Vector2.zero, Vector2.zero)), "hash");
            Assert.IsFalse(generated.ToTerrainTransferMeta().IsTemplate);
        }

        [Test]
        public void 未知モードは変換入口で例外になる()
        {
            var unknown = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-c", 1), string.Empty);
            unknown.MapMode = "unknown-mode";
            Assert.Throws<InvalidOperationException>(() => unknown.ToTerrainTransferMeta());
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TerrainTransferMetaModeTest"`
Expected: コンパイルエラー（`IsTemplate` が未定義）

- [ ] **Step 3: TerrainTransferMeta に判別子を持たせる**

```csharp
        public readonly string MapMode;

        // モード文字列の解釈はこの型の中だけで終わらせる。消費側は文字列比較を持たない
        // Mode-string interpretation ends inside this type; consumers never compare the string themselves
        public readonly bool IsTemplate;
```

privateコンストラクタで `IsTemplate = mapMode == WorldProvisioner.TemplateMapMode;` を設定する。

- [ ] **Step 4: TerrainRuntimeBuilder を先に変換してからドメイン型で分岐させる**

```csharp
            // モード解釈はToTerrainTransferMeta1本。未知モードもそこで例外になる
            // ToTerrainTransferMeta is the only mode interpreter, and it is also where unknown modes throw
            var wireMeta = mapLayout.TerrainMeta;
            var terrainMeta = wireMeta.ToTerrainTransferMeta();
            if (terrainMeta.IsTemplate)
                await BuildTemplateTerrainAsync(environmentRoot, terrainMaterial);
            else
                await BuildGeneratedTerrainAsync(terrainMeta, wireMeta.TerrainHash, environmentRoot, terrainMaterial);
```

`using Game.MapGeneration.Provisioning;` が未使用になったら削除する。

- [ ] **Step 5: TerrainDataFetcher の独自判定を消す**

```csharp
            // モード解釈はToTerrainTransferMeta1本。未知モードもそこで例外になる
            // ToTerrainTransferMeta is the only mode interpreter, and it is also where unknown modes throw
            var wireMeta = mapLayout.TerrainMeta;
            var terrainMeta = wireMeta.ToTerrainTransferMeta();

            // templateモードのワールドは地形バイナリを持たないので取得対象が無い
            // A template-mode world owns no terrain binary, so there is nothing to fetch
            if (terrainMeta.IsTemplate) return 0;
```

`using Game.MapGeneration.Provisioning;` が未使用になったら削除する。

- [ ] **Step 6: 二重実装が残っていないことを確認する**

Run: `grep -rn "TemplateMapMode\|GeneratedMapMode" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts --include=*.cs`
Expected: ヒットは `WorldProvisioner`（定義元とサーバー側の生成分岐）、`TerrainTransferMeta`、`TerrainTransferMetaMessagePack`、およびテストのみ。クライアントの消費側（Builder/Fetcher）に残っていたら未完了。

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TerrainTransferMetaModeTest|TerrainCacheFetchTest"`
Expected: 全PASS

- [ ] **Step 8: コミットする**

```bash
git commit -am "fix: 地形モード解釈をToTerrainTransferMeta1本へ寄せる(C2)"
```

---

### Task 3: C3 — 探索無効経路のログ追加と事実誤りコメントの是正

**背景:** `TerrainGenerationConfig.useSpawnOffsetSearch` の既定値は false ＝ 探索無効が主要経路なのに、早期returnが診断ログの上にあるため「無効」と「探索フォールバック」がどちらも `Vector2.zero` で出力からも診断からも区別できない。ユーザー裁定「設定ゼロでも世界は作られるべき」が名指ししたケースそのものが無言になっている。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`
- Test: 既存のスポーン探索テスト（`grep -rn "SpawnSearchSetup" moorestech_server/Assets/Scripts` で所在を確認し、`Disabled` フィクスチャを使うテストへ1ケース追加する）

- [ ] **Step 1: 失敗するテストを書く**

`SpawnSearchSetup.Disabled` 相当の設定で生成を走らせ、`[SpawnSearch]` を含むログが1行出ることを固定する。

```csharp
        [Test]
        public void 探索無効でも診断ログが1行残る()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SpawnSearch\] 探索無効"));
            // 既存フィクスチャの生成呼び出しをここへ（useSpawnOffsetSearch=false の config で VanillaGenerator を走らせる）
        }
```

実装者へ: 既存テストがEditMode/NUnit どちらの土俵にあるかを確認し、`LogAssert` が使えないプレーンNUnitなら `Application.logMessageReceived` を購読して収集する形へ置き換えてよい。ログ本文の固定は必須。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<追加先テストクラス名>"`
Expected: FAIL（`[SpawnSearch] 探索無効` が出ない）

- [ ] **Step 3: 早期return前にログを足す**

```csharp
            // 探索無効も1行残す。無効とフォールバックはどちらもオフセット0で、ログが無いと後から区別できない（ADR#13）
            // Log the disabled path too: disabled and fallback both yield a zero offset and become indistinguishable without it (ADR#13)
            if (!config.useSpawnOffsetSearch)
            {
                Debug.Log("[SpawnSearch] 探索無効（useSpawnOffsetSearch=false）");
                return Vector2.zero;
            }
```

- [ ] **Step 4: 事実誤りのコメントを実態へ狭める**

現状のコメントは「候補ゼロや設定不備でも生成は止めない」と書いているが、`SpawnRegionFinder.Find` 入口の `AssertSpawnTargetIsInsideGeneratedTile` が `gridSizeX/gridSizeZ` 偶数かつ `overrideSpawnScenePosition=false` のとき throw し、ワールド生成ごと落ちる。誤ったまま残すと次の読者が「D6は完了済み」と誤認する。

```csharp
            // 成否と診断を必ず残す。候補ゼロならフォールバックして生成は続ける（設定不備によるSpawnRegionFinderのthrowは別途裁定・ADR#13）
            // Always record the outcome and diagnostics: zero candidates fall back and generation continues (SpawnRegionFinder still throws on bad settings, pending a separate ruling; ADR#13)
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<追加先テストクラス名>"`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git commit -am "fix: スポーン探索の無効経路をログに残しコメントを実態へ狭める(C3・W2)"
```

---

### Task 4: C4 — 初期化待機ゲートをテスト可能な単位へ切り出す

**背景:** `MainGameInitializationFinalizer.cs` の `await allApplied;` を削除しても、`.As<IInitialEventApplyWaitTarget>()` の登録を削除しても、全テストが緑のまま通る。Finalizerは `.Forget()` され誰もawaitせず、`LoadMainGame` は固定1秒待ちで `GameInitializedEvent` を待たないため。本PRが作り直した当のゲートに実効テストが無い。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/InitialEventApplyWaiter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/InitialEventApplyWaiterTest.cs`

**Interfaces:**
- Produces: `InitialEventApplyWaiter.WaitAllAsync(IReadOnlyList<IInitialEventApplyWaitTarget> targets)` → `UniTask`
- Consumes: Task 1 で追加された Finalizer の開始プッシュ（順序を壊さないこと）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.Common;
using Client.Starter.Initialization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.UnitTest
{
    public class InitialEventApplyWaiterTest
    {
        [Test]
        public void 全対象が完了するまで待機は完了しない()
        {
            var first = new FakeWaitTarget();
            var second = new FakeWaitTarget();
            var waiting = InitialEventApplyWaiter.WaitAllAsync(new List<IInitialEventApplyWaitTarget> { first, second }).Preserve();

            Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
            first.Complete();
            Assert.AreEqual(UniTaskStatus.Pending, waiting.Status, "1本完了で待機が抜けている");
            second.Complete();
            Assert.AreEqual(UniTaskStatus.Succeeded, waiting.Status);
        }

        [Test]
        public void 対象の失敗は待機境界へ例外として届く()
        {
            var target = new FakeWaitTarget();
            var waiting = InitialEventApplyWaiter.WaitAllAsync(new List<IInitialEventApplyWaitTarget> { target }).Preserve();

            target.Fail(new InvalidOperationException("apply failed"));
            Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status);
        }

        private class FakeWaitTarget : IInitialEventApplyWaitTarget
        {
            private readonly UniTaskCompletionSource _completion = new();
            public UniTask WaitForInitialApplyAsync() => _completion.Task;
            public void Complete() => _completion.TrySetResult();
            public void Fail(Exception exception) => _completion.TrySetException(exception);
        }
    }
}
```

実装者へ: `IInitialEventApplyWaitTarget` の実際のnamespaceを確認して using を合わせること。`UniTask.Status` を読むために `WaitAllAsync` の戻りは `.Preserve()` してから検査する。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InitialEventApplyWaiterTest"`
Expected: コンパイルエラー（`InitialEventApplyWaiter` が未定義）

- [ ] **Step 3: 待機ロジックを専用クラスへ移す**

`InitialEventApplyWaiter.cs` を新規作成し、現行 `WaitAllInitialApplyAsync` の中身をそのまま移す。あわせて、複数系統が指摘した2点を直す。

- 5秒警告の `UniTask.Delay` を `DelayType.Realtime` にする（既定の `DeltaTime` は `timeScale=0` で永久に発火しない。旧実装は `Time.realtimeSinceStartup` だった）
- 待機完了後に警告タスクを打ち切る（起動失敗後に5秒警告が出て誤誘導するのを防ぐ）

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.Starter.Initialization
{
    // 初期イベント適用の待機境界。全対象の完了をひとつのawaitへ畳み、失敗は例外としてここへ届く
    // The wait boundary of initial event application: every target folds into one await and failures surface here as exceptions
    public static class InitialEventApplyWaiter
    {
        private const int StuckWarningSeconds = 5;

        public static async UniTask WaitAllAsync(IReadOnlyList<IInitialEventApplyWaitTarget> targets)
        {
            var waits = targets.Select(target => (target, task: target.WaitForInitialApplyAsync().Preserve())).ToList();
            var allApplied = UniTask.WhenAll(waits.Select(wait => wait.task));

            // 対象タスクはWhenAllで一度だけawaitする。警告側でも待つとUniTaskの二重await例外になる
            // Await the targets once through WhenAll; awaiting them again in the warning path throws UniTask's double-await error
            using var warningCancellation = new CancellationTokenSource();
            WarnStuckTargetsAsync(warningCancellation.Token).Forget();
            await allApplied;
            warningCancellation.Cancel();

            #region Internal

            // 5秒未完了で詰まっている対象を顕在化し、適用待機自体は継続する
            // Surface targets stuck past five seconds while continuing to wait for their application
            async UniTaskVoid WarnStuckTargetsAsync(CancellationToken cancellationToken)
            {
                // timeScale=0のEditorでも必ず発火させるためRealtimeで測る
                // Measure in realtime so the warning still fires in an Editor sitting at timeScale zero
                var canceled = await UniTask
                    .Delay(TimeSpan.FromSeconds(StuckWarningSeconds), DelayType.Realtime, PlayerLoopTiming.Update, cancellationToken)
                    .SuppressCancellationThrow();
                if (canceled) return;

                // 未完了(Pending)だけを並べる。faultedは例外として上がるので警告に載せない
                // List only Pending targets; faulted ones surface as exceptions instead
                var pending = string.Join(", ", waits.Where(wait => wait.task.Status == UniTaskStatus.Pending).Select(wait => wait.target.GetType().Name));
                if (pending.Length == 0) return;
                Debug.LogWarning($"[InitialEventApplyWaiter] 初期イベント適用が未完了のまま待機中: {pending}");
            }

            #endregion
        }
    }
}
```

- [ ] **Step 4: Finalizer を委譲へ置き換える**

`MainGameInitializationFinalizer.cs` から `WaitAllInitialApplyAsync` を削除し、呼び出しを差し替える。

```csharp
            await InitialEventApplyWaiter.WaitAllAsync(resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>());
```

不要になった using（`System`, `System.Linq`, `Cysharp...` のうち未使用分）を整理する。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InitialEventApplyWaiterTest"`
Expected: 全PASS

- [ ] **Step 6: mutation で有効性を確かめる**

`WaitAllAsync` の `await allApplied;` を一時的に削除 → `全対象が完了するまで待機は完了しない` が落ちることを確認して戻す。落ちなければテストが無効なので報告すること。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/Initialization moorestech_client/Assets/Scripts/Client.Tests/UnitTest
git commit -m "fix: 初期化待機をInitialEventApplyWaiterへ切り出しテストで固定する(C4)"
```

---

### Task 5: C5 — 地表探査の署名をXZ明示にし、既存のz=0バグを潰す

**背景:** `TryGetGroundPoint(Vector3 pos, ...)` は `pos.y` を読まないので、呼び出し側は意味のない `0f` を渡している（`MapVeinObjectDatastore.cs`）。同じファイルの `GetBlockFourCornerMaxHeight` は `GetGroundPoint(new Vector2(minPos.x, minPos.y), ...)` を呼んでおり、Vector2→Vector3の暗黙変換で `(x, y, 0)` になるため四隅すべてを z=0 で探査している実バグが現存する。あわせて、集約後のエントリポイントに名前付き定数が1つも無く（`1000` / `1500` が裸で重複・DrawRay長1000と実探査1500が不一致）、`GetGroundPoint` のデフォルト引数が規約違反として決定論チェックに残っている。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinObjectDatastore.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/SlopeBlockGroundProbeTest.cs`（既存 `Client.Tests/PlaceSystem/` へ追加。10ファイル上限を確認し、超えるならサブディレクトリを切る）

**Interfaces:**
- Produces: `SlopeBlockPlaceSystem.TryGetGroundPoint(float worldX, float worldZ, out Vector3 groundPoint)` / `GetGroundPoint(Vector3 pos)` / `GetGroundPoint(Vector3 pos, Color debugRayColor)`
- Consumes: なし

- [ ] **Step 1: 失敗するテストを書く**

```csharp
        [Test]
        public void 四隅の最大高さはZ座標を無視しない()
        {
            // z=0には地表を置かず、対象ブロックの実位置にだけ地表を置く
            // Leave z=0 without ground and place it only under the target block
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(10f, 30f, 10f);
            _ground.transform.localScale = new Vector3(6f, 1f, 6f);
            Physics.SyncTransforms();

            var height = SlopeBlockPlaceSystem.GetBlockFourCornerMaxHeight(
                new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);

            Assert.AreEqual(30.5f, height, 0.001f, "z=0を探査している");
        }

        [Test]
        public void TryGetGroundPointはXZだけを受け取る()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(4f, 12f, 8f);
            _ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();

            Assert.IsTrue(SlopeBlockPlaceSystem.TryGetGroundPoint(4f, 8f, out var groundPoint));
            Assert.AreEqual(12.5f, groundPoint.y, 0.001f);
            Assert.IsFalse(SlopeBlockPlaceSystem.TryGetGroundPoint(4f, 0f, out _), "地表の無いZでヒットしている");
        }
```

実装者へ: `GetBlockFourCornerMaxHeight` の引数型・`GetWorldBlockBoundingBox` の戻り値型（`minPos.y` が実際にZを表すか）を先に実コードで確認すること。想定と違ったらテストの座標を実態に合わせ、その旨を報告する。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SlopeBlockGroundProbeTest"`
Expected: `TryGetGroundPointはXZだけを受け取る` はコンパイルエラー、`四隅の最大高さはZ座標を無視しない` は FAIL（z=0を探査して地面が見つからずLogError）

- [ ] **Step 3: 定数化と署名変更を行う**

```csharp
        public static readonly int GroundLayerMask = LayerMask.GetMask("Ground");

        // 地表探査のレイ始点高さと探査距離。地形の最高点より十分上から、最低点より下まで貫く
        // Ray start height and probe length of ground probing: from well above the highest terrain to below the lowest
        private const float GroundProbeStartHeight = 1000f;
        private const float GroundProbeDistance = 1500f;
```

```csharp
        // 地表探査の単一エントリポイント。XZだけを取りY成分の取り違えを署名で封じる。露頭など大量プローブ用にログ無しで成否を返す
        // Single entry point of ground probing; taking only XZ makes a mistaken Y impossible, and bulk probes such as outcrops get the outcome without logging
        public static bool TryGetGroundPoint(float worldX, float worldZ, out Vector3 groundPoint)
        {
            var checkRay = new Ray(new Vector3(worldX, GroundProbeStartHeight, worldZ), Vector3.down);
            if (Physics.Raycast(checkRay, out var checkHit, GroundProbeDistance, GroundLayerMask))
            {
                groundPoint = checkHit.point;
                return true;
            }
            groundPoint = default;
            return false;
        }

        public static Vector3? GetGroundPoint(Vector3 pos)
        {
            return GetGroundPoint(pos, default);
        }

        public static Vector3? GetGroundPoint(Vector3 pos, Color debugRayColor)
        {
            Debug.DrawRay(new Vector3(pos.x, GroundProbeStartHeight, pos.z), Vector3.down * GroundProbeDistance, debugRayColor, 3);

            if (!TryGetGroundPoint(pos.x, pos.z, out var groundPoint))
            {
                Debug.LogError("地面が見つかりませんでした pos:" + pos + " layer:" + GroundLayerMask);
                return null;
            }
            return groundPoint;
        }
```

- [ ] **Step 4: 四隅探査のz=0バグを潰す**

`GetBlockFourCornerMaxHeight` の4呼び出しを、XZ明示の探査へ置き換える（`minPos.y` / `maxPos.y` がZ成分であることをコメントで明示する）。

```csharp
            // boundingBoxはXZ平面なので、.yはZ成分として渡す。Vector2の暗黙変換に任せるとz=0を探査してしまう
            // The bounding box lies on the XZ plane, so .y travels as Z; leaving it to the Vector2 conversion would probe z=0
            var heights = new List<float>
            {
                ProbeCornerHeight(minPos.x, minPos.y),
                ProbeCornerHeight(minPos.x, maxPos.y),
                ProbeCornerHeight(maxPos.x, minPos.y),
                ProbeCornerHeight(maxPos.x, maxPos.y),
            };

            return Mathf.Max(heights.ToArray());

            #region Internal

            float ProbeCornerHeight(float worldX, float worldZ)
            {
                if (TryGetGroundPoint(worldX, worldZ, out var groundPoint)) return groundPoint.y;
                throw new InvalidOperationException($"四隅の地表が見つかりませんでした x:{worldX} z:{worldZ}");
            }

            #endregion
```

（従来は `.Value` による無検査参照＝NREだったので、原因の分かる例外へ置き換える）

- [ ] **Step 5: 呼び出し側を追従させる**

`MapVeinObjectDatastore.cs` の `TryGetGroundPoint(new Vector3(x, 0f, z), out var groundPoint)` を `TryGetGroundPoint(x, z, out var groundPoint)` にする。

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SlopeBlockGroundProbeTest|PlayerFallRecoveryPositionTest"`
Expected: 全PASS

- [ ] **Step 7: 決定論チェックの残件が消えたことを確認する**

Run: `grep -n "= default)" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs`
Expected: ヒット0件（デフォルト引数違反の解消）

- [ ] **Step 8: コミットする**

```bash
git commit -am "fix: 地表探査をXZ明示署名へ変え四隅のz=0探査バグを潰す(C5)"
```

---

### Task 6: Codex High② — Train完了ソースが失敗を待機境界へ届けるようにする

**背景:** `TrainFullSnapshotEventNetworkHandler` の完了ソースは成功時の `TrySetResult()` しか持たない。live到着したsnapshotのデシリアライズや `ApplySnapshot()` が例外になると完了ソースはPendingのまま残り、`InitialEventApplyWaiter` の `WhenAll` が無期限待機する。`IInitialEventApplyWaitTarget` の「失敗は待機境界へ届く」という新契約に反する。本PRのトレードオフ欄が据え置きを明記しているのは `Dispose` の畳み込みだけで、例外伝播の欠落は免責対象外。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/TrainFullSnapshotFailurePropagationTest.cs`

**Interfaces:**
- Consumes: `InitialEventApplyWaiter`（Task 4）の失敗伝播契約
- Produces: なし（既存 `WaitForInitialApplyAsync()` の意味を強めるのみ）

- [ ] **Step 1: 失敗するテストを書く**

コールバックはローカル関数のままだとテストから到達できないため、Step 3 でクラス直下のprivateメソッドへ引き上げる。テストはリフレクション経由で叩く（`PlayerFallRecoveryPositionTest.cs` に前例あり）。

```csharp
        [Test]
        public void snapshotの適用失敗は待機タスクへ例外として届く()
        {
            var handler = new TrainFullSnapshotEventNetworkHandler(
                new RailGraphSnapshotApplier(...), new TrainUnitSnapshotApplier(...), new TrainUnitFutureMessageBuffer());
            var waiting = handler.WaitForInitialApplyAsync().Preserve();

            // デシリアライズできないpayloadを外部境界から流し込む
            // Feed an undeserializable payload in through the external boundary
            var method = typeof(TrainFullSnapshotEventNetworkHandler).GetMethod(
                "HandleTrainUnitFullSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(handler, new object[] { new byte[] { 0xC1 } });

            Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status, "適用失敗がPendingのまま残っている");
        }
```

実装者へ: 3つのapplierのコンストラクタ引数が重い場合、テストが成立する最小の組み立て方を探すこと。どうしても組めない場合は「テストが書けない」ことをそのまま報告し、アサーション不在のテストは絶対に出荷しない。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainFullSnapshotFailurePropagationTest"`
Expected: コンパイルエラー（`HandleTrainUnitFullSnapshot` が未定義）

- [ ] **Step 3: コールバックをprivateメソッドへ引き上げ、例外を完了ソースへ畳む**

`Initialize()` は購読登録だけにし、2つのローカル関数をクラス直下のprivateメソッド `HandleRailGraphFullSnapshot(byte[])` / `HandleTrainUnitFullSnapshot(byte[])` へ移す（`#region Internal` はメソッド内ローカル関数用なので、移動に伴って削除する）。

```csharp
        public void Initialize()
        {
            var vanillaApiEvent = ClientContext.VanillaApi.Event;
            _railSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, HandleRailGraphFullSnapshot);
            _trainSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, HandleTrainUnitFullSnapshot);
        }

        // ネットワーク受信payloadのデシリアライズと適用を隔離する外部境界。ここで畳まないと失敗が
        // 完了ソースをPendingのまま残し、初期化のWhenAllが無期限待機に化ける
        // External boundary isolating deserialization and application of a received network payload; without folding
        // failures here the completion source stays Pending and the startup WhenAll hangs forever
        private void HandleRailGraphFullSnapshot(byte[] payload)
        {
            try
            {
                var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(payload);
                _railGraphSnapshotApplier.ApplySnapshot(message.Snapshot);
            }
            catch (Exception applyException)
            {
                _initialApplyCompletion.TrySetException(applyException);
            }
        }
```

`HandleTrainUnitFullSnapshot` も同じ形にし、成功時の `TrySetResult()` は `try` ブロックの末尾に残す。

（`throw` で再送出しない理由も1行で残すこと: イベントディスパッチのループを巻き添えで止めないため。失敗は完了ソース経由で待機境界へ届く）

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainFullSnapshotFailurePropagationTest|TrainUnitFutureMessageBufferTest|TrainUnitTickStateTest"`
Expected: 全PASS

- [ ] **Step 5: コミットする**

```bash
git commit -am "fix: Train snapshot適用の失敗を初期化待機境界へ伝播させる(Codex High2)"
```

---

### Task 7: EditModeInPlayingTest の実機ゲートを1本だけ効かせる

**背景:** C1・C4 のどちらも「PlayMode系テストが全て `LogAssert.ignoreFailingMessages = true` を敷いており、起動時のLogErrorを握り潰す」ことで検出を免れていた。個別ユニットテスト（Task 1・Task 4）で挙動は固定できるが、起動シーケンス全体の回帰を1本だけ実機で押さえる。

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/PlayerStartsOnBuiltTerrainTest.cs`

- [ ] **Step 1: 起動後のプレイヤー座標を固定するテストを書く**

`MapVeinOutcropAndRangeViewTest.cs` の骨格（`EnterPlayModeUtil` → `EnterPlayMode` → `Body().ToCoroutine()` → `ExitPlayMode`）をそのまま踏襲する。ただし本テストの主眼は「握り潰されていたLogError」の検出なので、`LogAssert.ignoreFailingMessages = true` は **EnterPlayMode 直後の1フレームのみ** に留め、`LoadMainGame` の前に `false` へ戻す。

検証内容:
1. `PlayerSystemContainer.Instance.PlayerObjectController.Position` が handshake の `PlayerPos` と一致すること（落下復帰でスポーンへ飛ばされていない）
2. その座標の真下に地表があること（`SlopeBlockPlaceSystem.TryGetGroundPoint(pos.x, pos.z, out _)` が true）

実装者へ: `LogAssert.ignoreFailingMessages` を戻すと EnterPlayMode のフレームワーク内部エラーで落ちる可能性がある。落ちる場合は `LogAssert.Expect` で当該メッセージだけを個別に許可し、「何を許可したか」を報告に列挙すること。全面握り潰しへ戻すのは禁止（それがC1を隠していた当の仕組み）。

- [ ] **Step 2: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerStartsOnBuiltTerrainTest"`
Expected: PASS

ドメインリロードで「Unity is reloading」が出たら45秒待ってリトライする。

- [ ] **Step 3: mutation で有効性を確かめる**

Task 1 で足した `resolver.Resolve<PlayerSystemContainer>().StartPlayerRuntime();` を一時的に削除 → 本テストが落ちることを確認して戻す。落ちなければテストが無効なので報告すること。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain
git commit -m "test: 起動後のプレイヤーが構築済み地形の上に立つことを実機で固定する"
```

---

### Task 8: 最終ブランチ全体レビュー（必須・省略不可）

- [ ] **Step 1: ブランチdiffを生成する**

```bash
.claude/skills/subagent-driven-development/scripts/review-package "$(git merge-base origin/master HEAD)" HEAD
```

- [ ] **Step 2: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後に moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。4カテゴリcontextの「許容するトレードオフ」には本planの ADR#16〜#19 を出所ラベル付きで載せる。

---

## 対応しない指摘と、その理由（次のレビューで再掘りしないための記録）

- **`TerrainTransferMeta` に `TerrainHash` を畳んで2引数分離を解消する（4系統Warning）** — 不可。`TerrainStreamHasher.Compute(directory, terrainMeta)` はハッシュを `terrainMeta` から計算するため、メタがハッシュを持つと循環する。2引数は構造上必要。
- **`MapVeinRangeViewService.ManualUpdate()` の冒頭に `if (!_isVisible) return;`（domain-boundary Warning）** — 入れてはならない。`Show(false)` が「離脱時の残存ボックスを即座に畳む」ために `ManualUpdate()` を呼んでおり、早期returnすると畳みが実行されずボックスが残る。性能最適化として入れるなら「非表示かつ畳み済み」の状態を持つ必要があり、本planの範囲外。
- **`SpawnRegionFinder.AssertSpawnTargetIsInsideGeneratedTile` の既存throw（inv2-seam W3）** — 「設定ゼロでも世界は作られるべき」との整合はユーザー裁定が要る（挙動変更）。Task 3 ではコメントを事実へ狭めるに留め、throw自体は据え置く。
- **`MapObjectGameObjectDatastore.Construct` の二重開始ガード欠落（inv1-consist Warning）** — 本PRが触れていない既存経路。
- **`MainGameStarter.cs` の200行超過** — 努力目標（ユーザー裁定 2026-07-23）。
- **`LoadMainGame` の固定1秒待ちを `GameInitializedEvent` 待ちへ変える（Codex Medium②）** — 全EditModeInPlayingTestへ波及するため別タスク。Task 7 は個別テスト側で必要な待機を行う。

---

## 判断記録（ADR）

親specの判断台帳: `docs/plans/map-autogen-world-design.md` の「## 判断記録（ADR）」（#1〜#19）。本planで新規に生じた判断は #16〜#19 として同表へ追記済み。

| # | 判断 | 出所 |
|---|---|---|
| 16 | C1の解決方式を「新起動順序を保ち、プレイヤーの実行開始だけを地形構築後へ遅らせる」とする | ユーザー裁定 2026-08-03「bで進めたい」 |
| 17 | 地形モード解釈を `ToTerrainTransferMeta()` 1本に確定し、消費側は `TerrainTransferMeta.IsTemplate` で分岐する | 最終レビュー7系統一致（C2） |
| 18 | 地表探査の署名をXZ明示にし、四隅探査のz=0バグを同時に潰す | 最終レビュー3系統一致（C5） |
| 19 | Train snapshot適用の失敗を完了ソースへ畳み、待機境界へ例外として届ける | Codex High②・最終レビュー3系統一致 |
