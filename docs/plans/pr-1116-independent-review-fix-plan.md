# PR #1116 独立レビュー修正ハンドオフ

- 対象PR: #1116「build: Unity Editor経由の配布ビルドパイプラインを整備」（feature/editor-build-pipeline）
- レビュー対象head: `2bf849b22787f0e6a641949971b3fff3997d9dcc` / base: `origin/master`（`be1c680a`）
- レビュー実施: 2026-08-02 独立レビューセッション（verdict: Critical差し戻し）
- 出典: `.claude/skills/pr-independent-review/records/pr-1116.md`（縮約）・ダイジェストHTML（/tmp/pr-review-1116/index.html・揮発）
- 本書の使い方: **セクションAは裁定不要で着手可**。セクションBはユーザー裁定を得てから実装する。
  行番号はPR head時点のもの。着手時にheadが進んでいたら要再照合。

## 適用状況（2026-08-02 更新）

- **セクションA 11件すべて適用済み**。`e970e7bfd`（A-2/A-3）と `d5971cf11`（A-1/A-4〜A-11）で
  `feature/editor-build-pipeline` へpush済み。`uloop compile` エラー0を確認。
  行番号は以下の記述からずれているため、追加作業時は現HEADで再照合すること。
- 適用時の補足:
  - A-3はdeploy.shに加え `tools/terraform/main.tf:142` の `mkdir -p ~/game` も新レイアウトへ追従させた。
  - A-6のinstallコマンドは推奨どおり `--frozen-lockfile` へ統一（dev起動経路も同じ実装を通る）。
  - A-7の共有Copyは`.meta`とドット始まりディレクトリを飛ばすため、mod内の `.mooreseditor`
    （エディタのローカル情報）が配布物から外れる。サーバー側に参照コードが無いことを確認済み。
  - A-11でCI入口の `EditorApplication.Exit` は `PlayerBuildOutcome` 由来へ変更（挙動は同値）。
- **セクションB 9件すべて裁定済み**。実装を伴うものは `7af1c763d` でpush済み。裁定内容は
  `.decisions/2026-08-02-*.md` に記録。
  - B-1 raw input: **今回は対応しない**（後で根本対応）。応急処置のcherry-pickも部分OR戻しも行わない
  - B-2 Linux入口: **意図的失敗として残す**＋実行前ダイアログで必ず失敗する旨を明示（実装済み）
  - B-3 spawn集約: A-4/A-6で完了扱い（追加作業なし）
  - B-4 namespace: **案1採用**。`Client.Editor.Build` 付与＋build.ymlのクライアントジョブ2箇所を完全修飾（実装済み）
  - B-5 バリアント表現: **現状維持**（A-11のExitOnFinish削除で十分とする）
  - B-6/B-7 env浄化: **折衷採用**。内容ベース維持＋`NeverScrubbedNames`＋除去後の再走査でLogError（実装済み）。所有者は現状維持
  - B-8 根拠コメント: **復元する**（実装済み）
  - B-9 CI strict: **現行契約を維持**（変更なし）

## セクションA: 裁定不要のCritical修正手順（着手可・11件）

### A-1. localization検証とランタイム読込の不一致【配布ビルド不成立・最優先】
- 場所: `moorestech_client/Assets/Scripts/Editor/Build/GameDataBundler.cs:57-62`
- 事実: 検証は `mods/*/localization/localization.csv` を必須にするが、ランタイムの唯一の読込点
  `Client.Localization/Localize.cs:58` は `config/localization.csv` を読む。実master
  （`../moorestech_master/server_v8`）は config/ 側にあり mods 配下に localization/ は無い
  （2026-08-02時点で実確認）。mod配下レイアウトの移行コミットは未マージブランチのみ。
- 修正: `FindMissingRequiredPath` の localization 検証を `config/localization.csv` の存在チェックへ戻す。
  コメント「旧config/は廃止済み」も削除（事実と異なる）。
  ※mod配下レイアウトを採る選択肢もあるが、その場合は同一PRで `Localize.cs` の読込元をmod走査へ
  同時切替が必須（片方だけ変えると「ビルドは通るが起動時に辞書が空/例外」になる）。既定はconfig/へ戻す。
- 検証: `uloop compile` → メニューからWindows/macOSビルドを実行しstrict検証を通過すること。

### A-2. macOSのApplication.dataPath誤認【mac配布ビルドがgame/を発見できない】
- 場所: `moorestech_server/Assets/Scripts/Server.Boot/ServerDirectory.cs:16-19`（UNITY_STANDALONE_OSX分岐）
- 事実: コメントは「dataPath=<app>.app/Contents/Resources/Data」前提で4階層上るが、Unity公式仕様では
  Mac Playerの `Application.dataPath` は `<app bundle>/Contents`。4階層は出力ディレクトリの2階層上へ突き抜ける。
- 修正: `Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "game"))`（2階層）へ。
  コメントも実仕様（dataPath=<app>.app/Contents）へ書き直す。
- 検証: macでローカル配布ビルド→ `moorestech.app` の隣の `game/` を読んで起動できること（実機確認推奨）。

### A-3. Linux Dedicated Serverデプロイ契約の破壊
- 場所: `moorestech_server/Assets/Scripts/Server.Boot/ServerDirectory.cs:20-24`（#else分岐）と
  `tools/terraform/deploy.sh:34,40,57-61`
- 事実: #elseを2階層→1階層に変えたが、deploy.shはビルドを `~/moorestech_server/`、データを `~/game/` へ置く
  旧契約のまま（コメント「デフォルトで ../../game を参照」も未更新）。新実装は `~/moorestech_server/game` を参照し
  データを見失う。
- 修正: deploy.shのゲームデータ転送先を `~/moorestech_server/game/` へ変更し、コメントを新契約へ追従させる
  （クライアント配布側レイアウトと統一される）。代替: 起動行に `--serverDataDirectory ~/game` を明示。
- 検証: terraform経由のデプロイでサーバーがゲームデータを読めること（または起動ログのパス解決を確認）。

### A-4. PATH合成の3分岐と浄化迂回【集約は確定・spawn統合はB-3で裁定】
- 場所: `Client.WebUiHost/Editor/EditorProcessRunner.cs:34-53`（正）／
  `Client.WebUiHost/Vite/PnpmInstaller.cs:37`・`Vite/ViteProcess.cs:128`（旧形残存）
- 事実: EditorProcessRunnerだけがPATHキーを大小無視で解決し浄化済みenvから合成。Vite側2箇所は
  リテラル`"PATH"`直書き＋親プロセスの生env（`Environment.GetEnvironmentVariable`）読み直しで、
  直前に挿入した `Sanitize(psi)` を迂回している。
- 修正: `Client.WebUiHost/Common/SanitizedProcessEnvironment` に
  `public static void PrependPath(ProcessStartInfo startInfo, string directory)` を追加
  （キーは `StringComparison.OrdinalIgnoreCase` で `startInfo.Environment` から解決・既存値も同辞書から取得・
  directoryが空なら何もしない）。EditorProcessRunner/PnpmInstaller/ViteProcessの3箇所を委譲へ置換。
- 補足: 「Windowsの `ProcessStartInfo.Environment` は大小無視比較でありコメントの主張自体が偽」の可能性が
  レビューで未決着。集約すればどちらであっても1箇所の実装に閉じるため、集約自体は裁定不要。
  Windows実機でnode解決を1度確認し、コメントの主張が偽と判明したら防御ループを外し文言を直す。
- 検証: `uloop compile` → Windows実機でWebUI devモード（pnpm install / vite起動）が動くこと。

### A-5. LFSポインタ判定のマジック1024と前例迂回
- 場所: `Editor/Build/CefRuntimeBundler.cs:51,77` と既存前例 `Editor/Cef/CefPackageLfsValidator.cs`
- 事実: 「LFS未解決ポインタか」をサイズ`< 1024`の裸比較で2箇所に重複実装。既存前例はヘッダ文字列
  （`version https://git-lfs`）照合＋名前付き定数 `MaximumPointerFileSize = 1024`＋復旧案内つき。
  境界も不一致（前例は1024ちょうど=候補、新設は=実体扱い）。
- 修正: 前例の判定を共有ヘルパ（例: `Client.Editor` 配下 `CefLfsPointer.IsPointerFile(string filePath)`）へ
  切り出し、`CefPackageLfsValidator` と `CefRuntimeBundler` の両方から呼ぶ。定数とヘッダ文字列は共有側1箇所。
- 検証: LFS未解決の殻ファイルを用意しstrictビルドが正しく失敗すること（もしくはユニット的に判定関数を確認）。

### A-6. EnsureNodeModulesのPnpmInstaller再実装とinstallコマンド分岐
- 場所: `Client.WebUiHost/Editor/WebUiToolchainBootstrap.cs:44-53` と `Client.WebUiHost/Vite/PnpmInstaller.cs`
- 事実: node_modules存在判定〜pnpm installを無言で再実装し、コマンドが割れている
  （既存 `install` / 新設 `install --frozen-lockfile`）。dev起動経路とビルド経路で依存解決ポリシーが食い違う。
- 修正: PnpmInstallerへ同期コア（例 `InstallIfNeeded(nodePath, pnpmPath, webuiRoot)`）を置き、
  既存asyncの `RunIfNeeded` はそれを包む。`EnsureNodeModules` は同期コアを呼ぶだけにする。
  installコマンドはビルドの再現性を優先し `--frozen-lockfile` へ統一を推奨（軽微な裁定余地あり。
  逆に寄せる場合は理由をコメントで明記）。
- 検証: `uloop compile` → node_modules削除状態からのビルドとdev起動の両方が通ること。

### A-7. ディレクトリコピー実装の3重化（DirectoryProcessorへ集約）
- 場所: `Editor/Build/CefRuntimeBundler.cs:105-135`（CopyDirectory/CopyDirectoryContents）・
  `Editor/Build/GameDataBundler.cs:29-43`（インラインループ）・既存 `Editor/DirectoryProcessor.cs`
- 修正: `DirectoryProcessor` に除外ファイル名リストを取る
  `public static int Copy(string sourcePath, string copyPath, IReadOnlyList<string> excludedFileNames)`
  （`.meta`とドット始まりディレクトリのスキップ・`File.Copy(overwrite: true)`・件数返し）と、
  Deleteしてから同Copyを呼ぶ `CopyAndReplace` オーバーロードを置き、3実装を置換する。
  注意: Windows側（`CefRuntimeBundler.cs:93`）はUnity配置済み `Plugins/x86_64` を消してはいけないため
  Deleteを伴わない `Copy` を使う。`Func<>` 禁止のため述語引数にしない。
- 検証: mac/winビルドで同梱物が変わらないこと（コピー件数ログ比較）。

### A-8. 単一参照privateヘルパのローカル関数化（#region Internal規約・8箇所）
- 対象: `BuildPipeline.PlayerExecutableName`（:116）／`WebUiToolchainBootstrap.EnsureToolchain/EnsureNodeModules`
  （:22,44）／`GameDataBundler.FindMissingRequiredPath`（:47）／`CefRuntimeBundler.BundleMacOs/BundleWindows/
  CopyDirectory/CopyDirectoryContents`（:45,72,105,121）
- 修正: 呼び出し元メソッド末尾の `#region Internal` 内ローカル関数へ移す。
  **順序注意**: A-6（EnsureNodeModules集約）とA-7（コピー集約）を先に適用すると対象自体が減る。
  A-6/A-7適用後に残ったものだけローカル化する。
- 検証: `uloop compile`。

### A-9. 比較演算子の向き規約（機械的・2件）
- `Client.WebUiHost/Common/SanitizedProcessEnvironment.cs:78`:
  `i + 1 >= text.Length` → `text.Length <= i + 1`
- `Client.WebUiHost/Editor/EditorProcessRunner.cs:87`:
  `errorText.Length > 0` → `0 < errorText.Length`
- いずれも副作用オペランド無しをverifier照合済み。挙動変化なし。

### A-10. コメント折り返し規約違反（機械的・1件）
- `Client.Starter/InitializeScenePipeline.cs:137-140`: 日本語2行＋英語2行の折り返しを、
  各言語1行×2組（直列実行の理由／並列プリロード禁止の理由）へ分割して1行ずつに収める。

### A-11. Execute結果の結果型化（推奨案での確定修正。バリアント再設計はB-5と独立に実施可）
- 場所: `Editor/Build/BuildPipeline.cs:55-65,68-114` と `PlayerBuildRequest.cs`
- 事実: `Execute` がvoidのため、Addressables失敗・Player失敗でも `BuildInteractive` が無条件に
  `RevealInFinder` を実行し成功に見える（旧実装からの退行）。
- 修正（レビュー推奨案A）: `PlayerBuildRequest.cs` に
  `public enum PlayerBuildOutcome { Succeeded, AddressablesBuildFailed, PlayerBuildFailed }` を同居させ、
  `Execute` が返す。`BuildInteractive` はswitchで網羅し成功時のみReveal・失敗はDisplayDialogで区別表示。
  `BuildFromGithubAction` は `EditorApplication.Exit(outcome == Succeeded ? 0 : 1)`。
  副次効果: `PlayerBuildRequest.ExitOnFinish` と `Execute` 内の2箇所の分岐を削除できる。
- 検証: 故意にAddressablesを失敗させ、Revealが走らずダイアログ/exit 1になること。

## セクションB: ユーザー裁定待ちの設計判断（9件・実装前に裁定を得る）

### B-1. HybridInputマウス排他読みとraw input応急処置【出荷可否に直結・最優先で裁定】
- 背景: 本PRはcef-unityピンをraw input奪取症状を持ち込む版へ更新。followup doc
  （`docs/plans/windows-cef-rawinput-steal-followup.md`）が「実施済み」と記す応急処置
  `WindowsMouseRawInputReclaimer.cs` はPR headに存在しない（該当コミット`d4e4a877`は本リポジトリの
  参照可能ブランチに無し）。マウスのフォールバック条件は「`Mouse.current` の存在」であり
  「Input Systemがイベントを受信できるか」ではないため、奪取下では右/中クリックがlegacyに落ちず喪失する恐れ。
- 選択肢:
  - 案A（レビュー推奨）: 応急処置コミットをcherry-pickで持ち込み、winbuildでカメラ回転＋右クリック設置を
    再確認してからマージ。既知副作用: Windows版WebUIのホイールスクロール死（doc記載）。
  - 案B: `HybridInput` のマウス3メソッド（:46-62）のみOR読みへ戻し、実証済みのキーボード側だけ排他維持。
  - 案C: 現状のまま出荷（Windows配布でクリック喪失リスク・非推奨）。

### B-2. Linuxメニュービルドの扱い【A群と独立・ただしCritical】
- 背景: `BuildInteractive` は3OS共通strict、CefRuntimeBundlerはLinuxを `default:`→必ず失敗。
  Linuxメニューは毎回 `BuildFailedException` で中断する（旧実装からの退行・CIはLinux除外済み）。
- 選択肢:
  - 案A: Linuxメニュー入口（と `LinuxBuildFromGithubAction`）を削除し配布対象をWin/macに限定。
  - 案B: `PlayerBuildRequest.BundleCefRuntime` を追加し、CEF同梱の要否を入口が明示宣言。
    `default:` は「宣言したのに実体無し」の異常専用へ（Linux入口は `BundleCefRuntime = false`）。
  - 案C: 意図的失敗として残す（その場合ダイアログ/validateメニューで事前明示が必須）。

### B-3. spawn実装3本の集約先（A-4のPrependPath集約後の追加整理）
- 案A: env合成のみ共通化しspawn3本は維持（最小・A-4で完了扱い）。
- 案B: PnpmInstallerに同期コアを置き実行系も寄せる（A-6と同時実施なら自然）。
- 案C: EditorProcessRunnerを正としPnpmInstaller廃止（Editor限定制約の移動が必要・影響大）。

### B-4. Editor/Build のnamespace方針【BuildPipeline二重定義の解消】
- 背景: 新設4ファイルがグローバル名前空間。`tech.moores.server` 経由で取り込まれる `Server.Editor` にも
  同名グローバル `BuildPipeline`＋同名メソッドがあり、CIの `-executeMethod BuildPipeline.WindowsBuildFromGithubAction`
  の解決先が未定義。
- 選択肢:
  - 案1（レビュー推奨）: 4ファイルに `namespace Client.Editor.Build` を付与し、`.github/workflows/build.yml` の
    executeMethodを完全修飾（`Client.Editor.Build.BuildPipeline.WindowsBuildFromGithubAction`）へ更新（4行）。
  - 案2: 案1＋クラス名も `PlayerBuildEntry` へ変更（`UnityEditor.BuildPipeline` との概念混同も解消）。
  - 案3: 現状維持（グローバル2本併存を許容する理由コメントをコード側に必須で残す）。
- 注意: CI実確認が非目標のままexecuteMethod名を変えるリスクの指摘あり。裁定時に「CIを回して確認するか」も決める。

### B-5. PlayerBuildRequestのバリアント表現とSpawnFailureExitCode
- 背景: 3boolで「ローカル配布/CI」の固定2バリアントを暗黙表現（8組中有効2組）。`isStrict` が4階層スレッド。
  `SpawnFailureExitCode = -1` はin-bandセンチネルで消費者ゼロ（正当な-1と区別不能）。
- 選択肢:
  - 案A: `enum BundlingPolicy { FailFast, WarnOnly }` 導入の最小修正（A-11のOutcome化と併用）。
  - 案B: 入口バリアントを型に（`LocalDistributionBuildRequest` / `CiBatchBuildRequest`、Executeはswitch網羅）。
  - EditorProcessRunner戻り値は `readonly struct ProcessRunResult { Outcome, ExitCode }` 案（採否のみ）。

### B-6. env浄化の検出方式
- 案A: 現行の内容ベース（U+FFFD/孤立サロゲート）維持 — 未知の汚染源に効くが、正当なU+FFFDを含む
  PATH/HOME等を配布Playerでも毎起動・不可逆に丸ごと削除しうる。
- 案B: 名前ベースのリスト（既知の注入変数のみ除去。メモリの既存方針と一致・誤爆ゼロ）。
- 案C: 折衷 — 内容ベースは残し `NeverScrubNames = { "PATH", "Path", "HOME", "TMPDIR", ... }` で
  必須変数だけ除外（最悪ケースのみ排除・変更最小）。
- 付随修正（方式によらず）: 「除去しました」ログが名前不正で残存するケースでも成功と偽装する点は、
  除去後の再走査で残存名を `Debug.LogError` する形へ直す。

### B-7. SanitizeCurrentProcess（起動時グローバルスクラブ）の所有者
- 案A: 現状維持（`Client.WebUiHost.Common` が `[RuntimeInitializeOnLoadMethod]` を持つ）。
- 案B: 責務分割 — 起動時スクラブを `Client.Common`（または `Client.Starter`）のboot側へ新設クラスで移し、
  `EnvironmentScrubOnEditorLoad` の起点も移動。WebUiHostは子プロセス起動前処理（`Sanitize(psi)`/`PrependPath`）のみ。
- 論点: 配布Playerで毎起動グローバル副作用を持つべきか（汚染源は開発機固有）もここで裁定。
  エディタ限定にする案（Playerではspawn時サニタイズのみ）も選択肢に含める。

### B-8. CI Development Build根拠コメントの復元
- 背景: 旧実装の「バッチはメモリ効率優先でDevelopment Build（Release=LZ4圧縮はメモリを食う）」という
  WHYコメントが移設で喪失。「現行契約を維持」だけでは将来の誤修正（Release化）を招く。
- 復元タグ案: `// CIはメモリ効率優先でDevelopment固定（Release=CompressWithLz4は圧縮でバッチ機のメモリを食う）`
  ＋英語1行。`BuildPipeline.cs:147` 付近の既存コメント直後へ。採否のみ裁定。

### B-9. CI成果物の完成条件（strict化するか）
- 背景: CI入口は `IsStrictBundling = false` のため、CEF欠品・LFS殻でも警告のみでexit 0成果物がuploadされる。
  本PRは「現行契約の維持」と明示（コード内コメント）しており旧CIと同挙動＝退行ではない。
  一方Codex監査は「配布artifactを生成するCIはstrict必須・同梱結果を終了コードへ反映」を提案。
- 裁定: 現行契約維持のままか、CIもstrict化するか。strict化するならB-2/B-5の選択と整合させる。

## 対応不要と裁定済みの項目（再指摘しないこと）
- `EditorProcessRunner.cs:64` のtry-catch: 外部プロセス起動境界＋根拠コメント付きでAGENTS.md例外に適合。
- `InitializeScenePipeline.cs` 214行: 200行規約は努力目標。分割するならpartial禁止下の責務分割で（任意）。
- InitializeScenePipelineの直列化・MainGameSceneLoader削除自体: 機序コメント妥当・残存参照ゼロ確認済み。
- GraphicsSettings（Instancing Variants 0→2）: 実PR diffで達成確認済み（レビューpatchのexclude対象だっただけ）。

## Warning・コメント短縮などの残項目
- Warning全件（約28件）と、comment-length短縮案29件（機械的25/要判断4）は
  `.claude/skills/pr-independent-review/records/pr-1116.md` とダイジェストHTMLの折りたたみ参考を参照。
  本書のA/B完了後に余力があれば適用する（verdictには影響しない）。

## 完了時の検証チェックリスト
1. `uloop compile --project-path ./moorestech_client` エラー0
2. メニューからWindows/macOSローカル配布ビルドがstrict検証を通過し、成果物直下に実行ファイル＋`game/`
3. mac実機: `.app` 起動でgame/を解決（A-2の確認）
4. Windows実機: WebUI devモード起動（A-4）＋B-1裁定に応じたクリック/カメラ回転確認
5. terraformデプロイ経路のパス解決（A-3）
6. 失敗ビルドでRevealInFinderが走らないこと（A-11）
7. moores-code-review（または本スキルの再レビュー）を1パスして残Critical 0を確認
