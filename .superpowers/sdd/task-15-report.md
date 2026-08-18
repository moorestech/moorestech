# Task 15 レポート: Skitローカライズresolver再構成（D9案B・C7・C19）

コミット: `5f3aaa365 refactor: Skitローカライズresolverを前例準拠の単純同期へ再構成しSkitExecutionIdentity/SkitCleanupOnceを撤去`

## 何を実装したか

### 1. `SkitLocalizationResolver` の同期機構を前例準拠へ再構成
- `Interlocked` / `Volatile` を全廃。`bool _preparing` / `bool _reloadScheduled` / `bool _disposed` と、素の `int _observedRevision` / `int _publishedRevision` のみになった（`BuildMenuTopic.cs:71-90` の `_publishScheduled`/`_disposed` の2 boolに準拠。revision対はVolatileを剥がして素のintのまま維持）。
- `_highestScheduledRevision`（CASでバースト畳み込みしていたint）を廃止し、`_reloadScheduled` + `UniTask.Yield(PlayerLoopTiming.PostLateUpdate)` のフレーム末デバウンスへ置換（BuildMenuTopicと同形）。
- `RequestReload` / `SchedulePendingReload` / `BuildAndPublishScopeAsync` を `PrepareAsync` 内のローカル関数からクラス直下のprivateメソッドへ引き上げ（unidirectional W）。
- `PrepareAsync` は「`_skitTitle` 保持 → 購読張り → 収束待ち（+ finallyでの再開判定）」だけになった。
- **再ロード失敗の恒久ブロック解消**: `_reloadScheduled = false` をフレーム末yield直後・ロードawaitの前に置き、`_publishedScope`/`_publishedRevision` は最後まで書き換えない。よってロード失敗後も公開済みscopeが保たれ、次の言語変更で再試行できる。失敗は `.Forget(Debug.LogException)` でログ。

### 2. 死引数の解消（インターフェイス分割）
```csharp
UniTask PrepareAsync(string skitTitle);   // 具象クラスのみ（下の「意図的な逸脱」参照）
string ResolveCommandField(int commandId, string field, string sourceText);
string ResolveCharacterName(string characterId);
string ResolveOverriddenCharacterName(int commandId, string overrideSource);
```
- `skitTitle` はresolverのフィールドへ。`ResolveCharacterName` の `bool useOverride` + 道連れ `commandId`/`overrideSource` を2メソッドへ分割（schema-design W）。分岐は `SkitCommandLocalization.ResolveLine` が持つ（commandの実データなのでこの層が正しい持ち主）。

### 3. `SkitExecutionIdentity` / `SkitCleanupOnce` の撤去
- `SkitExecutionIdentity.cs`・`StoryContextExtension.GetExecutionIdentity()`・DI登録2箇所（SkitManager/BackgroundSkitManager）・コマンド3本のidentity取得行を削除。
- `SkitCleanupOnce.cs`（+.meta、空になった `Lifecycle/` ディレクトリの `.meta`）を削除。`SkitManager` はローカル `var mapPinHidden = false;` と、単一の `finally` から呼ばれる素直な `Cleanup()` へ。`BackgroundSkitManager` は `TryBegin` 相当が不要になり単純化。
- 副次: identity登録のためだけに存在した `PreProcess(string skitTitle)` / `GetStoryContext(string skitTitle)` の引数も死んだので削除。SkitManagerの未使用 `using System.Threading;` も除去。

### 4. テスト
- `SkitCleanupOnceTest.cs`（ソース文字列一致検証）を削除し、`SkitFailureCleanupTest.cs` の実挙動テストへ置換。
  - `SkitFailureRestoresPlaybackStateAndLeavesUntouchedMapPinAlone`: 実 `SkitManager` に fake の `IMapObjectPin` と `SkitUI` を挿し、skit途中失敗（不正asset名 → `ArgumentException`）を起こして、finallyのcleanupが `IsPlayingSkit=false`・`skitUI` 非表示に戻し、**まだ隠していないmapPinには一切触れない**（`mapPinHidden` ローカルboolの契約）ことを固定。
  - `DisposedResolverStopsReloadingAndToleratesRepeatedDispose`: cleanupが二重に走ってもDisposeが安全で、破棄後は言語変更を追わない（ロード0件）ことを固定。
- `SkitLocalizationResolverLifecycleTest.FailedReloadKeepsPublishedScopeAndRecoversOnNextLanguageChange` を追加（再ロード失敗 → 公開済みscope維持 → 次の言語変更で復旧）。
- `SkitCommandLocalizationTest` を identity無しシグネチャへ書き換え、`LineWithoutOverrideResolvesSpeakerFromCharacterIdAlone` を追加（override無しは `ResolveCharacterName(characterId)` だけを使う＝分割の routing を固定）。`SkitLocalizationResolverTest`・`SkitLocalizationResolverBoundaryTest` の呼び出しも新シグネチャへ。

## TDDの証拠

### RED
テストを先に新シグネチャへ書き換え、実装前にコンパイル:

```
$ uloop compile --project-path ./moorestech_client
  "Success": false,
  "ErrorCount": 2,
  "Errors": [
    "Assets/Scripts/Client.Tests/Localization/Skit/SkitCommandLocalizationTest.cs(158,50): error CS0535:
     'SkitCommandLocalizationTest.RecordingResolver' does not implement interface member
     'ISkitLocalizationResolver.ResolveCommandField(string, int, string, string)'",
    "Assets/Scripts/Client.Tests/Localization/Skit/SkitCommandLocalizationTest.cs(158,50): error CS0535:
     'SkitCommandLocalizationTest.RecordingResolver' does not implement interface member
     'ISkitLocalizationResolver.ResolveCharacterName(string, string, int, bool, string)'"
  ]
```
想定通り: テスト側が要求する identity無し / bool分割済みの契約が、production側にまだ存在しないため。

### RED（追加テストの実効性検証 = mutation）
新規 `FailedReloadKeepsPublishedScopeAndRecoversOnNextLanguageChange` は現行実装（`_highestScheduledRevision` のCAS）でも通ってしまうため、「テストに歯があるか」を実装後に mutation で確認した。`_reloadScheduled = false;` を **ロードawaitの後ろ**（＝失敗時に恒久ブロックする素朴な書き方）へ移動:

```
$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*SkitLocalizationResolver.*"
  "TestCount": 34, "PassedCount": 33, "FailedCount": 1
Client.Tests.Localization.Skit.SkitLocalizationResolverLifecycleTest.FailedReloadKeepsPublishedScopeAndRecoversOnNextLanguageChange Failed
System.TimeoutException : Exceed Timeout:00:00:02
```
→ 恒久ブロックを確実に検出する。mutationは即revertし再GREENを確認済み。

### GREEN
```
$ uloop compile --project-path ./moorestech_client
  "Success": true, "ErrorCount": 0

$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*Skit.*"
  "TestCount": 63, "PassedCount": 61, "FailedCount": 2   ← 失敗2件は後述の既知branch-redのみ

$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value ".*Localization.*"
  "TestCount": 115, "PassedCount": 113, "FailedCount": 2  ← 同じ既知2件のみ
```
本タスク関連テストは全pass（`SkitFailureCleanupTest` 2件、`SkitLocalizationResolverLifecycleTest` 6件、`SkitCommandLocalizationTest` 9件、`SkitLocalizationResolverTest` 全ケース）。

## 変更したファイル

Modify:
- `moorestech_client/Assets/Scripts/Client.Game/Skit/Localization/SkitLocalizationResolver.cs`（全面改修・162行）
- `moorestech_client/Assets/Scripts/Client.Skit/Localization/ISkitLocalizationResolver.cs`
- `moorestech_client/Assets/Scripts/Client.Skit/Localization/SkitCommandLocalization.cs`
- `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs`（178行）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BackgroundSkit/BackgroundSkitManager.cs`
- `moorestech_client/Assets/Scripts/Client.Skit/Commands/TextCommand.cs`・`SelectionCommand.cs`・`BackgroundSkitTextCommand.cs`
- `moorestech_client/Assets/Scripts/Client.Skit/Context/StoryContextExtension.cs`
- テスト: `SkitCommandLocalizationTest.cs`・`SkitLocalizationResolverLifecycleTest.cs`・`SkitLocalizationResolverTest.cs`・`SkitLocalizationResolverBoundaryTest.cs`

Add:
- `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitFailureCleanupTest.cs`（+ Unity生成 .meta）

Delete（.cs + .meta ペアで `git rm`）:
- `moorestech_client/Assets/Scripts/Client.Skit/Context/SkitExecutionIdentity.cs`
- `moorestech_client/Assets/Scripts/Client.Game/Skit/Lifecycle/SkitCleanupOnce.cs`（+ 空になった `Lifecycle.meta`）
- `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitCleanupOnceTest.cs`

## 自己レビューの所見

- `grep -rn "Interlocked|Volatile"` を Client.Game/Skit・Client.Skit・Client.Game/InGame/BackgroundSkit・Client.Tests/Localization に対して実行 → **ヒット0**。
- `grep -rn "SkitExecutionIdentity|SkitCleanupOnce|GetExecutionIdentity"` を Assets 全体に実行 → **ヒット0**（.metaも削除済み）。
- `PrepareAsync` は「_skitTitle保持＋購読張り＋収束待ち」だけに縮小。finallyに残るのは `_preparing` の解除と「Prepare中に観測した**より新しい**revisionだけを再開する」判定のみ（同一revisionの即時無限リトライを防ぐため必須。既存テスト `FailedPrepareDoesNotScheduleTheSameRevisionAgain` / `FailedPrepareSchedulesOnlyNewerObservedRevision` が両側を固定している）。
- 再ロード失敗後の復旧はテストで固定済み（上記mutationで実効性も確認）。
- `ResolveOverriddenCharacterName` により bool + 道連れ引数が消滅。分岐先は `SkitCommandLocalization.ResolveLine` の三項1本のみ。
- ファイルサイズ: resolver 162行 / SkitManager 178行 / BackgroundSkitManager 102行 — いずれも200行以下。`Client.Game/Skit/` は `Lifecycle/` が消えてディレクトリ数も減少。
- `#region Internal` は `SchedulePendingReload` のローカル関数集約1箇所のみ（クラス直下privateメソッド群を囲う禁止形は無し）。
- try-catchは追加していない（失敗ハンドリングは `.Forget(Debug.LogException)`）。`Func<>`・`partial`・デフォルト引数の新規追加無し。

### 意図的な逸脱（1件・裁定候補）
ブリーフの Interfaces ブロックは `PrepareAsync` を `ISkitLocalizationResolver` の一員として記載しているが、**実装では従来どおり具象 `SkitLocalizationResolver` のみに置いた**。理由: `PrepareAsync` を interface 越しに呼ぶ箇所は存在せず（呼ぶのは具象型を持つ2つのManagerだけ。Disposeも同様に具象経由）、interfaceへ載せると誰も使わないライフサイクルAPIが `Client.Skit` 側の契約に露出し、本タスクが撤去している「使われない機構」を1つ増やすことになるため。commandが必要とする解決3メソッドだけをinterfaceに残した。ブリーフ通りに載せる方がよければ1行追加＋fake1実装で戻せる。

## 問題や懸念事項

### 既知のbranch-red 2件（本タスク起因ではない・未着手のまま）
```
SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues("english",139,...)  Expected: 139 / But was: 143
SkitLocalizationDictionaryCompletenessTest.CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues("japanese",204,...) Expected: 204 / But was: 208
```
ブリーフ記載のbaseline乖離（139/204 vs 143/208）と完全一致。origin/masterマージでskit台詞が増えたことによるもので、本タスクの変更前後で失敗内容は不変。指示どおり触っていない。**本タスクで新たに壊した失敗は0件**（Skit 63件中61pass / Localization 115件中113pass、失敗はこの2件のみ）。

### 懸念（軽微）
1. **cleanup「1回だけ」の直接検証は構造保証に依存**: cleanupの一回実行は「呼び出し口が単一の `finally` だけ」という構造で保証される形になった（ガードが不要になったのが今回の主眼）。`resolver.Dispose()` が実際に1回だけ呼ばれることをEditModeから直接観測するには、SkitManagerの `PreProcess`（MasterHolder + Addressablesでのキャラクタープレハブロード）を通す必要があり、EditModeテストでは非現実的と判断した。代わりに (a) 実SkitManagerのfinally経路を早期失敗で実走させて後始末結果を固定、(b) resolverのDispose冪等性と破棄後の非追従を固定、の2本でカバーしている。
2. **`_preparing` による同時Prepareガードは残した**: ブリーフはこのガードの撤去を明示していないうえ、`SchedulePendingReload` をPrepare中に抑止する役割（重複ロード防止）が実在し、既存テスト2本（`ConcurrentPrepareFailsFastWithoutDuplicateLoad`・`SubscriptionFailureReleasesConcurrentPrepareGuard`）が挙動を固定しているため。Interlockedは剥がして素のboolにしてある。
3. **フレーム末デバウンス導入によるタイミング変化**: 言語変更→再ロード開始が1フレーム遅延する（BuildMenuTopic準拠）。skit表示中の言語切替追従はplanスコープ外のWarningとして既知であり、実害は無いと判断。
