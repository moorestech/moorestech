# 0071. 配布ビルドにApple Silicon向けmac depotを加え、ビルド要求を用途enumで表す

日付: 2026-09-26
状態: 採択（ADR 0061 の「配布はWindowsのみ」を拡張する。`.decisions/2026-08-02-PlayerBuildRequestは3boolのまま維持する.md` を置き換える）

## Context

ADR 0061 の配布工程（`scripts/playtest/release-playtest.sh`）は Windows だけを焼いて Steam の `playtest-staging` へ上げ、検証機で通し検証している。Mac のテスターへ配る経路が無い。

`BuildPipeline` は StandaloneOSX を焼けるが、Mac 向けのローカル配布ビルドには展示会用の再起動ループ `start-gamescom-loop.command` が「Mac なら同梱」という暗黙分岐で必ず入る。ffmpeg の同梱は Windows のみで、CEF の Mac ランタイムは `osx-arm64` しか無い。

## Decision

- **1コマンドで Windows と Mac を同じコミットから焼き、1回の `run_app_build`（ビルドID 1つ）に両 depot を入れて `playtest-staging` へ上げる。どちらかが失敗したら何も上げない。**
  出所: ユーザー裁定 2026-09-25 原文「いまwindowsだけでやってるsteamデプロイパイプラインをmac版でもビルド、デプロイするようにして」→ 質問「1回の配布でWindows版とMac版を同じコミット・同じSteamビルドとして上げますか？」→ 選択「A：1コマンドで両OSを焼き、1回のSteamアップロード（ビルドID 1つ）にWindows depotとMac depotを両方入れる」
  棄却案: OSごとに別コマンド・別アップロード／既定は両方で `--only windows|mac` の絞り込みを持つ

- **Mac 版は自動検証しない。`promotion.md` に「Mac 版は未検証、`playtest` 反映前に手元の Mac で `playtest-staging` を起動して確認する」と明記する。Windows の通し検証は従来どおり。**
  出所: ユーザー裁定 2026-09-25 質問「Mac版の配布前検証はどうしますか？」→ 選択「A：Mac版は自動検証なしで上げ、promotion.mdに未検証と明記し人が手元のMacで確認する」
  棄却案: Mac mini に Steam クライアントを入れて自動の通し検証を回す／Steam を通さず `.app` を直接 smoke 起動する

- **Mac 向け ffmpeg を非公開アセットへ追加し、`FfmpegRuntimeBundler` を Mac にも対応させて同梱する。**
  出所: ユーザー裁定 2026-09-25 質問「Mac版のバグ報告録画（ffmpeg）をどうしますか？」→ 選択「B：今回Mac向けffmpegを非公開アセットに追加し、FfmpegRuntimeBundlerをMac対応させる」
  棄却案: 今回は同梱せず Mac の報告は録画欠損を許容する

- **Mac 版は Apple Silicon（arm64）専用とする。配布入口で Mac のアーキテクチャを arm64 に固定し、ffmpeg も arm64 版だけを同梱する。**
  出所: ユーザー裁定 2026-09-25 質問「Mac版はどのCPUを対象にしますか？」→ 選択「A：Apple Silicon（arm64）専用」
  棄却案: Universal（Intel＋Apple Silicon）
  根拠: CEF の Mac ランタイムが `osx-arm64` のみで、Intel Mac では Web UI が動かない。

- **ビルド要求は「配布の用途」enum `BuildPurpose { Ci, LocalDevelopment, Exhibition, SteamPlaytest }` で表し、strict・ゲームデータ同梱・展示会用スクリプト同梱は用途から1か所で導く。`PlayerBuildRequest` の bool は `IsDevelopmentBuild` だけ残す（開発メニューのダイアログで人が選ぶため）。** 展示会用スクリプトは `Exhibition` のときだけ入り、「Mac なら同梱」の暗黙分岐は消す。
  出所: ユーザー裁定 2026-09-25 原文「だとしたら、展示会モードでビルドと、一般ビルドでビルドそのもののやり方を分けるべきでは？」→ 質問「展示会用ビルドとSteam配布用ビルドをどう分けますか？」→ 選択「B：要求に配布の用途を表すenumを持たせ、同梱物を用途から決める」→ 質問「用途enumと今の3つのboolの関係をどうしますか？」→ 選択「A：用途が同梱・検査方針をすべて決め、boolはIsDevelopmentBuildだけ残す」
  棄却案: 4つ目の bool `BundleExhibitionLaunchScript` を足す／Steam の depot 定義の `FileExclusion` で除外しビルドは変えない／含めたまま配布する／enum は同梱物の判断だけに使い strict とゲームデータ同梱の bool は残す

- **公証はしない。同梱の後に `.app` 全体を ad-hoc 署名し直して（`codesign --force --deep -s -`）から Steam へ上げる。**
  出所: ユーザー裁定 2026-09-26 質問「Mac版のコード署名と公証をどうしますか？」→ 選択「A：公証はせず、同梱後に.app全体をad-hoc署名し直す」
  棄却案: Developer ID 署名＋公証／何もしない

- **手元の Mac 確認で起動にセキュリティ設定の変更やコマンド実行（回避操作）が要った場合も配布は止めず、回避手順をキーと一緒にテスターへ案内する。`promotion.md` の Mac 確認手順には「回避操作が要ったか・要ったならその手順」を記録する欄を置く。**
  出所: ユーザー裁定 2026-09-26 質問「手元のMacでSteam経由の確認時に、テスターにセキュリティ設定の変更やコマンド実行が必要だった場合、playtest反映を止めますか？」→ 選択「B：初回は回避手順を案内して配布してよい」
  棄却案: 回避操作なしで起動できるまで反映を止める

- **Steamworks 側（macOS depot の作成・macOS 起動オプション・テスター用パッケージへの depot 追加）は README の手作業手順として人が行う。env は `MOORESTECH_STEAM_DEPOT_ID_WINDOWS` / `MOORESTECH_STEAM_DEPOT_ID_MAC` の対称名にし（旧 `MOORESTECH_STEAM_DEPOT_ID` は改名）、未設定ならビルド前に止まる。**
  出所: ユーザー裁定 2026-09-26 質問「Steamworks側で必要な設定はどう進めますか？」→ 選択「A：手作業手順をREADMEに書き人が行う。env未設定ならビルド前に止まる。既存変数はWINDOWSへ改名し対称にする」
  棄却案: 既存の変数名を残し `_MAC` だけ足す

- 以下は agent 前提:
  - 両 OS は同じ使い捨て worktree で Windows→Mac の順に焼き、成果物は `runs/<label>/build-windows/`・`build-mac/` に分ける。出所: agent前提（既存 release-playtest.sh の worktree・RUN_DIR 構成の延長）
  - Mac の成果物検査は `moorestech.app`・`game/mods`・`moorestech.app/Contents/Resources/Data/StreamingAssets/build-info.json`（target が StandaloneOSX）と、同梱 ffmpeg・CEF helper の実在。出所: agent前提（Windows 側の成果物検査と同型）
  - Mac 向け ffmpeg は Windows と同じ GPL の静的ビルドを `ffmpeg/macos-arm64/`（実行ファイル `ffmpeg` と `LICENSE`）に置く。出所: agent前提（既存 `ffmpeg/win-x64` の配置・ライセンス同梱の前例）

## Consequences

- Intel Mac のテスターは対象外。Steamworks の macOS depot のシステム要件に Apple Silicon を明記する。
- Steamworks 側の設定と env の追加が済むまで、配布コマンドは両 OS とも止まる。
- Mac 版の起動不良は自動では止まらず、`playtest` 反映前の人の確認だけが関門になる。
- Steam 以外の経路（zip 等）で `.app` を渡すと Gatekeeper に止められる。
