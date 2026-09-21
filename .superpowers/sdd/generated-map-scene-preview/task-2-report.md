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
