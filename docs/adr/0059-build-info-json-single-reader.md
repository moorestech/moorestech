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
- 既存の `Editor/Build/BuildInfoWriter` と `ComposeBuildInfoJson` は本 plan では変更しない。（→ 末尾の追記（2026-09-17）で改訂: plan E で変更済み）

### JSON キーの不一致について

共有契約 §1 は `masterDataCommit`・`steamBuildLabel`・`target` を挙げるが、実装済みの焼く側が出すキーは
`commit`・`branch`・`dirty`・`masterCommit`・`masterDirty`・`builtAt` である。

**実装済みの焼く側のキーを正とする。**（→ 末尾の追記（2026-09-17）で改訂: `masterDataCommit` へ一括改名） `BuildInfo.MasterDataCommit` は `masterCommit` から読む。
`steamBuildLabel`・`target` は現在の焼く側が出さないため `null` のまま置き、plan E が焼く側を拡張した時点で値が入る。
両方のキーを見るフォールバック（`masterDataCommit ?? masterCommit`）は採らない（AGENTS.md「フォールバックで吸収するのは設計の敗北」）。
plan E / plan H はこのキー名に合わせること。（→ 追記（2026-09-17）で上書き）

## 却下した案

- **案B: plan のとおり `BuildInfoReader` を新設する。** 同一ファイルの読み手が2つになり、焼く側の変更が片方に伝わらない。
  plan の前提（読み手が存在しない）が既に成立していないため、逐語実装は前提の誤りを固定するだけになる。
- **案C: 既存 `RepositoryStateProbe.ReadBuildInfo()` を共有契約 §1 のキーへ書き換え、焼く側も合わせる。**
  本 plan の範囲外（plan E の担当）であり、plan B が出荷済みの箱の互換を本 plan で壊す理由が無い。
- **案D: 両キーを見るフォールバックを入れる。** 上記のとおり規約が明示的に禁じている。

## 帰結

- plan G Task 4 の「Create: `Playtest/BuildInfoReader.cs`」は実施しない。差分は plan からの意図的な逸脱として PR 本文に記載する。
- `BuildInfoReaderTest.cs` は `RepositoryStateProbe.ReadBuildInfo()`（不在時 `null` 相当・壊れた JSON の縮退）を対象にする。

## 追記（2026-09-17・plan E Task 3 のユーザー裁定）: 焼く側・読む側を共有契約 §1 のキーへ一括改名

上の「実装済みの焼く側のキーを正とする」は plan G の範囲の暫定であり、plan E で案C（焼く側・読む側を §1 へ揃える）を採った。

- **キー改名**: `masterCommit` → `masterDataCommit`。焼く側・読む側（`BuildInfoJson.Parse`）・既存テストを同じ変更で追随させた。
  旧キーを読むフォールバックは入れない（旧キーの build-info.json を読むと `MasterDataCommit` は null＝欠損になる）。
- **追加キー**: `steamBuildLabel`（env `MOORESTECH_STEAM_BUILD_LABEL`。未設定・空は `BuildInfoComposer` が一度だけ解決して `null` で焼き、「ラベルなし」と「空ラベル」を区別できるようにする。読む側の `BuildInfoJson.Parse` も `steamBuildLabel`・`target` の空文字を null として運ぶ）と `target`（ビルドターゲット名）を焼く。
- **`masterDirty` は残す**: §1 には無いが、落とすとマスタの未コミット変更が常にクリーン扱いになる（F02 と同じ理由）。
- **`builtAt` は UTC（`...Z`）のまま**: §1 の例の `+09:00` 表記には合わせない。報告 manifest の `createdAt` 等と同じ書式を保つ。
- **型とリーダーは増やさない**: 唯一の型は `BuildOrigin.BuildInfo`、唯一のリーダーは `BuildInfoJson.Parse`（本 ADR の本旨を維持）。
  組み立ては `RepositoryStateProbe.ComposeBuildInfoJson` から `BuildOrigin/BuildInfoComposer.Compose` へ移した。
- **fail-closed は strict のときだけ**: git 読み取り失敗・master data ピン（`.moorestech-external-revisions.json` の `commitHash`）の欠落・
  ピンと実 HEAD の不一致は、`PlayerBuildRequest.IsStrictBundling=true` なら `BuildFailedException`。非 strict（CI互換）は理由を警告ログに出し、
  取れなかった値は null、ピンずれ時は実 HEAD を焼いて続行する。strict は `BuildPipeline` が `BuildInfoWriter.SetStrictBundling` で
  `BuildPlayer` 直前に渡す（`PlayerBuildRequest` は3boolのまま）。ピンの読み取りは `MasterDataRootLocator.ReadPinnedCommit` に寄せ、照合元は作業ツリーのファイルではなくコミット済みの値（`git show HEAD:.moorestech-external-revisions.json`）にする（GUI ビルドの同期が作業ツリーのピンを実 HEAD へ書き戻すと同値比較で素通りするため。2026-09-17 ユーザー裁定）。
- **master data の解決先はビルドする checkout 基準**: 焼く `masterDataCommit` は `GameDataBundler.MasterDataRepositoryRoot`
  （`MasterDataRootLocator.ResolveForBuildingCheckout`＝ビルドする checkout ＋ピンの `relativePath`）の HEAD を読む。同梱元も同じプロパティなので、
  焼いたコミット＝同梱した中身が構造上成立する。バグ報告 probe の `MasterDataRootLocator.Resolve`（正本clone基準）は変更しない。
- **`branch` は配布元 ref を優先する**: env `MOORESTECH_BUILD_BRANCH`（`release-playtest.sh` が渡す）が指定されていればその値を焼き、未指定・空なら従来どおり git の `rev-parse --abbrev-ref HEAD` を焼く。使い捨て worktree の一時ブランチ名を成果物の出所にしないため（2026-09-17 ユーザー裁定）。
