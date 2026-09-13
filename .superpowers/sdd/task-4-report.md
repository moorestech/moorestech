# Task 4 実装報告: `steamId` と `buildInfo` の埋め込み

## 何を実装したか

ADR 0059（`docs/adr/0059-build-info-json-single-reader.md`）の裁定どおり、`BuildInfoReader` は新設せず、
`build-info.json` を読む実装は `RepositoryStateProbe.ReadBuildInfo()` の1本に保った。

### 新規ファイル
- `Client.Game/InGame/BugReport/BuildInfo.cs` — `manifest.buildInfo` のDTO。`Commit`/`Branch`/`MasterDataCommit`/`Dirty`/`SteamBuildLabel`/`BuiltAt`/`Target` の7フィールド（ブリーフ・shared-contracts §1どおり）に加え、`MasterDataDirty` を1つ追加（下記「ブリーフ/ADRからの逸脱」参照）。
- `Client.Game/InGame/BugReport/BuildInfoJson.cs` — `build-info.json` のJSON文字列パース（`Parse`）と、既存消費側 `BugReportRepositoryFiles` が使う `BugReportBuildInfo` への射影（`ToBugReportBuildInfo`）を持つ `internal` クラス。ファイルI/Oは持たない（`RepositoryStateProbe.ReadBuildInfo()` が唯一のファイル読み取り元、というADR 0059の不変条件を保つため、パース処理だけをここへ分離した。200行制約のための分割）。
- `Client.Game/InGame/BugReport/Playtest/PlaytestSessionIdentity.cs` — `IPlaytestSessionIdentity`・`EmptyPlaytestSessionIdentity`（ブリーフどおり）。
- `Client.Tests/BugReport/Playtest/BuildInfoReaderTest.cs` — ADR 0059の指示どおり `RepositoryStateProbe.ReadBuildInfo()`（不在時null）と、その内部委譲先 `BuildInfoJson.Parse`（正常系・masterCommitキーの実キー確認・壊れたJSON）、および manifestのsteamId/buildInfo書き出しを検証。

### 変更ファイル
- `Client.Game/InGame/BugReport/RepositoryStateProbe.cs` — `ReadBuildInfo()` の戻り値を `BugReportBuildInfo` から `BuildInfo` へ変更。ファイル不在時は `null`（旧: 空commitの `BugReportBuildInfo`）。パース本体は `BuildInfoJson.Parse` へ委譲。`ComposeBuildInfoJson`・`ProbeGit`・`TryGit` は無変更。
- `Client.Game/InGame/BugReport/BugReportManifest.cs` — `Kind` の直後に `SteamId`（string）・`BuildInfo`（`BuildInfo`）を追加。
- `Client.Game/InGame/BugReport/BugReportBundleWriter.cs` — ctorで `IPlaytestSessionIdentity identity` を受け取るよう変更。`WriteAsync` 冒頭で `buildInfo`（`RepositoryStateProbe.ReadBuildInfo()`、Editorではnull）・`buildInfoForFiles`（`BuildInfoJson.ToBugReportBuildInfo(buildInfo)`、Editorではnull）を先に読み、`manifest.SteamId`/`manifest.BuildInfo` に詰め、`BugReportRepositoryFiles.Write` へは従来どおり `buildInfoForFiles` を渡す（Editor時はnullを渡して旧来のgit probe分岐を維持）。
- `Client.Starter/Registration/MainGameModelRegistration.cs` — `builder.Register<IPlaytestSessionIdentity, EmptyPlaytestSessionIdentity>(Lifetime.Singleton);` を `BugReportBundleWriter` 登録の直前に追加。
- `Client.Tests/BugReport/BugReportSubmitKindTest.cs`（`Playtest/`へ移動）— `new BugReportBundleWriter()` を `new BugReportBundleWriter(new EmptyPlaytestSessionIdentity())` に修正。

### ディレクトリ再編（コントローラー指示の10ファイル規約是正）
`Client.Tests/BugReport/` 直下が16ファイルで規約超過だったため、`git mv` で以下を移動（`.meta`も同時に移動。namespaceは`Client.Tests.BugReport`のまま変更していない）:
- `Playtest/` へ: `PlaytestReportKindTest.cs`・`BugReportSubmitKindTest.cs`・`NullBugReportCaptureSources.cs`・新規`BuildInfoReaderTest.cs`
- `LastSession/` へ: `CleanExitMarkerTest.cs`・`PreviousSessionSalvageTest.cs`

移動後もBugReport/直下は11ファイルで規約(10以下)を完全には満たさないが、指示は「plan Gが追加したぶんを移して悪化を止める」であり、それ以外の既存ファイル（master由来）は動かしていない。

## テスト

```
uloop compile --project-path ./moorestech_client
→ Success: true, ErrorCount: 0

uloop run-tests --project-path ./moorestech_client --filter-type regex \
  --filter-value "^Client\.Tests\.BugReport\..*(BuildInfo|Kind|CleanExit|PreviousSession).*$"
→ Status: Passed, TestCount: 18, PassedCount: 18, FailedCount: 0

uloop run-tests --project-path ./moorestech_client --filter-type regex \
  --filter-value "^Client\.Tests\.BugReport\..*$" --skip-compile
→ Status: Passed, TestCount: 81, PassedCount: 81, FailedCount: 0（BugReport配下全体の回帰確認。移動したファイルが壊れていないことも含む）
```

## ブリーフ/ADRからの逸脱と理由

1. **`Playtest/BuildInfoReader.cs` は作成しなかった**（ADR 0059の指示どおり）。`RepositoryStateProbe.ReadBuildInfo()` を唯一のファイル読み取り実装とした。
2. **`BuildInfo` に `MasterDataDirty` を1つ追加した**（ブリーフ・shared-contracts §1の7フィールドには無い）。
   理由: 焼く側 `ComposeBuildInfoJson` は既に `masterDirty` キーを実際に出力している。これを読み捨てると、
   `BugReportRepositoryFiles.Write` へ渡す射影 `BugReportBuildInfo.MasterData.Dirty` が常に `false` になり、
   「マスタデータに未コミット変更があるビルド」を常にクリーン扱いで報告する退行になる（旧実装は `masterDirty` を正しく反映していた）。
   ADR 0059は「実装済みの焼く側のキーを正とする」と明言しているため、この既存キーを読むことは裁定の趣旨に沿うと判断した。
   JSON出力（`manifest.buildInfo.masterDataDirty`）には無害な追加キーが1つ増えるが、契約の必須キーは全て1対1のままで、
   取り込み側が未知キーを無視する前提であれば影響はない（ここは推測であり、懸念として下記に記載）。
3. **`RepositoryStateProbe.cs` の200行超過を避けるため `BuildInfoJson.cs` へパース/射影ロジックを分離した。**
   ADR 0059は「ファイルを読むのはRepositoryStateProbeの1本」と定めており、`BuildInfoJson`はファイルI/Oを一切持たず
   文字列パースのみを行う内部ヘルパーなので、二重リーダーには当たらないと判断した。`ReadBuildInfo()`は
   引き続き`RepositoryStateProbe`唯一の公開エントリポイントである。
4. **`BugReportManifest.SchemaVersion` は上げなかった**。既存の `Kind` フィールド追加（plan G Task 1）でも
   スキーマバージョンは上げられておらず、フィールドの追加的な拡張は本コードベースでは非破壊的変更として
   バージョンを上げない慣習と判断した。

## 自己レビュー

- ADR 0059の核（読み手は1本・キーは実装済みの焼く側が正・両キーフォールバック禁止）は守っている。`masterDataCommit`側のキーは一切読んでいない。
- `BugReportRepositoryFiles`・`Editor/Build/BuildInfoWriter`・`ComposeBuildInfoJson` は無変更（差分なしを`git diff`で確認済み）。
- Editor実行時の既存挙動（`buildInfo`/`buildInfoForFiles`が共にnull→git probe分岐）を壊していないことを、`BugReportBundleWriter.cs`の差分とテスト(81件)で確認した。
- 全ファイル200行以内（最大175行 `BugReportBundleWriter.cs`）。
- コメントは日本語→英語の2行セット、各行1文に収めた。
- 新規`BuildInfoJson`は`internal`とし、`AssemblyInfo.cs`の`InternalsVisibleTo("Client.Tests")`経由でテストからのみアクセス可能にした（既存の`TryGit`と同じパターン）。

## 懸念事項

- 上記逸脱2（`MasterDataDirty`追加によるJSON追加キー）は、取り込み側（plan H）が厳密スキーマ検証（未知キー拒否）を行う場合に影響する可能性がある。plan Hの実装時にこのキーの扱いを確認すること。
- `Client.Tests/BugReport/` 直下は今回の移動後も11ファイルで「1ディレクトリ10ファイル以下」規約を完全には満たしていない。是正の完了ではなく「悪化を止める」範囲の対応。

---

## Fix: task-4レビュー Important指摘への対応（2026-09-14）

レビュー所見（`.superpowers/sdd/task-4-review.md`）のうち、コントローラー裁定に従い以下を実施した。

### Important 1（修正した）: masterCommit欠落時のDebug.LogWarning復元

`BuildInfoJson.ToBugReportBuildInfo` が `masterCommit` 欠落時に旧 `RepositoryStateProbe.ReadBuildInfo()` が出していた
`Debug.LogWarning("build-info.json に masterCommit が無いためマスタデータのリポジトリ状態は不明です path:...")` を
無言で落としていた（AGENTS.md「無音の縮退は禁止」違反）。`BuildInfoJson.cs` の当該分岐へ警告を復元した。
path情報は `Application.streamingAssetsPath` + `RepositoryStateProbe.BuildInfoFileName` から再構成し、
旧実装と同等以上の情報量にした（`using System.IO;` を追加）。

`build-info.json` 自体が不在のときの警告（`RepositoryStateProbe.ReadBuildInfo()` 内、path付き）は
今回の実装で既に維持されていたことを確認済み（変更不要）。

変更ファイル: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfoJson.cs`

### Important 2（コントローラー裁定により修正しない）: MasterDataDirty追加

裁定どおりコード変更なし。`BuildInfo.MasterDataDirty` はそのまま残す。plan Hは`manifest.get("buildInfo")`から
必要キーだけを`.get()`で読む実装で未知キー拒否が無いことをコントローラーが確認済みのため、この項目は据え置き。

### Minor 2（追加した）: WriteAsync経由のidentity/buildInfo配線テスト

`BugReportBundleWriter.WriteAsync` を通して `IPlaytestSessionIdentity.SteamId` が実際に manifest.json の
`steamId` へ渡ること、`buildInfo` が（Editor実行時＝build-info.json不在相当）nullでもJSONが壊れないことを
検証する `Client.Tests.BugReport.BugReportBundleWriterIdentityTest` を1件追加した。
`GameSystemPaths.BugReportOutboxDirectory`（固定の実配置場所）へ実際に書き出し、
`try/finally` で `Directory.Delete(result.BundleDirectory, true)` により後始末する。
既存の `Playtest/` サブディレクトリの配置規律・`Client.Tests.BugReport` namespace規律に合わせた。
async UniTaskをEditModeテストでブロッキング待機（`GetAwaiter().GetResult()`）するとメインスレッドの
デッドロックリスクがあるため、既存の `SkitLocalizationResolverTest.cs` 等と同じ `public async Task` パターンを採用した。

新規ファイル: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/BugReportBundleWriterIdentityTest.cs`

### 検証

```
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/playtest-client-report
uloop compile --project-path ./moorestech_client --timeout-seconds 600
→ Success: true, ErrorCount: 0, WarningCount: 0

uloop run-tests --project-path ./moorestech_client --filter-type regex \
  --filter-value "^Client\.Tests\.BugReport\..*$" --timeout-seconds 600
→ Status: Passed, TestCount: 82, PassedCount: 82, FailedCount: 0
（新規テスト1件を含む。task-4実装時の81件+1件=82件で全PASS）
```

Minor 1（テスト10ファイル規約超過）は最終レビューへ送る指示のため未対応のまま。

### 対象外だった変更

`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` はコミット前に作業ディレクトリに
既に存在していた変更（uloopのコンパイルトリガー用ダミー値の自動更新と見られる）で、本タスクの範囲外のためコミットしていない。
