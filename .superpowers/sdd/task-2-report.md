# Task 2 レポート: 鉱脈範囲表示を Show(bool)＋ManualUpdate() へ分離（ADR#12・D3）

**ステータス:** DONE_WITH_CONCERNS
**コミット:** `83c79c238` refactor: 鉱脈範囲表示をShow(bool)の変化時プッシュとManualUpdate()のフレーム駆動へ分離する (ADR#12)

> 注: このファイルは 7/30 の別SDDラン（pr-independent-review ダイジェストHTMLテンプレート）のレポートを上書きしている。
> 旧内容はコミット `d096b83f8` に残っており復元可能。親が指定したパスであり、同ランの `task-1-report.md` と同じ再利用スロットのため上書きした。

## 実装したもの

ブリーフのStep 1〜6を全て実装した。値・シグネチャ・コメント文言はブリーフの逐語指定どおり。

### Step 1: interfaceの2メソッド分離
`IMapVeinRangeView` を `void Show(bool isVisible)`（表示状態の変化時プッシュ）と `void ManualUpdate()`（カメラ距離カリングのフレーム駆動）へ分離。XMLサマリもブリーフ文言へ差し替えた。

### Step 2: サービス側の状態保持
`MapVeinRangeViewService` に `private bool _isVisible;` を追加。旧 `ManualUpdate(bool isPlacementPreviewing)` を `Show(bool)` ＋ `ManualUpdate()` の2本へ分割し、内部スイープの判定を `isPlacementPreviewing` → `_isVisible` へ置換。`Show` は `_isVisible` 更新の直後に `ManualUpdate()` を呼び、非表示遷移を次フレームへ持ち越さない。`#region Internal` のローカル関数群（`IsWithinVisibleRadius`/`ShowEntry`/`HideEntry`/`RentBox`/`CreateBox`）は `ManualUpdate()` 内にそのまま維持（クラス直下へ出していない）。

### Step 3: PlaceBlockState
- `OnEnter`: `SetTarget` 直後に `_mapVeinRangeView.Show(true);` を追加（ADR#12の根拠コメント付き）
- `GetNextUpdate`: `ManualUpdate(_placeSystemStateController.CurrentTarget != null)` → 引数なし `ManualUpdate()`。同メソッド内の `_placeSystemStateController.ManualUpdate()` / `_buildUndoService.ManualUpdate()` と同形になった
- `OnExit`: `ManualUpdate(false)` → `Show(false)`（直前の2行コメントは不変）

### Step 4: テストダブルと既存テスト
- `FakeMapVeinRangeView`: `PreviewingPushes` → `ShowPushes` へ改名し `ManualUpdateCount` を追加。**改名前に `grep -rn "PreviewingPushes" --include="*.cs" moorestech_client moorestech_server` を実行し、宣言・代入以外の参照が1件も無いことを確認済み**（`UIStateCameraInteractionTest.cs:120` と `UIStateFocusRestorationTest.cs:100` はコンストラクタ注入のみで、ブリーフの記載どおりだった）
- `MapVeinRangeViewMaterialReuseTest`: 66行・80行の `ManualUpdate(true)` → `Show(true)`、79行の `ManualUpdate(false)` → `Show(false)`
- `MapVeinOutcropAndRangeViewTest.DriveRangeViewFrames`: ループ**前**の `rangeView.Show(isPreviewing);` 1回＋ループ内の `rangeView.ManualUpdate();` へ分離。実運用（OnEnter/OnExitで1回プッシュ、毎フレームtick）と同じ呼び分けを通すようになった

## テスト結果

**コンパイル:** `uloop compile --project-path ./moorestech_client` → `Success: true, ErrorCount: 0`

**テスト:** `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinOutcropAndRangeView|MapVeinRangeViewMaterialReuse|UIStateCameraInteraction|UIStateFocusRestoration"`
→ `TestCount: 8, PassedCount: 8, FailedCount: 0`（`Test execution completed with status: Passed`）

8件が期待どおりの内訳であることも確認した（`[Test]`/`[UnityTest]` 属性の実数: UIStateCameraInteraction 3 + UIStateFocusRestoration 3 + MapVeinRangeViewMaterialReuse 1 + MapVeinOutcropAndRangeView 1 = 8）。つまりPlayMode遷移する `MapVeinOutcropAndRangeViewTest` も空振りせず実走している。

環境メモ: `run-tests` は「Unity is reloading (Domain Reload in progress)」で3回連続弾かれた。ただし同時刻に `compile` と `get-logs` は正常応答し、`UnityMcpSettings.json` の `customPort: 8714` も `lsof` の実LISTENポート（Unity PID 37709）と一致していたため、MCP不通ではなくテストランナー側のゲートと判断。50〜90秒待機のリトライループで通した。

## 自己レビュー

### 完全性
ブリーフのStep 1〜6を全て実施。旧シグネチャの残存を
`grep -rn "ManualUpdate(true)\|ManualUpdate(false)\|ManualUpdate(_placeSystem\|ManualUpdate(isPreviewing\|ManualUpdate(isPlacementPreviewing\|PreviewingPushes" --include="*.cs" moorestech_client moorestech_server`
で確認 → **0件**。

### 設計レンズ照合
- **ポーリング排除（レンズ3）**: `GetNextUpdate()` から毎tickの `CurrentTarget != null` 同値判定が消え、フレーム駆動は距離カリングという物理的進行だけになった。表示ON/OFFは状態遷移点でのプッシュに移った。裁定の趣旨どおり
- **依存方向（レンズ2）**: サービスは「表示状態」だけを受け取り、`CurrentTarget` や「プレビュー中」という上位ドメイン語彙を知らなくなった。判断（設置ステート滞在中は常に表示）は具体側の `PlaceBlockState` に移った
- **前例一致（レンズ1）**: 引数なし `ManualUpdate()` は同一メソッド内の `_placeSystemStateController.ManualUpdate()` / `_buildUndoService.ManualUpdate()` と同形になり、3者が揃った
- **配置規約（レンズ9）**: 全ファイル200行以内（`MapVeinRangeViewService` 168行 / `PlaceBlockState` 159行 / `FakeMapVeinRangeView` 25行）。partial・try-catch・`Func<>`・デフォルト引数の新規混入なし

### QA観点で潰した疑い
- **`Show(false)` の残存ボックス持ち越し**: `Show` が `_isVisible` 更新の直後に `ManualUpdate()` を呼ぶため、`OnExit` 時点で全エントリが即座に `HideEntry` されプールへ戻る。次フレームへ持ち越さない（ブリーフStep 2の設計意図どおり）。`MapVeinOutcropAndRangeViewTest` の「③プレビュー終了で表示が消える」「④3周しても溜まらない」がこれを実挙動で押さえており、両方PASS
- **OnEnter再入での二重表示**: `ShowEntry` が `entry.ViewObject != null` で早期returnするため、`Show(true)` を続けて呼んでもボックスは重複しない。同テストの④サイクルで実証済み
- **`CurrentTarget` が死にコードにならないか**: `PlaceSystemStateController` 内部（`ManualUpdate` の選択変更判定）と `Client.WebUiHost/Game/Topics/C2/PlacementModeTopic.cs:53` が引き続き使用しており参照が残る。削除対象ではない
- **`DriveRangeViewFrames` の `Show` がカメラ設置より前にある点**: 遠方ケース（`DriveRangeViewFrames(rangeView, farAway, true)`）では `Show(true)` が旧カメラ位置でスイープしボックスが一瞬出るが、続くループが `farAway` で `ManualUpdate()` を3フレーム回して全消しするため最終状態は正しい。本番の `OnEnter` も「その時点のカメラ位置」でスイープするので挙動として忠実。テストの「カメラが遠いと消える」アサートはPASS

### 規律（YAGNI）
コミットは指定6ファイルのみ。`git status --porcelain` で確認し、他エージェント由来の `.moorestech-external-revisions.json` と `docs/superpowers/plans/2026-08-02-pr1104-review-ruling-fixes.md` の未コミット変更は巻き込んでいない（コミット後も working tree に残存）。

## 懸念（DONE_WITH_CONCERNS の理由）

1. **`FakeMapVeinRangeView.ManualUpdateCount` を読むテストが存在しない。** ブリーフの逐語指定なのでそのまま実装したが、現時点では誰も参照しない記録用メンバーである（`ShowPushes` も同様に未参照だが、こちらは改名前の `PreviewingPushes` から引き継いだ既存状態）。レビューで「使われないなら消せ」と指摘されうる。逆に言えば `Show`/`ManualUpdate` の呼び分けを `UIState` 側テストでアサートする余地が空いたままなので、後続で「OnEnterでShow(true)が1回・GetNextUpdateではShowが増えない」を押さえるか、メンバーごと削るかの判断が要る。

2. **`ManualUpdateCount` が `{ get; private set; }` の自動プロパティである点。** AGENTS.md の「単純なgetter/setterプロパティは使用禁止」に文言上は触れうる（実態は外部setterを持たないカウンタで、当該規約が禁じる public set とは異なる）。ブリーフの逐語指定を優先した。テストダブル限定なので実害は無いと判断したが、機械チェックに引っかかるなら public フィールドへ落とすのが最小修正。
