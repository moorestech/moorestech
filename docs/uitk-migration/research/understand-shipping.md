# UITK 全面移行の調査：出荷・デザイン体系・基盤

調査日: 2026-10-22。対象は moorestech リポジトリ（master `e8e611f1c0`）、`../moorestech_master`、PoC ブランチ `poc/uitk-hard-cases`（origin へ push 済み）。
裁定は `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md` を、議論は `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md` を前提にしている。
以下の表記ルールで書く。「推測」と書いた箇所は実測していない。それ以外は、ファイルを読んだかコマンドで数えた結果。

---

## 0. 要点

1. **完了の定義「Kestrel ホスト削除」は、そのままだと遠隔実行を壊す。** 遠隔実行（ADR 0072）の入口 `/api/remote-exec` は Web UI 用の Kestrel に相乗りしている（`Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101` と `Client.RemoteExec/RemoteExecEndpoint.cs`）。プレイテストの Windows 検証（`scripts/playtest/remote-exec.sh`、`verify-on-windows.sh`）もこれを使っている。手は二つある。HTTP の待ち受けだけを独立させて残すか、`HttpListener` などへ載せ替えるか。どちらにするかは裁定が要る。
2. **CEF を外しても、Mac の arm64 専用は自動では解けない。** ffmpeg も `macos-arm64` しか同梱していない（`PersonalAssets/moorestech-client-private/ffmpeg/{macos-arm64,win-x64}`）。ADR 0071 は arm64 専用の根拠を CEF としているが、実態は ffmpeg にも縛られている。Universal 化するかは別の論点になる。
3. **CEF と一緒には消せない共用部品がある。** `CefLfsPointer`（ffmpeg と Harmony の同梱も使う）、`EnvironmentScrubOnEditorLoad`（WebUiHost に置かれているが、守っているのは全ネイティブ spawn）、`Client.ExternalProcess`（ビルド工程も使う）の 3 つ。改名や移設が要る。
4. **配布物の CEF は、Windows で 237 ファイル・パッケージ上で約 368MB、Mac で 252 ファイル・約 298MB。** 2026-10-21 のビルドログで確認した。Windows の総サイズは 5.10GB、Mac は 4.46GB。
5. **OneJS は今すぐ外してよい。** コードからの参照は 0 件。それでも未使用のネイティブ QuickJS（Win/Mac）と Resources 2.2MB（フォント・画像・JS）が配布物に入る。Editor にも `[InitializeOnLoad]` が 4 本刺さっている。移行の順番とは無関係に、最初の PR にできる。
6. **デザイン体系は技術に依らない原則と CSS の実装に分けて写す。** 原則は `ref-webui-design` のホワイトリストで、パネル面・族・層・装飾 6 語彙など。実装は CSS 3,407 行 64 ファイル、トークン約 238 個。USS に無いもののうち中核に当たるのは `mask-image` の 4 辺フェード（GamePanel）、グラデーション、`calc()` 70 箇所、`z-index` 44 箇所、データ属性セレクタ 40 箇所の 5 つ。GamePanel の面は、頂点カラー付きメッシュ（`generateVisualContent`）で作れば画像アセット無しで再現できる見込み（推測。未 PoC）。
7. **`--ui-scale` は PanelSettings の Scale With Screen Size にほぼそのまま対応する。** 基準 1280×720、Screen Match Mode = Expand にすると `min(w/1280, h/720)` と一致する。stage 族と viewport 族の二分法（ADR 0013）も、パネルルートと中央 1280×720 の子で表せる。
8. **Web UI の演出は実質 3 系統しか無い。** 通知の出入り、チュートリアルの脈動、ドラッグガイドの往復で、ほかは毎フレームの値更新。アニメーション基盤は小さくてよい。USS transition と、ループやキーフレームを回す薄い仕組みがあれば足りる。LitMotion（MIT。UITK 用の Bind 25 種、手動ディスパッチャで決定的テスト可。依存の Burst・Collections は導入済み）を導入するか、100〜150 行で自作するかを選ぶ。
9. **Mod から UI を足したり差し替えたりする仕組みは、現状どこにも無い。** 「Mod 配布性」は CEF 採用時の動機（`docs/webui/archive/cef-webui-plan.md:10`）としてあっただけ。実在する Mod 向けの UI 契約は 3 つだけ。チュートリアルのアンカー ID 文字列、Mod 同梱のアイコン画像、Mod 同梱のローカライズ CSV。アンカー ID の文字列は UITK でも同じ綴りで引けるようにする必要がある。
10. **過去の移行では、使える型と、繰り返したくない失敗の両方が出ている。** 使える型は、入口文書・処遇台帳・決定論ゲートの 3 点と、抽出 PR と削除 PR の分割。失敗は 4 つあった。状態機械だけで洗い出してオーバーレイを見落とした。実機検証の残りが 3 か月滞留した。ゲートテストが包括分類で無効化された。AI の画面確認が古いフレームを見ていた。

---

## 1. 配布ビルドでの CEF の扱い

### 1.1 現状の同梱経路

| 段 | 何をするか | 根拠 |
|---|---|---|
| UPM 取得 | `jp.juha.cefunity` を Git URL と LFS で取る。lock の hash は `012281d0…`。開発者は最初に `scripts/setup-cef.sh` / `.ps1` を流し、`git lfs install` と PackageCache の削除を行う | `moorestech_client/Packages/manifest.json`、`docs/webui/design/cef-binary-integration.md` |
| Editor 起動時の検査 | `CefPackageLfsValidator` が PackageCache に LFS ポインタが残っていないか見て、残っていれば LogError を出す | `Assets/Scripts/Editor/Cef/CefPackageLfsValidator.cs` |
| ビルド前処理 | `WebUiProductionArtifactBuilder`（IPreprocessBuildWithReport）が node と pnpm を自動導入し、`pnpm build` を回して `StreamingAssets/WebUi/dist` に配置する。配置物には sha256 manifest が付く | `Client.WebUiHost/Editor/WebUiProductionArtifactBuilder.cs`、`WebUiToolchainBootstrap.cs`、`.gitignore:128-130` |
| ビルド後処理 | `CefRuntimeBundler.Bundle` が UPM の resolvedPath から実体をコピーする。Win は `Plugins/x86_64` へ 237 ファイル、Mac は `PlugIns/cef-unity-server.app` へ 252 ファイル。strict ならビルドを落とす | `Editor/Build/Bundlers/CefRuntimeBundler.cs`、`BuildPipeline.cs:87-91` |
| Mac の arm64 固定 | 配布用途（Exhibition / SteamPlaytest）では arm64 に固定する。理由は「CEF の Mac ランタイムが arm64 のみ」 | `BuildPipeline.cs:62-71`、`MacPlayerArchitecture.cs`、`Client.Build.Policy/BuildPurpose.cs`、ADR 0071 |
| Mac の再署名 | 同梱で崩れた署名を `codesign --force --deep -s -` で張り直す | `MacAppAdHocSigner.cs`、ADR 0071 |
| 成果物検査 | Mac は CEF helper の実在・実行権・arm64 を見る。Windows は CEF を見ていない | `scripts/playtest/lib/release-artifact.sh:40-87`、`tests/test-release-playtest-mac.sh:58-102`、`tests/lib/release-playtest-build-stubs.sh:18,54` |
| CI | Windows ではコンテナ内で UPM と LFS が失敗するため、ホストで `cef-unity` を **`--branch main`** で clone し、`file:` に差し替える。lock の hash に固定していないので、CI だけ別版の CEF で焼かれうる（推測: 現状も起きうるドリフト）。Linux クライアントは CEF が無いため除外。Web UI 用の toolchain step と `webui-test` job もある | `.github/workflows/build.yml:185-186,234-247,277-300`、`run_test.yml:27-54,147-166` |
| 手動の確認手順 | 「CEF raw input 確認（初回合格ビルドのみ）」と「Apple Silicon 専用（CEF の Mac ランタイムが arm64 のみのため）」の明記 | `scripts/playtest/README.md:41,183-188` |
| 配布の手順書 | `moores-playtest-release` スキルは CEF に直接触れていない。Mac 確認の「Tab でインベントリが開くことをスクショで確かめる」は移行後も残る | `.agents/skills/moores-playtest-release/SKILL.md` |

実測のサイズ（`~/hermes-agent/data/services/playtest/runs/playtest-20261021-2150/unity-build-*.log`）:

- Windows: TotalSize 5,097,293,451 B。CEF は 237 ファイル。パッケージキャッシュ上の `win-x64` は 368MB。
- Mac: TotalSize 4,458,535,621 B。CEF helper は 252 ファイル。パッケージキャッシュ上の `osx-arm64` は 298MB。
- 開発側の負担: worktree ごとの PackageCache に `linux-x64` 1.6GB と `win-arm64` 377MB も入る。CEF パッケージ全体で約 2.6GB（`pr-apply` worktree で du を取った）。

### 1.2 CEF と Web UI に依存している出荷・検証の部品

| 部品 | 依存の中身 | 移行後の扱い |
|---|---|---|
| `Client.WebUiHost`（134 ファイル・約 10.1k 行） | Kestrel、WS、Vite、CEF ナビゲータ、topic、action | 撤去する。ただし topic のデータ源になっている C# のモデルは残る |
| `Client.RemoteExec/RemoteExecEndpoint.cs` | `Microsoft.AspNetCore.Http`、Kestrel の経路 | **残す必要がある**。待ち受けの置き場を決める（§1.4） |
| NuGet の `Microsoft.AspNetCore.*` 約 25 個と `Microsoft.Extensions.*` | Kestrel 用 | 遠隔実行の置き場次第で残るか消えるかが決まる |
| `Client.Playtest/WebUi/*`（`CefScreenMapper`・`CefInputForwarder`・`PlaytestDomQuery`）、`Operations/PlaytestWebUiOps.cs`、`Ui/PlaytestBuildMenuOps.cs` ほか | 録画プレイテスト DSL の `ClickWebUi(testid)`・`HoverWebUi`・`UntilWebUiElement`・`OpenBuildMenuAndSelectBlock` | UITK 版の DSL に置き換える（`panel.Pick` と `worldBound` から座標を出す）。スキル `unity-playmode-recorded-playtest/references/write-scenario.md:68,90-106` も書き直す |
| `Client.Tests/WebUi`（29 ファイル）、`Client.Tests/WebUiHost` | 契約、ゲート監査、topic のテスト | 画面ごとの受入条件へ読み替えてから消す |
| `EnvironmentScrubOnEditorLoad`（WebUiHost/Editor） | 自プロセスの汚れた env を浄化し、「CEF 等のネイティブ spawn」を守る | ffmpeg の spawn にも効いているかもしれない（推測）。消さずに `Client.ExternalProcess` 側へ移すか、要否を確かめる |
| `CefLfsPointer` | Ffmpeg と RemoteExecHarmony の同梱検査、`release-preflight.sh:146` のコメントも参照 | 中立名（例 `LfsPointerFile`）へ改名して残す |
| `MainGameUI.prefab` の Canvas と CefUnity（RawImage） | ADR 0052 の uGUI 例外 1 | 削除する。`com.unity.ugui` は例外 2〜4（MainMenu・ローディング、HP バー、デバッグ UI）のために残る |
| skill: `ref-webui-design`、`moores-code-review` の `core-ts_tsx-*` レビュアー 12 本、webui 向けの決定論チェック | Web 前提 | UITK 版へ作り替える。ts レビュアーは Web を消した時点で撤去 |
| docs: `docs/development/macmini/unity-editor.md:32-33`（CEF キャッシュ衝突）、`docs/webui/*` | 運用知識 | archive へ移す |

関連する bd の open 課題（読み取りのみ）。CEF の 12 件はほとんどが移行で消滅するか、受入条件へ転記するものになる。
`moorestech-1gc8`（worktree で CEF 未接続）、`moorestech-3xq6z`（CEF の code -6 で EditMode テストが落ちる）、`moorestech-rhvub.3`（NT handle リーク）、`moorestech-vfn3o.19`（CEF のフレーム消費）、`moorestech-7179.1/.2/.3`（プレイテストの CEF 座標・入力）、`moorestech-s2zk.1`（CEF raw input の確認）、`moorestech-lc9ty.3`、`moorestech-rtilw.3`、`moorestech-lnsf`（uGUI 撤去 epic。例外の CEF 描画面が消える）、`moorestech-fn8`（操作ヒントを Web 側へ再実装。UITK へ行き先を変える）。

### 1.3 外すもの：移行中に外せるものと、移行後に外すもの

| 対象 | いつ外せるか | 根拠・注意 |
|---|---|---|
| `com.singtaa.onejs` | **今すぐ**（移行と無関係） | §5 |
| 配布ビルドに入っている Vite・pnpm の起動コード（`Client.WebUiHost/Vite/*`。`#if UNITY_EDITOR` が無い） | 今すぐ外してよい（推測: 配布物では dev 経路を使わない） | `Common/WebUiHostMode.cs:25-28` は env `MOORESTECH_WEBUI_MODE=dev` を見て Player からも Development にでき、そのとき node を spawn する。AGENTS.md の「デバッグ専用 public をプロダクションに残さない」の対象。移行で丸ごと消えるので、わざわざ直すかは任意 |
| `com.annulusgames.debug-ui` | 今すぐ（参照 0 件） | `using DebugUI` が見つからない。UITK とは無関係のゴミ掃除 |
| CEF ランタイム、`CefRuntimeBundler`、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得、`release-artifact.sh` の CEF helper 検査と契約テスト、README の CEF raw input 確認 | **全画面の UITK 化が終わった PR で一括** | 一部の画面だけ Web に残る間は CEF が要る。検査とテスト（`test-release-playtest-mac.sh` の 3 ケース、stubs）は同じ PR で外さないと契約テストが赤になる |
| `WebUiProductionArtifactBuilder`、`StreamingAssets/WebUi`、`.gitignore:128-130`、`moorestech_web/`、CI の `webui-test` と Web UI toolchain step | 同上 | `run_test.yml:147` は Client shard でだけ node を用意している（`needs_webui`）。これも外す |
| `PinsAppleSilicon`（arm64 固定） | CEF を外しても**残る**（ffmpeg が arm64 のみ） | Universal にするなら、ffmpeg の x64 ビルドを非公開アセットへ足し、`release-artifact.sh:75-86` の lipo 検査を arm64 固定からゆるめる。ADR 0071 の更新も要る |
| ad-hoc 再署名 | 残る（ffmpeg のため） | 署名すべき入れ子の `.app` が CEF helper の分だけ減る |
| `com.unity.ugui` / TMP | 残る | ADR 0052 の例外 2〜4。ただし MainMenu とローディングが uGUI のままだった理由は「CEF の起動がゲーム内に限られるから」だった（`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`）。この前提は消えるので、今回の全面移行に含めるかを裁定する |
| Linux クライアント | CEF を外せばビルド対象に戻せる | `build.yml:185` の除外理由が CEF。ffmpeg の Linux 版は無いが、`FfmpegRuntimeBundler` は skip を Log に出して続行する |

### 1.4 完了の定義に足すべきもの

議論の記録（`ui-toolkit-migration-discussion-2026-10-18.md`「完了の定義」）にある「CEF 削除・Kestrel ホスト削除・`Client.WebUiHost` と `moorestech_web` の撤去・`CefRuntimeBundler` の撤去」は、次の点が抜けているか、そのままでは矛盾する。

| 項目 | 問題 | 選択肢 |
|---|---|---|
| 遠隔実行の待ち受け | Kestrel を消すと `/api/remote-exec` も消える。ADR 0072 の「既存の Web UI サーバーに入口を足す」が前提ごと崩れる | (a) Kestrel を `Client.RemoteExec` 専用の最小ホストとして残す（NuGet もそのまま）。(b) `System.Net.HttpListener` に載せ替えて AspNetCore 系 NuGet を全部外す。Mono の HttpListener の挙動は確かめる必要がある（推測）。(c) 待ち受けの開始位置（いまは `InitializeScenePipeline`）も見直す。ADR 0072 の agent 前提では「タイトル画面では使えない」 |
| 共用部品の移設 | §1.2 の `CefLfsPointer`、`EnvironmentScrubOnEditorLoad`、`Client.ExternalProcess` | 改名や移設を完了の定義に入れる |
| 録画プレイテスト DSL | Web 前提の操作群が消える | UITK 版 DSL を、最初の画面の移行と同じ時期に用意する |
| 決定論ゲート | `WebUiGateClassification` / `WebUiGateAuditTest`（新規 uGUI 禁止）が Web 命名のまま | 「新規 uGUI 禁止」と「新規 Web feature 禁止」を UITK 移行のゲートへ作り直す（§6） |
| Mac の arm64 | 上記 | 裁定が要る |

### 1.5 移行中の出荷形の制約：CEF と UITK が同時に出荷される期間

以下は**すべて推測（未 PoC）**。最初の PR で実測すべき項目として並べる。

- **重なり順。** CEF は uGUI Canvas の RawImage 1 枚で、UITK のパネルはその上か下のどちらか一方にしか置けない。Web 内の z 層（`--z-*` 約 15 段）と UITK の層を交互に重ねることはできない。UITK を上に置くと、Web 側の HUD・通知・ツールチップは UITK の画面の下に回る。通知が全画面 UI の裏に回るのは ADR 0017 の意図と合う。ただし `--z-viewport-grab`（掴んだアイテム）や `--z-portal-tutorial`（チュートリアルの強調）のように「最前面に出るべき Web の層」は UITK の画面に隠れる。
- **系統をまたぐ D&D。** 掴み状態は C# が持っている（スプリット配分も C# 所有。`docs/webui/TODO.md` B3）。クリックで置く操作なら系統をまたいでも成立する見込み。一方、ホットバーへのドラッグ（`useHotbarDragSource`。BuildMenuSlot→Hotbar、`elementFromPoint` で判定）は Web の中だけで閉じた仕組みになっている。インベントリ・ホットバー・装備 HUD・ブロックインベントリ・ビルドメニュー・掴みオーバーレイは、同じ波でまとめて移す単位とみなすのが安全。
- **チュートリアルのハイライト。** 強調の描画（`shared/tutorialAnchor/anchorRegistry.ts` と overlay）は DOM しか探せない。アンカーを持つ画面を UITK へ移したら、その時点で UITK 側にもアンカー登録と強調描画が要る。手は二つ。最初に UITK へ移すか、アンカー ID の所在で描画系統を振り分けるか。マスタが使っているアンカーは `recipe.craft-button`、`challenge.current-hud`、`research.node-<guid>` 7 件（`moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`）。
- **ポインタ判定。** `UiPointerHitTest` は `EventSystem.current.IsPointerOverGameObject()` に頼っている（ADR 0052 の agent 前提 1）。UITK 上のポインタは `panel.Pick` 系で別に判定しないと、UI 越しに世界をクリックしてしまう。
- **見た目の不一致。** CEF（Chrome の合成、Gamma）と UITK（Linear の合成）では、同じ色トークンでも半透明の面の濃さが違って見える（§2.4）。テスターには移行中に混在した見た目が届く（毎週の playtest は `origin/master` を焼くため。`moores-playtest-release` SKILL 手順 1）。

---

## 2. デザイン体系を USS 変数と共通クラスへ写す方針

### 2.1 現行体系の規模

| 項目 | 値 | 根拠 |
|---|---|---|
| CSS | 64 ファイル・3,407 行。うち `app/tokens.css` 476 行、`GamePanel/style.module.css` 216 行 | `find src -name '*.css' \| xargs wc -l` |
| トークン | `tokens.css` 192、`skitTokens.css` 38、`slotTokens.css` 8 の計約 238 | `grep -c '^\s*--'` |
| デザインの正本 | `ref-webui-design` SKILL.md（ホワイトリスト §0〜§10）と `moorestech_web/webui/docs/design/components.md`（§8〜§8.23、400 行） | |
| ADR | 0013（stage 族と viewport 族）、0014（副次アクションボタン）、0015（HUD 面 variant）、0016（通知の出入り）、0017（通知を背面層へ）、0019（ツールチップの書式は Web が所有）、0021（テキスト選択は入力欄のみ）、0033（アイコン上の文字の縁）、0039（チュートリアルの赤と脈動）、0024（ハイライトの祖先クリップ） | `docs/adr/` |
| Mantine に暗黙で頼っている値 | `<Text size="sm">` 27 箇所、`size="xs"` 4 箇所、`c="dimmed"` 8、`c="white"` 5、`color="red"` 2、`color="gray"` 1。どれも tokens.css を経由しない | `grep` で数えた |

### 2.2 写像方針

| Web の仕組み | UITK での置き場 | 備考 |
|---|---|---|
| 原則（ホワイトリスト。全画面 UI 禁止、GamePanel だけが面、スロットは shared 部品だけ、装飾は 6 語彙、色はトークンだけ） | **そのまま継承**する。新スキル（例 `ref-uitk-design`）の §1〜§9 に移す | 技術に依らない部分。`ref-` 接頭辞の扱いは `.decisions/2026-09-29-知識専用skillはwebui-designとtrain-systemだけref-prefixへ改名する.md` に従う |
| `--ui-scale`（`App.tsx:36-47` の `min(innerW/1280, innerH/720)`）と `--ui-viewport-*` | PanelSettings を Scale Mode = Scale With Screen Size、Reference = 1280×720、Screen Match Mode = **Expand** にする。パネルルートの寸法が、そのまま `--ui-viewport-width/height` に当たる | Expand は「基準より小さくならない＝倍率 min」（CanvasScaler と同じ定義と理解しているが、UITK での挙動は未実測＝推測）。既存の `SkitPanelSettings.asset` は 1920×1080・MatchWidthOrHeight(0) なので流用しない |
| stage 族（1280×720 を一様に拡縮）、viewport 族（実画面の辺に追従）、背面 viewport 族 | ルート直下に名前付きの層コンテナを固定順で並べる。stage は中央寄せした 1280×720 の子、viewport 族はルートに対する absolute | ADR 0013/0017 の三分法をそのまま表せる |
| `--z-*` 約 15 段（`--z-viewport-*`、`--z-stage-*`、`--z-portal-*`） | USS に z-index は無い。**C# の層 enum と層コンテナの兄弟順**で表し、層をまたぐ前後関係は enum の順序で決める。`zLayerTokens.test.ts` の不変条件は C# テストへ移す | パネル（UIDocument）を層ごとに分けて sortingOrder で並べる案もある。パネル間のイベントの受け渡しとバッチ数を比べる必要がある（推測） |
| 単一値のトークン（px・色） | USS 変数 `--name` を `var()` で使う | USS の変数は「演算不可」で、`rgb(var(--r),…)` のように関数の中にも書けない（Unity 6.3 マニュアル「USS custom properties」） |
| `calc()` で導くトークン（70 箇所。例 `--inventory-panel-width`、`--menu-content-height`、`--challenge-hud-width`） | 2 案ある。A. C# の `UiTokens` を正本にして USS を生成し、生成物との一致をテストで見る。B. USS にリテラルの結果を書き、導出式はコメントに残し、一致をテストで見る | 導出値は C# 側（Painter2D・配置計算）でも要る。Web の `labelGapToken.ts` のように実行時に読む前例もある。C# から読むなら `customStyle` と `CustomStyleProperty<float>` で解決後の値を取れる |
| `rgb(from var(--x) r g b / 24%)`・`color-mix`（5 箇所） | 事前に計算した色リテラルにする | |
| `rem` / `em`（71 箇所） | px に換算する（1rem = 16px。例 `--slot-size: 3rem` は 48px） | |
| データ属性による状態（`[data-selected]` など 40 箇所） | USS クラス（例 `.slot--selected`）と C# の `EnableInClassList` | USS に属性セレクタは無い |
| CSS Modules の `composes`（12）、`:where()` の低詳細度共有クラス（`keyHintText`、`iconTextOutline*`） | 共有 USS のクラスを複数付ける | PoC で「CSS と同じく詳細度で負ける」ことを実測済み（`.root Label` が `.h1` に勝った） |
| `:global(.mantine-*)` の上書き（12） | 消える。代わりに既定ランタイムテーマを暗色化する共通 USS が要る（PoC で 146 行。`Client.UitkPoc/Resources/UitkPoc/PocControls.uss`） | |
| フォント `--font-ui`（NotoSans-Regular → NotoSansJP-Medium、CJK ロケールは JP を優先） | PanelTextSettings のフォールバック連鎖と、ロケールごとのルートクラス（`:root[data-locale]` の代わり） | 配信しているロケールは `english,japanese,german,korean`（`Localization/localization.csv` 1 行目）。**Web は KR フォントを持たず、ハングルは OS フォントへ落ちている**（推測。`index.css` に KR の @font-face が無い）。Unity には `Assets/Dependencies/Font/NotoSansKR-Medium.otf` と SDF があるので、UITK では明示的にフォールバックへ入れる |
| `font-synthesis: none` | `-unity-font-style: bold` を使わない（実フォントは単一ウェイト） | |
| `user-select: none`（ADR 0021） | UITK の Label は既定で選択できない（`selection.isSelectable` を明示したときだけ選択可）。既定の挙動がすでに裁定どおり | |
| アイコン上の文字の縁（ADR 0033 の `-webkit-text-stroke` と `paint-order`） | `-unity-text-outline-width/color` | SDF の縁は内側にも食い込むので、Web の「stroke を fill の下に描く」と太り方が変わる（推測。目視で見直す） |
| `text-shadow`（11） | USS の `text-shadow` | |

### 2.3 USS で書けないものの対処

表の「使用数」は `moorestech_web/webui/src` の CSS で数えた値。

| CSS の機能 | 使用数 | 主な使い手 | 対処 | 備考 |
|---|---|---|---|---|
| `mask-image` の 4 辺フェード（`mask-composite: intersect`） | 12（4 ファイル） | **GamePanel の default と hud の面**、skit 窓の上端、下端フェード | **頂点カラー付きメッシュ**で描く。`generateVisualContent` で 3×3 の 9 パッチを作り、外周の頂点だけ α=0 にすれば、固定長 `--panel-edge-fade` の直線フェードと同じになる | 推測（未 PoC）。画像アセットを作らないので「装飾の画像アセット化禁止」（§6）とも矛盾しない。代案は UI Shader Graph のマテリアル。URP 限定で、要素ごとに 1 バッチ増える（Unity 6.3 マニュアル「Get started with UI Shader Graph」） |
| `linear-gradient`（32 箇所・9 ファイル）、`radial-gradient` 1、`repeating-linear-gradient` 1 | | GamePanel の縦グラデ、FadeRule（両端が消える罫線）、主要アクションの青グラデ `--recipe-action-background`、削除モードの斜線帯、App のヴィネット | 2 停止点以上の直線グラデは帯状の頂点カラーメッシュで描く。斜線帯は Painter2D の多角形。ヴィネットは放射状のメッシュ | PoC は Painter2D の `FillGradient` とテクスチャ焼きで確認済み |
| `box-shadow`（19・10 ファイル） | | GamePanel の外周ぼかし、craft 枠の内周線（inset） | 内周線は border か入れ子の要素で描く。ぼかしは背面の板に `filter: blur`（6.3）をかけるか、9-slice の影テクスチャを実行時に生成する | PoC の判定は「そのまま可」 |
| `clip-path: polygon`（8・3 ファイル） | | craft の右下グリップ、下向きの三角、チュートリアルの祖先クリップ（`ancestorClip.ts`） | 三角は Painter2D。祖先クリップは UITK の `overflow: hidden` の階層で自然に切れる | ハイライトを別層に描くなら、ADR 0024 と同形で祖先の矩形を計算する必要がある |
| `z-index`（44・25 ファイル） | | | §2.2 の層 enum と兄弟順、`BringToFront` | |
| `gap` / `row-gap` / `column-gap`（77・22 ファイル） | | | 子のマージンで組む。末尾の子の余白は C# でクラスを付ける（`:last-child` が無いため） | `:first-child` は `blockInventory/train` の 1 箇所だけ。`:nth-child` は 0 |
| `display: grid` と `grid-template`（8 と 6） | | App の stage 3 列（gridArea）、`SlotGrid`（既定 9 列）、`RecipeBox` の `1fr auto 1fr`、ビルドメニュー、機械インベントリ | stage は固定幅トークンの flex 行。SlotGrid は固定のスロット寸法と flex-wrap。`1fr auto 1fr` は `flex-grow:1; flex-basis:0` の両脇で表せる | |
| コンテナクエリ（`cqw`、`container-type`。RecipeRow） | 4 | | `GeometryChangedEvent` で C# が寸法を出して `style` へ入れる | §4 の例外規定 |
| `@keyframes`（5）、`animation` | | 通知、脈動、ドラッグガイド | §3 | |
| `filter: drop-shadow`（5） | | ドラッグガイドの SVG、ワールドピンの矢印 | USS の filter に drop-shadow は無い（Unity 6.3「Built-in filters」）。黒い複製をずらして重ねるか、影を省く裁定を取る | |
| `::before` / `::after`（16・5 ファイル） | | GamePanel の面、罫線 | 子要素か `generateVisualContent` | |
| `:focus-visible`（9） | | ModeSwitch、PanelActionButton ほか | `:focus` で代用する | ゲームパッドやキーボードでのフォーカス移動は PoC でも未検証 |
| `position: fixed`（16・10 ファイル） | | Portal 系、通知 | 層コンテナ直下の absolute | |
| `overflow-wrap: anywhere` / `word-break`（6） | | 通知の長い文言 | `white-space: normal`。空白の無い長い英数字列がどう折り返されるかは未検証（推測） | |
| `text-overflow`（8） | | | `text-overflow: ellipsis` | 複数行の省略は PoC で「高さ固定と ellipsis で効く」と確認済み |
| `aspect-ratio` | 0 | | Unity 6.3 の USS にはある | |

### 2.4 PoC で分かった問題への対処

| 問題 | 影響範囲（実測） | 方針 |
|---|---|---|
| **Linear 色空間で半透明が薄く出る**（PoC: `#9c7b42` に黒 60% を重ねると Chrome `#3e311a` / UITK `#664f29`）。プロジェクトは Linear（`ProjectSettings.asset` の `m_ActiveColorSpace: 1`） | `rgb(… / x%)` と `rgba` が 67 箇所・14 ファイル。中核は `--surface-navy rgb(10 14 27 / 80%)`（全パネル、HUD 面、通知、研究ノード、ワールドピン）、`--tooltip-face 94%`、`--slot-ghost-opacity 0.35`、`--screen-vignette-edge 16%` | 3 案ある。(1) α を実機で見て決め直す（PoC の提言）。(2) パネルを sRGB の RenderTexture に描いて Gamma で合成する（推測。Unity フォーラムでは PanelSettings を Gamma にしても α の合成がずれるという報告があり、要 PoC）。(3) α だけ `^2.2` で近似補正する（フォーラムの回避策。背景色によって誤差が出る）。どれを採っても、トークン表には「Web の値」と「UITK の値」を別の列で持ち、Web の値を流用しない |
| **11〜12px の文字が Chrome より細い**（14px 以上はほぼ同等） | 12px 以下のトークンは `--fluid-amount-font-size 10px`、`--inventory-count-base-font-size 10.565px`（×1.1678＝12.3px）、`--challenge-hud-label` 12px、`--operation-hud-label` 12px、`--recipe-info-text-size` 12px。CSS 直書きは 10px が 4、11px が 1、12px が 8。Mantine の `size="xs"`（12px）が 4 | stage 座標の px は拡縮後に物理 px が増える。1920×1080 では ×1.5 なので、12px は 18px になる（計算値）。細さが目立つのは 720p 以下と、アイテム個数バッジ（縁取り付き）。PoC は 1920×1080 の Game ビューで計測しており、そのときの拡縮基準は不明。手は 3 つ。拡縮後の物理 px で比べ直す。最小フォントのトークンを 13px 程度に上げる。FontAsset 側の SDF の太さや縁を調整する。Advanced Text Generator は未検証 |
| 既定のランタイムテーマが明色 | ボタン、入力、ドロップダウン、スライダー、トグル、スクロールバー、ポップアップ | PoC の 146 行（`PocControls.uss`）を共通テーマ USS の初版にする。スライダーの入力部が高さ 0 になる癖への対処も含める |
| フォントの SDF | `FontDefinition.FromFont` はビットマップ描画で、拡大すると崩れる | `Assets/Dependencies/Font/*SDF*.asset` がすでにある。これを PanelTextSettings に登録するのが素直（推測）。PoC のように実行時に `FontAsset.CreateFontAsset` で作るのは、Mod のフォントを足すときだけでよい |
| 文中アイコン（SpriteAsset）が内部 API 頼み | アイテム名に添えるアイコン、マークダウン風の説明 | PoC の `TextAndInput/RuntimeSpriteAssetBuilder.cs`（69 行。リフレクション）。Unity を更新したら最初に壊れる箇所なので、構築と描画を確かめるテストを置く（PoC の提言） |
| 画面確認の道具 | uloop screenshot の rendering モードに UITK が写らない | `ScreenCapture.CaptureScreenshot` を使う。録画プレイテストの `PlaytestScreenshot.cs` とバグ報告の `ScreenFrameReader.cs`（`CaptureScreenshotIntoRenderTexture`）は ScreenCapture 系なので、UITK も写るはず（推測。要確認） |

### 2.5 `ref-webui-design` の扱い

- §1、§1.5、§2〜§7、§9 は原則なので UITK 版へ写す。変えるのは名前（`GamePanel` などの部品名）と実装手段だけで、語彙は残す。
- §0（mock-host・Vite・Playwright・cloudflared の実装フロー）と §10（mock の撮影による目視 QA）は丸ごと置き換える。置き換え先は、EditMode で部品を組んで `worldBound` と `resolvedStyle` を実測すること、ScreenCapture での撮影、そして古いフレームを見ない仕組み（§6.2）。
- `components.md` の §8 系（23 節）は、画面を移すたびに UITK 版の節へ書き直す。書き直していない節は「Web 版のまま」と分かるように、台帳と節の対応を持つ。

---

## 3. アニメーション基盤

### 3.1 現行 Web UI の演出一覧（通知・進捗・トーストを含む全件）

CSS の `@keyframes` と `animation`、JS の `requestAnimationFrame`・`setInterval` を全部 grep して挙げた（`moorestech_web/webui/src`）。

| 演出 | 中身 | 尺・数値 | 根拠 | UITK での手段 |
|---|---|---|---|---|
| 通知の入場 | 左から `--notification-shift` 12px のスライドとフェードイン | 160ms ease-out | `features/notification/style.module.css:41-89`、ADR 0016 | USS transition（translate、opacity）。追加直後に開始クラスを外す |
| 通知の退場 | 入場の逆再生。開始は「生存尺 − 退場尺」 | 200ms ease-in。生存 7000ms（`NOTIFICATION_DISPLAY_MS`） | 同上、`notificationStore.ts:26` | `schedule.Execute(...).ExecuteLater(7000-200)` でクラスを付け、`TransitionEndEvent` で外す |
| 通知の集約 | 同じアイテムの行へ個数を加算し、**寿命を再始動**する（`lifetimeEpoch + 1`） | | `notificationStore.ts:41`、`.decisions/2026-08-19-獲得通知の集約はWebUI側で行う.md` | 予約済みの退場スケジュールを取り消して張り直す。集約は表示側の状態なので、UITK の View モデルへ移す |
| 通知の積み替え | 補間しない（裁定） | | ADR 0016 | 不要 |
| チュートリアルの脈動 | `scale` 1→1.08→1（strong）と 1→1.03→1（subtle） | 1200ms ease-in-out、無限 | `app/tokens.css:430-440`、`tutorial/worldPin.module.css:57`、`keyControlHint.module.css:29`、`overlay/style.module.css:21`、ADR 0039 | **無限ループ**。USS transition では素直に書けない。往復を回す仕組みが要る（自作か LitMotion の Yoyo） |
| ドラッグガイドの矢印 | 4 キーフレーム（0/15/75/100%）で translate と opacity。drop-shadow 付き | 3200ms、無限 | `tutorial/overlay/style.module.css:35,45-50` | キーフレームを回す仕組みが要る。影の扱いは §2.3 |
| スキットの文字送り | `setInterval(intervalMs)` で表示文字数を増やす。初回と再接続は全文を即時表示 | C# の状態 `textReveal.intervalMs` | `features/skit/SkitPresentation.tsx:30-46` | `schedule.Every(intervalMs)`。部分文字列で表示すると行の折り返しが途中で変わる。残りを `<alpha=#00>` で透明にして配置を固定するか、裁定が要る（推測） |
| スキットの暗転 | 黒い面を出すか消すかだけ（Web はフェードしない） | | `features/skit/SkitTransition.tsx`、`skitTokens.css:69` | 表示の切り替え。旧 UITK の `SkitUI.ShowTransition` は `Time.deltaTime` でフェードしていた（`Client.Skit/UI/SkitUI.cs:62`）が、Web 化でやめている |
| クラフト長押しの進捗 | 矢印グリフの充填値を rAF で毎フレーム更新。transition は禁止 | | `features/recipe/logic/useHoldCraft.ts`、components.md §8.13 | 値を毎フレーム push して `MarkDirtyRepaint`。補間はしない |
| 採掘の進捗バー | `ui.progress` の topic 値。補間なし | | components.md §8.18 | 値の反映だけ |
| ツリービューのパン慣性 | rAF で速度を積分する | | `shared/treeView/viewport/usePanInertia.ts` | `schedule.Every(0)` で積分（PoC の `SpringDragCard.cs` と同形） |
| モーダル | 尺 0（演出なし） | | `features/modal/ModalHost.tsx:35`、SKILL §6 | 不要 |
| トースト（ToastHost） | Mantine `Notification` 素のまま。様式外の負債（ADR 0016 の棄却案、別 issue `moorestech-cgm`） | | | 発生源の多くは WS ブリッジの障害表示で、移行後は大半が消える（推測）。何を残すか（障害の表出先）を決めてから作る |
| ホバー | transition は 0 件 | | `grep transition` | USS の `:hover` だけ |

このほか、ホワイトリスト §6 に「装飾アニメーションは基本入れない」「テスト時に尺を 0 へ落とす抜け道は作らない」とある。これは UITK でも継ぐ前提で書いている。

### 3.2 UITK の素の手段（Unity 6.3）

| 手段 | できること | できないこと・罠（PoC で実測） |
|---|---|---|
| USS `transition` | opacity、translate、scale、rotate、色、寸法の補間。`TransitionRun/Start/End/CancelEvent` がある | `height: auto` は補間できない。現在の px に固定してから動かす。無限ループやキーフレームは無い。追加と同じフレームでクラスを外すと補間されない。PoC は 16ms 遅らせて回避していた（`Motion/ToastStack.cs:33`）が、`GeometryChangedEvent` を契機にするほうが確実（推測） |
| `experimental.animation`（ValueAnimation） | FLIP、任意の値の補間、完了コールバック | API が experimental。`schedule.Execute` は同じフレームのレイアウトより先に走るので、並べ替え直後の位置は取れない。`GeometryChangedEvent` の旧矩形と新矩形を使う（PoC `Motion/FlipReorderList.cs`） |
| `schedule.Execute().Every(0)` と自前の積分 | ばね、慣性、ループ | 時間源は自前で決める（下記） |

時間源について。AGENTS.md の「時間は `GameUpdater` のティックで測る」は**サーバーのゲームロジック**に対する規則で、クライアントの UI 演出には掛からない。ただし、ポーズ中も通知が動き続けるべきかは実時間か `timeScale` かで変わるので、明示しておくべきである。

### 3.3 導入候補の比較

| 候補 | UITK 対応 | ライセンス・入手 | テストの決定性 | 備考 |
|---|---|---|---|---|
| 自作の薄い層（`UiMotion`。100〜150 行の見込み） | 任意（style へ直接書く） | — | 時計を注入できるので、EditMode で時間を手で進められる | 要件は §3.1 の 3 系統だけ。`VisualElement.schedule` で駆動すれば detach 時に自動で止まる |
| **LitMotion** v2.0.2（2026-05-10）、AnnulusGames | `LitMotion.Extensions` に UITK 用の Bind が 25 種ある（`BindToStyleOpacity/Translate/Scale/Rotate/BackgroundColor/Color/Left/Top/Width/Height/FontSize…`、`BindToText`、`BindToProgressBar`）。`WithLoops(n, LoopType.Yoyo)` | MIT、git URL | `ManualMotionDispatcher`（docs/articles/ja/manual-motion-dispatcher.md）で手動で進められる。Editor 用 scheduler もある | 依存は `com.unity.burst ≥1.6`・`collections ≥1.5.1`・`mathematics ≥1.0.1`。lock に burst 1.8.23、collections、mathematics 1.3.2 がすでにある。作者は導入済みの `com.annulusgames.tween-playables` と同じ |
| PrimeTween v1.5.1（2026-10-21） | 1.0.7 で UITK に対応（ITransitionAnimations 相当）。1.1.14 で `Tween.VisualElementColor/BackgroundColor`、のちに `VisualElementOpacity` が入った。1.4.6 からは Unity 6.2+ で style の translate/rotate/scale を使う | 独自ライセンス。ゲームでの利用・改変・商用は可。ソースや tgz の再配布は不可。npm の scoped registry（`com.kyrylokuzyk`）から入れる | 手動で進める API は確認できなかった | ゼロアロケーションを謳う。最新版は 1 日前のリリースなので、入れるなら 2 週間以上前の版に固定する |
| DOTween（導入済み。`Assets/Plugins/Demigiant/DOTween`。カメラとスキットで 4 ファイルが使用） | UITK 専用のモジュールは無い。`DOTween.To(getter, setter)` で何でも補間できる | 導入済み | — | 系統を増やさずに済むが、UITK 向けの語彙が無い |

出典: `gh api repos/annulusgames/LitMotion`（README、`Runtime/Extensions/UIToolkit/LitMotionUIToolkitExtensions.cs`、package.json）、`gh api repos/KyryloKuzyk/PrimeTween`（README:72、changelog、LICENSE）、Unity 6.3 マニュアル（USS の対応プロパティ、カスタムプロパティ、ビルトインフィルタ、UI Shader Graph）。

### 3.4 推奨（判断材料として）

- 入退場とホバーは **USS transition を基本**にする（要件の大半を占める）。
- ループ・キーフレーム・ばねは**どちらか 1 系統だけ**を入れる。
  - 自作を推す理由: 要件が小さく、時計を注入して EditMode で決定的に検証できる。外部の版上げにも振り回されない。
  - LitMotion を推す理由: 手動ディスパッチャで決定性を確保でき、UITK の Bind がそろっていて、ライセンスが MIT。
  - PrimeTween は再配布の制限と手動ステップ API が未確認なので、優先度は下がる。
- 演出の尺と曲線は、Web と同じくトークン（USS 変数か C# 定数）に置く。ADR 0016 の「生存尺を単一の正として一方向に流す」もそのまま継ぐ。

---

## 4. Mod から UI を足す・差し替える要件

| 観点 | 現状 | 根拠 |
|---|---|---|
| Mod が UI のコードやマークアップを足す仕組み | **無い**。ゲームのクライアントには Mod の UI プラグインを読み込む経路が存在しない | `grep` しても `plugins` や plugin.json を読むクライアントのコードは無い |
| `moorestech_master/.../plugins/node-graph`（`dist/index.js` 129KB と `index.css`） | **マスタ編集ツール mooreseditor 用のプラグイン**（`@moorestech/mooreseditor-plugin-sdk`、`@xyflow/react`、Mantine を import）。ゲーム内の UI ではないので、移行の影響を受けない | `plugins/node-graph/plugin.json`、`dist/index.js` の先頭 |
| Mod のコード（DLL） | サーバー側だけ。`MoorestechServerModEntryPoint` を `Assembly.LoadFrom` で読む。クライアント側に UI 用の入口は無い | `moorestech_server/Assets/Scripts/Mod.Loader/ModsResource.cs:158-169` |
| CEF 採用時の動機 | 「UI が HTML/CSS/TS なら Unity を介さず Mod 側が UI を差し替えられる**土壌**」。将来像であり、実装されていない | `docs/webui/archive/cef-webui-plan.md:10,79` |
| **実在する Mod 向けの UI 契約 1: チュートリアルのアンカー ID** | challenges.json の `highLightAnchorId` / `fromAnchorId` / `toAnchorId` に、Web 側の実際のアンカー ID 文字列を直書きする。変換も検証もしない。誤設定は黙って素通りする | `VanillaSchema/challenges.yml:277-340`、`.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md`、`docs/webui/anchor-convention.md`。マスタの使用例は §1.5 |
| 契約 2: アイコン画像 | Mod の `assets/item/*.jpeg` と `assets/fluid/*.jpeg` を実行時に読む | `Client.Mod/Texture/ItemTextureLoader.cs` ほか。UITK の文中アイコンは PoC の実行時 SpriteAsset（内部 API）に依存する |
| 契約 3: ローカライズ | Mod 同梱の `localization/localization.csv`。キーは `<type>.<guid>.<field>`。いまは Web 側の辞書で解決している | ADR 0006（第 5 項「Web の Guid 辞書解決へ統一」）。移行後は C# の `Localize` が直接解決する。ADR 0006 の第 5 項の更新が要る |
| Mod に約束した安定参照 | ブロック位置と各種 GUID だけ。UI は約束していない | `.decisions/2026-09-17-modに約束する安定参照キーは位置とGUID類に限りBlockInstanceIdは含めない.md` |

**移行への含意:**

- **アンカー ID の文字列は事実上の Mod API になっている。** UITK でも同じ綴りで引けるアンカー登録が要る。手は二つで、ひとつは `VisualElement` に付ける登録の拡張メソッドと、ID から要素を引くレジストリを置くこと。もうひとつはクラス名で表すこと。改名すれば Mod が黙って壊れるが、その代償は 2026-08-19 の裁定で受け入れ済み。ここで ID を変えると、バニラのマスタ 9 件も同じ PR で直すことになる。
- 将来 Mod から UI を足す要求が出たら、UITK なら設計の選択肢が 2 つある。Mod の DLL から VisualElement を足す方法と、AssetBundle の UXML/USS を使う方法。今回の移行で前倒しする理由は見当たらない。

---

## 5. manifest に残る `com.singtaa.onejs` の扱い

| 項目 | 実測 |
|---|---|
| manifest / lock | `"com.singtaa.onejs": "https://github.com/Singtaa/OneJS.git#onejs-v3"`、lock の hash `d16ed851…`、package version 3.0.2 |
| コードからの参照 | **0 件**（`.cs`、`.asmdef`、シーン、Prefab を grep）。言及があるのは裁定文書と DeadMemberAudit の説明文だけ |
| 配布物に入るもの | `Plugins/Windows/x64/quickjs_unity.dll`（Win64 有効）、`Plugins/macOS/libquickjs_unity.dylib`（OSXUniversal 有効）。`Resources/OneJS/`（Consolas-Regular.ttf、画像 2 枚、svg、`QuickJSBootstrap.js.txt`、計 2.2MB。パッケージ内の Resources もビルドに入る）。`Runtime/Styling/ExCSS.Unity.dll` |
| Editor への副作用 | `[InitializeOnLoad]` が 4 本ある。`JSRunnerAutoWatch`（PlayMode 遷移のフック）、`OneJSEditorOverlay`、`TypeGeneratorService`（ドメインリロードのたびに全シーンの JSRunner を走査）、`JSPadEditor`。asmdef は 7 本（うちテスト系 4 本） |
| 置き場 | `moorestech_client/Library/PackageCache/com.singtaa.onejs@d16ed851ec34/`（22MB） |

**結論:** 裁定で不採用が決まっており、参照も無いので、**移行の第 0 弾として単独の PR で外す**のがよい。リスクは小さい。manifest と lock から 1 行ずつ消すだけ。確認するのは 3 点。コンパイルが通ること。ビルドに quickjs が入らなくなること（Windows の `Plugins/x86_64` に quickjs_unity.dll が無くなる）。他パッケージからの依存が無いこと（lock の `dependencies` は `{}`）。

---

## 6. 過去の移行計画の進め方と、失敗・学び

### 6.1 流用すべき型

| 型 | 中身 | 出典 |
|---|---|---|
| 唯一の入口文書と三点セット | `MIGRATION.md`（方針・完了の定義・Phase と依存）、`TODO.md`（チェックリスト。完了日と短 SHA を必ず書く）、`disposition.md`（旧台帳の全項目を「済・吸収先 Phase・除外（理由）」のどれかに分類する。「未分類は計画の欠陥」） | `docs/webui/MIGRATION.md`、`TODO.md` 冒頭の更新運用、`disposition.md` |
| 依存を 2 種に分ける | 着手依存（実装を始められるか）と、完了ゲート依存（Phase の完了判定に要るか） | `MIGRATION.md` §4 |
| ファイルの衝突で並列の Wave を切る | 共有ファイル（契約スキーマ、`App.tsx`、Binder、レジストリ、台帳）は 1 セッションが直列で所有する。並列の作業者は自分の feature の新規ファイルの中で閉じる | `MIGRATION.md` §4 の並列実行マップ |
| 1 画面の定型フロー | 「旧実装の実コードで実装漏れを確定 → データ契約を拡充 → ビュー実装 → 検証」の順 | `MIGRATION.md` §5 |
| 最初の PR から決定論ゲート | 全ファイルの分類を強制し、未分類ならテストを落とす。uGUI のトークン混入と、ルールの腐敗（削除やリネームへの追従漏れ）を検出する | `gate-audit-2026-07-18.md`、`Client.Tests/WebUi/Gate/WebUiGateAuditTest`、`.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` |
| 抽出 PR と削除 PR の 2 本立て | PR1 で論理状態を UI 非依存のモデルへ抽出する（挙動は変えない）。PR2 で残骸を一括削除する | ADR 0052、`.decisions/2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md` |
| 状態主権は Unity | `UIStateControl` が正。ビューは購読するだけ | `.decisions/2026-09-05-uGUIビューの論理状態はUnity側の純ロジッククラスへ抽出する.md` |
| 負債の Work Unit（WU）分割 | ファイルが衝突しない単位に束ね、推奨順を付ける。保留リストを「着手禁止」として明記する | `subagent-execution-plan-2026-07-18.md` |

### 6.2 起きた失敗・学びと、今回の対策

| 起きたこと | 根拠 | 今回の対策 |
|---|---|---|
| 状態機械（`UIStateEnum`）を軸に洗い出したため、状態の外にあるオーバーレイ `BackgroundSkitManager` を見落とした。再監査のエージェントも気づかなかった | `archive/ui-completeness-reaudit-plan.md:3-12` | 棚卸しの軸を 3 本にして突き合わせる。(a) `features/*` のディレクトリ、(b) `App.tsx` のマウント点と Portal 層、(c) C# の topic と action の登録（`WebUiGameBinder`）。状態の外にあるもの（常駐 HUD、Ctrl+U、BG スキット、カットシーンの退避、カーソル追従）は別の表で確認する（`gate-audit-2026-07-18.md` の表をそのまま流用できる） |
| 二重表示のゲート漏れ（ProgressBar）を監査で見つけた | `gate-audit-2026-07-18.md` | 共存期間は「画面ごとの所有者は Web か UITK のちょうど 1 つ」をルーティング表で持ち、それを C# テストで固定する |
| 見た目のパリティ目標（`parity_targets.py`、parity-check 47/47）が足かせになり、2026-08-17 に打ち切った | `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`、`subagent-execution-plan` の決定事項 3 | 画素の一致は目標にしない。一致させるのは機能とトークン（色・寸法・層）。Linear の合成と文字の太さはどうせ一致しない（§2.4） |
| 実機検証の残り（Windows 実機、IME、音声の同時再生、clean worktree の証跡）が 2026-07-18 から未チェックのまま滞留した | `TODO.md` の A1・A2・Phase D 最終検証が `[ ]` のまま | 実機が要る完了条件は、プレイテスト配布の工程（Windows 検証機、遠隔実行、Mac mini の Steam）に組み込む。チェックボックスで後回しにしない |
| ゲート監査テストが、分類を包括 1 本へ畳んだことで構造的に無効化された（全部緑のまま新規 uGUI を素通しする） | `.decisions/2026-09-08-…` | ゲートは「分類の粒度」と「禁止トークンの検査」を併用する。UITK 移行では、`moorestech_web/webui/src/features` への新規ファイル追加禁止（凍結）と、新規 uGUI 禁止を同じ形で守る |
| Phase 1 で恒久 true にしたゲート参照 26 箇所が、Phase 5 まで死に分岐として残った | `ugui-retirement-plan.md` Phase 1・5 | 共存用の切り替え（ルーティング表）の撤去を、完了の定義に明記する |
| Web ホストが起動に失敗すると UI がゼロになった。uGUI のフォールバックを撤去したことの副作用 | `ugui-retirement-plan.md:23-24`、`archive/2026-07-06-all-code-review-progress.md:24` | 共存期間に Web が落ちても、移行済みの UITK 画面は生き残る。この非対称を受入条件に書く |
| AI の自動修正ループが古いフレームに気づかず「完了」と報告した。CEF の「全ゼロ＝転送失敗」と決めつけて調査が空転した | `archive/cef-webui-plan.md:20,80`、`archive/cef-webui-tree2-render-investigation-2026-07-04.md:115-117` | UITK の画面確認では、撮影にフレーム番号か時刻の透かしを入れ、撮影対象が UITK を含むこと（ScreenCapture 系）を最初に確かめる。外部監査も挟む |
| 並行セッションが研究ツリーを改修していたため、負債解消の F5/F6 を保留した | `subagent-execution-plan` の「追加の保留」 | 移行期間は、Web 側への新機能追加を凍結するか、新機能は UITK にだけ実装する。いずれにせよ、新機能の行き先を最初に裁定する |
| Web の凍結（WU1〜9 の完了待ち）と移行の Phase 依存を明記したので、衝突を避けられた | `MIGRATION.md` §4 の並行運用ルール | 同じ書式で「UITK 基盤（テーマ、層、ツールチップ、D&D、SpriteAsset、アニメ、DSL）の完了まで画面移行を始めない」を着手依存として書く |
| ADR を伴う語彙の追加（副次ボタン、HUD 面）は、「様式が先、実装が後」で回した | ADR 0014/0015、SKILL 冒頭 | UITK 版でもホワイトリスト方式を保つ。基盤の部品（GamePanel、スロット、層）を最初に様式として確定させてから画面に入る |

---

## 7. この観点から裁定が要る論点

1. **遠隔実行の HTTP 待ち受けをどこに置くか**（Kestrel を最小で残すか、HttpListener に載せ替えるか）。完了の定義に直結する（§1.4）。
2. **Mac を Universal にするか**（CEF が消えた後、arm64 固定の根拠は ffmpeg だけになる）（§1.3）。
3. **MainMenu とローディングを今回の全面移行に含めるか**。uGUI のまま残した理由（CEF の起動位置）が消える（§1.3）。
4. **共存期間の出荷形**: master へ strangler で入れ、毎週の playtest に混在 UI を出すか。移行ブランチに溜めて一括で入れるか。前者なら、UITK を CEF の上に置くことと、D&D が絡む画面群を同じ波で移すことを決める（§1.5）。
5. **色と α の決め直し方**（実機で目視するか、sRGB の RT で合成するか、近似補正するか）と、**最小フォント寸法**（§2.4）。
6. **トークンの正本をどちらに置くか**（C# から USS を生成するか、USS を正本にして C# が読むか）（§2.2）。
7. **アニメーション**: 自作するか LitMotion か（§3.4）。ドラッグガイドの drop-shadow を残すか。
8. **OneJS の即時撤去**（第 0 弾の PR。裁定済みの不採用を実行するだけ）（§5）。

---

## 8. 根拠ファイル一覧

| 区分 | パス |
|---|---|
| 裁定・議論 | `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、`docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`、`docs/adr/0052-ugui-removal-scope-and-exceptions.md`、`docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md`、`docs/adr/0072-remote-exec-in-distributed-builds.md`、`docs/adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md`、`docs/adr/0013`〜`0021`（webui 系）、`.decisions/2026-09-05-*`（uGUI 撤去 7 本）、`.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` |
| ビルドと配布 | `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs`、`Bundlers/CefRuntimeBundler.cs`、`Bundlers/FfmpegRuntimeBundler.cs`、`Bundlers/RemoteExecHarmonyBundler.cs`、`MacPlayerArchitecture.cs`、`Editor/Cef/CefLfsPointer.cs`、`Editor/Cef/CefPackageLfsValidator.cs`、`Client.Build.Policy/BuildPurpose.cs`、`Client.WebUiHost/Editor/*`、`scripts/setup-cef.sh`、`scripts/playtest/lib/release-artifact.sh`、`scripts/playtest/README.md`、`.github/workflows/build.yml`、`.github/workflows/run_test.yml`、`.agents/skills/moores-playtest-release/SKILL.md` |
| 遠隔実行 | `Client.RemoteExec/RemoteExecEndpoint.cs`、`Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、`scripts/playtest/remote-exec.sh` |
| デザイン | `.agents/skills/ref-webui-design/SKILL.md`、`moorestech_web/webui/docs/design/components.md`、`moorestech_web/webui/src/app/{tokens,index,skitTokens,slotTokens}.css`、`App.tsx:31-55`、`src/shared/ui/GamePanel/style.module.css` |
| 演出 | `src/features/notification/{style.module.css,notificationStore.ts}`、`src/features/tutorial/overlay/style.module.css`、`src/features/skit/SkitPresentation.tsx`、`src/features/recipe/logic/useHoldCraft.ts`、`src/shared/treeView/viewport/usePanInertia.ts` |
| PoC | `docs/uitk-migration/poc/hard-cases/index.html`、ブランチ `poc/uitk-hard-cases` の `moorestech_client/Assets/Scripts/Client.UitkPoc/`（`Resources/UitkPoc/PocControls.uss`、`Motion/*`、`TextAndInput/RuntimeSpriteAssetBuilder.cs`、`Effects/*`、`Overlay/TooltipLayer.cs`） |
| Mod | `VanillaSchema/challenges.yml`、`moorestech_master/server_v8/mods/moorestechAlphaMod_8/{master/challenges.json,plugins/node-graph,assets}`、`moorestech_server/Assets/Scripts/Mod.Loader/ModsResource.cs` |
| 過去の移行 | `docs/webui/{MIGRATION,TODO,disposition,ugui-retirement-plan,gate-audit-2026-07-18,subagent-execution-plan-2026-07-18}.md`、`docs/webui/archive/{ui-completeness-reaudit-plan,cef-webui-plan,cef-webui-tree2-render-investigation-2026-07-04,2026-07-06-all-code-review-progress}.md` |
| 実測ログ | `~/hermes-agent/data/services/playtest/runs/playtest-20261021-2150/unity-build-{windows,mac}.log` |
| 外部 | Unity 6.3 マニュアル（USS の対応プロパティ、USS カスタムプロパティ、ビルトインフィルタ、UI Shader Graph の始め方）、github.com/annulusgames/LitMotion、github.com/KyryloKuzyk/PrimeTween、Unity Discussions「Recommended way to handle color spaces in UI-Toolkit」 |
