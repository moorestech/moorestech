# Task 2 report: サーバー CompleteResearchChallengeTask

## 何を実装したか

ブリーフのStep 1〜7を実装。研究完了イベント（`Game.Research.ResearchEvent.OnResearchCompleted`）とチャレンジ開始前に既に完了済みの研究（`IResearchDataStore.IsResearchCompleted`）の両方を検知して、`completeResearch`タスクタイプのチャレンジを完了させる`CompleteResearchChallengeTask`を追加した。あわせてChallengeMasterUtilにcompleteResearchのResearchNodeGuid実在チェックとuiDragGuideのbuildMenuBlock:{guid}実在チェック（計画Requirement #9）を追加した。

### 変更ファイル

- 新規: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/CompleteResearchChallengeTask.cs`
  - `IChallengeTask`実装。コンストラクタで`ResearchEvent.OnResearchCompleted`を購読し、対象研究の完了で即完了通知。`ManualUpdate`で初回tickのみ`IResearchDataStore.IsResearchCompleted`を照会し、チャレンジ開始前に完了済みの研究を回収する（コンストラクタで即時OnNextすると`ChallengeDatastore.CreateChallenge`の購読前に発火してしまうため）。
- 変更: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs`
  - `CompleteResearchTask = "completeResearch"`定数を追加。
- 変更: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs`
  - `_taskCreators`に`CompleteResearchTask -> CompleteResearchChallengeTask.Create`を登録。
- 変更: `moorestech_server/Assets/Scripts/Game.Challenge/Game.Challenge.asmdef`
  - `references`に`Game.Research`を追加（Game.Researchは循環しないことを確認済み）。
- 変更: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs`
  - `TaskParamValidation`に`CompleteResearchTaskParam`ケースを追加。`MasterHolder.ResearchMaster.ResearchElements`の実在チェック。
  - `TutorialValidation`に`UiDragGuideTutorialParam`ケースを追加。`FromUIObjectId`/`ToUIObjectId`それぞれについて、`buildMenuBlock:`プレフィックスを持つ場合のみGUIDをパースしBlockMasterで実在確認するローカル関数`ValidateDragGuideObjectId`を追加。
- 変更: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json`
  - Category1にブリーフ指定の`completeResearch`チャレンジ（GUID `...101`、研究1完了で達成、`prevChallengeGuids: []`＝初期チャレンジ）を追加。
- 新規: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/CompleteResearchChallengeTaskTest.cs`
  - ブリーフのサンプルテストをそのまま採用（型名等の齟齬なし）。

### ブリーフに無かった追加変更（実装中に発覚した必須修正）

1. **`moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`（brief未記載・修正必須）**
   - `MasterHolder.Load`は`ChallengeMaster`を`ResearchMaster`より先にロード/バリデーションしていた。今回`ChallengeMasterUtil`のTaskParamValidationが`MasterHolder.ResearchMaster.ResearchElements`を参照するようになったため、この順序だと`ResearchMaster`静的プロパティがまだ`null`でNREになる（実測: `System.NullReferenceException` at `ChallengeMasterUtil.cs:78`、EditModeテストで再現）。
   - `ResearchMaster`のロード順を`ChallengeMaster`より前に移動して解決。`ResearchMasterUtil.Validate`はItemMaster/CraftRecipeMaster/MachineRecipesMaster/BlockMaster/TrainUnitMasterにのみ依存しており、これらは全て元の位置でも既にロード済みだったため、この並び替えで新たな依存関係違反は生じない。
   - コミットの`git add`対象パス一覧（ブリーフStep 7）に本ファイルが含まれていなかったため、`git add moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`を追加で実行しコミットに含めた。

2. **`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/ChallengeSaveLoadTest.cs`**
   - `_initialChallenge`（初期チャレンジGUIDリスト）に新チャレンジGUID`...101`を追加（3件→4件）。新チャレンジは`prevChallengeGuids: []`のため初期チャレンジとして起動し、既存の期待値がずれるとブリーフのStep 6注記どおり判明した。

3. **`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetChallengeInfoProtocolTest.cs`**
   - `GetCompletedChallengeTest`内の3箇所の期待件数を更新: 初期CurrentChallenges数 3→4、チャレンジ1クリア後のCurrentChallenges数 3→4。
   - `CategoryUnlockStartsFirstChallengeTest`内、チャレンジ2/3/4クリア後に残るCurrentChallengesの期待件数を1→2に修正（チャレンジ5に加え、未クリアの101も残るため）。コメントも実態に合わせて修正。

## テストとその結果（TDD証拠）

### RED（Step 3）

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CompleteResearchChallengeTaskTest"`

実装前（VanillaChallengeType/ChallengeFactory未変更、CompleteResearchChallengeTask未作成の状態でテストファイルとchallenges.jsonのみ追加した時点）で実行し、想定通り2/2 FAILED:

```
System.Collections.Generic.KeyNotFoundException : The given key 'completeResearch' was not present in the dictionary.
  at Game.Challenge.Task.Factory.ChallengeFactory.CreateChallengeTask (...)
  at Game.Challenge.ChallengeDatastore.CreateChallenge (...)
```

想定通りの失敗（ChallengeFactoryの辞書にcompleteResearchが未登録）であることを確認。

### 中間の失敗（MasterHolder順序問題の発見）

実装（Step 4）完了後、コンパイルは成功したがテスト実行で別のRED（NRE）が発生:

```
System.NullReferenceException : Object reference not set to an instance of an object
  at Core.Master.Validator.ChallengeMasterUtil.<Validate>g__TaskParamValidation|0_1 (...) ChallengeMasterUtil.cs:78
```

`MasterHolder.ResearchMaster`が未初期化（`ChallengeMaster`のバリデーションが`ResearchMaster`より先に走る）ことが原因と特定し、`MasterHolder.cs`のロード順を修正。

### GREEN（Step 5〜6）

- `uloop compile --project-path ./moorestech_client` → `Success: true, ErrorCount: 0`
- `uloop run-tests ... --filter-value "CompleteResearchChallengeTaskTest"` → `TestCount: 2, PassedCount: 2, FailedCount: 0`
- `uloop run-tests ... --filter-value "Challenge"`（回帰） → `TestCount: 17, PassedCount: 17, FailedCount: 0`（ChallengeSaveLoadTest×3、GetChallengeInfoProtocolTest×2 の期待値修正込みで全件PASS）
- 追加の回帰確認（MasterHolderロード順変更の影響範囲確認のため自主的に実行）:
  - `--filter-value "Research"` → `TestCount: 21, PassedCount: 21`
  - `--filter-value "Master"` → `TestCount: 61, PassedCount: 61`
  - `--filter-value "SaveLoad"` → `TestCount: 88, PassedCount: 88`

すべてPASS、テスト出力にノイズ（余計な警告・エラー）なし。

## 自己レビューの所見

- **`ChallengeMasterUtil.cs`は元々402行（AGENTS.mdの200行/ファイル上限を大幅超過）で、今回の変更で433行に拡大した。** ブリーフはこの既存ファイルへの追記を明示的に指示しており（`Modify: ChallengeMasterUtil.cs:45-74`）、分割の指示は無い。契約に従い「計画の指示なく自分の判断でファイルを分割しない」を遵守し、追記のみに留めた。ただし200行ルールへの違反は今回のタスクで悪化させているため、最終ブランチレビューまたは人間判断でのファイル分割検討を推奨する（懸念として報告）。
- `MasterHolder.cs`のロード順変更はブリーフの想定ファイル一覧に無かったが、`ChallengeMasterUtil`のResearchNodeGuid検証を機能させるために技術的に必須だった（NREで実証済み）。依存関係コメントも合わせて更新し、既存の「ロード順は依存関係に基づいて決定」という設計方針に従った。
- テストは実際の挙動（研究完了イベント経由の完了、および既完了研究の初回tick回収）を検証しており、モックへの依存はない。

## 懸念事項

- `ChallengeMasterUtil.cs`が200行ルールに違反したまま拡大した点（上記自己レビュー参照）。分割はブリーフ範囲外のため実施していない。

## Fix報告

レビュー（`.superpowers/sdd/task-2-review.md`）の指摘に対応した。

### Important #1（Must Fix扱い）: マスタ検証2本のテスト追加

前例 `Tests/UnitTest/Core/Challenge/ChallengeMasterValidationTest.cs` の veinPin 異常系テスト（JObjectを直接壊してから`ChallengeMaster.Validate`を呼ぶ形式）と同型で、同ファイルに3テストを追加した。

- `completeResearchが存在しないresearchNodeGuidを参照すると失敗する()` — テストmod fixtureに既にある `completeResearch` チャレンジ（`challenges[5]`）の `taskParam.researchNodeGuid` を存在しないGUIDに差し替え、`Validate()`がfalseかつログに`"invalid TaskParam.ResearchNodeGuid"`を含むことを確認。
- `uiDragGuideが存在しないbuildMenuBlockを参照すると失敗する()` — テストmodにuiDragGuideのfixtureが無いため、`tutorials`配列に`fromUIObjectId: "buildMenuBlock:{存在しないGUID}"`を持つuiDragGuideチュートリアルを追加し、`Validate()`がfalseかつログに`"invalid uiDragGuide target"`を含むことを確認。
- `uiDragGuideが実在するbuildMenuBlockを参照すると成功する()` — レビュー指摘どおり正常系も追加し、実在ブロックGUID（`00000000-0000-0000-0000-000000000001`）を指すuiDragGuideで`Validate()`がtrueになることを確認。これにより`ValidateDragGuideObjectId`のtrueルートが初めて実行された。

### Minor対応

- **#3（対応）**: `ChallengeMasterUtil.cs`の`ValidateDragGuideObjectId`内`StartsWith`にカルチャ依存が無いよう`StringComparison.Ordinal`を明示。
- **#2（対応）**: `CompleteResearchChallengeTask.cs`のTaskParamキャストと`IResearchDataStore`解決を、前例`InInventoryItemChallengeTask`に合わせてコンストラクタでキャッシュする形に変更。マスタのtaskParam型不整合が生成時にfail-fastするようになった。`OnResearchCompleted`/`ManualUpdate`は毎回のキャストを行わずキャッシュ済みフィールドを参照する。
- **#4（見送り）**: uiObjectIdのうち`buildMenuBlock:`以外（静的アンカーIDや`researchNode:`書式）は本タスクのRequirement 9範囲外（buildMenuBlockのみ要求）。レビューも本タスクでの対応は不要と明記しており、Task 4/5で`researchNode:`書式を使う際に同型検証を検討する。
- **#5（見送り）**: `ChallengeMasterUtil.cs`は既にAGENTS.mdの200行/ファイル上限を超過していたファイルで、ブリーフが当該ファイルへの追記を明示指示しており分割指示は無い。契約（「計画の指示なく自分の判断でファイルを分割しない」）に従い本タスクでは追記のみとし、最終ブランチレビューでの分割検討対象として残す。
- **#6（見送り）**: `ResearchEvent.OnResearchCompleted.Subscribe`のIDisposable未破棄は、前例`BlockPlaceChallengeTask`と同型（レビューが「前例一致を優先した選択として許容」と明記）。既存の同型を含めて直すなら別タスクとレビューが述べており、本タスクでは対応しない。

### テスト再実行（レビュー対応後）

Run: `uloop compile --project-path ./moorestech_client`
→ `Success: true, ErrorCount: 0, WarningCount: 0`

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ChallengeMasterValidationTest|CompleteResearchChallengeTaskTest"`
→ `TestCount: 6, PassedCount: 6, FailedCount: 0`（ChallengeMasterValidationTest 1→4件、CompleteResearchChallengeTaskTest 2件）

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Challenge"`（回帰）
→ `TestCount: 20, PassedCount: 20, FailedCount: 0`（対応前の17件+新規3件）

すべてPASS、出力にノイズなし。

### 変更ファイル（このFixで触ったパスのみ）

- `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs`
- `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/CompleteResearchChallengeTask.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Challenge/ChallengeMasterValidationTest.cs`

### 自己レビュー所見

- Critical/Important指摘は全て解消。uiDragGuide分岐は正常系・異常系の両方が初めて実行されるようになった。
- Minor #2の変更でコンストラクタが`ServerContext.GetService<IResearchDataStore>()`を呼ぶようになったが、既存の`InInventoryItemChallengeTask`が同じタイミングで`IPlayerInventoryDataStore`を解決している前例と揃っており、DIコンテナ初期化順の問題は無い（テスト実行で確認済み）。
- 懸念事項なし。
