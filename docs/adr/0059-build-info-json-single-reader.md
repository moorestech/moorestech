# ADR 0059: build-info.json の読み取り口は1本に保つ（plan G Task 4 の裁定）

- 状態: 採用
- 日付: 2026-09-14
- 文脈: plan G（`docs/superpowers/plans/2026-09-13-playtest-g-report-kind-crash-and-progress-record.md`）Task 4
- 裁定者: 実装セッション（ユーザー就寝中の無人実行。質問禁止の指示下で本体が裁定した）

## 文脈

plan G Task 4 は `Client.Game/InGame/BugReport/BuildInfo.cs` と `Playtest/BuildInfoReader.cs` を**新規作成**し、
`StreamingAssets/build-info.json` を読んで `manifest.buildInfo` に載せると定めている。
plan 執筆時点（2026-09-13）の前提では、plan B の manifest は `repository` しか持たず build-info.json を読む実装は無かった。

しかし PR #1352 のマージ後の master には既に次が実在する:

- `RepositoryStateProbe.ReadBuildInfo()` — `StreamingAssets/build-info.json` を読む唯一の実装
- `RepositoryStateProbe.ComposeBuildInfoJson()` と `Editor/Build/BuildInfoWriter` — 同じファイルを焼く側
- `BugReportBuildInfo`（`Repository`・`MasterData` の2枠）と `BugReportRepositoryFiles.Write(...)` の消費経路

plan のとおり `BuildInfoReader` を新設すると、同一ファイルに対して**型が2つ・パース地点が2つ**でき、
焼く側（`ComposeBuildInfoJson`）の変更が片方だけに反映される二重管理になる。

## 裁定

**build-info.json を読む実装は `RepositoryStateProbe` の1本に保つ。新しい `BuildInfoReader` は作らない。**

- 共有契約 §1 の形を表す `BuildInfo` 型は作る（`manifest.buildInfo` の DTO として必要）。
- ただしファイルを読むのは `RepositoryStateProbe.ReadBuildInfo()` のみとし、そこが `BuildInfo` を返す。
  既存消費側が使う `BugReportBuildInfo`（`Repository`/`MasterData`）は `BuildInfo` からの射影として導く。
- 既存の `Editor/Build/BuildInfoWriter` と `ComposeBuildInfoJson` は本 plan では変更しない。

### JSON キーの不一致について

共有契約 §1 は `masterDataCommit`・`steamBuildLabel`・`target` を挙げるが、実装済みの焼く側が出すキーは
`commit`・`branch`・`dirty`・`masterCommit`・`masterDirty`・`builtAt` である。

**実装済みの焼く側のキーを正とする。** `BuildInfo.MasterDataCommit` は `masterCommit` から読む。
`steamBuildLabel`・`target` は現在の焼く側が出さないため `null` のまま置き、plan E が焼く側を拡張した時点で値が入る。
両方のキーを見るフォールバック（`masterDataCommit ?? masterCommit`）は採らない（AGENTS.md「フォールバックで吸収するのは設計の敗北」）。
plan E / plan H はこのキー名に合わせること。

## 却下した案

- **案B: plan のとおり `BuildInfoReader` を新設する。** 同一ファイルの読み手が2つになり、焼く側の変更が片方に伝わらない。
  plan の前提（読み手が存在しない）が既に成立していないため、逐語実装は前提の誤りを固定するだけになる。
- **案C: 既存 `RepositoryStateProbe.ReadBuildInfo()` を共有契約 §1 のキーへ書き換え、焼く側も合わせる。**
  本 plan の範囲外（plan E の担当）であり、plan B が出荷済みの箱の互換を本 plan で壊す理由が無い。
- **案D: 両キーを見るフォールバックを入れる。** 上記のとおり規約が明示的に禁じている。

## 帰結

- plan G Task 4 の「Create: `Playtest/BuildInfoReader.cs`」は実施しない。差分は plan からの意図的な逸脱として PR 本文に記載する。
- `BuildInfoReaderTest.cs` は `RepositoryStateProbe.ReadBuildInfo()`（不在時 `null` 相当・壊れた JSON の縮退）を対象にする。
