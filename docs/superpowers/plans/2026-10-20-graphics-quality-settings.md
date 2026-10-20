# 画質設定と常時軽量化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 大規模工場ワールドでも平均20fps以上（目標30fps）で遊べるよう、遠方の動き更新を常時止め、設定画面に「画質設定（高/中/低プリセット＋項目別）」を足し、開発用v7超軽量設定を廃止する。

**Architecture:** 常時軽量化は既存の距離カリング基盤（`PlayerCenteredDistanceCulling`＋`BlockDistanceCullingRegistrar`）とベルトの表示範囲定数で行う。画質設定は走り方式設定（`SprintInputModePreference`＋`SprintInputModeTopic`＋`SettingsActions`）と同型の「端末ごとの静的Preference＋変化Subject＋Topic/Action」で持ち、各描画箇所の適用役（Applier）がSubjectを購読してその場で反映する。

**Tech Stack:** Unity 6 (6000.3.8f1) / URP 17 / UniRx / VContainer / Cinemachine 2 / React + Mantine + zod（moorestech_web/webui）/ NUnit / vitest

## Requirements

- R1: カメラ（プレイヤー）から50mより遠い歯車は回転アニメーションしない。50m以内に入れば回転を再開する。受け入れ: 距離カリング対象の `SetWithinRange(false)` で全 `GearStateChangeProcessor.enabled == false`、`true` で `true` に戻るテストが通る
- R2: 流体パイプの水位表示は状態受信時（`OnChangeState`）だけ更新し、毎フレームの `Update` を持たない。受け入れ: `FluidPipeStateChangeProcessor` に `Update` メソッドが無く、PlayModeで水位が状態変化に追従する（録画テストで目視）
- R3: 50mより遠いベルト上アイテムは描画しない（表示物を具現化しない）。受け入れ: `BeltSegmentVisibleRange.LeaveDistance == 50f` で既存の `BeltItemObjectPresenterTest` が通る
- R4: 設定画面に画質設定を出す。項目と選べる値: 描画距離 300/1000/5000m、影 オン/オフ、描画解像度 100/75/50%、SSAO オン/オフ、ポストエフェクト オン/オフ、エフェクト(VFX・パーティクル・ライト) オン/オフ、木や岩 オン/オフ、地形 精細/粗い、海 オン/オフ。スライダーは使わない
- R5: 三段階プリセット: 高=5000m・影オン・100%・全部オン・地形精細／中=描画距離300mのみ他は高と同じ／低=300m・影オフ・50%・SSAOオフ・ポストオフ・エフェクトオフ・木や岩オフ・地形粗い・海オフ
- R5b: 項目に無いv7の変更は既存項目の最低値に含める: 影オフ=ソフト影・追加ライトの影も停止／描画解像度50%=MSAA・HDRも停止／エフェクトオフ=パーティクルの計算と追加ライトも停止／木や岩オフ=lodBias 0.4・LODクロスフェード停止／海オフ=屈折用の画面コピー（opaque texture）停止。vSync 0 と Chr001 の一部非表示は捨てる。受け入れ: Task 4 Step 3 のPlayMode確認
- R6: 画質設定を一度も保存していないプレイヤーは「中」で始まる（既存テスターも未設定なら中）
- R7: プリセット選択後に項目を個別に変えると、プリセット欄は「カスタム」表示になる。どのプリセットとも一致しない値の組がカスタム。高/中/低を選び直すと全項目が上書きされる
- R8: 全項目を再起動なしでその場で反映する（木や岩・地形・海・既存ブロック/列車のエフェクトを含む）
- R9: 設定はPCごとに保存する（PlayerPrefs。走り方式設定と同じ保存先）
- R10: v7超軽量設定（コミット e97360fc64 の全変更: UltraLightPreset・prefab・URPアセット・QualitySettings・mapObject距離定数10/20m）を撤去し、画質設定「低」で代用する
- R11: 完了基準: 対象の大規模ワールド（メインworktreeでPlayを押して入るワールド。ブロック約1.4万・歯車約1万）を「中」で、歩く・建築する操作を含めて計測し平均20fps以上（目標30fps）
- やらないこと: 300ms〜1秒のカクつき対策（bd moorestech-kr8w）、サーバー側のchangeBlockState送信量削減、セーブ形式の変更、メインメニュー（タイトル）への画質設定の追加、GPU別の自動プリセット判定

## Global Constraints

- AGENTS.md の規約すべて（日英2行コメント、1ファイル200行以下、1ディレクトリ10ファイル以下、partial禁止、`Func<>`禁止、デフォルト引数禁止、try-catch原則禁止、イベントはUniRx、単純getter/setter禁止、`[SerializeField]`は小文字キャメル、初期化は`Initialize`、エディタ専用コードは`#if UNITY_EDITOR`でファイル末尾）
- Prefab・シーン・ScriptableObject・ProjectSettings をテキストで直接編集しない。v7の撤去は `git revert` で行う（Unityが以前シリアライズした内容へ戻すだけなので許容）。`.meta` は手で作らない
- fail-closed・縮退経路は開発者が読めるログ（`Debug.LogWarning`/`LogError`）を出す。意図して黙る行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を通す
- 値の出典: 距離50m（歯車・ベルト）／プリセット値／既定「中」／カスタム表示／即時反映は `.decisions/2026-10-20-*` が正
- Web UI は `moorestech_web/webui`。ローカライズ文言は `Localization/localization.csv`（列: key,Source,english,japanese,german,korean）に足し `npm run gen:i18n` で `src/shared/i18n/generated/localizationKeys.ts` を再生成する

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反3件・修正3件 / エフェクト付与をApplier1か所へ、TopicをDTO形式へ、v7死活表の欠落をユーザー裁定（既存項目の最低値へ含める）で解消
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱3・第3バケツ2 / 強はユーザー裁定で据え置き・プレイヤー中心へ確定、弱は(c)のみ修正、第3バケツはレビュー依頼文へ

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GearRotation/GearRotationCullingManager.cs` | 歯車回転用のプレイヤー中心距離カリング（半径50m）を持つITickable |
| `.../Block/GearRotation/GearRotationCullingRegisterService.cs` | ブロック設置・撤去を距離カリングへ登録（`IBlockDistanceCullingSource`） |
| `.../Block/GearRotation/GearRotationCullingTarget.cs` | 範囲内外を歯車の `GearStateChangeProcessor.enabled` へ渡す |
| `.../BlockSystem/StateProcessor/FluidPipeStateChangeProcessor.cs` | 水位を状態受信時だけ書く（Update撤去） |
| `.../BeltTransport/View/BeltSegmentVisibleRange.cs` | 表示範囲定数を 45/50m へ |
| `.../InGame/GraphicsQuality/GraphicsQualitySettings.cs` | 画質9項目の値（不変struct） |
| `.../GraphicsQuality/GraphicsDrawDistance.cs` / `GraphicsRenderScale.cs` / `GraphicsTerrainDetail.cs` / `GraphicsQualityPreset.cs` | 値域enum（1ファイル1型） |
| `.../GraphicsQuality/GraphicsQualityPresets.cs` | プリセット→値、値→一致プリセット |
| `.../GraphicsQuality/GraphicsQualityPreference.cs` | PlayerPrefs保存・読込・不正値修復・変化Subject |
| `.../GraphicsQuality/Appliers/CameraQualityApplier.cs` | 描画距離・影・ポストをカメラへ |
| `.../GraphicsQuality/Appliers/RenderPipelineQualityApplier.cs` | URPアセットの実行時複製に描画解像度・SSAOを当てる（Editorでは終了時に元へ戻す） |
| `.../GraphicsQuality/Appliers/EnvironmentQualityApplier.cs` | 地形（精細/粗い）と海の表示 |
| `.../GraphicsQuality/Appliers/EffectsQualitySwitch.cs` | ブロック/列車1個分のVFX・パーティクル・ライトの有効状態を保存・復元するMonoBehaviour |
| `.../GraphicsQuality/Appliers/EffectsQualityApplier.cs` | 設定変化時に全ブロック・列車のEffectsQualitySwitchへ配る（IStartable） |
| `.../Map/MapObject/Visibility/MapObjectRendererVisibility.cs` | 距離表示と画質表示のANDでRendererを切替 |
| `.../Map/MapObject/Visibility/MapObjectRendererVisibilityRegistry.cs` | 全mapObject（ランドマーク含む）の表示物を持ち画質変化を配る |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Settings/GraphicsQualityTopic.cs` | `settings.graphicsQuality` の初期値・変化配信 |
| `.../Client.WebUiHost/Game/Actions/Settings/GraphicsQualityActions.cs` | `settings.applyGraphicsQualityPreset` / `settings.setGraphicsQuality` |
| `.../Client.WebUiHost/Game/Actions/Settings/GraphicsQualityWire.cs` | C#値⇔wire JSON |
| `moorestech_web/webui/src/features/settings/GraphicsQualitySettings.tsx` | 設定画面の画質セクション |

前例: 走り方式設定 `Client.Input/Sprint/SprintInputModePreference.cs`・`Client.WebUiHost/Game/Topics/Settings/SprintInputModeTopic.cs`・`Client.WebUiHost/Game/Actions/Settings/SettingsActions.cs`・`moorestech_web/webui/src/features/settings/SprintModeSelect.tsx`。ブロック距離カリング `Client.Game/InGame/Block/Preview/BlockPreviewRadiusCullingManager.cs`・`BlockPreviewRadiusRegisterService.cs`・`BlockPreviewRadiusCullingTarget.cs`。

---

### Task 1: v7超軽量設定を撤去する

**Files:**
- Modify（git revert）: e97360fc64 が触った17ファイル（`moorestech_client/Assets/Scripts/Client.Game/InGame/UltraLight/UltraLightPreset.cs` 削除、`moorestech_client/Assets/Asset/Common/URPSettings/UniversalRP-HighQuality.asset`、`moorestech_client/Assets/Asset/Common/URPSettings/URP Data.asset`、`moorestech_client/ProjectSettings/QualitySettings.asset`、`moorestech_client/Assets/AddressableResources/Block/Stone_oven.prefab`、`moorestech_client/Assets/AddressableResources/Character/Chr001.prefab`、`moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef` ほか）

**Interfaces:**
- Produces: `UltraLightPreset` が存在しない状態。`Client.Game.asmdef` から URP 参照が外れる（Task 6 で必要分を戻す）

- [ ] **Step 1: revertする**

```bash
git revert --no-edit e97360fc64
```
コンフリクトしたら、e97360fc64 より後の変更を残しつつ v7 の差分だけを打ち消す向きで解消する（`git show e97360fc64` を見て逆適用）。

- [ ] **Step 2: 残骸が無いことを確かめる**

Run: `grep -rn "UltraLight" moorestech_client/Assets/Scripts` → Expected: 出力なし
Run: `grep -n "RestoreDistance = \|HideDistance = " moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/Visibility/MapObjectDistanceVisibilityController.cs` → Expected: `340f` と `350f`

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client` → Expected: ErrorCount 0

（revertはコミット済みなので追加コミットは不要）

### Task 2: 常時軽量化（遠方歯車の回転停止・パイプ水位の状態駆動・遠方ベルトアイテム非表示）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GearRotation/GearRotationCullingManager.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GearRotation/GearRotationCullingRegisterService.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GearRotation/GearRotationCullingTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`BlockPreviewRadiusRegisterService` 登録の直後）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/FluidPipeStateChangeProcessor.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/View/BeltSegmentVisibleRange.cs:12-13`
- Modify: `docs/adr/0085-belt-item-objects-materialize-within-camera-range.md`（末尾に改訂注記1行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Gear/GearRotationCullingTargetTest.cs`

**Interfaces:**
- Consumes: `PlayerCenteredDistanceCulling(PlayerSystemContainer, float chunkSize, float activeRadius, float reevalMoveDistance)`、`BlockDistanceCullingRegistrar(BlockGameObjectDataStore, PlayerCenteredDistanceCulling, IBlockDistanceCullingSource)`、`BlockDistanceCullingEntry(Bounds, IDistanceCullingTarget)`（internal ctor。同アセンブリ）
- Produces: `GearRotationCullingTarget(GearStateChangeProcessor[] processors)` : `IDistanceCullingTarget`

- [ ] **Step 1: テストを書く** — `Client.Tests/Gear/GearRotationCullingTargetTest.cs`

```csharp
using Client.Game.InGame.Block.GearRotation;
using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearRotationCullingTargetTest
    {
        [Test]
        public void OutsideRangeDisablesAndInsideRangeRestoresEveryGearProcessor()
        {
            var root = new GameObject("gear");
            var processors = new[] { root.AddComponent<GearStateChangeProcessor>(), new GameObject("child").AddComponent<GearStateChangeProcessor>() };
            processors[1].transform.SetParent(root.transform);
            var target = new GearRotationCullingTarget(processors);

            // 範囲外で全停止し、範囲内で全再開する
            // Every processor stops outside the range and resumes inside it
            target.SetWithinRange(false);
            Assert.IsFalse(processors[0].enabled);
            Assert.IsFalse(processors[1].enabled);
            target.SetWithinRange(true);
            Assert.IsTrue(processors[0].enabled);
            Assert.IsTrue(processors[1].enabled);
            Object.DestroyImmediate(root);
        }
    }
}
```
（`GearStateChangeProcessor` は `[RequireComponent(typeof(GearStateChangeProcessorSimulator))]` なので AddComponent で Simulator も付く。付かずに失敗した場合は先に Simulator を AddComponent する）

- [ ] **Step 2: 実装を書く**

`GearRotationCullingManager.cs`:
```csharp
using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// - 歯車回転の更新範囲を制御
    /// - プレイヤー周囲50mが基準
    /// - Control which gears animate rotation
    /// - Use a 50m radius around the player
    /// </summary>
    public sealed class GearRotationCullingManager : ITickable
    {
        // 半径50mはユーザー裁定。チャンク8mで超過を最大約11mに抑え、再評価は2m移動ごと
        // 50m radius is a user ruling; 8m chunks cap overshoot near 11m, re-evaluated per 2m of movement
        private const float ChunkSize = 8f;
        private const float ActiveRadius = 50f;
        private const float ReevalMoveDistance = 2f;

        internal readonly PlayerCenteredDistanceCulling Culling;

        public GearRotationCullingManager(PlayerSystemContainer playerSystemContainer)
        {
            Culling = new PlayerCenteredDistanceCulling(playerSystemContainer, ChunkSize, ActiveRadius, ReevalMoveDistance);
        }

        public void Tick()
        {
            Culling.Tick();
        }
    }
}
```

`GearRotationCullingRegisterService.cs`:
```csharp
using Client.Game.InGame.Block.DistanceCulling;
using Client.Game.InGame.BlockSystem.StateProcessor;
using VContainer.Unity;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// ブロックを歯車回転の距離判定へ占有セルの範囲で登録する
    /// Registers blocks with the gear-rotation distance culling using their occupied cells
    /// </summary>
    public sealed class GearRotationCullingRegisterService : IStartable, IBlockDistanceCullingSource
    {
        private readonly BlockDistanceCullingRegistrar _registrar;

        public GearRotationCullingRegisterService(BlockGameObjectDataStore dataStore, GearRotationCullingManager manager)
        {
            _registrar = new BlockDistanceCullingRegistrar(dataStore, manager.Culling, this);
        }

        public void Start()
        {
            _registrar.Initialize();
        }

        public BlockDistanceCullingEntry CreateEntry(BlockGameObject block)
        {
            // 歯車を持たないブロックも空の対象で登録し、登録経路を一本に保つ
            // Blocks without gears register an empty target so the registration path stays single
            var posInfo = block.BlockPosInfo;
            var bounds = new UnityEngine.Bounds();
            bounds.SetMinMax(posInfo.MinPos, posInfo.MaxPos + UnityEngine.Vector3Int.one);
            var processors = block.GetComponentsInChildren<GearStateChangeProcessor>(true);
            return new BlockDistanceCullingEntry(bounds, new GearRotationCullingTarget(processors));
        }
    }
}
```

`GearRotationCullingTarget.cs`:
```csharp
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.DistanceCulling;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// 半径内/外の指示を歯車の回転更新の有効状態へ渡す。状態受信は無効中も続く
    /// Forwards within/outside-radius instructions to gear rotation updates; state reception continues while disabled
    /// </summary>
    public sealed class GearRotationCullingTarget : IDistanceCullingTarget
    {
        private readonly GearStateChangeProcessor[] _processors;

        public GearRotationCullingTarget(GearStateChangeProcessor[] processors)
        {
            _processors = processors;
        }

        public void SetWithinRange(bool withinRange)
        {
            foreach (var processor in _processors) processor.enabled = withinRange;
        }
    }
}
```

`MainGameModelRegistration.cs`（`builder.RegisterEntryPoint<BlockPreviewRadiusRegisterService>();` の直後に追加。using `Client.Game.InGame.Block.GearRotation;`）:
```csharp
            builder.Register<GearRotationCullingManager>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.RegisterEntryPoint<GearRotationCullingRegisterService>();
```

`FluidPipeStateChangeProcessor.cs` の `Start`〜`OnChangeState` を次に置き換える（`Update` は削除）:
```csharp
        private void Start()
        {
            if (meshRenderer == null) return;

            _waterLevelMaterial = Instantiate(meshRenderer.sharedMaterial);
            meshRenderer.sharedMaterial = _waterLevelMaterial;

            // Start前に届いた状態があればそれを、無ければ空を表示する
            // Show a state that arrived before Start, otherwise show empty
            ApplyCurrentWaterLevel();
        }

        public void Initialize(BlockGameObject blockGameObject) { }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentFluidPipeState = blockState.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.BlockStateDetailKey);

            // 共有マテリアルを書き換えないよう複製前は反映をStartへ任せる
            // Leave the update to Start until the material is cloned so the shared material is never written
            // silent-ok: 複製前の状態はStartのApplyCurrentWaterLevelが必ず反映する / 録画テストの水位表示で気づく
            if (_waterLevelMaterial == null) return;
            ApplyCurrentWaterLevel();
        }

        private void ApplyCurrentWaterLevel()
        {
            var waterLevel = _currentFluidPipeState == null ? 0f : _currentFluidPipeState.Amount / _currentFluidPipeState.Capacity;
            UpdateWaterLevel(waterLevel);
        }
```

`BeltSegmentVisibleRange.cs:12-13`:
```csharp
        public const float EnterDistance = 45f;
        public const float LeaveDistance = 50f;
```

`docs/adr/0085-belt-item-objects-materialize-within-camera-range.md` 末尾に追記:
```markdown

改訂: 範囲は 45/50m に縮めた（`.decisions/2026-10-20-遠方の歯車回転停止とパイプ水位の状態駆動とベルトアイテム非表示は常時入れる.md`）。
```

- [ ] **Step 3: コンパイルしテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearRotationCullingTargetTest|BeltItemObjectPresenterTest|BeltSegmentVisibleRange"` → PASS（既存テストが340/350mを直書きしていて落ちたら、定数参照に直すか値を45/50へ直す）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GearRotation moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/FluidPipeStateChangeProcessor.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/View/BeltSegmentVisibleRange.cs moorestech_client/Assets/Scripts/Client.Tests/Gear docs/adr/0085-belt-item-objects-materialize-within-camera-range.md
git commit -m "perf: 遠方の歯車回転・パイプ水位の毎フレーム更新・遠方ベルトアイテム表示を止める"
```

### Task 3: 画質設定のモデルと保存

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsDrawDistance.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsRenderScale.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsTerrainDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsQualityPreset.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsQualitySettings.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsQualityPresets.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/GraphicsQualityPreference.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/GraphicsQuality/GraphicsQualityPreferenceTest.cs`

**Interfaces:**
- Produces:
  - `enum GraphicsDrawDistance { Meters300, Meters1000, Meters5000 }` ＋ `static class GraphicsDrawDistanceExtensions { public static float ToMeters(this GraphicsDrawDistance) }`
  - `enum GraphicsRenderScale { Percent100, Percent75, Percent50 }` ＋ `ToScale(this GraphicsRenderScale) : float`
  - `enum GraphicsTerrainDetail { Fine, Coarse }`
  - `enum GraphicsQualityPreset { High, Medium, Low }`
  - `readonly struct GraphicsQualitySettings : IEquatable<GraphicsQualitySettings>`（ctor 引数順: drawDistance, shadowsEnabled, renderScale, ssaoEnabled, postProcessingEnabled, effectsEnabled, mapObjectsVisible, terrainDetail, oceanVisible／同名PascalCaseの `{ get; }`）
  - `static class GraphicsQualityPresets { GraphicsQualitySettings Get(GraphicsQualityPreset); bool TryMatch(GraphicsQualitySettings, out GraphicsQualityPreset) }`
  - `static class GraphicsQualityPreference { GraphicsQualitySettings Current; IObservable<GraphicsQualitySettings> OnSettingsChanged; void SetSettings(GraphicsQualitySettings); void ApplyPreset(GraphicsQualityPreset); internal const string PreferenceKey = "GraphicsQuality"; }`

- [ ] **Step 1: テストを書く** — `Client.Tests/GraphicsQuality/GraphicsQualityPreferenceTest.cs`

```csharp
using System.Text.RegularExpressions;
using Client.Game.InGame.GraphicsQuality;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.GraphicsQuality
{
    public class GraphicsQualityPreferenceTest
    {
        private bool _hadSaved;
        private string _saved;

        [SetUp]
        public void SetUp()
        {
            // 開発者の保存値を退避し未保存から始める
            // Stash the developer preference and start unsaved
            _hadSaved = PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey);
            _saved = PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey);
            PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSaved) PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, _saved);
            else PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void UnsavedPreferenceStartsAtMedium()
        {
            Assert.AreEqual(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium), GraphicsQualityPreference.Current);
        }

        [Test]
        public void SavedSettingsRoundTripAndNotifyOnlyOnChange()
        {
            var changes = 0;
            using var subscription = GraphicsQualityPreference.OnSettingsChanged.Subscribe(_ => changes++);
            var custom = new GraphicsQualitySettings(GraphicsDrawDistance.Meters1000, false, GraphicsRenderScale.Percent75, true, false, true, false, GraphicsTerrainDetail.Coarse, true);

            // 同じ値の再保存では通知しない
            // Re-saving the same value does not notify
            GraphicsQualityPreference.SetSettings(custom);
            GraphicsQualityPreference.SetSettings(custom);
            Assert.AreEqual(custom, GraphicsQualityPreference.Current);
            Assert.AreEqual(1, changes);
            Assert.IsFalse(GraphicsQualityPresets.TryMatch(custom, out _));
        }

        [Test]
        public void ApplyPresetOverwritesEveryItem()
        {
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            Assert.AreEqual(GraphicsQualityPresets.Get(GraphicsQualityPreset.Low), GraphicsQualityPreference.Current);
            Assert.IsTrue(GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out var preset));
            Assert.AreEqual(GraphicsQualityPreset.Low, preset);
        }

        [TestCase("garbage")]
        [TestCase("Meters300|true|Percent100|true|true|true|true|Fine")]
        [TestCase("Meters999|true|Percent100|true|true|true|true|Fine|true")]
        public void InvalidSavedValueIsRepairedToMediumWithWarning(string saved)
        {
            PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, saved);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphicsQualityPreference\] Unknown saved graphics quality"));
            Assert.AreEqual(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium), GraphicsQualityPreference.Current);
            Assert.AreEqual(GraphicsQualityPreference.Serialize(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium)), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

`GraphicsDrawDistance.cs`:
```csharp
namespace Client.Game.InGame.GraphicsQuality
{
    public enum GraphicsDrawDistance
    {
        Meters300,
        Meters1000,
        Meters5000,
    }

    public static class GraphicsDrawDistanceExtensions
    {
        public static float ToMeters(this GraphicsDrawDistance drawDistance)
        {
            return drawDistance switch
            {
                GraphicsDrawDistance.Meters300 => 300f,
                GraphicsDrawDistance.Meters1000 => 1000f,
                GraphicsDrawDistance.Meters5000 => 5000f,
                _ => throw new System.ArgumentOutOfRangeException(nameof(drawDistance), drawDistance, null),
            };
        }
    }
}
```

`GraphicsRenderScale.cs`:
```csharp
namespace Client.Game.InGame.GraphicsQuality
{
    public enum GraphicsRenderScale
    {
        Percent100,
        Percent75,
        Percent50,
    }

    public static class GraphicsRenderScaleExtensions
    {
        public static float ToScale(this GraphicsRenderScale renderScale)
        {
            return renderScale switch
            {
                GraphicsRenderScale.Percent100 => 1f,
                GraphicsRenderScale.Percent75 => 0.75f,
                GraphicsRenderScale.Percent50 => 0.5f,
                _ => throw new System.ArgumentOutOfRangeException(nameof(renderScale), renderScale, null),
            };
        }
    }
}
```

`GraphicsTerrainDetail.cs`:
```csharp
namespace Client.Game.InGame.GraphicsQuality
{
    public enum GraphicsTerrainDetail
    {
        Fine,
        Coarse,
    }
}
```

`GraphicsQualityPreset.cs`:
```csharp
namespace Client.Game.InGame.GraphicsQuality
{
    public enum GraphicsQualityPreset
    {
        High,
        Medium,
        Low,
    }
}
```

`GraphicsQualitySettings.cs`:
```csharp
using System;

namespace Client.Game.InGame.GraphicsQuality
{
    /// <summary>
    /// 画質設定の全項目の値。プリセットとの一致判定に値の等価を使う
    /// Every graphics-quality item value; value equality decides whether it matches a preset
    /// </summary>
    public readonly struct GraphicsQualitySettings : IEquatable<GraphicsQualitySettings>
    {
        public GraphicsDrawDistance DrawDistance { get; }
        public bool ShadowsEnabled { get; }
        public GraphicsRenderScale RenderScale { get; }
        public bool SsaoEnabled { get; }
        public bool PostProcessingEnabled { get; }
        public bool EffectsEnabled { get; }
        public bool MapObjectsVisible { get; }
        public GraphicsTerrainDetail TerrainDetail { get; }
        public bool OceanVisible { get; }

        public GraphicsQualitySettings(GraphicsDrawDistance drawDistance, bool shadowsEnabled, GraphicsRenderScale renderScale, bool ssaoEnabled,
            bool postProcessingEnabled, bool effectsEnabled, bool mapObjectsVisible, GraphicsTerrainDetail terrainDetail, bool oceanVisible)
        {
            DrawDistance = drawDistance;
            ShadowsEnabled = shadowsEnabled;
            RenderScale = renderScale;
            SsaoEnabled = ssaoEnabled;
            PostProcessingEnabled = postProcessingEnabled;
            EffectsEnabled = effectsEnabled;
            MapObjectsVisible = mapObjectsVisible;
            TerrainDetail = terrainDetail;
            OceanVisible = oceanVisible;
        }

        public bool Equals(GraphicsQualitySettings other)
        {
            return DrawDistance == other.DrawDistance && ShadowsEnabled == other.ShadowsEnabled && RenderScale == other.RenderScale &&
                   SsaoEnabled == other.SsaoEnabled && PostProcessingEnabled == other.PostProcessingEnabled && EffectsEnabled == other.EffectsEnabled &&
                   MapObjectsVisible == other.MapObjectsVisible && TerrainDetail == other.TerrainDetail && OceanVisible == other.OceanVisible;
        }

        public override bool Equals(object obj)
        {
            return obj is GraphicsQualitySettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(DrawDistance);
            hash.Add(ShadowsEnabled);
            hash.Add(RenderScale);
            hash.Add(SsaoEnabled);
            hash.Add(PostProcessingEnabled);
            hash.Add(EffectsEnabled);
            hash.Add(MapObjectsVisible);
            hash.Add(TerrainDetail);
            hash.Add(OceanVisible);
            return hash.ToHashCode();
        }
    }
}
```

`GraphicsQualityPresets.cs`:
```csharp
namespace Client.Game.InGame.GraphicsQuality
{
    /// <summary>
    /// 高/中/低の値表。中は描画距離だけを縮め、低は全項目を最低値にする（v7の残りは各項目の最低値が担う）
    /// The high/medium/low value table; medium only shortens the draw distance, low puts every item at its lowest (the rest of v7 rides on those lowest values)
    /// </summary>
    public static class GraphicsQualityPresets
    {
        private static readonly GraphicsQualityPreset[] AllPresets = { GraphicsQualityPreset.High, GraphicsQualityPreset.Medium, GraphicsQualityPreset.Low };

        public static GraphicsQualitySettings Get(GraphicsQualityPreset preset)
        {
            return preset switch
            {
                GraphicsQualityPreset.High => new GraphicsQualitySettings(GraphicsDrawDistance.Meters5000, true, GraphicsRenderScale.Percent100, true, true, true, true, GraphicsTerrainDetail.Fine, true),
                GraphicsQualityPreset.Medium => new GraphicsQualitySettings(GraphicsDrawDistance.Meters300, true, GraphicsRenderScale.Percent100, true, true, true, true, GraphicsTerrainDetail.Fine, true),
                GraphicsQualityPreset.Low => new GraphicsQualitySettings(GraphicsDrawDistance.Meters300, false, GraphicsRenderScale.Percent50, false, false, false, false, GraphicsTerrainDetail.Coarse, false),
                _ => throw new System.ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        // どのプリセットとも一致しなければfalse（設定画面ではカスタム表示）
        // Returns false when no preset matches (shown as custom on the settings screen)
        public static bool TryMatch(GraphicsQualitySettings settings, out GraphicsQualityPreset matched)
        {
            foreach (var preset in AllPresets)
            {
                if (!Get(preset).Equals(settings)) continue;
                matched = preset;
                return true;
            }
            matched = GraphicsQualityPreset.Medium;
            return false;
        }
    }
}
```

`GraphicsQualityPreference.cs`:
```csharp
using System;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.GraphicsQuality
{
    /// <summary>
    /// - 端末ごとの画質設定の保存
    /// - 走り方式と同じ保存先
    /// - 値の変化通知
    /// - Stores graphics quality per device
    /// - Uses the same store as the sprint mode
    /// - Notifies value changes
    /// </summary>
    public static class GraphicsQualityPreference
    {
        internal const string PreferenceKey = "GraphicsQuality";
        private const char Separator = '|';
        private const int FieldCount = 9;
        private static readonly Subject<GraphicsQualitySettings> SettingsChangedSubject = new();

        public static IObservable<GraphicsQualitySettings> OnSettingsChanged => SettingsChangedSubject;
        public static GraphicsQualitySettings Current => Load();

        public static void ApplyPreset(GraphicsQualityPreset preset)
        {
            SetSettings(GraphicsQualityPresets.Get(preset));
        }

        public static void SetSettings(GraphicsQualitySettings settings)
        {
            var changed = !Load().Equals(settings);
            PlayerPrefs.SetString(PreferenceKey, Serialize(settings));
            PlayerPrefs.Save();
            // silent-ok: 同値の再保存は画面に変化が無い / 保存テストで再通知が無いことを確認する
            if (!changed) return;
            SettingsChangedSubject.OnNext(settings);
        }

        internal static string Serialize(GraphicsQualitySettings s)
        {
            return string.Join(Separator, s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled,
                s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible);
        }

        private static GraphicsQualitySettings Load()
        {
            // 未保存は既定の中（裁定 2026-10-20）
            // Unsaved means the default medium (ruling 2026-10-20)
            var medium = GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);
            if (!PlayerPrefs.HasKey(PreferenceKey)) return medium;

            var saved = PlayerPrefs.GetString(PreferenceKey);
            if (TryDeserialize(saved, out var settings)) return settings;

            // 不正値は中へ修復し、開発者が読めるログへ残す
            // Repair an invalid value to medium and leave a log developers can read
            Debug.LogWarning($"[GraphicsQualityPreference] Unknown saved graphics quality '{saved}'. Using Medium.");
            PlayerPrefs.SetString(PreferenceKey, Serialize(medium));
            PlayerPrefs.Save();
            return medium;

            #region Internal

            bool TryDeserialize(string text, out GraphicsQualitySettings result)
            {
                result = medium;
                var fields = text.Split(Separator);
                if (fields.Length != FieldCount) return false;
                if (!TryEnum(fields[0], out GraphicsDrawDistance drawDistance) || !bool.TryParse(fields[1], out var shadows) ||
                    !TryEnum(fields[2], out GraphicsRenderScale renderScale) || !bool.TryParse(fields[3], out var ssao) ||
                    !bool.TryParse(fields[4], out var post) || !bool.TryParse(fields[5], out var effects) ||
                    !bool.TryParse(fields[6], out var mapObjects) || !TryEnum(fields[7], out GraphicsTerrainDetail terrain) ||
                    !bool.TryParse(fields[8], out var ocean)) return false;
                result = new GraphicsQualitySettings(drawDistance, shadows, renderScale, ssao, post, effects, mapObjects, terrain, ocean);
                return true;
            }

            bool TryEnum<TEnum>(string text, out TEnum value) where TEnum : struct, Enum
            {
                // TryParseは"7"等の数値文字列も通すため定義域も確かめる
                // TryParse also accepts numeric strings like "7", so check the defined set too
                return Enum.TryParse(text, out value) && Enum.IsDefined(typeof(TEnum), value);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: コンパイルしテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.GraphicsQuality.GraphicsQualityPreferenceTest"` → PASS

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality moorestech_client/Assets/Scripts/Client.Tests/GraphicsQuality
git commit -m "feat: 画質設定の値・プリセット・端末保存を追加する"
```

### Task 4: カメラと描画パイプラインへの適用（描画距離・影・ポスト・解像度・SSAO）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers/CameraQualityApplier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers/RenderPipelineQualityApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef`（references に `Unity.RenderPipelines.Universal.Runtime`・`Unity.RenderPipelines.Core.Runtime` を足す。Task 1 のrevertで外れたもの）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs`（`Initialize`）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`

**Interfaces:**
- Consumes: `GraphicsQualityPreference.Current` / `OnSettingsChanged`、`GraphicsDrawDistance.ToMeters()`、`GraphicsRenderScale.ToScale()`
- Produces: `CameraQualityApplier.Apply(Camera camera, CinemachineVirtualCamera virtualCamera, GraphicsQualitySettings settings)`、`RenderPipelineQualityApplier : IStartable`

- [ ] **Step 1: 実装を書く**

`CameraQualityApplier.cs`:
```csharp
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// 描画距離・影・ポストエフェクトをカメラ単位で当てる
    /// Applies draw distance, shadows and post-processing per camera
    /// </summary>
    public static class CameraQualityApplier
    {
        public static void Apply(Camera camera, CinemachineVirtualCamera virtualCamera, GraphicsQualitySettings settings)
        {
            // Cinemachineのlensが毎フレーム上書きするため両方へ入れる
            // Cinemachine's lens overwrites the camera every frame, so set both
            var farClipPlane = settings.DrawDistance.ToMeters();
            camera.farClipPlane = farClipPlane;
            var lens = virtualCamera.m_Lens;
            lens.FarClipPlane = farClipPlane;
            virtualCamera.m_Lens = lens;

            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = settings.ShadowsEnabled;
            cameraData.renderPostProcessing = settings.PostProcessingEnabled;

            // 海を消すと屈折用の画面コピーも要らない
            // With the ocean hidden, the refraction screen copy is unnecessary
            cameraData.requiresColorOption = settings.OceanVisible ? CameraOverrideOption.UsePipelineSettings : CameraOverrideOption.Off;
        }
    }
}
```

`InGameCameraController.Initialize` を次にする（using `Client.Game.InGame.GraphicsQuality;` `Client.Game.InGame.GraphicsQuality.Appliers;` `UniRx;`）:
```csharp
        public void Initialize()
        {
            CameraManager.RegisterCamera(this);

            // 画質設定を今すぐ当て、以後の変更も即時反映する
            // Apply graphics quality now and follow later changes immediately
            CameraQualityApplier.Apply(mainCamera, virtualCamera, GraphicsQualityPreference.Current);
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(settings => CameraQualityApplier.Apply(mainCamera, virtualCamera, settings)).AddTo(this);
        }
```

`RenderPipelineQualityApplier.cs`:
```csharp
using System.Reflection;
using UniRx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// URPアセットとrenderer dataを実行時に複製し、描画解像度とSSAOを複製側だけに当てる（アセットを汚さない）
    /// Clones the URP asset and renderer data at runtime and applies render scale and SSAO to the clone only (assets stay untouched)
    /// </summary>
    public sealed class RenderPipelineQualityApplier : IStartable
    {
        private const string RendererDataListField = "m_RendererDataList";
        private const float LowLodBias = 0.4f;
        private UniversalRenderPipelineAsset _source;
        private UniversalRenderPipelineAsset _runtimePipeline;
        private ScriptableRendererFeature[] _ssaoFeatures;
        private float _sourceLodBias;

        public void Start()
        {
            _source = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            _sourceLodBias = QualitySettings.lodBias;
            _runtimePipeline = Object.Instantiate(_source);
            _ssaoFeatures = CloneRendererData(_runtimePipeline);
            QualitySettings.renderPipeline = _runtimePipeline;
#if UNITY_EDITOR
            RestoreOnExitPlayMode(_source, _sourceLodBias);
#endif

            Apply(GraphicsQualityPreference.Current);
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(Apply);
        }

        private void Apply(GraphicsQualitySettings settings)
        {
            _runtimePipeline.renderScale = settings.RenderScale.ToScale();
            foreach (var feature in _ssaoFeatures) feature.SetActive(settings.SsaoEnabled);

            // 項目に無いv7の変更は各項目の最低値に含める（裁定 2026-10-20）。最低値以外は元アセットの値へ戻す
            // v7 changes without an item ride on each item's lowest value (ruling 2026-10-20); otherwise restore the source asset's value
            var isLowResolution = settings.RenderScale == GraphicsRenderScale.Percent50;
            _runtimePipeline.msaaSampleCount = isLowResolution ? 1 : _source.msaaSampleCount;
            _runtimePipeline.supportsHDR = !isLowResolution && _source.supportsHDR;
            CopyOrDisable("m_SoftShadowsSupported", settings.ShadowsEnabled);
            CopyOrDisable("m_AdditionalLightShadowsSupported", settings.ShadowsEnabled);
            CopyOrDisable("m_EnableLODCrossFade", settings.MapObjectsVisible);
            SetField("m_AdditionalLightsRenderingMode", settings.EffectsEnabled ? GetField("m_AdditionalLightsRenderingMode", _source) : LightRenderingMode.Disabled);
            QualitySettings.lodBias = settings.MapObjectsVisible ? _sourceLodBias : LowLodBias;

            #region Internal

            void CopyOrDisable(string fieldName, bool keepSource)
            {
                SetField(fieldName, keepSource && (bool)GetField(fieldName, _source));
            }

            #endregion
        }

        // 公開setterの無い直列化フィールドは複製側だけをリフレクションで書く。名前が変わったら起動時に止める
        // Serialized fields without public setters are written on the clone only via reflection; a renamed field stops startup
        private void SetField(string fieldName, object value)
        {
            FindField(fieldName).SetValue(_runtimePipeline, value);
        }

        private static object GetField(string fieldName, UniversalRenderPipelineAsset asset)
        {
            return FindField(fieldName).GetValue(asset);
        }

        private static FieldInfo FindField(string fieldName)
        {
            var field = typeof(UniversalRenderPipelineAsset).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new System.InvalidOperationException($"[RenderPipelineQualityApplier] {fieldName} not found on URP asset.");
            return field;
        }

        // renderer dataとfeatureも複製しないとSSAOの切替が元アセットへ書き込まれる
        // Without cloning renderer data and features, toggling SSAO would write into the source assets
        private static ScriptableRendererFeature[] CloneRendererData(UniversalRenderPipelineAsset pipeline)
        {
            var field = typeof(UniversalRenderPipelineAsset).GetField(RendererDataListField, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new System.InvalidOperationException($"[RenderPipelineQualityApplier] {RendererDataListField} not found on URP asset.");
            var sourceList = (ScriptableRendererData[])field.GetValue(pipeline);
            var clonedList = new ScriptableRendererData[sourceList.Length];
            var ssaoFeatures = new System.Collections.Generic.List<ScriptableRendererFeature>();
            for (var index = 0; index < sourceList.Length; index++)
            {
                var clonedData = Object.Instantiate(sourceList[index]);
                var features = clonedData.rendererFeatures;
                for (var featureIndex = 0; featureIndex < features.Count; featureIndex++)
                {
                    features[featureIndex] = Object.Instantiate(features[featureIndex]);
                    if (features[featureIndex] is ScreenSpaceAmbientOcclusion) ssaoFeatures.Add(features[featureIndex]);
                }
                clonedData.SetDirty();
                clonedList[index] = clonedData;
            }
            field.SetValue(pipeline, clonedList);
            return ssaoFeatures.ToArray();
        }

#if UNITY_EDITOR
        // Editorでは実行時の差し替えがQualitySettingsへ残るため、PlayMode終了で元アセットへ戻す
        // In the Editor the runtime swap would persist in QualitySettings, so restore the source asset when PlayMode exits
        private static void RestoreOnExitPlayMode(UniversalRenderPipelineAsset source, float sourceLodBias)
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
                QualitySettings.renderPipeline = source;
                QualitySettings.lodBias = sourceLodBias;
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
#endif
    }
}
```
（`m_AdditionalLightsRenderingMode` の型が `LightRenderingMode` でない場合は `GetField` の戻り値の型に合わせる。`ScreenSpaceAmbientOcclusion` が internal で参照できない場合は `features[featureIndex].GetType().Name == "ScreenSpaceAmbientOcclusion"` で判定する）

`MainGameModelRegistration.cs`（Task 2 で足した行の直後。using `Client.Game.InGame.GraphicsQuality.Appliers;`）:
```csharp
            builder.RegisterEntryPoint<RenderPipelineQualityApplier>();
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0

- [ ] **Step 3: PlayModeで確かめる**

PlayModeに入り、`uloop execute-dynamic-code` で `Client.Game.InGame.GraphicsQuality.GraphicsQualityPreference.ApplyPreset(Low)`→`High` を順に呼び、各回 `Camera.main.farClipPlane`・`GetUniversalAdditionalCameraData().renderShadows`・`((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale` が Low=300/false/0.5、High=5000/true/1 になり、Low で `msaaSampleCount==1`・`supportsHDR==false`・`QualitySettings.lodBias==0.4`、High で元の値（2・true・1）に戻ることを返り値で確かめる。PlayModeを止めた後 `git status --short moorestech_client/ProjectSettings moorestech_client/Assets/Asset/Common/URPSettings` が空であること（アセットを汚していない）を確かめる。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs
git commit -m "feat: 描画距離・影・ポスト・描画解像度・SSAOを画質設定から即時反映する"
```

### Task 5: 地形・海・木や岩・エフェクトへの適用

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers/EnvironmentQualityApplier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers/EffectsQualitySwitch.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers/EffectsQualityApplier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/Visibility/MapObjectRendererVisibilityRegistry.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/Visibility/MapObjectRendererVisibility.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/Visibility/MapObjectDistanceVisibilityController.cs`（`Register` の引数）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectLayoutInstantiator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`（`TerrainRuntimeBuilder.BuildAsync` の直後）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarObjectDatastore.cs`（列車生成の通知 `OnTrainCarCreated`）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility/MapObjectDistanceVisibilityControllerTest.cs`（`Register` 呼び出しの追従）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility/MapObjectRendererVisibilityTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/GraphicsQuality/EffectsQualitySwitchTest.cs`

**Interfaces:**
- Consumes: `GraphicsQualityPreference.Current` / `OnSettingsChanged`、`BuiltWorldTerrain.Terrains`、`BlockGameObjectDataStore.BlockGameObjectDictionary`、`TrainCarObjectDatastore.EnumerateTrainCars()`
- Produces:
  - `MapObjectRendererVisibility.SetDistanceVisible(bool)` / `SetQualityVisible(bool)`（旧 `SetVisible` は `SetDistanceVisible` に改名）
  - `MapObjectRendererVisibilityRegistry.Add(MapObjectRendererVisibility)` / `SetQualityVisible(bool)`
  - `MapObjectDistanceVisibilityController.Register(MapObjectRendererVisibility visibility, Vector3 position, bool isLandmark)`
  - `EnvironmentQualityApplier(IReadOnlyList<Terrain> terrains, Transform environmentRoot)` ＋ `Initialize()`
  - `EffectsQualitySwitch : MonoBehaviour` の `Initialize(bool effectsEnabled)` / `SetEffectsEnabled(bool)`
  - `EffectsQualityApplier : IStartable`
  - `TrainCarObjectDatastore.OnTrainCarCreated : IObservable<TrainCarEntityObject>`

- [ ] **Step 1: テストを書く**

`MapObjectRendererVisibilityTest.cs`（`MapObjectRendererVisibility` は internal。`Client.Tests` から internal が見えない場合は既存 `MapObjectDistanceVisibilityControllerTest` と同じ参照方法に合わせる）:
```csharp
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.Visibility
{
    public class MapObjectRendererVisibilityTest
    {
        [Test]
        public void RendererIsShownOnlyWhenBothDistanceAndQualityAllowIt()
        {
            var root = new GameObject("mapObject");
            var mapObject = root.AddComponent<MapObjectGameObject>();
            var renderer = root.AddComponent<MeshRenderer>();
            var visibility = new MapObjectRendererVisibility(mapObject);

            // 距離と画質のどちらかが隠すなら描画しない
            // Hidden whenever either distance or quality hides it
            visibility.SetQualityVisible(false);
            Assert.IsFalse(renderer.enabled);
            visibility.SetDistanceVisible(false);
            visibility.SetQualityVisible(true);
            Assert.IsFalse(renderer.enabled);
            visibility.SetDistanceVisible(true);
            Assert.IsTrue(renderer.enabled);
            Object.DestroyImmediate(root);
        }
    }
}
```

`EffectsQualitySwitchTest.cs`:
```csharp
using Client.Game.InGame.GraphicsQuality.Appliers;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public class EffectsQualitySwitchTest
    {
        [Test]
        public void DisablingSavesAuthoredLightStateAndEnablingRestoresIt()
        {
            var root = new GameObject("block");
            var onLight = new GameObject("on").AddComponent<Light>();
            var offLight = new GameObject("off").AddComponent<Light>();
            onLight.transform.SetParent(root.transform);
            offLight.transform.SetParent(root.transform);
            offLight.enabled = false;
            var effects = root.AddComponent<EffectsQualitySwitch>();
            effects.Initialize(true);

            // 意図的に消してあるライトを復元で点けない
            // Restoring must not turn on a light that was authored off
            effects.SetEffectsEnabled(false);
            Assert.IsFalse(onLight.enabled);
            effects.SetEffectsEnabled(true);
            Assert.IsTrue(onLight.enabled);
            Assert.IsFalse(offLight.enabled);
            Object.DestroyImmediate(root);
        }
    }
}
```

- [ ] **Step 2: mapObjectの表示を2入力にする**

`MapObjectRendererVisibility.cs` の `_isVisible` と `SetVisible` を次へ置き換える:
```csharp
        private bool _isDistanceVisible = true;
        private bool _isQualityVisible = true;

        public void SetDistanceVisible(bool visible)
        {
            _isDistanceVisible = visible;
            ApplyRenderers();
        }

        public void SetQualityVisible(bool visible)
        {
            _isQualityVisible = visible;
            ApplyRenderers();
        }

        private void ApplyRenderers()
        {
            // 破壊済み個体は再表示しない
            // A destroyed instance is never shown again
            var visible = _isDistanceVisible && _isQualityVisible && !_mapObject.IsDestroyed;

            // rootやColliderには触れずRendererだけを揃える
            // Match only Renderers without touching the root or Colliders
            for (var index = 0; index < _renderers.Length; index++)
            {
                _renderers[index].enabled = visible && _authoredEnabledStates[index];
            }
        }
```

`MapObjectRendererVisibilityRegistry.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    /// ランドマークを含む全mapObjectの表示物を持ち、画質設定の表示可否を配る
    /// Holds every map object's visibility including landmarks and distributes the graphics-quality visibility
    /// </summary>
    internal sealed class MapObjectRendererVisibilityRegistry
    {
        private readonly List<MapObjectRendererVisibility> _visibilities = new();
        private bool _isQualityVisible = true;

        public void Add(MapObjectRendererVisibility visibility)
        {
            _visibilities.Add(visibility);
            visibility.SetQualityVisible(_isQualityVisible);
        }

        public void SetQualityVisible(bool visible)
        {
            _isQualityVisible = visible;
            foreach (var visibility in _visibilities) visibility.SetQualityVisible(visible);
        }
    }
}
```

`MapObjectDistanceVisibilityController.Register` を次へ置き換え、内部の `_visibilityByIndex[index].SetVisible(...)` 呼び出しを `SetDistanceVisible(...)` へ改名する:
```csharp
        public void Register(MapObjectRendererVisibility visibility, Vector3 position, bool isLandmark)
        {
            if (isLandmark) return;
            if (_registeredCount == _boundingSpheres.Length)
                throw new InvalidOperationException("MapObject distance visibility capacity exceeded.");

            var index = _registeredCount;
            _registeredCount++;
            _visibilityByIndex[index] = visibility;
            _boundingSpheres[index] = new BoundingSphere(position, 0f);
            _cullingGroup.SetBoundingSphereCount(_registeredCount);

            if (_camera != null) ApplyDistanceBand(index, GetDistanceBand(position));
        }
```

`MapObjectLayoutInstantiator`: ctor に `MapObjectRendererVisibilityRegistry visibilityRegistry` を足してフィールドに持ち、`_distanceVisibilityController.Register(mapObject, isLandmark);` を次へ置き換える:
```csharp
            // 表示物は距離表示と画質表示の両方へ渡す（ランドマークも画質では隠れる）
            // Hand the visibility to both distance and quality control (landmarks are also hidden by quality)
            var visibility = new MapObjectRendererVisibility(mapObject);
            _visibilityRegistry.Add(visibility);
            _distanceVisibilityController.Register(visibility, mapObject.transform.position, isLandmark);
```

`MapObjectGameObjectDatastore.Construct`: `var visibilityRegistry = new MapObjectRendererVisibilityRegistry();` を作り instantiator へ渡し、直後に:
```csharp
            // 画質設定の木や岩を今すぐ当て、以後の変更も即時反映する
            // Apply the trees-and-rocks quality item now and follow later changes immediately
            visibilityRegistry.SetQualityVisible(GraphicsQualityPreference.Current.MapObjectsVisible);
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(settings => visibilityRegistry.SetQualityVisible(settings.MapObjectsVisible)).AddTo(this);
```
`MapObjectDistanceVisibilityControllerTest` の `Register(mapObject, isLandmark)` 呼び出しは `Register(new MapObjectRendererVisibility(mapObject), mapObject.transform.position, isLandmark)` へ直す。

- [ ] **Step 3: 地形と海**

`EnvironmentQualityApplier.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// 地形の細かさと海の表示を画質設定に合わせる
    /// Matches terrain detail and ocean visibility to graphics quality
    /// </summary>
    public sealed class EnvironmentQualityApplier
    {
        // 精細はTerrain既定の5、粗いはv7と同じ10
        // Fine is the Terrain default 5; coarse matches v7's 10
        private const float FinePixelError = 5f;
        private const float CoarsePixelError = 10f;

        private readonly IReadOnlyList<UnityEngine.Terrain> _terrains;
        private readonly Renderer[] _oceanRenderers;

        public EnvironmentQualityApplier(IReadOnlyList<UnityEngine.Terrain> terrains, Transform environmentRoot)
        {
            _terrains = terrains;
            var oceanRenderers = new List<Renderer>();
            foreach (var ocean in environmentRoot.GetComponentsInChildren<GeneratedOceanSurface>(true))
                oceanRenderers.AddRange(ocean.GetComponentsInChildren<Renderer>(true));
            _oceanRenderers = oceanRenderers.ToArray();
        }

        public void Initialize()
        {
            Apply(GraphicsQualityPreference.Current);
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(Apply);
        }

        private void Apply(GraphicsQualitySettings settings)
        {
            var isFine = settings.TerrainDetail == GraphicsTerrainDetail.Fine;
            foreach (var terrain in _terrains)
            {
                terrain.heightmapPixelError = isFine ? FinePixelError : CoarsePixelError;
                terrain.drawTreesAndFoliage = isFine;
            }
            foreach (var oceanRenderer in _oceanRenderers) oceanRenderer.enabled = settings.OceanVisible;
        }
    }
}
```
`MainGameInitializationFinalizer`（`var builtTerrain = await TerrainRuntimeBuilder.BuildAsync(...)` の直後）:
```csharp
            // 地形と海が揃ったので画質設定を当て始める
            // Terrain and ocean exist now, so start applying graphics quality
            new EnvironmentQualityApplier(builtTerrain.Terrains, starter.EnvironmentRoot.transform).Initialize();
```
（`FinePixelError` は revert 後の `TerrainObjectFactory` が設定しない既定値。既定が5でなければ、revert後のTerrainの値を PlayMode で読みその値に合わせる）

- [ ] **Step 4: エフェクト**

`EffectsQualitySwitch.cs`:
```csharp
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// ブロック/列車1個分のVFX・パーティクル・ライトを画質設定で止め、止める直前の有効状態へ戻す
    /// Stops one block's or train's VFX, particles and lights by graphics quality and restores the state saved just before stopping
    /// </summary>
    public sealed class EffectsQualitySwitch : MonoBehaviour
    {
        private VisualEffect[] _visualEffects;
        private ParticleSystem[] _particleSystems;
        private ParticleSystemRenderer[] _particleRenderers;
        private Light[] _lights;
        private bool[] _savedVisualEffects;
        private bool[] _savedParticlePlaying;
        private bool[] _savedParticleRenderers;
        private bool[] _savedLights;
        private bool _effectsEnabled = true;

        public void Initialize(bool effectsEnabled)
        {
            _visualEffects = GetComponentsInChildren<VisualEffect>(true);
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            _lights = GetComponentsInChildren<Light>(true);
            _savedVisualEffects = new bool[_visualEffects.Length];
            _savedParticlePlaying = new bool[_particleSystems.Length];
            _savedParticleRenderers = new bool[_particleRenderers.Length];
            _savedLights = new bool[_lights.Length];
            SetEffectsEnabled(effectsEnabled);
        }

        public void SetEffectsEnabled(bool effectsEnabled)
        {
            if (_effectsEnabled == effectsEnabled) return;
            _effectsEnabled = effectsEnabled;

            // 状態駆動の演出が消したものを点けないよう、止める瞬間の状態を保存して戻す
            // Save the state at the moment of stopping and restore it, so effects turned off by state logic stay off
            for (var i = 0; i < _visualEffects.Length; i++) _visualEffects[i].enabled = Toggle(_visualEffects[i].enabled, ref _savedVisualEffects[i]);
            for (var i = 0; i < _particleRenderers.Length; i++) _particleRenderers[i].enabled = Toggle(_particleRenderers[i].enabled, ref _savedParticleRenderers[i]);
            for (var i = 0; i < _lights.Length; i++) _lights[i].enabled = Toggle(_lights[i].enabled, ref _savedLights[i]);

            // パーティクルは計算も止め、止める直前に再生中だったものだけ再生し直す（裁定 2026-10-20）
            // Particles stop simulating too; only those playing just before stopping are replayed (ruling 2026-10-20)
            for (var i = 0; i < _particleSystems.Length; i++)
            {
                if (effectsEnabled)
                {
                    if (_savedParticlePlaying[i]) _particleSystems[i].Play(false);
                    continue;
                }
                _savedParticlePlaying[i] = _particleSystems[i].isPlaying;
                _particleSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            #region Internal

            bool Toggle(bool current, ref bool saved)
            {
                if (effectsEnabled) return saved;
                saved = current;
                return false;
            }

            #endregion
        }
    }
}
```

`TrainCarObjectDatastore.cs` に列車生成の通知を足す（`CreateTrainEntityIfMissing` の `_entities[trainCarInstanceId] = entityObject;` の直後で発火。using `UniRx;` `System;`）:
```csharp
        public IObservable<TrainCarEntityObject> OnTrainCarCreated => _onTrainCarCreated;
        private readonly Subject<TrainCarEntityObject> _onTrainCarCreated = new();
```
```csharp
            _entities[trainCarInstanceId] = entityObject;
            _onTrainCarCreated.OnNext(entityObject);
```

`EffectsQualityApplier.cs`（Switchの付与と配布をここ1か所に集める。ブロックは既存分の走査＋`OnBlockPlaced`、列車は既存分の走査＋`OnTrainCarCreated`。前例 `Client.Game/InGame/Block/DistanceCulling/BlockDistanceCullingRegistrar.cs`）:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.View.Object.Core;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// 全ブロック・列車へエフェクト切替を付け、画質設定のエフェクト項目の変化を配る
    /// Attaches the effects switch to every block and train and distributes changes of the effects quality item
    /// </summary>
    public sealed class EffectsQualityApplier : IStartable
    {
        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly List<EffectsQualitySwitch> _switches = new();

        public EffectsQualityApplier(BlockGameObjectDataStore blockDataStore, TrainCarObjectDatastore trainCarDatastore)
        {
            _blockDataStore = blockDataStore;
            _trainCarDatastore = trainCarDatastore;
        }

        public void Start()
        {
            // 既存分へ付けてから生成通知を購読する
            // Attach to existing objects first, then subscribe to creation
            foreach (var block in _blockDataStore.BlockGameObjectDictionary.Values) Attach(block.gameObject);
            foreach (var trainCar in _trainCarDatastore.EnumerateTrainCars()) Attach(trainCar.gameObject);
            _blockDataStore.OnBlockPlaced.Subscribe(block => Attach(block.gameObject));
            _trainCarDatastore.OnTrainCarCreated.Subscribe(trainCar => Attach(trainCar.gameObject));
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(settings => Apply(settings.EffectsEnabled));
        }

        private void Attach(GameObject root)
        {
            var effects = root.AddComponent<EffectsQualitySwitch>();
            effects.Initialize(GraphicsQualityPreference.Current.EffectsEnabled);
            _switches.Add(effects);
        }

        private void Apply(bool effectsEnabled)
        {
            // 撤去済み（破棄済み）の切替は配布前に外す
            // Drop switches whose objects were removed before distributing
            _switches.RemoveAll(effects => effects == null);
            foreach (var effects in _switches) effects.SetEffectsEnabled(effectsEnabled);
        }
    }
}
```
（ブロック・列車の生成ファクトリは変更しない。`BlockGameObject` がキャッシュするのは VFX の `gameObject.SetActive` 用で、Switch が触る `enabled` とは別なので、付与が `Initialize` の後でも衝突しない）
`MainGameModelRegistration`: `builder.RegisterEntryPoint<EffectsQualityApplier>();`（`TrainCarObjectDatastore` が DI に登録済みか確認し、無ければ `RegisterComponentInHierarchy` 等の既存登録元に合わせる）

- [ ] **Step 5: コンパイルしテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectRendererVisibilityTest|MapObjectDistanceVisibilityControllerTest|EffectsQualitySwitchTest"` → PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/GraphicsQuality/Appliers moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarObjectDatastore.cs moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility moorestech_client/Assets/Scripts/Client.Tests/GraphicsQuality
git commit -m "feat: 地形・海・木や岩・エフェクトを画質設定から即時反映する"
```

### Task 6: Web UIとの接続（Topic・Action・設定画面）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Settings/GraphicsQualityTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Settings/GraphicsQualityActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Settings/GraphicsQualityWire.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Settings/SettingsActions.cs`（`Register` に2行）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:95` の直後
- Modify: `Localization/localization.csv`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`、`moorestech_web/webui/src/bridge/contract/payloadTypes.ts`、`moorestech_web/webui/src/bridge/contract/validators.ts`、`moorestech_web/webui/src/bridge/transport/protocol.ts`、`moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/settings/GraphicsQualitySettings.tsx`
- Modify: `moorestech_web/webui/src/features/settings/index.ts`、`moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx:30`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Settings/GraphicsQualityContractTest.cs`
- Test: `moorestech_web/webui/src/features/settings/GraphicsQualitySettings.test.ts`

**Interfaces:**
- Consumes: Task 3 の型
- Produces（wire）:
  - topic `settings.graphicsQuality`: `{ preset: "high"|"medium"|"low"|"custom", settings: { drawDistanceMeters: 300|1000|5000, shadows: boolean, renderScalePercent: 100|75|50, ssao: boolean, postProcessing: boolean, effects: boolean, mapObjects: boolean, terrainDetail: "fine"|"coarse", ocean: boolean } }`
  - action `settings.applyGraphicsQualityPreset` `{ preset: "high"|"medium"|"low" }`、失敗 `unknown_graphics_quality_preset`
  - action `settings.setGraphicsQuality` `{ settings: <上の settings と同形> }`、失敗 `invalid_graphics_quality`
  - C#: `GraphicsQualityWire.ToData(GraphicsQualitySettings) : GraphicsQualitySettingsData`、`TryFromJObject(JObject, out GraphicsQualitySettings) : bool`、`PresetToWire(GraphicsQualitySettings) : string`、`TryPresetFromWire(string, out GraphicsQualityPreset) : bool`

- [ ] **Step 1: C#契約テストを書く** — `GraphicsQualityContractTest.cs`（SetUp/TearDown は `GraphicsQualityPreferenceTest` と同じ退避・復元）

```csharp
        [Test]
        public async Task PresetActionPersistsAndTopicReportsPresetName()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            var result = await hub.ResolveAction("settings.applyGraphicsQualityPreset").ExecuteAsync(new JObject { ["preset"] = "low" });
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("low", snapshot["preset"]?.Value<string>());
            Assert.AreEqual(300, snapshot["settings"]?["drawDistanceMeters"]?.Value<int>());
            Assert.IsFalse(snapshot["settings"]?["shadows"]?.Value<bool>());
        }

        [Test]
        public async Task ChangingOneItemAfterPresetReportsCustom()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            var settings = JObject.Parse(WebUiJson.Serialize(GraphicsQualityWire.ToData(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium))));
            settings["shadows"] = false;
            var result = await hub.ResolveAction("settings.setGraphicsQuality").ExecuteAsync(new JObject { ["settings"] = settings });
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("custom", snapshot["preset"]?.Value<string>());
        }

        [TestCase("ultra")]
        [TestCase(null)]
        public async Task UnknownPresetIsRejectedWithoutSaving(string wire)
        {
            var result = await new ApplyGraphicsQualityPresetActionHandler().ExecuteAsync(new JObject { ["preset"] = wire });
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_graphics_quality_preset", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        [Test]
        public async Task InvalidSettingsAreRejectedWithoutSaving()
        {
            var settings = JObject.Parse(WebUiJson.Serialize(GraphicsQualityWire.ToData(GraphicsQualityPresets.Get(GraphicsQualityPreset.High))));
            settings["drawDistanceMeters"] = 777;
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(new JObject { ["settings"] = settings });
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_graphics_quality", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }
```
（クラス冒頭の using は `SprintInputModeContractTest` と同じ＋`Client.Game.InGame.GraphicsQuality`・`Client.WebUiHost.Common`。`GraphicsQualityWire` は internal なので、`Client.Tests` から見えなければ WebUiHost の既存 `InternalsVisibleTo` の有無を確認し、無ければ ToData を使わず JObject を手で組む）

- [ ] **Step 2: C#実装**

`GraphicsQualityWire.cs`:
```csharp
using Client.Game.InGame.GraphicsQuality;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 画質設定のC#値とWeb契約の相互変換。未知の値は変換失敗にする
    /// Converts graphics quality between C# values and the web contract; unknown values fail conversion
    /// </summary>
    // topicのsettings節。WebUiJsonがcamelCaseで書き出す
    // The settings section of the topic; WebUiJson writes it in camelCase
    internal class GraphicsQualitySettingsData
    {
        public int DrawDistanceMeters;
        public bool Shadows;
        public int RenderScalePercent;
        public bool Ssao;
        public bool PostProcessing;
        public bool Effects;
        public bool MapObjects;
        public string TerrainDetail;
        public bool Ocean;
    }

    internal static class GraphicsQualityWire
    {
        public static string PresetToWire(GraphicsQualitySettings settings)
        {
            if (!GraphicsQualityPresets.TryMatch(settings, out var preset)) return "custom";
            return preset switch { GraphicsQualityPreset.High => "high", GraphicsQualityPreset.Medium => "medium", _ => "low" };
        }

        public static bool TryPresetFromWire(string wire, out GraphicsQualityPreset preset)
        {
            preset = GraphicsQualityPreset.Medium;
            switch (wire)
            {
                case "high": preset = GraphicsQualityPreset.High; return true;
                case "medium": preset = GraphicsQualityPreset.Medium; return true;
                case "low": preset = GraphicsQualityPreset.Low; return true;
                default: return false;
            }
        }

        public static GraphicsQualitySettingsData ToData(GraphicsQualitySettings s)
        {
            return new GraphicsQualitySettingsData
            {
                DrawDistanceMeters = (int)s.DrawDistance.ToMeters(),
                Shadows = s.ShadowsEnabled,
                RenderScalePercent = ToPercent(s.RenderScale),
                Ssao = s.SsaoEnabled,
                PostProcessing = s.PostProcessingEnabled,
                Effects = s.EffectsEnabled,
                MapObjects = s.MapObjectsVisible,
                TerrainDetail = s.TerrainDetail == GraphicsTerrainDetail.Fine ? "fine" : "coarse",
                Ocean = s.OceanVisible,
            };
        }

        private static int ToPercent(GraphicsRenderScale renderScale)
        {
            return UnityEngine.Mathf.RoundToInt(renderScale.ToScale() * 100f);
        }

        public static bool TryFromJObject(JObject json, out GraphicsQualitySettings settings)
        {
            settings = default;
            if (json == null) return false;
            if (!TryDrawDistance(json["drawDistanceMeters"], out var drawDistance) || !TryRenderScale(json["renderScalePercent"], out var renderScale) ||
                !TryTerrain(json["terrainDetail"], out var terrain) || !TryBool(json["shadows"], out var shadows) || !TryBool(json["ssao"], out var ssao) ||
                !TryBool(json["postProcessing"], out var post) || !TryBool(json["effects"], out var effects) ||
                !TryBool(json["mapObjects"], out var mapObjects) || !TryBool(json["ocean"], out var ocean)) return false;
            settings = new GraphicsQualitySettings(drawDistance, shadows, renderScale, ssao, post, effects, mapObjects, terrain, ocean);
            return true;

            #region Internal

            bool TryBool(JToken token, out bool value)
            {
                value = false;
                if (token == null || token.Type != JTokenType.Boolean) return false;
                value = token.Value<bool>();
                return true;
            }

            // 数値との対応はToMeters/ToScaleを正とし、ここでは逆引きだけする
            // ToMeters/ToScale own the numeric mapping; this only looks it up in reverse
            bool TryDrawDistance(JToken token, out GraphicsDrawDistance value)
            {
                value = GraphicsDrawDistance.Meters300;
                if (token == null || token.Type != JTokenType.Integer) return false;
                foreach (GraphicsDrawDistance candidate in System.Enum.GetValues(typeof(GraphicsDrawDistance)))
                {
                    if ((int)candidate.ToMeters() != token.Value<int>()) continue;
                    value = candidate;
                    return true;
                }
                return false;
            }

            bool TryRenderScale(JToken token, out GraphicsRenderScale value)
            {
                value = GraphicsRenderScale.Percent100;
                if (token == null || token.Type != JTokenType.Integer) return false;
                foreach (GraphicsRenderScale candidate in System.Enum.GetValues(typeof(GraphicsRenderScale)))
                {
                    if (ToPercent(candidate) != token.Value<int>()) continue;
                    value = candidate;
                    return true;
                }
                return false;
            }

            bool TryTerrain(JToken token, out GraphicsTerrainDetail value)
            {
                value = GraphicsTerrainDetail.Fine;
                var text = token?.Type == JTokenType.String ? token.Value<string>() : null;
                if (text == "fine") return true;
                value = GraphicsTerrainDetail.Coarse;
                return text == "coarse";
            }

            #endregion
        }
    }
}
```

`GraphicsQualityActions.cs`:
```csharp
using Client.Game.InGame.GraphicsQuality;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Webからのプリセット選択を保存窓口へ渡す
    /// Passes a preset selection from the web to the preference store
    /// </summary>
    public class ApplyGraphicsQualityPresetActionHandler : IActionHandler
    {
        public string ActionType => "settings.applyGraphicsQualityPreset";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var wire = payload?["preset"]?.ToString();

            // 不明値は失敗契約で返し、保存値は変えない
            // An unknown value returns the failure contract and leaves the saved value as is
            if (!GraphicsQualityWire.TryPresetFromWire(wire, out var preset))
            {
                Debug.LogWarning($"[GraphicsQualityActions] Rejected unknown graphics quality preset: {wire}");
                return UniTask.FromResult(ActionResult.Fail("unknown_graphics_quality_preset"));
            }
            GraphicsQualityPreference.ApplyPreset(preset);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// Webからの項目変更（全項目の値）を保存窓口へ渡す
    /// Passes an item change (all item values) from the web to the preference store
    /// </summary>
    public class SetGraphicsQualityActionHandler : IActionHandler
    {
        public string ActionType => "settings.setGraphicsQuality";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // 定義外の値を含む組は失敗契約で返し、保存値は変えない
            // A set containing an undefined value returns the failure contract and leaves the saved value as is
            if (!GraphicsQualityWire.TryFromJObject(payload?["settings"] as JObject, out var settings))
            {
                Debug.LogWarning($"[GraphicsQualityActions] Rejected invalid graphics quality: {payload}");
                return UniTask.FromResult(ActionResult.Fail("invalid_graphics_quality"));
            }
            GraphicsQualityPreference.SetSettings(settings);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

`SettingsActions.Register` に追加:
```csharp
            hub.RegisterAction(new ApplyGraphicsQualityPresetActionHandler());
            hub.RegisterAction(new SetGraphicsQualityActionHandler());
```

`GraphicsQualityTopic.cs`:
```csharp
using System;
using Client.Game.InGame.GraphicsQuality;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// - 画質設定の初期値配信
    /// - 画質設定の変化通知
    /// - Publishes the initial graphics quality
    /// - Publishes graphics quality changes
    /// </summary>
    public class GraphicsQualityTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "settings.graphicsQuality";

        private readonly IDisposable _settingsSubscription;

        public GraphicsQualityTopic(WebSocketHub hub)
        {
            _settingsSubscription = GraphicsQualityPreference.OnSettingsChanged.Subscribe(_ => hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _settingsSubscription.Dispose();
        }

        private static string BuildJson()
        {
            var settings = GraphicsQualityPreference.Current;
            return WebUiJson.Serialize(new GraphicsQualityData
            {
                Preset = GraphicsQualityWire.PresetToWire(settings),
                Settings = GraphicsQualityWire.ToData(settings),
            });
        }

        private class GraphicsQualityData
        {
            public string Preset;
            public GraphicsQualitySettingsData Settings;
        }
    }
}
```
（`GraphicsQualityWire` は internal なので Topic と同じアセンブリ内で参照できる）

`WebUiGameBinder.cs:95` の直後:
```csharp
            hub.RegisterTopic(GraphicsQualityTopic.TopicName, new GraphicsQualityTopic(hub));
```

- [ ] **Step 3: ローカライズ** — `Localization/localization.csv` の `ui.settings.sprintModeHold` 行の直後に追加（列: key,Source,english,japanese,german,korean）:

```csv
ui.settings.graphics,Graphics,Graphics,画質,Grafik,그래픽
ui.settings.graphicsPreset,Preset,Preset,プリセット,Voreinstellung,프리셋
ui.settings.graphicsPresetHigh,High,High,高,Hoch,높음
ui.settings.graphicsPresetMedium,Medium,Medium,中,Mittel,중간
ui.settings.graphicsPresetLow,Low,Low,低,Niedrig,낮음
ui.settings.graphicsPresetCustom,Custom,Custom,カスタム,Benutzerdefiniert,사용자 지정
ui.settings.graphicsDrawDistance,Draw distance,Draw distance,描画距離,Sichtweite,시야 거리
ui.settings.graphicsShadows,Shadows,Shadows,影,Schatten,그림자
ui.settings.graphicsRenderScale,Render resolution,Render resolution,描画解像度,Renderauflösung,렌더링 해상도
ui.settings.graphicsSsao,Ambient occlusion (SSAO),Ambient occlusion (SSAO),アンビエントオクルージョン（SSAO）,Umgebungsverdeckung (SSAO),앰비언트 오클루전(SSAO)
ui.settings.graphicsPostProcessing,Post-processing,Post-processing,ポストエフェクト,Nachbearbeitung,후처리
ui.settings.graphicsEffects,Visual effects,Visual effects,エフェクト,Effekte,이펙트
ui.settings.graphicsMapObjects,Trees and rocks,Trees and rocks,木や岩,Bäume und Felsen,나무와 바위
ui.settings.graphicsTerrainDetail,Terrain detail,Terrain detail,地形の細かさ,Geländedetails,지형 디테일
ui.settings.graphicsTerrainFine,Fine,Fine,精細,Fein,세밀
ui.settings.graphicsTerrainCoarse,Coarse,Coarse,粗い,Grob,거침
ui.settings.graphicsOcean,Ocean,Ocean,海,Ozean,바다
ui.settings.on,On,On,オン,An,켜기
ui.settings.off,Off,Off,オフ,Aus,끄기
```
Run: `cd moorestech_web/webui && npm run gen:i18n` → `src/shared/i18n/generated/localizationKeys.ts` に `graphicsPreset` 等が入る

- [ ] **Step 4: Web契約** —

`schemas/ui.ts`（`SprintInputModeDataSchema` の直後）:
```ts
// Graphics quality; Unity owns the default and the preset match
export const GraphicsQualitySettingsSchema = z.object({
  drawDistanceMeters: z.union([z.literal(300), z.literal(1000), z.literal(5000)]),
  shadows: z.boolean(),
  renderScalePercent: z.union([z.literal(100), z.literal(75), z.literal(50)]),
  ssao: z.boolean(),
  postProcessing: z.boolean(),
  effects: z.boolean(),
  mapObjects: z.boolean(),
  terrainDetail: z.enum(["fine", "coarse"]),
  ocean: z.boolean(),
});
export const GraphicsQualityDataSchema = z.object({
  preset: z.enum(["high", "medium", "low", "custom"]),
  settings: GraphicsQualitySettingsSchema,
});
```
`payloadTypes.ts`: import に2スキーマを足し
```ts
export type GraphicsQualityData = z.infer<typeof GraphicsQualityDataSchema>;
export type GraphicsQualitySettings = z.infer<typeof GraphicsQualitySettingsSchema>;
export type GraphicsQualityPresetChoice = Exclude<GraphicsQualityData["preset"], "custom">;
```
`protocol.ts`: Topics に `graphicsQuality: "settings.graphicsQuality",`、payload表に `[Topics.graphicsQuality]: GraphicsQualityData;`（import追加）
`validators.ts`: `[Topics.graphicsQuality]: GraphicsQualityDataSchema,`（import追加）
`actionContract.ts`: 型表に
```ts
  "settings.applyGraphicsQualityPreset": { preset: GraphicsQualityPresetChoice };
  "settings.setGraphicsQuality": { settings: GraphicsQualitySettings };
```
と、134行付近のaction名配列に `"settings.applyGraphicsQualityPreset", "settings.setGraphicsQuality",`（import追加）

- [ ] **Step 5: 画面** — `GraphicsQualitySettings.tsx`:

```tsx
import { Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic, type GraphicsQualityPresetChoice, type GraphicsQualitySettings as Settings } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

type BoolKey = "shadows" | "ssao" | "postProcessing" | "effects" | "mapObjects" | "ocean";

// 画質のプリセットと各項目。現在値はsettings.graphicsQualityトピックを正とする
// Graphics preset and items; the settings.graphicsQuality topic is authoritative
export function GraphicsQualitySettings() {
  const { t } = useI18n();
  const data = useTopic(Topics.graphicsQuality);
  const label = t(L.ui.settings.graphics);
  if (data === undefined) return <section aria-label={label}><Title order={2}>{label}</Title></section>;
  const settings = data.settings;
  const setItem = (next: Settings) => void dispatchAction("settings.setGraphicsQuality", { settings: next });
  const onOff = [
    { value: "on", label: t(L.ui.settings.on) },
    { value: "off", label: t(L.ui.settings.off) },
  ];
  const boolSwitch = (key: BoolKey, title: string) => (
    <div key={key}>
      <Text>{title}</Text>
      <ModeSwitch<string>
        value={settings[key] ? "on" : "off"}
        options={onOff.map((o) => ({ ...o, testId: `graphics-${key}-${o.value}` }))}
        onChange={(next) => setItem({ ...settings, [key]: next === "on" })}
        orientation="horizontal"
        testId={`graphics-${key}`}
      />
    </div>
  );

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      <Text>{t(L.ui.settings.graphicsPreset)}</Text>
      <ModeSwitch<string>
        value={data.preset}
        options={[
          { value: "high", label: t(L.ui.settings.graphicsPresetHigh), testId: "graphics-preset-high" },
          { value: "medium", label: t(L.ui.settings.graphicsPresetMedium), testId: "graphics-preset-medium" },
          { value: "low", label: t(L.ui.settings.graphicsPresetLow), testId: "graphics-preset-low" },
        ]}
        onChange={(next) => void dispatchAction("settings.applyGraphicsQualityPreset", { preset: next as GraphicsQualityPresetChoice })}
        orientation="horizontal"
        testId="graphics-preset"
      />
      {data.preset === "custom" && <Text role="status" data-testid="graphics-preset-custom">{t(L.ui.settings.graphicsPresetCustom)}</Text>}
      <Text>{t(L.ui.settings.graphicsDrawDistance)}</Text>
      <ModeSwitch<string>
        value={String(settings.drawDistanceMeters)}
        options={[300, 1000, 5000].map((m) => ({ value: String(m), label: `${m}m`, testId: `graphics-draw-distance-${m}` }))}
        onChange={(next) => setItem({ ...settings, drawDistanceMeters: Number(next) as Settings["drawDistanceMeters"] })}
        orientation="horizontal"
        testId="graphics-draw-distance"
      />
      {boolSwitch("shadows", t(L.ui.settings.graphicsShadows))}
      <Text>{t(L.ui.settings.graphicsRenderScale)}</Text>
      <ModeSwitch<string>
        value={String(settings.renderScalePercent)}
        options={[100, 75, 50].map((p) => ({ value: String(p), label: `${p}%`, testId: `graphics-render-scale-${p}` }))}
        onChange={(next) => setItem({ ...settings, renderScalePercent: Number(next) as Settings["renderScalePercent"] })}
        orientation="horizontal"
        testId="graphics-render-scale"
      />
      {boolSwitch("ssao", t(L.ui.settings.graphicsSsao))}
      {boolSwitch("postProcessing", t(L.ui.settings.graphicsPostProcessing))}
      {boolSwitch("effects", t(L.ui.settings.graphicsEffects))}
      {boolSwitch("mapObjects", t(L.ui.settings.graphicsMapObjects))}
      <Text>{t(L.ui.settings.graphicsTerrainDetail)}</Text>
      <ModeSwitch<string>
        value={settings.terrainDetail}
        options={[
          { value: "fine", label: t(L.ui.settings.graphicsTerrainFine), testId: "graphics-terrain-fine" },
          { value: "coarse", label: t(L.ui.settings.graphicsTerrainCoarse), testId: "graphics-terrain-coarse" },
        ]}
        onChange={(next) => setItem({ ...settings, terrainDetail: next as Settings["terrainDetail"] })}
        orientation="horizontal"
        testId="graphics-terrain"
      />
      {boolSwitch("ocean", t(L.ui.settings.graphicsOcean))}
    </section>
  );
}
```
（`ModeSwitch` が options に無い value（`"custom"`）を受けたとき選択なしで描けるか `src/shared/ui` の実装を確認する。描けない場合は custom の間だけ value に `undefined` 相当を渡せる既存の形に合わせる。`@/bridge` が型を再exportしていなければ `payloadTypes` の再export箇所に足す）

`features/settings/index.ts`: `export { GraphicsQualitySettings } from "./GraphicsQualitySettings";`
`PauseMenuPanel.tsx:30`: `<LanguageSelect /><SprintModeSelect /><GraphicsQualitySettings />`（import追加）

- [ ] **Step 6: Webテスト** — `GraphicsQualitySettings.test.ts`（`SprintModeSelect.test.ts` と同じモック構成。`useTopic` が `settings.graphicsQuality` で `{ preset, settings }` を返す）で次の3件:
  1. `preset: "medium"` のとき `graphics-preset` の value が `"medium"` で、`graphics-preset-custom` の Text が無い
  2. `preset: "custom"` のとき `graphics-preset-custom` の Text が1件出る
  3. `graphics-shadows` の onChange("off") で `dispatchAction("settings.setGraphicsQuality", { settings: { ...medium, shadows: false } })` が呼ばれ、`graphics-preset` の onChange("low") で `dispatchAction("settings.applyGraphicsQualityPreset", { preset: "low" })` が呼ばれる

- [ ] **Step 7: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.WebUi.Settings.GraphicsQualityContractTest"` → PASS
Run: `cd moorestech_web/webui && npx tsc --noEmit && npm run lint && npx vitest run src/features/settings src/bridge` → PASS

- [ ] **Step 8: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi/Settings Localization/localization.csv moorestech_web/webui/src
git commit -m "feat: 設定画面に画質設定（プリセットと項目）を追加する"
```

### Task 7: unityプレイ録画テストと完了基準の計測

**Files:**
- Create: 録画シナリオ（`unity-playmode-recorded-playtest` スキルの置き場所規約に従う）
- Create: `docs/evidence/2026-10-20-graphics-quality-fps.md`（計測結果）

- [ ] **Step 1: 録画シナリオ** — `unity-playmode-recorded-playtest` スキル（プレイテストDSL）で次を通しで録画する: ワールドに入る → Escでポーズメニュー → 設定画面 → 画質プリセット「低」→ 画面が軽量表示（遠景が消え、影が無い）になる → 「高」→ 戻る → 影だけオフにしてプリセット欄が「カスタム」になる → 閉じて流体パイプの水位が見える場所で水位が変わることを確認。Web UIの自動操作が難しければ、設定変更は `settings.applyGraphicsQualityPreset` / `settings.setGraphicsQuality` actionを直接送る形でよい（判断記録へ1行残す）

- [ ] **Step 2: 完了基準の計測** — メインworktree（`/Users/katsumi/moorestech`）でPlayを押して入るワールドと同じセーブを使い、「中」で、歩く・ブロックを1つ設置する操作を含む60秒の平均fpsを計る。計り方: `Observable.EveryEndOfFrame` で `Time.unscaledDeltaTime` を集計して `[PERF]` タグで `Debug.Log` し `uloop get-logs --search-text "[PERF]"` で回収する。Editorは前面（`uloop focus-window`）。平均20fps以上なら合格。結果（平均・p50・p95）を `docs/evidence/2026-10-20-graphics-quality-fps.md` に書く。20fps未満ならそこで止め、計測値を添えてユーザーへ報告する

- [ ] **Step 3: コミット**

```bash
git add docs/evidence/2026-10-20-graphics-quality-fps.md <録画シナリオのパス>
git commit -m "test: 画質設定の録画テストとfps計測を追加する"
```

### Task 8: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定経路・条件式・評価時点に触れたら、Task 7 の録画テストを反映後に再実施する

### Task 9: PR作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える

---

## 判断記録（ADR）

設計裁定（ユーザー裁定。本文は各ファイル）:
- `.decisions/2026-10-20-歯車回転と流体の描画更新はカメラ周囲50mに限る.md`
- `.decisions/2026-10-20-遠方の歯車回転停止とパイプ水位の状態駆動とベルトアイテム非表示は常時入れる.md`
- `.decisions/2026-10-20-ユーザー向け軽量化は項目ごとの設定と三段階プリセットにする.md`（`…軽量モードON-OFFひとつにする.md` を覆す）
- `.decisions/2026-10-20-軽量化の設定項目はv7相当を全部持ち最軽量プリセットで全部軽くする.md`
- `.decisions/2026-10-20-画質プリセットは高が現状で中は描画距離300mで低はv7相当.md`
- `.decisions/2026-10-20-画質プリセットの既定は中.md`
- `.decisions/2026-10-20-画質の項目を個別に変えたらプリセット欄はカスタムになる.md`
- `.decisions/2026-10-20-画質設定は全項目をその場で反映する.md`
- `.decisions/2026-10-20-v7超軽量設定は廃止し画質プリセット低で代用する.md`
- `.decisions/2026-10-20-画質の各項目はプリセットの値に中間値を足した選択式にする.md`
- `.decisions/2026-10-20-画質設定の完了基準は中で操作込み平均20fps以上.md`

planning中の判断:
- D1 保存はPCごと・PlayerPrefs・走り方式設定と同じ静的Preference＋Subjectの形。出所: agent前提（前例 `Client.Input/Sprint/SprintInputModePreference.cs`。設計対話の最後にユーザーへ提示し「ok」）
- D2 描画距離はカメラの farClipPlane（Cinemachine lens 併記）で切る。100m超Rendererの個別非表示は採らない。出所: agent前提（実測: v7なし・カメラ固定で farClip 300m 35.5fps、Renderer個別非表示より効く。ユーザーへ提示し「ok」）
- D3 プリセットは保存せず値から導出する（どれとも一致しない組＝カスタム）。中と同じ値へ手で戻すと表示も「中」に戻る。出所: agent前提（保存値と表示の食い違いを型で無くすため）
- D4 歯車の距離ゲートはカメラでなくプレイヤー中心の既存 `PlayerCenteredDistanceCulling` を使う（半径50m・チャンク8m・再評価2m）。出所: シミュレーター予測→ユーザー承認 2026-10-20 質問「歯車の回転を止める50mは、どこから測りますか？」→「プレイヤー中心でよい」（`.decisions/2026-10-20-歯車回転停止の50mはプレイヤー中心で測る.md`）
- D5 ベルト上アイテムの範囲は既存 `BeltSegmentVisibleRange` の定数を 45/50m に変える（ADR 0085 の 340/350m を改訂）。出所: ユーザー裁定の距離＋agent前提（ヒステリシス5m）
- D6 描画解像度とSSAOは URP アセット・renderer data・feature を実行時に複製して当てる（リフレクションで `m_RendererDataList` を差し替え）。Editor では PlayMode 終了で元アセットへ戻す。出所: agent前提（アセットを実行時に書き換えるとEditorで保存され汚れるため。代替の「SSAO無しrenderer資産を別に作り SetRenderer で切替」は資産の二重管理になるため採らない）
- D7 エフェクト項目は VFX・パーティクル（描画と計算）・ライトを止め、止めた瞬間の状態を保存して戻す（状態駆動で消えている演出を点けない）。Switchの付与と配布は `EffectsQualityApplier` 1か所（既存分の走査＋`OnBlockPlaced`／`OnTrainCarCreated`）。出所: agent前提（前例 `BlockColliderCullingTarget` の保存・復元、`BlockDistanceCullingRegistrar` の付与経路。配置検査の違反指摘で生成ファクトリへの直書きから移した）＋パーティクル計算停止は `.decisions/2026-10-20-v7の残り項目は画質設定の既存項目の最低値に含める.md`
- D8 木や岩はランドマークも含めて隠す（v7と同じ）。mapObjectの表示は距離と画質のAND。出所: ユーザー裁定「低=v7相当」＋agent前提（AND合成）
- D9 地形「粗い」は heightmapPixelError 10 と木・草（drawTreesAndFoliage）非表示。detailデータ自体は常に載せる（即時反映のため）。出所: ユーザー裁定「即時反映」＋agent前提
- D10 不正な保存値は中へ修復し `Debug.LogWarning`。走り方式設定にある「復旧したことを画面に出す」表示は持たない。出所: agent前提（YAGNI。手で壊さない限り起きない）
- D12 `MapObjectGameObjectDatastore.cs` は `MapObjectRendererVisibilityRegistry` を生成して instantiator へ渡し、画質設定の木や岩を購読して配る（DataStoreへ画質の判断は持ち込まず、表示可否のboolをpushするだけ）。出所: agent前提（既存の距離表示コントローラを同じ場所で生成・配線している前例に合わせる）
- D13 `GraphicsQualityPreference` は静的クラスで読み書きを同居させる（I*Lookup/I*Mutation に分けない）。出所: シミュレーター予測→ユーザー承認 2026-10-20 質問「画質設定の保存窓口は、読む口と書く口を分けますか？」→「このままでよい」
- D14 型閉包の弱い指摘は (c) enum⇔数値の対応を `ToMeters`/`ToScale` に一本化する（Wire は逆引き）だけ拾い、(a) 型の置き場・(b) 翻訳欠損は前例どおり。出所: シミュレーター予測→ユーザー承認 2026-10-20 質問「型閉包検査の弱い指摘が3件あります。どれを拾いますか？」→「(c)だけ直す」
- D15 v7の残り項目は既存項目の最低値に含める（R5b）。公開setterの無いURPの直列化フィールド（`m_SoftShadowsSupported`・`m_AdditionalLightShadowsSupported`・`m_EnableLODCrossFade`・`m_AdditionalLightsRenderingMode`）は複製アセットへリフレクションで書き、見つからなければ起動時に例外で止める。出所: ユーザー承認の裁定（`.decisions/2026-10-20-v7の残り項目は画質設定の既存項目の最低値に含める.md`）＋実装方法は agent前提
- D16 Topic は既存24本と同じ DTO＋`WebUiJson.Serialize` で組む。出所: agent前提（配置検査の違反指摘。前例 `SprintInputModeTopic.cs`）
- D11 ランタイム挙動に触れるため unityプレイ録画テスト（Task 7）を入れる。出所: moores-writing-plans 規定

## 本PR外のリファクタ提案（設計検査 第3バケツ。planのタスクにはしない。PR本文へ転記する）
- 既存 `Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingTarget.cs` ／新設 `EffectsQualitySwitch.SetEffectsEnabled` ／どちらも「止める直前のenabled配列を保存し、再開時に戻す」役割で、部品の型だけが違う
- 既存 `Client.Input/Sprint/SprintInputModePreference.cs` の `Load` ／新設 `GraphicsQualityPreference.Load` ／どちらも「Enum.TryParse＋IsDefinedで保存値を検証し、不正なら既定へ書き戻してLogWarning」を各自で持つ
