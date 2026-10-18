# 設置モードのプレビュー表示をプレイヤー周囲20mに限る Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 設置モード中の既設ブロックのプレビュー専用表示（枠線・搬入出口矢印・歯車接続表示）を、プレイヤー周囲20m以内のブロックだけ描画して重さを解消する。

**Architecture:** 既存のコライダー距離カリング（`ColliderCullingChunkTable`）をドメイン非依存の `DistanceCullingChunkTable` へ一般化し、同じテーブルをもう1本（4mチャンク・半径20m）立ててブロックごとの「半径内か」をプッシュする。ブロック側は `BlockPreviewOnlyObjectsView` が「表示モード（enum）×半径内フラグ」から GameObject の有効/レンダラーの有効を決める。設置レイの当たり判定（枠線の面コライダー）は半径外でも残す。

**Tech Stack:** Unity / C# / VContainer（ITickable・IStartable）/ UniRx / NUnit（EditMode）/ プレイテストDSL（unity-playmode-recorded-playtest）

## Requirements

- R1: 設置モード（PlaceBlockState）中、プレイヤー位置から半径20m以内（XZ平面）のブロックだけ、PreviewOnlyObject（`BlockPreviewBoundingBox`・`InventoryConnectorView`・`GearConnectorView`）のレンダラーを有効にする。受け入れ: 半径外のブロックの枠線レンダラーが無効、半径内は有効（録画テストで確認）
- R2: 対象判定は全ブロックの毎フレーム走査をしない。チャンク登録＋プレイヤーが一定距離（1m）動いた時だけ差分を切り替える。受け入れ: `DistanceCullingChunkTable` の差分テストが通る
- R3: 半径外でも枠線（BlockPreviewBoundingBox）の GameObject と面コライダーは有効のまま（搬入出口矢印・歯車接続表示はコライダーを持たず、既存実装どおり描画OFF＝GameObject非アクティブになる）。受け入れ: 半径外ブロックの上面に照準してその上へ設置できる（録画テストで確認）
- R4: 設置モード外では従来どおり全ブロックのプレビュー専用表示を消す。設置モード中に新規設置されたブロックは、設置アニメーション中はレンダラー無効・完了後に半径判定に従う。アニメーション無しで置かれたブロック（一括ワールド同期 `WorldDataHandler` 経路）は即座に半径判定に従う（変更前は描画されないままだった既存不具合を直す）
- R5: デバッグブロック情報（DebugBlockInfoState）のホバー表示は従来どおり距離に関係なくホバー中のブロックだけ描画する
- R6: 枠線（非同期ロード）が設置モード中にロード完了した場合も、その時点の表示モードと半径判定に従う
- R7: 前後の重さを実測して報告する（多数ブロックのワールドで設置モード中のフレーム時間を、変更前ベースと変更後で比較）
- やらないこと: 設置ゴースト（BlockPreviewObject）側の PreviewOnlyObject には触らない／半径の設定UIは作らない／Y方向の距離は見ない（前例と同じXZ判定）

## Global Constraints

- 半径20m・中心はプレイヤー位置（ADR 0083 決定1。ユーザー裁定）
- 1ファイル200行未満、1ディレクトリ10ファイルまで（AGENTS.md）
- コメントは日本語1行→英語1行の2行セット。`#region Internal` はメソッド内ローカル関数専用
- `Func<>`・partial・try-catch・デフォルト引数・単純getter/setterプロパティ禁止。イベントはUniRx
- .metaを手で作らない。ファイル移動は `git mv` で `.cs` と `.cs.meta` を一緒に動かす（新規フォルダの .meta は Unity 起動で生成されたものをコミット）
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/build-preview-player-radius`（ブランチ `feat/build-preview-player-radius`）。コマンドはこのディレクトリで実行する

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 既存コライダー距離カリングの部品を中立化して共有、死活表で喪失操作なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱2・第3バケツ0 / 接続表示のGameObject切替はR3文言修正、Manager・登録の重複は共通化（ユーザー裁定）

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling/DistanceCullingChunkTable.cs`（`ColliderStreaming/ColliderCullingChunkTable.cs` を git mv して改名） | チャンク登録・アクティブチャンク集合の差分で対象へ半径内/外を指示（ドメイン非依存） |
| `.../InGame/DistanceCulling/DistanceCullingChunkUtil.cs`（`ColliderCullingChunkUtil.cs` を改名） | チャンク座標計算・半径判定の純粋関数 |
| `.../InGame/DistanceCulling/IDistanceCullingTarget.cs`（`IColliderDistanceCullingTarget.cs` を改名） | `void SetWithinRange(bool withinRange)` だけを持つ対象 |
| `.../InGame/DistanceCulling/PlayerCenteredDistanceCulling.cs`（新規） | プレイヤーが一定距離動くたびにテーブルを再評価するドライバ（定数は引数） |
| `.../InGame/Block/DistanceCulling/BlockDistanceCullingEntry.cs`・`IBlockDistanceCullingSource.cs`・`BlockDistanceCullingRegistrar.cs`（新規） | ブロック設置/削除での登録・解除の共通部品 |
| `.../InGame/ColliderStreaming/ColliderDistanceCullingManager.cs`（変更） | 定数を持ってドライバへ委譲する薄いITickable |
| `.../InGame/ColliderStreaming/Block/BlockColliderCullingTarget.cs`（変更） | `IDistanceCullingTarget` 実装へ追従 |
| `.../InGame/ColliderStreaming/Block/BlockColliderCullingRegisterService.cs`（変更） | コライダーから範囲と対象を作る `IBlockDistanceCullingSource` |
| `.../InGame/Block/Preview/PreviewOnlyObjectsDisplay.cs`（新規） | 表示モード enum |
| `.../InGame/Block/Preview/BlockPreviewOnlyObjectsView.cs`（新規） | 表示モード×半径内フラグ→各PreviewOnlyObjectの有効/レンダラー |
| `.../InGame/Block/Preview/BlockPreviewRadiusCullingManager.cs`（新規） | 4mチャンク・半径20m・1m再評価の定数でドライバを持つ ITickable |
| `.../InGame/Block/Preview/BlockPreviewRadiusCullingTarget.cs`（新規） | テーブルの指示を BlockGameObject へ渡す対象 |
| `.../InGame/Block/Preview/BlockPreviewRadiusRegisterService.cs`（新規） | 占有セルから範囲と対象を作る `IBlockDistanceCullingSource` |
| `.../InGame/Block/BlockGameObject.cs`（変更） | `_previewOnlyObjects` リストを View へ置換、API差し替え |
| `.../InGame/UI/UIState/State/PlaceBlockState.cs`（変更） | 新APIで表示モードを指示 |
| `.../InGame/UI/UIState/State/DebugBlockInfoState.cs`（変更） | 新APIで表示モードを指示 |
| `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（変更） | 新マネージャ・登録サービスのDI登録 |
| `moorestech_client/Assets/Scripts/Client.Tests/DistanceCulling/*`（`Client.Tests/ColliderStreaming/*` 3本を git mv して改名） | テーブル・ユーティリティのテスト |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPreviewOnlyObjectsViewTest.cs`（新規） | View の表示モード×半径判定テスト |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`（新規） | 録画テスト兼フレーム時間計測シナリオ |

`Client.Game/InGame/Block/Preview/` は既存1本＋新規5本で6本。

### 配置と前例

- 半径判定は `ColliderStreaming/ColliderDistanceCullingManager.cs`（プレイヤー位置・一定距離移動で再評価・チャンク差分）と `ColliderStreaming/Block/BlockColliderCullingRegisterService.cs`（設置/削除購読で登録）の中身を共通部品へ抜き出し、両用途で共有する（ユーザー裁定）
- テーブルは同じ実装を2用途で使うため、ドメイン語彙（Collider）を外して `InGame/DistanceCulling/` へ移す（汎用基盤にドメイン語彙を持ち込まない）

### データフロー

```
（プレイヤー移動）→ BlockPreviewRadiusCullingManager.Tick → DistanceCullingChunkTable → BlockPreviewRadiusCullingTarget ─┐
（UIState遷移）→ PlaceBlockState / DebugBlockInfoState ─────────────────────────────────────────────────────────────┤
                                                                         → ［BlockPreviewOnlyObjectsView: 表示モード×半径内］→ SetActive / SetEnableRenderers
```

マネージャとUIStateはどちらも View へ書くだけの書き手で、View が両方から描画を導出する。分岐・逆流は無い。

### 死活表（PreviewOnlyObject にぶら下がる操作）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 設置モードで既設ブロックの枠線・搬入出口・歯車接続を見る | 20m内のみ（裁定どおり） | ADR 0083 決定1 |
| 既設ブロックの面へ照準して隣接設置 | 生きる | 枠線の面コライダーは RenderedWithinPlayerRadius で有効のまま（Task 5 で半径外ブロックの上へ設置） |
| 設置直後のアニメーション中は表示を隠す | 生きる | CollidersOnly |
| デバッグ情報のホバー枠線 | 生きる（距離無関係） | Rendered |
| 設置モード外では非表示 | 生きる | Hidden |
| コライダー距離カリング（120m） | 生きる | テーブル改名のみ・既存テスト維持 |
| 設置ゴースト（BlockPreviewObject）の接続表示 | 無関係 | `BlockPreviewObject` は別リストで本変更の外 |

---

### Task 1: 変更前ベースのフレーム時間を計測するシナリオを作り、ベースを計測する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`

**Interfaces:**
- Produces: シナリオ名 `build-preview-radius`。result.json の Timeline に NOTE `avgFrameMs=<値>` を出す

- [ ] **Step 1: 計測部分だけのシナリオを書く**（Task 5 で半径の検証を足す。手順は `.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md` と `run-scenario.md`）

```csharp
using Client.Game.InGame.Context;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

// 1600ブロック並べて設置モードのフレーム時間を測る
// Lay out 1600 blocks and measure frame time in build mode
var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("build-preview-radius", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    // サーバー直置きなので足場外でもよい。2m間隔で40x40個（-40..38）、プレイヤーは足場(-25..25)内に立つ
    // Server-direct placement needs no floor; 40x40 at 2m spacing (-40..38), the player stays on the floor (-25..25)
    for (var x = -40; x < 40; x += 2)
    for (var z = -40; z < 40; z += 2)
        p.PlaceBlockDirect("木のチェスト", new Vector3Int(x, 32, z), BlockDirection.North);
    await p.Until(() => ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Count >= 1600, 60f, "1600ブロックのクライアント反映");

    // 設置モードへ入り3秒間の平均フレーム時間を出す
    // Enter build mode and report the average frame time over 3 seconds
    await p.OpenBuildMenuAndSelectBlock("木のチェスト");
    var frames = 0;
    var elapsed = 0f;
    while (elapsed < 3f)
    {
        await UniTask.Yield();
        elapsed += Time.unscaledDeltaTime;
        frames++;
    }
    p.Note($"avgFrameMs={elapsed * 1000f / frames:F2}");
    await p.Screenshot("01-build-mode-dense");
});
```

`ClientDIContext.BlockGameObjectDataStore` は既存シナリオ `scenarios/train/train-station-default-name.cs` と同じ参照経路。足場は `Client.Playtest/Operations/PlaytestSetup.cs` の x,z=-25..25 固定。ブロックは `PlaceBlockDirect`（サーバー直置き）なので足場外に置いてよいが、プレイヤーのワープ先は必ず足場内にする（半径20m内に入るのは全体の一部に保ち、前後比較を効かせるため配置範囲は縮めない）。

- [ ] **Step 2: ベース（Task 2 以降の変更前）で実行し、`avgFrameMs` を記録する**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`（引数は `<unity-project-path> <scenario.cs> [master-server-dir]`。`references/run-scenario.md` を正とする）
Expected: result.json `Success: true`、`Timeline` の `NOTE` 行に `avgFrameMs=...`。値を plan 末尾「計測結果」へ `ベース: <値>ms` と追記

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs docs/superpowers/plans/2026-10-18-build-preview-player-radius.md
git commit -m "test: 設置モードのフレーム時間を測る録画シナリオを追加しベースを計測"
```

### Task 2: チャンクテーブルをドメイン非依存の DistanceCulling へ一般化する

**Files:**
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/ColliderCullingChunkTable.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling/DistanceCullingChunkTable.cs`
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/ColliderCullingChunkUtil.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling/DistanceCullingChunkUtil.cs`
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/IColliderDistanceCullingTarget.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling/IDistanceCullingTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/ColliderDistanceCullingManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingRegisterService.cs`（using 追従のみ）
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Tests/ColliderStreaming/ColliderCullingChunkTableTest.cs` → `moorestech_client/Assets/Scripts/Client.Tests/DistanceCulling/DistanceCullingChunkTableTest.cs`
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Tests/ColliderStreaming/ColliderCullingChunkUtilTest.cs` → `moorestech_client/Assets/Scripts/Client.Tests/DistanceCulling/DistanceCullingChunkUtilTest.cs`
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Tests/ColliderStreaming/ColliderCullingTestSupport.cs` → `moorestech_client/Assets/Scripts/Client.Tests/DistanceCulling/DistanceCullingTestSupport.cs`

**Interfaces:**
- Produces:
  - `namespace Client.Game.InGame.DistanceCulling`
  - `public interface IDistanceCullingTarget { void SetWithinRange(bool withinRange); }`
  - `public sealed class DistanceCullingChunkTable { public DistanceCullingChunkTable(float chunkSize); public IDisposable Add(Bounds worldBounds, IDistanceCullingTarget target); public void UpdateForCenter(Vector3 center, float radius); }`
  - `public static class DistanceCullingChunkUtil`（メソッドは既存と同名同引数）

- [ ] **Step 1: git mv でファイルと .meta を移す**

```bash
C=moorestech_client/Assets/Scripts/Client.Game/InGame
T=moorestech_client/Assets/Scripts/Client.Tests
mkdir -p $C/DistanceCulling $T/DistanceCulling
for pair in "ColliderCullingChunkTable:DistanceCullingChunkTable" "ColliderCullingChunkUtil:DistanceCullingChunkUtil" "IColliderDistanceCullingTarget:IDistanceCullingTarget"; do
  old=${pair%%:*}; new=${pair##*:}
  git mv $C/ColliderStreaming/$old.cs $C/DistanceCulling/$new.cs
  git mv $C/ColliderStreaming/$old.cs.meta $C/DistanceCulling/$new.cs.meta
done
for pair in "ColliderCullingChunkTableTest:DistanceCullingChunkTableTest" "ColliderCullingChunkUtilTest:DistanceCullingChunkUtilTest" "ColliderCullingTestSupport:DistanceCullingTestSupport"; do
  old=${pair%%:*}; new=${pair##*:}
  git mv $T/ColliderStreaming/$old.cs $T/DistanceCulling/$new.cs
  git mv $T/ColliderStreaming/$old.cs.meta $T/DistanceCulling/$new.cs.meta
done
```

`Client.Tests/ColliderStreaming/` が空になったらそのフォルダと `ColliderStreaming.meta` を `git rm` する（Unity生成の既存metaの削除であり手動作成ではない）。

- [ ] **Step 2: 名前を置換する**

- 3本の本体: `namespace Client.Game.InGame.ColliderStreaming` → `namespace Client.Game.InGame.DistanceCulling`、クラス名・ファイル内参照を `ColliderCullingChunkTable`→`DistanceCullingChunkTable`、`ColliderCullingChunkUtil`→`DistanceCullingChunkUtil`、`IColliderDistanceCullingTarget`→`IDistanceCullingTarget`
- インターフェース本体を次に置換:

```csharp
namespace Client.Game.InGame.DistanceCulling
{
    /// <summary>
    /// 距離カリングの対象。半径内/外の指示だけ受け取り、具体処理は実装側が行う
    /// A distance-culling target. Receives only within/outside-range instructions; concrete work is done by the implementation
    /// </summary>
    public interface IDistanceCullingTarget
    {
        // 半径内ならtrue、外ならfalse。状態が変わった時だけ呼ばれる
        // true = within range, false = outside. Called only when the state changes
        void SetWithinRange(bool withinRange);
    }
}
```

- テーブルの `Entry.SetActive` 内 `Target.SetCollider(on);` → `Target.SetWithinRange(on);`。テーブル/ユーティリティの XML コメントの「コライダー」「プレイヤー」語は「対象」「中心」へ直す
- `ColliderDistanceCullingManager.cs`・`BlockColliderCullingRegisterService.cs`: `using Client.Game.InGame.DistanceCulling;` を足し、型名を追従
- `BlockColliderCullingTarget.cs`: `: IDistanceCullingTarget`、`public void SetCollider(bool on)` → `public void SetWithinRange(bool withinRange)`（本体の `on` を `withinRange` に改名、挙動不変）
- テスト3本: `using Client.Game.InGame.ColliderStreaming;` → `using Client.Game.InGame.DistanceCulling;`、型名追従、`FakeCullingTarget` を `: IDistanceCullingTarget` と `public void SetWithinRange(bool withinRange)` へ、テストクラス名をファイル名に合わせる

- [ ] **Step 3: 旧名の残りが無いことを確認しコンパイル・テスト**

Run: `grep -rn "ColliderCullingChunk\|IColliderDistanceCullingTarget\|SetCollider(" moorestech_client/Assets/Scripts` → 0件
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DistanceCullingChunk"`
Expected: ErrorCount 0 / 全PASS（既存テスト本数と同数）

- [ ] **Step 4: コミットする**（Unity が生成したフォルダ .meta を含める）

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling.meta moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming moorestech_client/Assets/Scripts/Client.Tests
git commit -m "refactor: 距離カリングのチャンクテーブルをドメイン非依存のDistanceCullingへ一般化"
```

### Task 3: ブロックのプレビュー表示を「表示モード×半径内」で決める View を入れる

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/PreviewOnlyObjectsDisplay.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/BlockPreviewOnlyObjectsView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs:42,86,94-97,148-155`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs:77-97,174-177`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DebugBlockInfoState.cs:71-72,118`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPreviewOnlyObjectsViewTest.cs`

**Interfaces:**
- Consumes: `IPreviewOnlyObject`（既存: `SetActive(bool)`, `SetEnableRenderers(bool)`）
- Produces:
  - `public enum PreviewOnlyObjectsDisplay { Hidden, CollidersOnly, Rendered, RenderedWithinPlayerRadius }`
  - `public class BlockPreviewOnlyObjectsView { public BlockPreviewOnlyObjectsView(List<IPreviewOnlyObject> previewOnlyObjects); public void Add(IPreviewOnlyObject previewOnlyObject); public void SetDisplay(PreviewOnlyObjectsDisplay display); public void SetWithinPlayerRadius(bool withinPlayerRadius); }`
  - `BlockGameObject.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay display)`／`BlockGameObject.SetPreviewOnlyObjectsWithinPlayerRadius(bool withinPlayerRadius)`（`EnablePreviewOnlyObjects` は削除）

- [ ] **Step 1: enum と View を書く**

`PreviewOnlyObjectsDisplay.cs`:

```csharp
namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 既設ブロックのプレビュー専用表示の出し方
    /// How a placed block shows its preview-only objects
    /// </summary>
    public enum PreviewOnlyObjectsDisplay
    {
        // 非アクティブ（当たり判定も無し）
        // Inactive, no colliders either
        Hidden,
        // 当たり判定だけ有効で描画しない（設置アニメーション中）
        // Colliders active but not rendered (during place animation)
        CollidersOnly,
        // 距離に関係なく描画（デバッグのホバー）
        // Rendered regardless of distance (debug hover)
        Rendered,
        // 当たり判定は有効のまま、プレイヤー半径内だけ描画（設置モード）
        // Colliders stay active, rendered only within the player radius (build mode)
        RenderedWithinPlayerRadius,
    }
}
```

`BlockPreviewOnlyObjectsView.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 表示モードと半径内フラグから各プレビュー専用部品の有効・描画を決める
    /// Decides each preview-only object's active/render state from the display mode and the within-radius flag
    /// </summary>
    public class BlockPreviewOnlyObjectsView
    {
        private readonly List<IPreviewOnlyObject> _previewOnlyObjects;
        private PreviewOnlyObjectsDisplay _display = PreviewOnlyObjectsDisplay.Hidden;
        // 初回評価前は半径内扱い（距離カリングの既定onに揃える）
        // Treated as within range before the first evaluation (matches the culling table's default-on)
        private bool _isWithinPlayerRadius = true;

        public BlockPreviewOnlyObjectsView(List<IPreviewOnlyObject> previewOnlyObjects)
        {
            _previewOnlyObjects = previewOnlyObjects;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject);
        }

        // 遅れてロードされた部品も現在の状態で表示を揃える
        // A late-loaded object is aligned to the current state right away
        public void Add(IPreviewOnlyObject previewOnlyObject)
        {
            _previewOnlyObjects.Add(previewOnlyObject);
            Apply(previewOnlyObject);
        }

        public void SetDisplay(PreviewOnlyObjectsDisplay display)
        {
            _display = display;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject);
        }

        // 半径判定が効くのは設置モード表示の時だけなので、それ以外は記録のみ
        // The radius only matters in build-mode display, so otherwise just record it
        public void SetWithinPlayerRadius(bool withinPlayerRadius)
        {
            _isWithinPlayerRadius = withinPlayerRadius;
            if (_display != PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius) return;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject);
        }

        private void Apply(IPreviewOnlyObject previewOnlyObject)
        {
            var isActive = _display != PreviewOnlyObjectsDisplay.Hidden;
            var isRendered = _display switch
            {
                PreviewOnlyObjectsDisplay.Rendered => true,
                PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius => _isWithinPlayerRadius,
                _ => false,
            };
            previewOnlyObject.SetActive(isActive);
            previewOnlyObject.SetEnableRenderers(isRendered);
        }
    }
}
```

`BlockPreviewOnlyObjects.Initialize(gameObject, BlockId, false)` は `SetActive(false)` 済みのリストを返すので、View のコンストラクタで Hidden を再適用しても状態は同じ。

- [ ] **Step 2: BlockGameObject を View へ置き換える**

- フィールド: `private List<IPreviewOnlyObject> _previewOnlyObjects = new();` → `private BlockPreviewOnlyObjectsView _previewOnlyObjectsView;`
- Initialize 内: `_previewOnlyObjects = BlockPreviewOnlyObjects.Initialize(gameObject, BlockId, false);` → `_previewOnlyObjectsView = new BlockPreviewOnlyObjectsView(BlockPreviewOnlyObjects.Initialize(gameObject, BlockId, false));`
- `AddBoundingBox` 内: `_previewOnlyObjects.Add(await ...)` → `_previewOnlyObjectsView.Add(await ...)`
- `EnablePreviewOnlyObjects` を削除し次の2メソッドに置換:

```csharp
        public void SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay display)
        {
            _previewOnlyObjectsView.SetDisplay(display);
        }
        public void SetPreviewOnlyObjectsWithinPlayerRadius(bool withinPlayerRadius)
        {
            _previewOnlyObjectsView.SetWithinPlayerRadius(withinPlayerRadius);
        }
```

設置アニメーション中かを公開する（`_isShaderAnimating` は撤去アニメーションでも立つので流用しない）:

```csharp
        public bool IsPlayingPlaceAnimation { get; private set; }
```

`PlayPlaceAnimation` の先頭（`_isShaderAnimating = true;` の直後）に `IsPlayingPlaceAnimation = true;`、`_isShaderAnimating = false;` の直後に `IsPlayingPlaceAnimation = false;` を入れる。`BlockGameObjectDataStore.PlaceBlock` は `PlayPlaceAnimation().Forget()` を `_onBlockPlaced.OnNext` より前に同期的に呼ぶので、購読側から見た時点で値は確定している。

不要になった `using` は残さない。行数が200未満であることを `wc -l` で確認する。

- [ ] **Step 3: 呼び出し側を差し替える**

PlaceBlockState.OnEnter（コメント「ここが重くなったら近いブロックだけ…」は削除）:

```csharp
            // 当たり判定は全ブロック有効にし、描画はプレイヤー半径内だけにする
            // Enable colliders on every block, but render only within the player radius
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            #region Internal

            void OnPlaceBlock(BlockGameObject blockGameObject)
            {
                // アニメーション無しの設置は完了通知が来ないので即座に半径判定へ
                // A placement without animation never gets the finish notice, so follow the radius right away
                if (!blockGameObject.IsPlayingPlaceAnimation)
                {
                    blockGameObject.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
                    return;
                }

                blockGameObject.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.CollidersOnly);

                _blockPlacedDisposable.Add(blockGameObject.OnFinishedPlaceAnimation.Subscribe(_ =>
                {
                    blockGameObject.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
                }));
            }

            #endregion
```

PlaceBlockState.OnExit: `EnablePreviewOnlyObjects(false, false)` → `SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.Hidden)`。
DebugBlockInfoState: `EnablePreviewOnlyObjects(false, false)`（2箇所）→ `SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.Hidden)`、`EnablePreviewOnlyObjects(true, true)` → `SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.Rendered)`。両ファイルに `using Client.Game.InGame.Block.Preview;` を足す。

- [ ] **Step 4: View のテストを書く**

`Client.Tests/BlockPreview/BlockPreviewOnlyObjectsViewTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests
{
    /// <summary>
    /// 表示モードと半径内フラグの組み合わせで有効・描画が決まることを確かめる
    /// Verifies active/render state follows the display mode and the within-radius flag
    /// </summary>
    public class BlockPreviewOnlyObjectsViewTest
    {
        [Test]
        public void BuildModeOutsideRadius_KeepsActiveButStopsRendering()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            Assert.IsTrue(fake.Active);
            Assert.IsTrue(fake.Rendered);

            view.SetWithinPlayerRadius(false);
            Assert.IsTrue(fake.Active, "当たり判定のため有効のまま");
            Assert.IsFalse(fake.Rendered);

            view.SetWithinPlayerRadius(true);
            Assert.IsTrue(fake.Rendered);
        }

        [Test]
        public void RadiusChangeWhileHidden_DoesNotActivate()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetWithinPlayerRadius(true);
            Assert.IsFalse(fake.Active);
            Assert.IsFalse(fake.Rendered);
        }

        [Test]
        public void EnteringBuildModeOutsideRadius_StartsUnrendered()
        {
            // 設置モード外で半径外になったブロックは、設置モードに入っても描画しない
            // A block that went out of range outside build mode stays unrendered on entering it
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            Assert.IsTrue(fake.Active);
            Assert.IsFalse(fake.Rendered);
        }

        [Test]
        public void DebugRendered_IgnoresRadius_AndCollidersOnlyNeverRenders()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetDisplay(PreviewOnlyObjectsDisplay.Rendered);
            Assert.IsTrue(fake.Rendered);

            view.SetDisplay(PreviewOnlyObjectsDisplay.CollidersOnly);
            Assert.IsTrue(fake.Active);
            Assert.IsFalse(fake.Rendered);
        }

        [Test]
        public void LateAddedObject_FollowsCurrentState()
        {
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject>());
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            view.SetWithinPlayerRadius(false);

            var late = new FakePreviewOnlyObject();
            view.Add(late);
            Assert.IsTrue(late.Active);
            Assert.IsFalse(late.Rendered);
        }

        private sealed class FakePreviewOnlyObject : IPreviewOnlyObject
        {
            public bool Active;
            public bool Rendered;
            public void Initialize(BlockId blockId) { }
            public void SetActive(bool active) { Active = active; }
            public void SetEnableRenderers(bool enable) { Rendered = enable; }
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `grep -rn "EnablePreviewOnlyObjects" moorestech_client/Assets/Scripts` → 0件
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.BlockPreviewOnlyObjectsViewTest"`
Expected: ErrorCount 0 / 5件PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Block moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DebugBlockInfoState.cs moorestech_client/Assets/Scripts/Client.Tests/BlockPreview moorestech_client/Assets/Scripts/Client.Tests/BlockPreview.meta
git commit -m "feat: ブロックのプレビュー表示を表示モードと半径内フラグで決めるViewを導入"
```

### Task 4: 再評価ドライバと登録を共通化し、プレイヤー周囲20mの判定を回す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling/PlayerCenteredDistanceCulling.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/DistanceCulling/BlockDistanceCullingEntry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/DistanceCulling/IBlockDistanceCullingSource.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/DistanceCulling/BlockDistanceCullingRegistrar.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/ColliderDistanceCullingManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingRegisterService.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/BlockPreviewRadiusCullingManager.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/BlockPreviewRadiusCullingTarget.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/BlockPreviewRadiusRegisterService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs:94-95`

**Interfaces:**
- Consumes: `DistanceCullingChunkTable`・`IDistanceCullingTarget`（Task 2）、`BlockGameObject.SetPreviewOnlyObjectsWithinPlayerRadius(bool)`（Task 3）
- Produces:
  - `public sealed class PlayerCenteredDistanceCulling { public PlayerCenteredDistanceCulling(PlayerSystemContainer playerSystemContainer, float chunkSize, float radius, float reevalMoveDistance); public IDisposable Register(Bounds worldBounds, IDistanceCullingTarget target); public void Tick(); }`
  - `public readonly struct BlockDistanceCullingEntry { public readonly Bounds WorldBounds; public readonly IDistanceCullingTarget Target; }`
  - `public interface IBlockDistanceCullingSource { BlockDistanceCullingEntry CreateEntry(BlockGameObject block); }`
  - `public sealed class BlockDistanceCullingRegistrar : IDisposable { public BlockDistanceCullingRegistrar(BlockGameObjectDataStore dataStore, PlayerCenteredDistanceCulling culling, IBlockDistanceCullingSource source); public void Start(); public void Dispose(); }`
  - `ColliderDistanceCullingManager` / `BlockPreviewRadiusCullingManager`: 共に `ITickable`、`public readonly PlayerCenteredDistanceCulling Culling;`（コンストラクタで生成）

- [ ] **Step 1: 再評価ドライバを書く**（`ColliderDistanceCullingManager.Tick` の本体をここへ移す）

```csharp
using System;
using Client.Game.InGame.Player;
using UnityEngine;

namespace Client.Game.InGame.DistanceCulling
{
    /// <summary>
    /// プレイヤーが一定距離動くたびにチャンクテーブルへ半径内/外の差分を指示する
    /// Each time the player moves a set distance, drives the chunk table's within/outside-range delta
    /// </summary>
    public sealed class PlayerCenteredDistanceCulling
    {
        private readonly PlayerSystemContainer _playerSystemContainer;
        private readonly DistanceCullingChunkTable _chunkTable;
        private readonly float _radius;
        private readonly float _reevalMoveDistance;

        private Vector3 _lastEvalPosition;
        private bool _hasEvaluated;

        public PlayerCenteredDistanceCulling(PlayerSystemContainer playerSystemContainer, float chunkSize, float radius, float reevalMoveDistance)
        {
            _playerSystemContainer = playerSystemContainer;
            _chunkTable = new DistanceCullingChunkTable(chunkSize);
            _radius = radius;
            _reevalMoveDistance = reevalMoveDistance;
        }

        public IDisposable Register(Bounds worldBounds, IDistanceCullingTarget target)
        {
            return _chunkTable.Add(worldBounds, target);
        }

        public void Tick()
        {
            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) return; // silent-ok: プレイヤー生成前の数フレームだけ / 生成後のTickで評価が走り録画テストで確認

            var center = player.Position;
            if (_hasEvaluated && (center - _lastEvalPosition).sqrMagnitude <= _reevalMoveDistance * _reevalMoveDistance) return;
            _hasEvaluated = true;
            _lastEvalPosition = center;

            _chunkTable.UpdateForCenter(center, _radius);
        }
    }
}
```

- [ ] **Step 2: ブロック登録の共通部品を書く**（`BlockColliderCullingRegisterService` の購読・辞書管理をここへ移す）

`Block/DistanceCulling/BlockDistanceCullingEntry.cs`:

```csharp
using Client.Game.InGame.DistanceCulling;
using UnityEngine;

namespace Client.Game.InGame.Block.DistanceCulling
{
    /// <summary>
    /// 1ブロック分の距離カリング登録内容（判定範囲と対象）
    /// One block's distance-culling registration: its bounds and its target
    /// </summary>
    public readonly struct BlockDistanceCullingEntry
    {
        public readonly Bounds WorldBounds;
        public readonly IDistanceCullingTarget Target;

        public BlockDistanceCullingEntry(Bounds worldBounds, IDistanceCullingTarget target)
        {
            WorldBounds = worldBounds;
            Target = target;
        }
    }
}
```

`Block/DistanceCulling/IBlockDistanceCullingSource.cs`:

```csharp
namespace Client.Game.InGame.Block.DistanceCulling
{
    /// <summary>
    /// ブロックから距離カリングの範囲と対象を作る（用途ごとに実装）
    /// Builds the culling bounds and target from a block (implemented per use)
    /// </summary>
    public interface IBlockDistanceCullingSource
    {
        BlockDistanceCullingEntry CreateEntry(BlockGameObject block);
    }
}
```

`Block/DistanceCulling/BlockDistanceCullingRegistrar.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.DistanceCulling;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Block.DistanceCulling
{
    /// <summary>
    /// ブロックの設置/削除に合わせて距離カリングへ登録・解除する
    /// Registers/unregisters blocks with a distance culling as they are placed/removed
    /// </summary>
    public sealed class BlockDistanceCullingRegistrar : IDisposable
    {
        private readonly BlockGameObjectDataStore _dataStore;
        private readonly PlayerCenteredDistanceCulling _culling;
        private readonly IBlockDistanceCullingSource _source;
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Vector3Int, IDisposable> _registrations = new();

        public BlockDistanceCullingRegistrar(BlockGameObjectDataStore dataStore, PlayerCenteredDistanceCulling culling, IBlockDistanceCullingSource source)
        {
            _dataStore = dataStore;
            _culling = culling;
            _source = source;
        }

        public void Start()
        {
            // 設置・削除を購読し、既存ブロックも登録する
            // Subscribe to place/remove, and register existing blocks
            _dataStore.OnBlockPlaced.Subscribe(Register).AddTo(_disposables);
            _dataStore.OnBlockRemoved.Subscribe(Unregister).AddTo(_disposables);
            foreach (var block in _dataStore.BlockGameObjectDictionary.Values) Register(block);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        private void Register(BlockGameObject block)
        {
            // 置き換え等で既登録なら先に解除する
            // Unregister first if already registered (e.g. block replacement)
            var pos = block.BlockPosInfo.OriginalPos;
            Unregister(pos);

            var entry = _source.CreateEntry(block);
            _registrations[pos] = _culling.Register(entry.WorldBounds, entry.Target);
        }

        private void Unregister(Vector3Int pos)
        {
            if (!_registrations.TryGetValue(pos, out var registration)) return;
            registration.Dispose();
            _registrations.Remove(pos);
        }
    }
}
```

- [ ] **Step 3: コライダー側を共通部品へ載せ替える**

`ColliderDistanceCullingManager.cs` を次に置換（定数とコメントは既存のまま）:

```csharp
using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// コライダーの距離カリング。プレイヤー周囲120mのブロックだけコライダーを有効にする
    /// Collider distance culling: only blocks within 120m of the player keep colliders enabled
    /// </summary>
    public sealed class ColliderDistanceCullingManager : ITickable
    {
        // 有効半径はブロック選択レイ(100m)+カメラ最大距離(10m)+再評価移動量(8m)を包含
        // Active radius covers pick ray (100m) + max camera boom (10m) + re-eval move (8m)
        private const float ChunkSize = 32f;
        private const float ActiveRadius = 120f;
        private const float ReevalMoveDistance = 8f;

        public readonly PlayerCenteredDistanceCulling Culling;

        public ColliderDistanceCullingManager(PlayerSystemContainer playerSystemContainer)
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

`BlockColliderCullingRegisterService.cs`: 購読・辞書・`Unregister` を削除し、`IBlockDistanceCullingSource` を実装して `BlockDistanceCullingRegistrar` に委ねる。`CalcWorldBounds` ローカル関数（有効コライダーだけで AABB を作る既存ロジック）は `CreateEntry` 内へそのまま移す:

```csharp
    public sealed class BlockColliderCullingRegisterService : IStartable, IDisposable, IBlockDistanceCullingSource
    {
        private readonly BlockDistanceCullingRegistrar _registrar;

        public BlockColliderCullingRegisterService(BlockGameObjectDataStore dataStore, ColliderDistanceCullingManager manager)
        {
            _registrar = new BlockDistanceCullingRegistrar(dataStore, manager.Culling, this);
        }

        public void Start()
        {
            _registrar.Start();
        }

        public void Dispose()
        {
            _registrar.Dispose();
        }

        public BlockDistanceCullingEntry CreateEntry(BlockGameObject block)
        {
            // ブロック配下のコライダーを対象化し、AABBでチャンクをまたぐ大きなブロックも網羅して登録する
            // Wrap the block's colliders as a target and register with an AABB so multi-chunk blocks are covered
            var colliders = block.GetComponentsInChildren<Collider>(true);
            return new BlockDistanceCullingEntry(CalcWorldBounds(colliders, block.transform.position), new BlockColliderCullingTarget(colliders));

            #region Internal

            // （既存 CalcWorldBounds ローカル関数の本体をコメントごと逐語で移す）

            #endregion
        }
    }
```

最後の `#region Internal` の中身は既存ファイルの `Bounds CalcWorldBounds(Collider[] targetColliders, Vector3 fallbackCenter) { ... }` を1文字も変えずに移す（挙動不変のリファクタ）。

- [ ] **Step 4: プレビュー半径側を書く**

`Block/Preview/BlockPreviewRadiusCullingManager.cs`:

```csharp
using Client.Game.InGame.DistanceCulling;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 設置モードのプレビュー表示の距離カリング。プレイヤー周囲20mのブロックだけ描画する
    /// Preview distance culling for build mode: only blocks within 20m of the player render
    /// </summary>
    public sealed class BlockPreviewRadiusCullingManager : ITickable
    {
        // 半径20mはユーザー裁定。チャンク4mで超過を最大約5.7mに抑え、再評価は1m移動ごと
        // 20m radius is a user ruling; 4m chunks cap overshoot near 5.7m, re-evaluated per 1m of movement
        private const float ChunkSize = 4f;
        private const float ActiveRadius = 20f;
        private const float ReevalMoveDistance = 1f;

        public readonly PlayerCenteredDistanceCulling Culling;

        public BlockPreviewRadiusCullingManager(PlayerSystemContainer playerSystemContainer)
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

`Block/Preview/BlockPreviewRadiusCullingTarget.cs`:

```csharp
using Client.Game.InGame.DistanceCulling;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 半径内/外の指示をブロックのプレビュー表示へ渡す
    /// Forwards within/outside-radius instructions to a block's preview display
    /// </summary>
    public sealed class BlockPreviewRadiusCullingTarget : IDistanceCullingTarget
    {
        private readonly BlockGameObject _block;

        public BlockPreviewRadiusCullingTarget(BlockGameObject block)
        {
            _block = block;
        }

        public void SetWithinRange(bool withinRange)
        {
            _block.SetPreviewOnlyObjectsWithinPlayerRadius(withinRange);
        }
    }
}
```

`Block/Preview/BlockPreviewRadiusRegisterService.cs`:

```csharp
using System;
using Client.Game.InGame.Block.DistanceCulling;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// ブロックをプレビュー半径判定へ占有セルの範囲で登録する
    /// Registers blocks with the preview radius culling using their occupied cells
    /// </summary>
    public sealed class BlockPreviewRadiusRegisterService : IStartable, IDisposable, IBlockDistanceCullingSource
    {
        private readonly BlockDistanceCullingRegistrar _registrar;

        public BlockPreviewRadiusRegisterService(BlockGameObjectDataStore dataStore, BlockPreviewRadiusCullingManager manager)
        {
            _registrar = new BlockDistanceCullingRegistrar(dataStore, manager.Culling, this);
        }

        public void Start()
        {
            _registrar.Start();
        }

        public void Dispose()
        {
            _registrar.Dispose();
        }

        public BlockDistanceCullingEntry CreateEntry(BlockGameObject block)
        {
            // 占有セルのAABB（MaxPosはセル座標なので+1して外周まで含める）
            // AABB over occupied cells (MaxPos is a cell coordinate, so +1 to reach the far edge)
            var posInfo = block.BlockPosInfo;
            var bounds = new Bounds();
            bounds.SetMinMax(posInfo.MinPos, posInfo.MaxPos + Vector3Int.one);
            return new BlockDistanceCullingEntry(bounds, new BlockPreviewRadiusCullingTarget(block));
        }
    }
}
```

- [ ] **Step 5: DI登録する**

`MainGameModelRegistration.cs` の `builder.RegisterEntryPoint<BlockColliderCullingRegisterService>();` の直後:

```csharp
            builder.Register<BlockPreviewRadiusCullingManager>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.RegisterEntryPoint<BlockPreviewRadiusRegisterService>();
```

`using Client.Game.InGame.Block.Preview;` を足す。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DistanceCulling|BlockPreviewOnlyObjectsView"`
Expected: ErrorCount 0 / 全PASS。`wc -l` で触った全 .cs が200行未満

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/DistanceCulling moorestech_client/Assets/Scripts/Client.Game/InGame/Block moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs
git commit -m "feat: 距離カリングの再評価と登録を共通化し設置モードのプレビュー表示をプレイヤー周囲20mに限る"
```

### Task 5: 録画テストで半径・隣接設置・フレーム時間を確かめる

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`

**Interfaces:**
- Consumes: Task 1 のシナリオ、`BlockPreviewBoundingBox`（既存コンポーネント）、`BlockPreviewRadiusCullingManager.ActiveRadius`

- [ ] **Step 1: 計測の後ろへ半径と隣接設置の検証を足す**（`p.Note($"avgFrameMs=...")` の直後に挿入）

```csharp
    // 枠線レンダラーの有効数で半径内だけ描画されていることを確かめる
    // Check only blocks within the radius render their bounding box
    // 枠線は非同期ロードなので、判定対象のロード完了を先に待つ
    // The bounding box loads asynchronously, so wait for it before judging
    bool HasBox(Vector3Int pos) => ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos).GetComponentInChildren<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject.BlockPreviewBoundingBox>(true) != null;
    foreach (var probe in new[] { new Vector3Int(0, 32, 0), new Vector3Int(-30, 32, -30), new Vector3Int(28, 32, 0) })
        await p.Until(() => HasBox(probe), 30f, $"枠線ロード {probe}");
    bool IsBoxRendered(Vector3Int pos)
    {
        var block = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        var box = block.GetComponentInChildren<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject.BlockPreviewBoundingBox>(true);
        return box.gameObject.activeInHierarchy && box.GetComponentInChildren<Renderer>(true).enabled;
    }
    p.WarpPlayer(new Vector3(0, 33.5f, 0));
    await UniTask.DelayFrame(5);
    p.Assert(IsBoxRendered(new Vector3Int(0, 32, 0)), "足元のブロックは枠線あり");
    p.Assert(!IsBoxRendered(new Vector3Int(-30, 32, -30)), "約42m先のブロックは枠線なし");

    // 移動すると遠かったブロックが描画され、元の足元は消える
    // Moving lights up the far block and hides the old one
    p.WarpPlayer(new Vector3(-24, 33.5f, -24));
    await UniTask.DelayFrame(5);
    p.Assert(IsBoxRendered(new Vector3Int(-30, 32, -30)), "移動後は約8.5mに近づいたブロックに枠線");
    p.Assert(!IsBoxRendered(new Vector3Int(0, 32, 0)), "移動後は離れたブロックの枠線なし");
    await p.Screenshot("02-after-move");

    // 半径外ブロックの上面へ照準して設置できる（当たり判定が残っている）
    // Placement on top of an out-of-radius block still works (colliders remain)
    var farTarget = new Vector3Int(28, 32, 0);
    p.WarpPlayer(new Vector3(0, 33.5f, 0));
    await UniTask.DelayFrame(5);
    p.Assert(!IsBoxRendered(farTarget), "照準先は半径外");
    await p.AimAt(new Vector3(28.5f, 33f, 0.5f));
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(farTarget + Vector3Int.up) != null, 10f, "半径外ブロックの上へ設置できた");
    await p.Screenshot("03-place-on-far-block");
```

`AimAt`・`ClickPlace` が同期メソッドの場合は `await` を外す（`references/write-scenario.md` の Driver API 表と実装を確認）。`GetBlock` の戻り値の存在判定も同表に合わせる。半径判定はチャンク近似で最大約25.7mまで描画され得るため、「なし」を見る対象は26m超（farTarget は28m）、「あり」を見る対象は16m未満にする。28m先の上面が画面外で照準できない場合は、プレイヤーを `(-2, 33.5, 0)` など足場内で farTarget との距離が26m超を保つ位置へ寄せ、カメラ向きを farTarget へ向けてから照準する。

- [ ] **Step 2: 調査実行（Record=false）で全Assertを通す**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`
Expected: result.json `Success: true`、Asserts 全 pass、Timeline NOTE の `avgFrameMs` を plan 末尾「計測結果」に `変更後: <値>ms` で追記

- [ ] **Step 3: 提出用に `Record = true` で再実行し、動画とスクショ3枚を目視確認する**

Expected: 02 で足元周辺だけ枠線、03 で半径外ブロックの上に設置されている。動画・スクショのパスを plan 末尾「計測結果」に書く

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs docs/superpowers/plans/2026-10-18-build-preview-player-radius.md
git commit -m "test: プレビュー半径・半径外への設置・フレーム時間を録画テストで確認"
```

### Task 6: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] レビュー指摘の反映が判定経路・条件式・評価時点に触れたら、Task 5 の録画テストを反映後のバイナリで再実施する

### Task 7: PR作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR本文に「計測結果」のベース/変更後 avgFrameMs を載せる
- [ ] PR作成後 `moores-wt rm build-preview-player-radius` でworktreeとEditorを畳む

## 計測結果

（Task 1・Task 5 で追記）

## 判断記録（ADR）

- 設計ADR: `docs/adr/0083-build-mode-preview-only-objects-player-radius.md`、裁定 `.decisions/2026-10-18-設置モードのプレビュー表示はプレイヤー周囲20mのブロックに限る.md`（照準1ブロック案を覆したもの: `.decisions/2026-10-18-設置モードの枠線は照準レイが最初に当たった1ブロックだけ描画する.md`）
- テーブルを複製せず `DistanceCulling` へ一般化して2用途で共有する。出所: agent前提（汎用基盤にドメイン語彙を持ち込まない・DRY）
- チャンク4m・再評価1m移動。チャンク最近点判定なので描画範囲は最大で約20+5.7m（チャンク対角）まで広がる。出所: agent前提（前例の32m/8mは半径120m用で、20mには粗すぎる）
- 表示状態は bool 2本でなく enum `PreviewOnlyObjectsDisplay` の4値。デバッグホバー（距離無視）と設置モード（半径判定）を区別するため。出所: agent前提（R5の維持）
- 半径判定は設置モード外でも回し続け、View は設置モード表示の時だけ反映する。設置モードへ入った瞬間に半径状態が既に正しい。プレイヤーが1m動いた時だけ評価するので常時稼働の費用は無視できる。出所: agent前提
- 設置モード入場時の全ブロック `SetActive(true)` は残る（当たり判定を全ブロックで残すADR決定4の帰結。毎フレームではなく入場時1回）。出所: agent前提
- 設置モード中に `playPlaceAnimation=false`（一括ワールド同期）で置かれたブロックが描画されないままになる既存不具合を本PRで直す（`BlockGameObject.IsPlayingPlaceAnimation` で分岐）。原因: 推測（コード読み: `OnFinishedPlaceAnimation` を流すのは `PlayPlaceAnimation` だけ）。テストは録画DSLにアニメ無し設置の経路が無いため置かず、コードレビューで確認する。出所: シミュレーター予測（確信高・前提宣言）→ユーザーの拒否権行使なし 2026-10-18。bd moorestech-2iyu8 は本PRで閉じる
- 再評価ドライバ（`PlayerCenteredDistanceCulling`）とブロック登録（`BlockDistanceCullingRegistrar`）をコライダー用とプレビュー用で共通化する。出所: ユーザー裁定 2026-10-18 選択「共通クラスにまとめる」（棄却: 前例と同じ形で2本並べる）
- Phase 2.6 弱「接続表示の SetEnableRenderers が GameObject ごと切る」: 接続表示はコライダーを持たないため設置判定に影響しない。R3 の文言を「枠線の GameObject と面コライダー」に正した。出所: agent前提
- 録画テスト: ランタイムの表示と設置操作に触れるため unity-playmode-recorded-playtest を含める（Task 5）。前後のフレーム時間も同シナリオで測る（ADR 0083 背景の「実装時に前後比較」）
