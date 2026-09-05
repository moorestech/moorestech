# 0036. ローカル配布ビルドをGUIメニューから1経路で出せるようにする

- Status: Accepted
- Date: 2026-08-26

## Context

展示会用のmac配布ビルド（Release・同梱失敗は即失敗・`game/` にゲームデータ同梱）を出す口は2つある。
`moorestech/Build/MacOsBuild` はDevelopment/Releaseをダイアログで聞くため、Releaseのつもりで
左ボタン（Development）を押すと開発ビルドが出る。もう一方の `ReleaseLocalBuildCli.MacOsReleaseLocalBuild`
は設定が固定で事故らないが、出力先を環境変数 `MOORESTECH_BUILD_OUTPUT` から読み末尾で
`EditorApplication.Exit` するbatchmode専用入口で、GUIから押すとEditorごと終了する。

さらに、成果物をそのまま現地へ持っていくには `scripts/event/start-gamescom-loop.command` を
毎回手でコピーする必要があり、コピー忘れが配備事故になる。

## Decision

Release固定のローカル配布ビルド用メニュー `moorestech/Build/MacOsReleaseLocalBuild` を新設する。
Development/Releaseの問いは出さず、出力先だけ従来どおりフォルダ選択パネルで聞く（前回パスを記憶）。
既存の `MacOsBuild` / `WindowsBuild` / `LinuxBuild` は現状のまま残す。

出所: ユーザー裁定 2026-08-26 原文「guiから実行できるようにして」→ 選択「Release専用メニューを追加」
出所: ユーザー裁定 2026-08-26 出力先の決め方 → 選択「フォルダ選択パネル（現行と同じ）」

### 起動ループスクリプトの同梱

`scripts/event/start-gamescom-loop.command` を成果物直下へ実行権つきでコピーする。
同梱条件は `BundleLocalGameData` かつ `Target == StandaloneOSX`。CI入口（`BundleLocalGameData=false`）と
Windows/Linux成果物には入らない。欠損時の扱いは既存Bundlerと揃え、strictならビルド失敗にする。

出所: ユーザー裁定 2026-08-26 「.command同梱」→ 選択「このビルドに含める」

### PlayerBuildRequestにフラグを足さない

同梱条件は既存の `BundleLocalGameData` とターゲットの組み合わせで表す。
4つ目のboolは足さない（[[.decisions/2026-08-02-PlayerBuildRequestは3boolのまま維持する.md]] の維持）。

出所: agent前提（既決裁定「PlayerBuildRequestは3boolのまま維持する」の踏襲）

## Considered Options

- **却下: 既存 `MacOsBuild` をRelease固定に変える** — GUIからの開発ビルドが作れなくなり、
  Windows側の同じダイアログをどうするかの判断も巻き込む。
- **却下: `ReleaseLocalBuildCli.MacOsReleaseLocalBuild` に `[MenuItem]` を付ける** —
  batchmode前提（環境変数入力・末尾で `EditorApplication.Exit`）なので、GUIで押すとEditorが落ちる。
- **却下: 前回パスを無確認で使う／日付フォルダ自動生成** — 前者は旧成果物を黙って上書きし
  旧Addressablesの残骸が混ざる。後者は古い成果物が溜まる。

## Consequences

- 展示会ビルドはメニュー1つ＋フォルダ選択だけになり、Development誤選択の事故が消える。
- 成果物ディレクトリに `.command` が入るので、そのままコピーして現地で使える。
- ~~batchmode入口（`ReleaseLocalBuildCli`）はCI・自動化用として残り、GUI入口と設定は同一のまま。~~
  **訂正（2026-09-04 ユーザー裁定）**: `ReleaseLocalBuildCli` は削除した。参照0で、同日のplan
  `docs/superpowers/plans/2026-08-26-new-world-defaults-to-generated-map.md` が「QA用の一時ファイルであり
  コミットしない」と明記しているため、plan側を正とする。Release相当のローカル配布ビルドはGUIメニュー
  `moorestech/Build/MacOsReleaseLocalBuild` のみが入口。恒久的な無人ビルド入口が要るなら別途設計する。
