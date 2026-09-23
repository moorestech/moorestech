# Task 2 report: EditMode の専用一時 Stage に生成マップを表示する

Status: DONE

Worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview`

Base: `b87350bb7`（Task 1 完了）

## 実装

- `GeneratedMapPreviewWindow` に `moorestech/Generated Map Preview` メニュー、生成・再生成、スポーン移動、全地形表示、Close を追加した。Window を開くだけでは Stage も生成物も作らない。現在の同型 Stage を再利用する。
- Prefab 編集中の生成は「Prefab編集を閉じてから生成してください」を Window と Console に示して拒否する。新規 Stage は MainStage から開き、Stage 遷移失敗も表示・ログする。
- `GeneratedMapPreviewStage` が PreviewSceneStage の開始・終了と CancellationTokenSource を所有する。Play 移行、assembly reload、editor quit は GoToMainStage 経由の終了へ集約した。
- Stage の状態判定は `GeneratedMapPreviewState` に集約した。再生成は以前の run を Dispose して新しい run を作る。正常完了後だけ件数を評価し、欠損 0 で Ready、欠損ありで Failed と件数を表示して部分生成物を破棄する。
- `GenerateAsync` の finally は Generating の場合だけ失敗へ遷移させる。閉じた Stage の Closing/Closed を変更しない。最外側の Forget ハンドラが通常例外を Console と StatusText に伝え、閉じる操作によるキャンセルは通常終了として扱う。
- `GeneratedMapPreviewRun` は一度だけ実行でき、world と content を所有する。生成の前に EditMode の Yield を挟み、全 await 後に cancellation を確認する。MapObjects は 50 件ごとに Yield する。
- `GeneratedMapPreviewWorld` は既存 MapAuthoringImporter と同じ順でマスタを読み、DefaultGeneratedWorldProvisioner と WorldTerrainSession.Open を使用する。root は `Temp/GeneratedMapPreview/<Editor PID>/<Guid>` とし、EnsureWorld より前に作成しない。
- World の作成失敗時も Dispose 時も、この run の root と `ProvisioningTempDirectory` だけを削除する。共有 world cache、snapshot、セーブ領域、Library は削除しない。ディスク／JSON 境界の失敗は理由と残った専用領域をログして伝播する。
- 起動・domain reload 時には専用 Temp 領域の PID ディレクトリを検査し、生存 PID を保護しながら終了済みプロセスの領域だけを回収する。
- `GeneratedMapPreviewContent` は root を preview Scene に移してから子を作れる状態にする。所有対象は root と自身が new した TerrainData のみで、Root → TerrainData の順に DestroyImmediate する。二重 Dispose は無害で、Dispose 後の追加確保は例外にする。
- `GeneratedMapPreviewTerrain` は全 TileCoordinates を列挙し、既存の layer/detail/material loader、AssembleIntoAsync、TerrainObjectFactory、TerrainNeighborLinker を使用する。地形変換、原点、材質、detail 設定は既存ファサードの値をそのまま使う。
- MapObjects は生成済み map.json の配置値をそのまま MapObjectPrefabPlacement へ渡す。GUID ごとに Prefab と解決失敗をキャッシュし、期待数と実際の Instantiate 成功数を記録する。初期化 snapshot、登録簿、カリング、採掘、通信は起動しない。
- SpawnPointObject を正本のスポーン位置に置き、専用 Scene 内の Directional Light で照明を用意する。FrameAll は全 Terrain の Bounds を合成する。SceneView の lighting、pivot、rotation、size、orthographic と active Scene を終了時に復元する。生成中に SceneView が閉じられても、フレーミング時に表示先を用意する。
- Editor asmdef に実装で使用する既存 assembly への参照を追加した。Task 1 の lazy duplicate address rejection は変更していない。

## 検証

最終コードで **36/36 passing、0 failed、0 skipped**。最終 Unity compile は **Success=true、ErrorCount=0、WarningCount=0**。

| 対象 | 結果 |
| --- | --- |
| GeneratedMapPreviewLifecycleTest 単独 | 4/4 pass（2026-09-21T16:20:20Z） |
| MapPreview + Terrain の関連全クラス | 36/36 pass（2026-09-21T16:22:07Z）。新規 Lifecycle 4 件を含む |
| 最終 Unity compile | success、0 errors、0 warnings |
| C# ファイル数・行数 | Preview 7 files、最大 188 行。全変更 C# は 200 行以内 |
| 静的検査 | partial / Func なし、source の末尾空白なし、asmdef JSON parse 成功 |

LifecycleTest の実測対象:

- preview Scene に root と Terrain が配置される。
- 二重 Dispose で root と生成 TerrainData が破棄される。
- 借用 Material と TerrainLayer、無関係な主 Scene の GameObject が保持される。
- 同じ Scene に Content を再作成しても、古い所有者の Dispose は新しい root/data を破壊しない。
- 保存済み・dirty の両方の主 Scene について、Stage 開閉後に dirty 状態と active Scene が保持される。
- Stage の preview Scene が閉じ、SceneView の lighting、位置、向き、size、orthographic が復元される。
- Window を開くだけでは Stage、preview Scene、生成 GameObject が増えない。

実行コマンド（project-path はすべて担当 worktree の絶対パス）:

```text
uloop compile --project-path <worktree>/moorestech_client
uloop run-tests --project-path <worktree>/moorestech_client --filter-type class --filter-value GeneratedMapPreviewLifecycleTest --unsaved-changes fail
uloop run-tests --project-path <worktree>/moorestech_client --filter-type regex --filter-value '^Client\.Tests\.UnitTest\.(MapPreview|Terrain)\.' --unsaved-changes fail
```

初回の失敗と修正:

1. 初回 compile は ServerConst の using 不足と LookAtDirect の存在しない overload で 2 errors。Client.Common を参照し、5 引数の LookAt（instant=true）へ修正した。
2. 次の compile は test namespace の Terrain と UnityEngine.Terrain の衝突で 2 errors。テストの型を完全修飾した。
3. 初回 LifecycleTest は 4 件とも SetUp で失敗した。Unity Test Runner が作る未保存 Untitled bootstrap Scene に対して additive Scene を開けないことが原因だった。
4. PackageCache の CreateBootstrapSceneTask と TaskList を確認し、fixture は専用 Single Scene を作って終了時に bootstrap と同じ DefaultGameObjects Scene を戻す形へ変更した。ユーザーの元 Scene は Test Runner 自身の Store/RestoreSceneSetup が保護する。修正後は 4/4 pass、最終関連スイートも 36/36 pass。
5. 意図的な実装前 RED は実測していない。上記は実装後の compile / fixture failures と修正の実測である。

生の証拠（worktree 内のローカル資料）:

- `.superpowers/sdd/generated-map-scene-preview/task-2-evidence/final-compile.json`
- `.superpowers/sdd/generated-map-scene-preview/task-2-evidence/lifecycle-tests.json`（初回失敗）
- `.superpowers/sdd/generated-map-scene-preview/task-2-evidence/lifecycle-tests-retry.json`
- `.superpowers/sdd/generated-map-scene-preview/task-2-evidence/affected-suite-tests.json`
- 初回失敗 XML: `moorestech_client/.uloop/outputs/TestResults/20260921_161757_9143810_2d1d50eb21814365ba77880cb5fecf4d.xml`

## 変更ファイル

- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewWindow.cs`
- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewStage.cs`
- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewRun.cs`
- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewWorld.cs`
- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewTerrain.cs`
- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewContent.cs`
- 新規 `Editor/MapScene/Preview/GeneratedMapPreviewState.cs`
- 変更 `Editor/MapScene/Client.MapScene.Editor.asmdef`
- 新規 `Client.Tests/UnitTest/MapPreview/GeneratedMapPreviewLifecycleTest.cs`
- 新規 source/folder に対して Unity が自動生成した `.meta`（手作成・手編集なし）。
- 本 report。Task 3–5、plan checkbox、PR は変更・作成していない。

## 自己レビュー・懸念

- lens digest と Task 2 brief を照合した。生成内部の再実装、配置座標の再計算、ゲーム初期化への侵入を避け、既存のファサード・asset resolver・Prefab 配置を使用した。
- セーブ／永続化の再確認を実施した。保存 schema、ID/GUID 表現、アイテム・液体・ブロックの保存値は変更していない。既存生成器が出力する JSON と transfer metadata の読み取りのみで、save migration は不要。
- Cancellation は Stage → Run → Terrain / asset loader / assembler / Yield まで伝播する。OnCloseStage は Cancel → run.Dispose → base.OnCloseStage の順で閉じる。TerrainData の所有者を Content に固定している。
- Unity 6000.3 の [StageNavigationManager](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/SceneManagement/StageManager/StageNavigationManager.cs) と [PreviewSceneStage](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/SceneManagement/StageManager/PreviewSceneStage.cs) を確認し、MainStage への同期後に旧 Stage の OnCloseStage が呼ばれる順序と、base の Scene cleanup を確認した。
- `agent-runtime-compat/SKILL.md` は main/worktree/global skills に見つからなかった。controller に報告済みで、Task 1 と同様に該当 uloop skill と CLI を直接使用した。
- 生成待ち中の Close、assembly reload、Play 移行、全マップ実表示・欠損・再生成・実データの一時領域回収は、ブリーフで指定された Task 3 の実機確認へ引き継ぐ。Task 2 の必須実装と対象テストは完了している。
- Beads `moorestech-uvrb.1` は全実装の項目であるため close していない。完了判断は controller に委ねる。

## 独立レビュー修正: 生成待ち中のClose（2026-09-22）

Status: DONE

実装コミット: `b3cbe00d6` — `Task 2: retain pending preview resources until generation ends`

レビューの Critical 1 件を修正した。上記の旧記述「OnCloseStage は Cancel → run.Dispose → base.OnCloseStage」は、この節の終了順序で置き換わる。Important 所見はなかった。Tasks 3–5、plan checkbox、PR は変更していない。

### 修正内容

- Stage は `ExecuteAsync` が返す実行タスクを監視し、その終端 finally まで `_executionPending` を保持する。この値は Closed 後も残り得る非同期処理の寿命を表し、画面状態の enum と役割を分けている。
- OnCloseStage は Closing → Cancel と進める。キャンセルの継続が同期で終了した場合は終端 finally が run と CancellationTokenSource を解放する。まだ Pending なら Content の root を Content 所有の別 preview Scene へ移し、run/world/content/token を保持する。
- 元の Stage Scene はその場で `base.OnCloseStage()` により閉じる。SceneView と active Scene も同期で復元し、State/StatusText を Closed にする。後続の実行完了・例外は Closed を上書きせず、finally で root → 自前 TerrainData → 退避 Scene → 専用 world/provisioning ディレクトリを回収する。
- Scene 退避だけでは十分でないことをテストで発見した。Stage 切り替え後に root は生存したが、まだ Terrain に接続されていない TerrainData が破棄された。Unity の StageNavigationManager は切り替え後に未使用アセット回収を行うため、Content が new する TerrainData のみ `HideFlags.DontUnloadUnusedAsset` を設定した。最終破棄は既存の明示的な DestroyImmediate のままとし、借用アセットの flags/所有権は変えていない。
- Run の最初の Yield と50件ごとの配置 Yield、および共有 `TerrainAlphamapApplier` の平面ごとの Yield を `UniTask.Yield(PlayerLoopTiming.Update, cancellationToken, true)` に統一した。現在の EditorTerrainAssetLoader は同期の AssetDatabase ロードなので、既存の非同期中断箇所はこの3種の Yield でキャンセルを同期伝播できる。reload/Play/quit の既存 hooks は同じ Close → terminal finally の経路を使い、メインスレッドをブロックしない。
- `TerrainAlphamapApplier` は Task 1 で導入した共有ファイルだが、controller に範囲拡張を確認した上で、同一のキャンセル寿命不変条件に必要な Yield の変更だけを行った。
- テスト用の public API・interface・delegate は追加していない。新しいテストは私有の実行監視へ制御可能な UniTask を reflection で渡し、実物の root・TerrainData・world/provisioning ディレクトリと StageUtility の同期クローズを使う。

判断根拠:

- [Unity 6000.3 StageNavigationManager](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/SceneManagement/StageManager/StageNavigationManager.cs) は Stage を DestroyImmediate した直後に元の preview Scene のリークを検査し、その後に未使用アセットを回収する。base の Scene close を非同期に遅らせる案は使っていない。
- [Unity 6000.3 PreviewSceneStage](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/SceneManagement/StageManager/PreviewSceneStage.cs) の OnCloseStage は同期で Scene を閉じる。
- ローカル `Library/PackageCache/com.cysharp.unitask@86b6e6a2e286/Runtime/PlayerLoopHelper.cs:337` の Editor update は、Play 移行中・compile中・update中には継続を進めない。このため「キャンセル後に次の Editor update が来る」という前提を除いた。

### 回帰検証と正確な結果

修正前に RED を実測した。新しい private observer 引数とテストだけを導入し、終了処理と Yield は旧実装のままで次を実行した。

```text
uloop run-tests --project-path /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview/moorestech_client --filter-type regex --filter-value 'GeneratedMapPreviewPendingCloseTest|TerrainAlphamapApplierTest.Cancellation' --unsaved-changes fail
```

2026-09-21T16:36:36.1216580Z: `Success=false, TestCount=5, PassedCount=0, FailedCount=5, SkippedCount=0`。失敗箇所は次のとおり。

- pending 終了後に成功する場合／例外になる場合の2件: `Close must retain native objects while the execution is pending. Expected: True / But was: False`。
- Run の最初の Yield: `Reload and Play transitions may prevent another editor update. Expected: True / But was: False`。
- alphamap の最初／最後の平面の2件: `Expected: Canceled / But was: Pending`。

Scene 退避と即時キャンセルだけを入れた最初の修正確認は `9 total, 7 passed, 2 failed`。root/data を別々に判定する診断再実行は `3 total, 1 passed, 2 failed` で、2件とも `Close must retain native TerrainData while the execution is pending` に失敗した。root の保持は成功していた。TerrainData の自動回収対策後に以下を実行した。

```text
uloop run-tests --project-path /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview/moorestech_client --filter-type regex --filter-value 'GeneratedMapPreview.*Test|TerrainAlphamapApplierTest.Cancellation' --unsaved-changes fail
```

2026-09-21T16:39:56.4559140Z: `Success=true, TestCount=9, PassedCount=9, FailedCount=0, SkippedCount=0`。

その後、Editor の設定に依存せず自動回収への耐性を検証するため、pending-close テストに明示的な `EditorUtility.UnloadUnusedAssetsImmediate()` を追加した。最終コードで関連全クラスを一度実行した。

```text
uloop run-tests --project-path /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview/moorestech_client --filter-type regex --filter-value '^Client\.Tests\.UnitTest\.(MapPreview|Terrain)\.' --unsaved-changes fail
```

2026-09-21T16:40:55.7277740Z の出力:

```json
{"Success":true,"Status":"Passed","TestCount":39,"PassedCount":39,"FailedCount":0,"SkippedCount":0,"HasFailures":false,"NoTestsFound":false,"XmlPath":null}
```

続けて最終コンパイルを実行した。

```text
uloop compile --project-path /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview/moorestech_client
```

```json
{"Success":true,"ErrorCount":0,"WarningCount":0,"Errors":[],"Warnings":[],"ErrorCode":null}
```

回帰テストが実測した内容:

- pending のまま Close すると元 Stage Scene は同期で閉じ、別の一時 preview Scene が1つだけ root を保つ。
- キャンセルをまだ観測していない継続が、Close と未使用アセット回収の後でも root の名前、TerrainData の解像度、world ディレクトリへアクセスできる。
- 継続が成功しても token 確認でキャンセルとなり、継続が例外を投げた場合も資源を回収する。両方で Closed とクローズ表示を保持する。
- 終端後は root・TerrainData・退避 Scene・world/provisioning ディレクトリがすべて消え、二重 Dispose でも借用 layer、主 Scene の active/dirty 状態と preview Scene 数を保つ。
- Run の最初の Yield、alphamap の最初／最後の平面のキャンセルは、次の Editor update を待たず終端となる。50件ごとの配置 Yield は同じ呼び出しへ変更したが、その実配置経路の再実測はこの単体検証には含めていない。

ローカル証拠ディレクトリ: `.superpowers/sdd/generated-map-scene-preview/task-2-evidence/`

- `fix-red-tests-retry.json`
- `fix-green-tests.json`（Scene 退避だけでは TerrainData が失われた失敗）
- `fix-retention-diagnostic-tests.json`
- `fix-green-tests-retry.json`
- `fix-affected-suite-tests.json`
- `fix-final-compile.json`
- `fix-editor-relaunch.json`

RED の失敗 XML: `moorestech_client/.uloop/outputs/TestResults/20260921_163636_1226220_3e5b6deada154b8cb18e40d877ba8a15.xml`。

検証環境の中断: 最初の RED 呼び出しでは compile 待機中に担当 Editor PID 11842 が終了し、テスト結果を取得できなかった。終了原因は特定していない。`moores-wt status` とプロセス照合で Editor が0本であることを確認し、controller の指示どおりこの worktree のみ `uloop launch` で起動した（PID 7594、Ready=true）。uloop が stale UnityLockfile と Temp を自動回収した。Library は削除していない。以後の RED/GREEN/関連全体/compile は上記の実測結果であり、中断した実行を合格扱いしていない。

### 変更ファイル・自己レビュー・残る検証範囲

- `Editor/MapScene/Preview/GeneratedMapPreviewStage.cs`: 実行監視と終端破棄、同期 Stage close。
- `Editor/MapScene/Preview/GeneratedMapPreviewContent.cs`: 退避 Scene の所有、未使用 TerrainData の保護と明示破棄。
- `Editor/MapScene/Preview/GeneratedMapPreviewRun.cs`: Content 退避の内部経路、即時キャンセル可能な2か所の Yield。
- `Client.Game/InGame/Environment/Terrain/Build/TerrainAlphamapApplier.cs`: token と即時キャンセルを共有の平面 Yield へ伝播。
- `Client.Tests/UnitTest/MapPreview/GeneratedMapPreviewPendingCloseTest.cs`: 制御された pending close 2件、初回 Yield の同期キャンセル1件。
- `Client.Tests/UnitTest/Terrain/TerrainAlphamapApplierTest.cs`: 既存2件へ、キャンセル直後の終端状態の検証を追加。
- 新規テストの `.meta` は Unity が自動生成した。手作成・手編集はしていない。
- この report の追記。

自己レビューでは所有者以外の破棄・Scene の遅延 close・Closed の上書き・取消済み CTS の早期解放を確認した。`git diff --check` は空、変更した C# はすべて200行以下（最大 Stage 197行）、新規コードファイル数は MapPreview ディレクトリ4件。新しい `Func<>`、partial、デフォルト引数、毎tickポーリング、テスト専用 public API はない。

永続化の再確認: 変更は preview の native object と一時ディレクトリの寿命に限る。保存 JSON、Item/Fluid/Block のID/GUID表現、マスタ値の保存、save version は変更しておらず、save migration は不要。

本修正範囲の未解決所見はない。実マップ全件生成中の Close・assembly reload・Play 移行・quit の通し検証は Task 3 の担当範囲として残る。本単体検証が実際の domain reload を跨いだとは主張しない。Beads `moorestech-uvrb.1` は親の全実装項目なので close していない。
