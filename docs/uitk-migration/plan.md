# UI Toolkit 全面移行 統合計画

- **2026-11-06 改訂: 束ごとの切替・毎週出荷をやめ、長命ブランチ `feature/uitk` で全部作って一気に切り替える。** 正本は `.decisions/2026-11-06-*` の UITK 関係 6 本と CEF 停止の記録 1 本（§0 の裁定表）。覆った裁定は §7.3。
- 2026-10-23 レビュー反映版（Fable 4 観点・Codex gpt-6-astra 2 観点）。**裁定ではない。** 作成時は repo を読んだだけで、コードには触れていない。
- 採点表・調査と草案の事実誤りの訂正・レビューでの訂正は `docs/uitk-migration/plan-notes.md` に分けた（計画の中身ではないため）。
- 数の確認: master HEAD `e8e611f1c0` で数え直した。本文で「実測」と書いた数がそれにあたる。規模・期間の数字はすべて推測。
- 入力: 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、議論 `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`、PoC 報告 `docs/uitk-migration/poc/hard-cases/index.html`・`docs/uitk-migration/poc/editor/index.html`（コードはブランチ `poc/uitk-hard-cases` の `de6c68621c`。master には無い）、調査 5 本 `docs/uitk-migration/research/understand-{screens,host,input,tests,shipping}.md`（以下 screens・host・input・tests・shipping）
- 表記: パスは repo 相対。`Scripts/` は `moorestech_client/Assets/Scripts/`、`web/` は `moorestech_web/webui/src/`。「推測」と書いた所は実測していない。

---

## 0. 判定と裁定

**判定: 長命ブランチ `feature/uitk` で全画面を作り、全部そろったら一気に切り替える。** 作りかけの UITK は `feature/uitk` に隔離し、master は完成まで Web だけにする。Web の変更は止めず、UITK 側が追いかける。切替の日に、全画面の UITK への切替と CEF・Web ホスト・`moorestech_web` の撤去を続けて入れる。切替前のビルドはどこにも配らない。段階 0 は master 済み、段階 1 は `feature/uitk` に入っている（2026-11-06 時点）。

**裁定の一覧**（2026-10-26 の裁定は選択肢と理由を §7.1 に残す。覆ったものは §7.3）

| # | 論点 | 裁定 | 帰結 |
|---|---|---|---|
| 裁定 1（旧 D1。2026-10-26。**2026-11-06 に覆った**） | 移行中の出荷形 | ~~束単位で暗転で作り、所有表の 1 行で切り替えて毎週出す~~ → 長命ブランチで作り一気に切り替える（下の「一気切替」） | 束ごとの切替・所有表・毎週の playtest 反映・1 周期後の削除 PR は無くなった |
| 裁定 2（旧 D2。2026-10-26） | 第一弾の画面 | 命綱（ポーズ・設定・バグ報告・トースト）。CEF の延命は §3.11 の最小限だけ。段階 1 は済んだ | ~~CEF が死んでも Esc でセーブと報告ができる~~ → **切替の日までテスターに届かない**（2026-11-06 の一気切替の帰結）。自動再起動は作らない |
| 裁定 3（旧 D6。2026-10-26 に (a)） | 遠隔実行の待受 | `HttpListener` へ載せ替える。配布ビルドで動かなければ遠隔実行専用の最小 Kestrel | **ADR 0072 と矛盾**。段階 0 で改訂した |
| 裁定 4（旧 D7。2026-10-26） | Web の扱い | UITK が安定稼働するまで Web の変更（不具合修正・新機能）を続け、凍結しない。移す範囲は現行 Web の全機能（`.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`） | 移す対象が移行中も動く。G1 は追従ゲート。2026-11-06 から、追従の記帳は `feature/uitk` 側で行う（§1.1・§4.7） |
| 確認（旧 D18。2026-10-26） | 時間の規則の読み方 | AGENTS.md の時間の規則はサーバーのゲームロジックの規則と読み、UI の演出は実時間で動かす | — |
| 様式化（旧 D14。2026-10-26。**2026-11-06 に覆った**） | Mantine の素の見た目の 4 画面（トースト・チャレンジ・列車 HUD・ポーズのボタン） | ~~移すときにホワイトリストの語彙で様式化する~~ → Web 版と同じ見た目にする（下の「見た目」） | 見た目は変えない。負債 `moorestech-cgm` は移行では閉じない |
| 一気切替（2026-11-06） | 置き場と切り替え方 | 長命ブランチに隔離し、全部そろったら一気に切り替える。master と配布物で画面ごとに UITK と Web が混ざる状態は作らない。デバッグで全体を切り替えられる程度の混在はよい。Web の新機能は止めない（`.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`） | 段階 1 の「CEF が死んでも Esc でセーブ・報告できる」は切替の日までテスターに届かない |
| 切替の日（2026-11-06） | 切替と撤去 | 切替の日に、全画面の UITK への切替と CEF・Web ホスト・`moorestech_web` の撤去を続けて入れる。Web へ戻すデバッグ切替は残さない。ブランチは `feature/uitk-stage1` から `feature/uitk` へ改名し、PR #1589 は閉じて切替の日に新しい PR を作る（`.decisions/2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`。名前 `feature/uitk` は agent 前提） | 切替の後に不具合が出たら、切替前のビルドへ戻すしかない |
| 全体スイッチと追従（2026-11-06） | 長命ブランチの中の切り替えと追従 | 画面別の所有表は使わず「全部 Web／全部 UITK」の 1 つのデバッグスイッチにする。UITK の間、未実装の画面は使えない。G1 と追従台帳は master から外し、master を取り込むたびに長命ブランチ側で記帳する。追従は未移植画面の実装より優先する。切替前のビルドは配らず（テスター・staging とも）、確認は Unity Editor の中だけ。切替 PR の中でだけ Windows/Mac ビルドと検証機を通す（`.decisions/2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`） | 段階 1 の所有表・委譲・Editor 限定の上書きは全体スイッチへ作り直す |
| 切り出しと取り込み（2026-11-06） | CEF 停止の記録・最終検証中の Web 更新・レーンのマージ | CEF 停止の記録（§3.11）は別 PR で先に master へ入れる。最終検証中に master へ Web の更新が入ったら取り込んで再検証する（切替の日が延びる）。レーンは moores-code-review を通して agent が `feature/uitk` へマージし、master への切替 PR はユーザーがマージする（`.decisions/2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md`） | テスターの UI 全停止の報告に、今から原因の値が載る |
| CEF の不具合（2026-11-06） | 切替までの CEF 由来の不具合 | 起動不能・データ消失を含めて直さない。CEF 停止の記録の切り出しはそのまま行う。CEF 由来でない Web 画面の不具合は従来どおり master で直す（`.decisions/2026-11-06-UITK切替までCEF由来の不具合は直さない.md`） | テスターは切替の日まで CEF の不具合を被り続ける（§6） |
| 見た目（2026-11-06） | UITK の画面の見た目 | 全段階の画面を Web 版と同じ見た目にする。無理に同じにする必要はなく、影響が小さく非本質的な対応が入るのであれば推奨案でもよい（例外はユーザー原文どおり「無理に同じにする必要はない、影響が小さく非本質的な対応が入るのであれば、推奨でも良い」）。ポーズのボタンに操作音は付けない。ref-uitk-design の「面は GamePanel だけ」も直す（`.decisions/2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`・`.decisions/2026-11-06-UITK段階2以降の画面もWeb版と同じ見た目にする.md`） | チャレンジ・列車 HUD も様式化しない |

ほかの論点は agent の既定として進め、台帳に記録する（§7.2）。

**段階 0 の中で裁定したもの（2026-10-26。一覧は §7.1）**: 旧 SkitUI の削除（0-12）、差し込み記法の名前付きへの統一（0-6）、Codex で進められない段階 0 の実装を Claude Code で進めること、UITK が主パネルを出しているあいだの Web の全面オフ（S15。2026-11-06 の全体スイッチで、UITK 側では常に満たされる形に読み替えた。§3.3）。

**何を移すか**
- CEF（cef-unity）と Web UI 一式を、C#・UXML・USS の UI Toolkit（以下 UITK）に置き換える
  - React の本番コード: TS/TSX 12.2k 行＋CSS 3.4k 行（テスト除く。features・shared・app・bridge。実測）
  - `Client.WebUiHost` 140 ファイル・10,565 行（実測）、Kestrel・WebSocket
  - 配布物の CEF（Windows 237 ファイル・約 368MB、Mac 252 ファイル・約 298MB。shipping §1.1）
- 作る単位は「束」。同時に表示され、掴みや D&D でつながる面の集まり。束は作る順と台帳の単位で、切り替える単位ではない。長命ブランチの中で描く系は、全体スイッチで「全部 Web」か「全部 UITK」の 1 つにする（§3.3）

**規模・期間（推測）**
- 合計 12〜16 週に、追従（master の Web 変更を UITK へ写す作業）と最終検証の取り込みのぶんが足される（足される量は Web の変更の量次第で、見積もれない）。段階 2 と 3 は `feature/uitk` の上で並走できる。律速は Editor の本数（§3.13）と追従
- `feature/uitk` へのレーンは 55〜70 本。master への PR は CEF 停止の記録と切替 PR の 2 本。UITK の新規コードは本体約 2.3〜3.2 万行（§1 の合計）・テスト約 1 万行。削除は約 45〜50k 行

---

## 1. 段階一覧

段階 0 は master、段階 1〜5 は長命ブランチ `feature/uitk` の上で作る（§1.1）。段階の区切りでは何も配らず、確かめるのは Unity Editor の中だけ。テスターに届くのは切替の日の 1 回だけで、Web のコードが消えるのもその日にまとめてになる（§2.6）。物差しは CEF 無し起動ラダー（§4.4）。「Web の `x`」は `web/features/x` を指す。

| 段階 | 中身 | Editor で確かめられること | 物差し | 規模・期間（推測） |
|---|---|---|---|---|
| 0 地ならしと実測（master 済み） | 遠隔実行を Kestrel から外す、状態を DI へ移す、OneJS と旧 SkitUI の撤去、差し込み記法、DSL の付け替え、台帳・参照キャプチャ・追従ゲート、実測 5 本 | Web ホストを起動しなくても、状態クラスと遠隔実行が生きている（配布物から quickjs が消えたのは段階 0 の master 反映による） | 同左 | 40〜60 ファイル。新規 1〜2k 行、移設 2〜3k 行、シナリオ約 40 本。1.5〜2 週 |
| 1 基盤と命綱（`feature/uitk` 済み。全体スイッチへの作り直しが残る） | UITK の基盤、ポーズ・設定・バグ報告・トースト、CEF 停止の記録（master へ切り出す。§3.11） | UITK 側で、Web ホスト無しに Esc でセーブ・設定・バグ報告ができる | `uitk-lifeline` | 基盤 60〜90 ファイル（C# 4〜5.5k 行）、画面 25〜35 ファイル（1.5〜2.5k 行）。2.5〜3 週 |
| 2 常駐表示 | 入力の無い HUD 9 種、通知、ツールチップ、ワールドピン、チュートリアル枠、スキット | UITK 側で、ゲーム画面とストーリーが Web 無しで描ける | `uitk-opening-hud` | 70〜90 ファイル、3.5〜4.5k 行。1.5〜2 週 |
| 3 建築の束 | ビルドメニュー、ホットバー、BP 名入力モーダル | UITK 側で、ビルドメニューから選んで建てられる | `uitk-build-bp` | 40〜55 ファイル、3〜4k 行。1〜1.5 週（段階 2 と並走） |
| 4 持ち物の束 | インベントリ・装備・掴み・レシピ・ブロック UI 全族（列車を含む）・研究 | UITK 側で、生産ループ全体が Web 無しで回る | `uitk-production-loop`、`uitk-research-train` | 100〜150 ファイル、8〜11k 行、テスト 300〜400 件。3〜4 週 |
| 5 最後の束と切替の日 | チャレンジ一覧・出展モードの言語ゲート・Web の横断層。続けて切替 PR で、全画面の UITK への切替と CEF・Web の撤去を続けて入れる（§2.6） | 全画面が UITK でそろう。切替 PR の中でだけ、CEF の無い Windows/Mac ビルドを検証機で通す | `uitk-challenge-gate`。切替 PR でラダー 6 本が CEF の存在しないビルドで通る | 最後の束 25〜40 ファイル・1.5〜2.5k 行、撤去 5 区分（§2.6）。1 週＋最終検証 1〜1.5 週（取り込みで延びる） |

### 1.1 長命ブランチの運用

| 項目 | 中身 | 出所 |
|---|---|---|
| ブランチ | `feature/uitk`（`feature/uitk-stage1` を改名。段階 1 はここに入っている）。PR #1589 は閉じ、切替の日に新しい PR を作る | 裁定（名前は agent 前提） |
| レーン | 各レーンは `feature/uitk` から切った worktree で作り、moores-code-review を通して agent が `feature/uitk` へマージコミットで取り込む。master への PR は作らない | 裁定 |
| master の取り込み | `feature/uitk` へ master をマージコミットで取り込む。頻度は **週 1 回と、各レーンを `feature/uitk` から切る直前**（レーンが古い Web を写さないため） | agent 既定 |
| 束 1 の暗転戻し | 台帳の束 1 は `切替済み` から `暗転` へ戻す作業を、この文書を `feature/uitk` へ取り込む最初の取り込み PR で行う（作業の詳細は §2.2 作業（画面）の 2 行目）。取り込み担当は最初の取り込みの前に必ず読む | 裁定の帰結 |
| 取り込みのたびの記帳 | 取り込みは `feature/uitk` への PR（またはローカルで G1 のスクリプト `.github/scripts/check-uitk-web-follow.py --base <取り込み前の feature/uitk> --head <取り込み後>`）で行い、その回に入った Web の差分を台帳 `docs/uitk/TODO.md` の「## 追従記録」へ `写した`／`捨てた（理由）` で記帳する。G1 は `feature/uitk` の中では未記帳の件数を出すだけで赤にしないので、取り込みの PR では件数が 0 であることを見てからマージする（§4.7） | 記帳は裁定。件数 0 を見てマージするのは agent 既定 |
| 追従の優先 | 移し終えた画面の追従（「写した」の実装）を、未移植画面の実装より先にする。両立しなければ残り画面の完成を延ばす | 裁定 |
| 全体スイッチ | 長命ブランチの中は「全部 Web／全部 UITK」の 1 つのデバッグスイッチで切り替える（§3.3） | 裁定 |
| 配らない | 切替前の `feature/uitk` のビルドは、テスターにも staging にも配らない。確かめるのは Unity Editor の中だけ | 裁定 |
| CEF 停止の記録 | 長命ブランチから切り出し、別 PR で先に master へ入れる（§3.11） | 裁定 |
| 切替 PR | 全画面がそろい、G1 の未記帳が全束で 0 になったら、`feature/uitk` から master へ切替 PR を作る。中身は全画面の UITK への切替と CEF・Web の撤去（§2.6）。PR の中で Windows/Mac ビルドを回し、Windows 検証機・Mac の Steam・`test-release-playtest-mac.sh` を通す。独立レビューを付け、マージはユーザーが行う | 裁定（独立レビューは §3.13 の agent 既定） |
| 最終検証中の取り込み | 切替 PR の検証中に master へ Web の更新が入ったら、取り込んで追従し、影響箇所を再検証してから切り替える。切替の日はその分延びる | 裁定 |
| 戻し | 切替の後に不具合が出たら、切替前のビルドへ戻すしかない（Web へ戻すデバッグ切替は残さない）。戻しの手順は §7.4 | 裁定 |

---

## 2. 各段階の詳細

### 2.0 全段階の共通条件

各段階の終わりに、次の 3 つを満たす。

各段階の終わりに、`feature/uitk` の Unity Editor の中で次の 4 つを満たす。配布ビルドは作らない。

1. ラダーが、前段ぶんも含めて、全体スイッチを UITK にした設定（Web ホストを起動しない）で緑（§4.4）。
2. 録画シナリオが緑。移した面に触るシナリオは、UITK 側で DSL の UITK ロケータで解決している（ログで確かめる）。全体スイッチを Web にした設定でも、同じシナリオが段階の前と同じ合否である（Web 側を壊していない）。
   - 全本を流すのは 3 回だけ: 段階 1 の終わり、段階 4 の終わり、切替 PR。ほかの段は「移した面に触るシナリオ＋ラダー」
   - 対象外は壊れている 10 本（bd `moorestech-wwxy` の本文 8 本＋NOTES の 2 本）
3. Unity の Error ログが 0。
4. G1 の未記帳がその段の束で 0（§1.1。追従を優先するため）。

配布は切替の日の 1 回だけ。切替 PR がマージされたら、既存の工程（moores-playtest-release）で playtest へ反映し、次の 2 つを足す。
- Mac の確認項目を、ラダー 6 本の先頭の操作のスクショに置き換える（Esc でポーズ、採掘ゲージと通知、B でビルドメニュー、Tab でインベントリ、研究ツリー、チャレンジ一覧）。Windows 検証機の通し検証は UI を開かないため（`Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeRunner.cs:67-75`）
- パッチノートに、全画面が UITK に移ったことと既知の差分を書く。受け口の無人修正（bug-report-auto-fix）が既知の差分を拾わないようにするため

オーナーの目視は 1 回、切替 PR の検証ビルドで、マージの前に行う（agent 既定。段階の区切りでの staging の目視は棄却された）。

### 2.1 段階 0 地ならしと実測（見た目は不変）

**目的**: 挙動を変えずに、後回しにすると全段階を塞ぐものを片付ける。段階 1 の前提を実測で確定する（§4.8 の 5 本）。並列作業の土台（台帳・参照キャプチャ・追従ゲート・bd）を置く。

**出し方**: 各レーンのブランチを統合ブランチ `feature/uitk-stage0` へマージコミットで取り込み、PR は 1 本にする（2026-10-26 裁定）。レーンは PR を作らない。

| # | 作業 | 中身と根拠 |
|---|---|---|
| 0-1 | 使っていないパッケージを外す | `com.singtaa.onejs`（manifest 13 行目）と `com.annulusgames.debug-ui`（3 行目）を manifest と lock から外す。どちらも参照は 0 件。OneJS は配布物に quickjs と Resources 2.2MB を入れている（shipping §5） |
| 0-2 | 実測 5 本（§4.8） | 使い捨ての隔離 worktree で行う。PoC ブランチを土台にしてよい。1 本 1 日で打ち切り、「不可」も結果として記録する。master へ入れるのは結論と、UI Test Framework の manifest 追加だけ |
| 0-3 | 遠隔実行を Kestrel から切り離す（裁定 3） | 下の「0-3」 |
| 0-4 | 状態を Hub の外へ移す（抽出 PR。挙動は変えない） | 下の「0-4」 |
| 0-5 | `UIStateControl.OnStateChanged` を UniRx にする | 今は `event Action<UIStateEnum>`（`Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs:16`）。AGENTS.md「イベント発火に Action を使わない」に反している |
| 0-6 | 差し込み記法を名前付きに統一する（2026-10-26 裁定） | 下の「0-6」 |
| 0-7 | DSL を描画系に依存しない形にする（この段は Web 実装だけ） | 下の「0-7」 |
| 0-8 | 台帳・参照キャプチャ | 下の「0-8」 |
| 0-9 | 追従ゲート（裁定 4） | G1（§4.7）を段階 0 の最初の PR で入れる。Web の変更は止めない。新しい Web 画面が入ったら台帳のその束の行に足す（例: Tier9 の #1551・#1552 が足す `blockInventory/details/{rocket,spaceElevator}` は段階 4 の特殊族）。2026-11-06 から G1 は `feature/uitk` への PR と切替 PR だけで走り、行の追加と記帳は `feature/uitk` 側で行う（§1.1・§4.7） |
| 0-10 | Web ホストを起動しない Editor 限定スイッチ | `#if UNITY_EDITOR` で囲み、ファイル末尾に置く。Hub が null の経路（起動失敗と同じ形）を PlayMode で再現する。ラダー（§4.4）の土台。長命ブランチの全体スイッチ（§3.3）の UITK 側はこのスイッチを使う |
| 0-11 | bd | epic を立て、段階と単位を子にする。派生は `--parent` で系譜を残す |
| 0-12 | 旧 UITK の SkitUI を消す（純削除。2026-10-26 裁定） | `SkitUI.prefab`（UIDocument・`SkitPanelSettings`）、`Scripts/Client.Skit/UI/SkitUI.cs`、`SkitPresentationMode` を消す（bd `moorestech-lnsf.8`）。`SkitManager.cs:197` が true に固定しているので、非 Web の分岐は死にコード。prefab とシーンの参照は uloop で外す。段階 1 からパネルが 1 枚になる |

**0-3 遠隔実行**
- `/api/remote-exec` は Web UI の Kestrel に相乗りしている（`Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、ADR 0072）。Web ホストの起動に失敗すると遠隔実行も開かず（`WebUiStartup.cs:19`）、検証機で CEF の不具合を調べる手段まで失う
- `RemoteExecEndpoint` を `HttpContext` に依存しない形にして裁定 3 の待受に載せ、起動の Web 依存（`InitializeScenePipeline.cs:70-73` の `KestrelPort` 受け渡し）を外す。`RemoteExecEndpointTest` と `PlaytestBootUnityCallbackBindingTest` を直し、`scripts/playtest/remote-exec.sh` は `access.json` 経由のまま使えるようにする。ADR 0072 の改訂を同じ PR に入れる
- 配布ビルドは Mono で、`managedStrippingLevel` は Standalone 未設定（既定）。この設定で `HttpListener` が動かなければ、遠隔実行専用の最小 Kestrel にする

**0-4 状態の移設**
- 対象: `ResearchTopic._nodeStates`、`ChallengeTopicState`、`BlockNetworkInfoCache`、`BlockInventoryTopic.TrackBlock` の監視判断、`OpenTrainTimetableTracker`・`TrainTimetableFetcher`、`NotificationTopic` の連番と移行告知、`WebUiModalService`（static をやめ、`Client.Ui.State` の DI サービスにする）、`BlueprintNameInputWebBridge` の状態部分、`EventLanguageGate`
- 移す先は `Client.Ui.State` アセンブリ（`Client.Game` を参照し、`Client.Game` からは参照されない）の型で、登録は MainGameStarter の LifetimeScope。今はこれらが `WebUiGameBinder.Bind()` の中でしか作られず、Hub が null だと丸ごと作られない（`Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:48-55`）。置き場の裁定は `.decisions/2026-10-29-UIの画面状態はClient.Gameに置かず専用アセンブリClient.Ui.Stateに置く.md`
- 移した topic は「型付きの購読元」と「JSON アダプタ」に分ける（§3.2）。前例は ADR 0052 の「抽出 PR と削除 PR の 2 本」

**0-6 差し込み記法**
- `LocalizationTextInterpolator` は今 `{pN}` だけを埋める。名前付き（44 キー・370 か所。実測）は Web の `i18nStore.ts` が解いているので、C# の補間器を名前付きに対応させる（必須）
- 位置指定 `{pN}` の 32 キー（全言語列で 190 か所。実測）は、同じ PR で名前付きへ機械的に書き換える。C# の呼び出し元（`TooltipLine.cs`・`ConnectServer.cs`・`RemoteConnectionDenyReason.cs`・`LoadingProgressLog.cs` など）と `Localize.GetFormatted` の位置指定シグネチャも名前付きへ改める
- `LocalizationPlaceholderParityTest` は残す。型付きの書式メソッドの生成器は作らない（§7.3 の 08-26 裁定）。mod 同梱の CSV に差し込みは 0 件で、mod への波及は無い（実測: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`）

**0-7 DSL**
- `IUiElementLocator` を入れる。Find（可視・矩形・文字列・状態フラグ）と `SetText(testid, text)` を持つ。`SetText` は、UITK では `TextField.value` を設定して ChangeEvent を流す（IME は通さない）。今の DSL に文字を打つ口は無く（`Scripts/Client.Playtest/Operations/PlaytestWebUiOps.cs:28-83`・`Scripts/Client.Playtest/Input/SemanticInput.cs:35-61`）、既存シナリオは state への直書きで回避している（`blueprint-copy-paste-via-ui.cs:2`）
- `ClickWebUi` 系を `ClickUi` 系へ一度に改名し、旧名は残さない。対象は約 40 本（式で確定。数え方: `grep -rlE 'WebUi|DomQuery|Cef' .agents/skills/unity-playmode-recorded-playtest/scenarios --include='*.cs'`）
- `PlaytestDomQuery` の直接呼び出しは、ロケータの照会 API に置き換える。`data-selected` などの属性は「状態フラグ」として抽象化する（Web は属性、UITK はクラス）
- スキル文書 4 本（`write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`）も同じ PR で直す

**0-8 台帳・参照キャプチャ**
- 台帳は `docs/uitk/TODO.md` 1 枚。行は `web/features` の 20 機能と、`web/app/App.tsx` のオーバーレイ数行（背景スキット・Ctrl+U・カットシーン中の退避・カーソル追従など、状態機械の外にあるもの。前回は状態機械だけで洗い出して背景スキットを見落とした。shipping §6.2）。agent の既定と実測の結論もここに書く
- 写さないテストの理由は `docs/uitk/disposition.md` 1 枚に書く（§4.1）。旧 `docs/webui/MIGRATION.md` は archive への案内にする。所有表の初版（全部 Web）もここで置く
- 参照キャプチャ: Web 版の全画面と主要な状態を撮る（Playwright の mock-host と PlayMode）。通知・脈動・ドラッグガイドは短い動画で残す。置き場は repo の外（`../moorestech_logs` か share）で、台帳からリンクする。束を消した後も「意図した見た目」を辿れるようにするため

**完了条件**
- Windows のビルドログで、`Plugins/x86_64` に `quickjs_unity.dll` が無い。それ以外の差分が無い
- Windows 検証機の配布ビルドで、`--remoteExec` 付きなら `remote-exec.sh` が `Succeeded` を返し、付けなければ待受が開かず、Web ホストの起動を失敗させても遠隔実行は開く（`HttpListener` が配布ビルドで動くかの確認を兼ねる）
- Editor 限定スイッチで Web ホストを起動しない PlayMode でも、研究・チャレンジ・ブロック詳細・時刻表・モーダル・通知の状態が DI から取れる（EditModeInPlayingTest）
- 改名したシナリオの合否が、改名前と同じ
- 実測 5 本の報告が証拠（ログの行・スクショ・数値）付きでそろっている。成立しないものがあれば、段階 1 に入らず裁定に上げる
- G1 が、暗転の起点より後の未記帳の Web 変更と、切替済みの束の Web 変更のそれぞれで赤になる

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 移設で topic の配信タイミングが変わる | 抽出 PR は「移動と配線」だけにする。既存の C# テスト（ビューモデル 90・アクション 36・状態ストア 28）と、移した topic に触る録画で回帰を見る |
| 遠隔実行の載せ替えで、検証機の運用（以降の全段階の検証路）が止まる | 旧経路と新経路の切替を 1 PR にまとめ、検証機で通してから master へ入れる |
| DSL の改名でシナリオが壊れる | 機械的な置換に限る。照会 API への置き換えは 1 本ずつ合否を突き合わせる |

**規模と並列（推測）**: 同時に走らせるのは Editor 要 2 本＋Editor 不要 2 本（§3.13 の勘定）。Editor の要る作業は 3 レーン（遠隔実行／状態の移設と 0-5／DSL）と実測 1 本（S1・S2・S3・S15 を順に）で、0-6 と 0-12 は先に空いたレーンが持つ。Editor の要らない作業は OneJS・台帳・追従ゲート・参照キャプチャの Web 側。S9（IME）は Editor 不要の自動操作（§4.9）。

### 2.2 段階 1 基盤と命綱

**状態（2026-11-06）**: 段階 1 は `feature/uitk`（旧 `feature/uitk-stage1`）に入っている。束ごとの切替を前提に作ったルータ・所有表・Web への委譲・旧 R1 の Web 全面オフ・Editor 限定の上書き（旧 R4）は、全体スイッチ（§3.3）へ作り直す。これは段階 2 の前の作業にする（下の「段階 2 の前の作り直し」）。CEF 停止の記録は master へ切り出す（§3.11）。

**目的**: 全画面が乗る基盤を作る。ポーズ一式を UITK に移し、UITK 側で Web ホストが起動しなくても Esc でセーブ・セーブして終了・設定・バグ報告ができる状態を作る。CEF が止まったことを開発者に届ける（§3.11）。

**命綱を第一弾にした理由**（裁定 2）と、その価値が切替の日まで届かないこと
- 2026-11-06 の一気切替の帰結として、「CEF が死んでも Esc でセーブ・報告できる」は切替の日までテスターに届かない。段階 1 を先にした理由のうち今も効くのは、IME・ESC・フォーカス・ローカライズ・フォント・モーダル層の難所を掴み抜きで最初に通せたこと。テスターの実害への手当ては、master へ切り出す CEF 停止の記録（原因の値が報告に載る）だけになる
- 以下は 2026-10-26 時点の理由
- テスターの実害が「UI がまるごと死ぬ・報告もできない」に集まっている（実測。Discord ミラー）
  - 6 時間放置で cef-unity のプロセスが消え、Tab・Esc・T・R と施設の UI が一切開かなくなった（2026-10-02 https://discord.com/channels/888315641709330433/1555576468828987532 ）
  - helper のメモリが伸び続け、約 14,600MB 使っていたところで UI がフリーズした。Esc の連打だけで 51MB → 170MB に伸びる（https://discord.com/channels/888315641709330433/1554924554697187389 ）
- 束 D（ポーズ・設定・トースト）は掴みも D&D も持たず、単独で切り出せる（screens §7.1）。ポーズのアンカー（`pause.*`）は、マスタからの参照が v8 で 0 件（実測）
- IME・ESC・フォーカス・ローカライズ・フォント・モーダル層の難所を、掴み抜きで最初に通せる

**対象**: PauseMenuPanel（トップ、サブページ、切断の表示、入れ子のポーズ〈スキット中 skitPause・乗車中 trainPause。ADR 0035〉）、設定 3 種（画質のプリセットと個別設定、言語、ダッシュ方式）、バグ報告フォーム（種別・本文・送信・配送状況の 1 行・下書き）、トースト層（バグ報告の結果と、良性でないコマンド拒否の分。通信失敗のトーストは Web が残る間 Web に残る）、CEF 停止の記録（§3.11）。

**作業（基盤。設計は §3）**
- UI ルート: `GameUiRoot`（UIDocument 1 枚）を MainGameUI.prefab に事前配置する（`uloop execute-dynamic-code`）。PanelSettings は Scale With Screen Size、基準 1280×720、Expand。層 enum と層コンテナ（`PickingMode.Ignore` を既定にする）
- ルータと所有表（作った形）: SCREEN_TRAITS の C# 版、所有表、Web への委譲と全面オフ、Editor 限定の上書き。全体スイッチへ作り直す（下の「段階 2 の前の作り直し」）
- 入力（作った形）: `UiPointerHitTest` に Pick を論理和で足す（呼び出し側の 23 ファイル・28 か所は変えない。実測）。UITK が主パネルを出しているあいだは Web の値を読まない。全体スイッチでは、スイッチの系だけを読む形へ作り直す（§3.8）。`WebUiInputExclusivity` を `UiInputExclusivity` へ改名して汎用化する。入力欄以外は `focusable=false`
  - ESC ラッチ。レーンの先頭で、TextField の上の ESC を EventSystem と `UIStateControl.Update` のどちらが先に処理するかを確かめ（S5）、置き場を決める
  - UI 専用のアクション資産で、MainGame の `DefaultInputActions`（`Assets/Scenes/Game/MainGame.unity:8435`）を uloop で差し替える。レーンの先頭で、資産で WASD・矢印の Navigate、Space・Enter の Submit を止められるかを確かめ（S4）、止められなければルートで NavigationSubmitEvent を打ち消す
- テーマ: レーンの先頭で、Linear 色空間の半透明 3 案（実機で決め直す・α の近似補正・sRGB の RT で合成）と、10〜13px の文字（720p・1080p・4K）を実機で比べ（S7）、§7.2 の既定を確かめる。`Tokens.uss` に全トークン約 238 個を機械的に移す。`Theme.uss` は PoC の `PocControls.uss`（146 行、PoC ブランチ）を初版にする
- フォント: TextCore の SDF FontAsset（JP・EN・KR）とフォールバック。レーンの先頭で、Editor で作ってコミットできるかを確かめ（S10）、できなければ PoC と同じく実行時に生成する
- UiMotion（§3.6）。段階 3 の smooth scroll と段階 4 の研究ツリーの慣性も使う
- ローカライズ: `LocalizedLabel`、InvariantCulture の数値書式、UXML の生文字の禁止
- アンカー: `AnchorIds` を C# の唯一の正本にする（`web/shared/tutorialAnchor/anchorIds.ts` から移す）。`TutorialAnchor.Mark` と登録簿も作る。枠の描画は段階 2
- テストの土台: `UiTestHost`（本番のテーマ・フォント・1280×720 を当てる）、`FakeXxxModel` と `RecordingXxxCommands` の雛形、L2 の規約テスト。最初の L1 テストを既存 CI に載せる（S13）
- DSL は `IUiElementLocator` の UITK 版（§4.4）。スキルとレビューは `ref-uitk-design` と moores-code-review の UITK 観点の初版

**作業（画面）**
- `PauseMenuState` と `PauseMenuStateService` は C# のまま使い、ビューと Presenter だけを作る。`pause_menu.*` と `bug_report.submit` のハンドラを、型付きコマンドと JSON アダプタに分ける（host §3）
- 台帳の束 1 の `切替済み` から `暗転` への戻し（束 1 の行の所有者も `Web` に戻す。起点 `979725bcd1` はそのまま）は、この文書を `feature/uitk` へ取り込む最初の取り込み PR で行う（作り直しより前に）。`切替済み` のままだと、master の取り込みで入るポーズ系の Web の変更を G1 が落とす（§4.7）
- 作った形では、ポーズ系の状態（入れ子の列車中・スキット中を含む）で Web を全面的にオフにした。全体スイッチでは UITK 側で Web ホストを起動しないので、この仕組みは要らなくなる（作り直しで外す）
- 見た目は Web 版と同じにする（`.decisions/2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`）。枠は GamePanel の様式（題の横線・下端の三角・半透明）ではなく Web の枠（不透明に近い暗色・角丸 12px・大きい太字の題・節見出し）にし、トーストも Mantine Notification の見た目（暗色の角丸の小窓・丸い色帯）に合わせる。参照は `moorestech_logs/uitk/reference-captures/web-a9d5123537/1-*.png`。ポーズのボタンに操作音は付けない
- GamePanel の初出で、720p と 21:9 の 2 解像度を 1 回撮る（§4.6）
- Web の `features/pauseMenu`・`features/settings`・テスト・`App.tsx` のマウントは、長命ブランチの中では消さない（全体スイッチの Web 側で使う）。切替の日にまとめて消す（§2.6）

**段階 2 の前の作り直し**（全体スイッチ。integrator が直列で持つ。独立レビューを付ける。§3.13）
- 画面別の所有表・Web への委譲・`ui_state.current` の委譲の印・旧 R1 の Web 全面オフ（休止と解除時の送り直し）・Editor 限定の上書き `UiOwnershipEditorOverride` を、全体スイッチ 1 つ（§3.3）に置き換える
- UITK 側は 0-10 の「Web ホストを起動しない」経路に載せ、Web 側は UITK のルートを出さない
- 入力の裁き（`UiPointerHitTest`・`UiInputExclusivity`）を、スイッチの系だけを読む形にする（§3.8）
- 段階 1 で Web 側に入れた休止の仕組み（台帳の追従記録 a97575e5b5・1095cab8bf・ccb3d512f7 で写した旧 R1 の Web 側の変更）は master に無い（実測: 4 コミットとも origin/master の祖先でない）。`feature/uitk` の Web 側から外し、CEF 停止の記録を切り出した後の master と同じ Web に戻す（agent 既定。全体スイッチの Web 側を master と同じものにし、取り込みの衝突も減らすため）
- ラダー `uitk-lifeline` と入れ子ポーズの録画を、作り直した UITK 側で流し直す。Web 側でポーズの既存シナリオが master と同じ合否になることも見る

**完了条件**（段階 1 として満たしたもの。作り直しの後に流し直す）
- 共通条件（§2.0）を満たす。ラダー 1 `uitk-lifeline`（§4.4）が緑。本文は SetText で入れ、IME は §4.9 の合成キーで確かめる
- 報告バンドルの manifest に、CEF の生の値 3 つ（§3.11）が入っている。最後の WS 受信から閾値を超えたとき、入力排他の Web 側の値が無視される（L0）。この 2 つは master へ切り出す PR の完了条件になる
- IME の確認（Windows と Mac。§4.9 の合成キー）の記録がある。バグ報告の本文（複数行）で、変換・確定・取消と、変換中の ESC で画面が閉じないこと、S9 で見つかった要件（§4.9）を満たすことを見る
- 入れ子ポーズの 2 経路（スキット中・列車中）の録画がある。最初の L1 テストが既存 CI で通る（S13）
- 作り直しの後: UITK 側で Web ホストが起動していない（CEF の RawImage が無く、WS の接続が 0）。Web 側で UITK のルートが出ていない

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| ESC が二重に処理される | ESC は C# の UIState に一本化する。全体スイッチでは片方の系しか動かない。ESC ラッチを L1 で固定する |
| 作り直しで段階 1 の検証が無効になる | 作り直しの後に、ラダーと入れ子ポーズの録画・IME の L1 を流し直す |
| 言語切替で再描画が漏れる | UITK 側と Web 側のそれぞれで録画を流す |
| 基盤を作り込みすぎて段階 1 が長引く | 部品は「その段で使うもの」だけを作る（§3.4）。例外は全画面が使う基盤（層・入力・テーマ・i18n・アンカーの登録簿）と UiMotion |

**規模と並列（推測）**
- 基盤 60〜90 ファイル（C# 4〜5.5k 行、USS 0.8〜1.2k 行）、画面 25〜35 ファイル（1.5〜2.5k 行）、テスト 120〜180 件。作り直しは減らす方向の変更が主で、0.5〜1 週
- 共有ファイル（層 enum・全体スイッチ・composition root・Tokens.uss・AnchorIds・DSL のロケータ本体）は integrator 1 人が直列で持つ

### 2.3 段階 2 常駐表示（画面を開いていない時間）

**目的**: テスターが一番長く見ている「ゲーム画面」と「ストーリー」を UITK で作る。完了時、全体スイッチの UITK 側で、GameScreen・PlaceBlock・DeleteBar・Story・TrainHUDScreen の各状態が Web 無しで描ける。欠けるのはホットバーと装備 HUD（段階 3・4 で作る）だけになる。

**対象**
- 入力の無い HUD 9 種: Crosshair、KeyHintHud、KeyControlHintHud、ProgressBar（採掘ゲージ）、TrainRidingHud、CurrentChallengeHud（アンカー `challenge.current-hud`。マスタが参照）、PlacementModeHud、DeleteModeWarningBands、DeleteBoxHeightHud。見た目は全部 Web 版と同じにする（列車 HUD も様式化しない。§0「見た目」）
- 通知（獲得通知の集約と出入りの演出）、CursorTooltip（topic `ui.tooltip`）とワールドピン
- UITK のチュートリアル枠（UITK のアンカーを照らす。Web の TutorialOverlay は全体スイッチの Web 側でそのまま動く）
- スキット（会話窓・タイプライタ・選択肢・ツールバー・暗転・背景スキット）、UITK 側の Ctrl+U とカットシーン中の全体非表示
- 対象外: ホットバーと装備 HUD。束に縛られるため、段階 3・4 で作る。それまで UITK 側には出ない

**作業**
- 基盤: ツールチップ層（オフセットは既存の `OFFSET` を参照。現在 12）。アンカーの枠（祖先クリップとの交差、脈動、ラベルの反転、二重解決のログ。§4.5）。Painter2D の図形（FadeRule・GaugeBar・削除モードの斜線帯）。hud と skit の面（Web 版と同じ見た目の完成形で作る）
- データ: 通知の集約（`web/features/notification/notificationStore.ts`）を C# の通知モデルへ移す。生存尺は `NOTIFICATION_DISPLAY_MS` を参照（現在 7000ms）、入場 160ms・退場 200ms（ADR 0016）。ワールドピンは同じフレームで投影し、WS を通さない
- スキット: 入力は単一消費者なので、描画者を 1 つにする
- 通知の重なり（ADR 0017 は「全画面 UI の裏」）: 全体スイッチでは UITK と CEF が重ならないので、UITK の層の並び（背面の通知層）で ADR 0017 の形をそのまま保つ。通知を隠す処理は作らない
- ホイール: `wheel-passthrough` の印と `UiPointerHitTest` の判定は、ホットバーを作る段階 3 で入れる

**完了条件**
- 共通条件（§2.0）を満たす。ラダー 2 `uitk-opening-hud`（§4.4）が緑
- マスタの `challenge.current-hud` のハイライトが UITK で出る録画がある
- ADR 0024 の判定役テスト（祖先のクリップ）の UITK 版が 1 本ある（`.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md` の型）
- 同じアンカーが UITK の木で 2 つ解決したら、エラーログが出る（テスト）
- CEF の 0F-wait（`_enableLog=1` の `block_avg`）の段階 2 の前後の比較はやめる。UITK 側は CEF を起動しないので比べる相手が無く、配布物から CEF が消える効果は切替の日にまとめて出る（`docs/webui/cef-unity-frame-rate-followup-2026-07-29.md` §2.2）

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| チュートリアルの取りこぼし | 全体スイッチの系だけが自分の木で探す（共通のリゾルバは作らない）。系をまたぐ dragGuide は無い（マスタは 0 件） |
| スキットの入力が二重に送られる | 全体スイッチで片方の系しか動かない。Web のスキットは切替の日に消す |
| タイプライタで、行の折り返しが途中で変わる | 残りの文字を透明にして配置を固定する（推測。shipping §3.1） |
| UITK 側で未移植の画面（インベントリ等）が開けず、通しの検証が狭い | 段階 2 のラダーは未移植の画面に触らない形にする（§4.4）。通しは段階 4 の終わりと切替 PR の全本で見る |

**規模と並列（推測）**: 70〜90 ファイル、C# 3.5〜4.5k 行、UXML/USS 約 1k 行。同時は Editor 要 2 本＋Editor 不要 2 本（§3.13）。Editor の要る 4 レーン（HUD とツールチップ／チュートリアル・アンカー・ワールドピン／スキット／通知）を 2 本ずつ回し、Editor の要らない 2 本で段階 3 と段階 4 の純ロジック（`shared/itemMove` など。L0）を移す。

### 2.4 段階 3 建築の束（ビルドメニュー・ホットバー・モーダル）

**目的**: 最初の操作の多い画面で、D&D・ホバーツールチップ・IME 付きの検索欄・スクロールスパイ・入力の取り合いを本番で踏む。ホットバーは常駐 HUD なので、段階 4 でインベントリ画面と同時に出る。

**対象**
- BuildMenuPanel 一式: カテゴリのサイドバー、検索欄（IME）、1 本のスクロールに並ぶカテゴリ群（スクロールスパイ・smooth scroll）、ホバーで固定される詳細サイドバー、BP の右クリック削除、不足時のツールチップ、セッション中の状態の保持（2026-08-05 の裁定群）
- HotbarPanel（9 枠、選択、D&D の割当・入替・解除）
- モーダル層と BP 名入力: `WebUiModalService` の後継。単一消費者なので、描画者を UITK に一本化する。モーダルを使うのは BP 名入力だけ（実測）

**作業**
- 段階 2 と並走して `feature/uitk` の上で作る
- 部品: press ジェスチャ（閾値は既存の `usePressGesture` を参照。現在 5px）、D&D 層（`panel.Pick` と落とし先の印のクラス）、HoverTooltip（祖先がスクロールしたら引っ込める）、SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、IconButton、スクロール領域（components.md §8.10 の様式）、TextField（§8.9）
- ロジックの移植: `buildMenu/logic`、`hotbarDnd.ts`、`placementTarget`、`materialTooltipText`。`useBuildMenuCategoryScroll.ts`（305 行）の計算部分は、先に L1 で固定してからビューを作る
- データ: `build_menu.entries` と `local_player.hotbar` を型付きの購読元にし、`build_menu.select`・`blueprint.delete`・`hotbar.*` を型付きコマンドにする
- 床の一致: ホットバーと床を共有するトークン（`--equipment-bottom: var(--hotbar-bottom)`。ADR 0013）を `Tokens.uss` に持ち、段階 4 の装備 HUD が同じトークンを使う
- ホイール: ホイールを通す印（`wheel-passthrough`）を付け、`UiPointerHitTest` が尊重する（§3.8）
- アンカー: `build-menu.entry-*` とホットバー系を UITK で付ける

**完了条件**
- 共通条件（§2.0）を満たす。ラダー 3 `uitk-build-bp`（§4.4）が緑
- 移すテスト（§4.1）: buildMenu（P 19・C 22・L 30）、hotbar（P 6・C 7・L 3）、modal（vitest 8）（tests §2）。例:「エントリを 5px を超えて動かし、ホットバーの枠 1 に落とすと、`AssignHotbar(1, id)` が 1 回だけ発行され、`SelectBuildMenu` は発行されない」
- 入力の受入: UITK の検索欄に入力している間は、ゲームのキーが動かない。検索欄の ESC は、フォーカスを外すだけで画面は閉じない

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 捕捉中のドラッグにホバーツールチップが出る | PoC の罠。press ジェスチャ側で最初から抑止する |
| スクロールで子の `GeometryChangedEvent` が来ず、スクロールスパイやアンカーが追従しない | `scrollOffset` を監視して判定する |
| 装備 HUD とホットバーの床がずれる | 床トークンを 1 つにし、段階 4 で装備 HUD を作るときに L1 で並びを見る |

**規模と並列（推測）**: 40〜55 ファイル、3〜4k 行、テスト 100〜140 件。部品（D&D・ジェスチャ・スロット）を 1 本が先に持ち、その後ビルドメニューとホットバー・モーダルの 2 本で並べる。

### 2.5 段階 4 持ち物の束

**目的**: 掴みでつながる面を、UITK でまとめて作る。研究画面も InventoryPanel を同時に出し、掴みを許す画面なので同じ束に入れる（`uiScreenRouting.ts` の SCREEN_TRAITS で researchTree は `grab: true`、`web/app/App.tsx:95,100` で InventoryPanel と ResearchTreePanel が同時にマウントされる。実測）。列車族も subInventory で InventoryPanel を使うので、同じ束に入れる。

**対象**
- InventoryPanel、EquipmentPanel（HUD とホイールの循環。循環は C# の 1 か所へ移す。`web/features/inventory/EquipmentPanel/index.tsx:55-80`）、GrabOverlay、スロットの HoverTooltip、RecipeViewer と ItemListPanel
- BlockInventoryPanel の全族
  - 汎用族: SectionStackView（チェスト・機械・発電機・採掘機・ポンプ・パイプと液体）
  - 特殊族: FilterSplitter、ElectricToGear、GearToElectric、ElectricPole、Gear・Shaft・GearBeltConveyor、ベルトの流量、電力網、歯車網
  - 列車族: 列車インベントリ、駅名、プラットフォーム、時刻表
- ResearchTreePanel（TreeView・ノード 4 状態・詳細ペイン・研究ボタン）、インベントリ内の DragGuide
- アンカー: `recipe.craft-button`、`recipe.item-<itemId>`、`research.node-<guid>`（マスタは 7 件）

**作り方（`feature/uitk` の上で波で並列に作る）**

| 波 | 中身 | 本数 |
|---|---|---|
| 4-α 中核 | `shared/itemMove` の計画の移植（左・右・ダブルクリック・Shift 配分）、掴み層、分割ドラッグ、右ドラッグ配り、GamePanel の default と craft、SectionStackView とレジストリ、RecipeRow（`.decisions/2026-08-20-レシピ行は独自gridを維持しSlotGridの例外とする.md`） | 1〜2 本が直列で先に作る |
| 4-β 並列 | 次の 5 群。(1) インベントリ・装備・レシピ・一覧（長押しクラフトはパネルのスケジューラで動かす。コンテナクエリは `GeometryChangedEvent` で計算する）、(2) 機械と汎用族（`.decisions/2026-08-30-機械UIはタブを廃しレシピ選択とインベントリをSatisfactory方式で往復する.md`）、(3) 特殊族（BarSeries、←→ での窓の切替）、(4) 列車族（駅名の IME、時刻表の楽観更新）、(5) 研究（TreeView: パンとズーム 0.4〜2.5、慣性 τ=325ms、Painter2D の線） | 同時は Editor 要 2 本＋Editor 不要 2 本（§3.13） |
| 4-γ 統合 | 同時に出る組（playerInventory、subInventory、researchTree）を確かめる。全体スイッチの UITK 側で全録画を回す（§2.0 の全本の 2 回目） | integrator |

**完了条件**
- 共通条件（§2.0）を満たす。ラダー `uitk-production-loop` と `uitk-research-train`（§4.4）が緑。駅名は SetText で入れ、IME は §4.9 の合成キー
- 移すテスト（§4.1。tests §2）: インベントリ・装備（P 20・C 16・L 57）、レシピ（P 18・C 10・L 19）、ブロック UI（P 42・C 27・L 45）、列車（P 6・C 6・L 14）、研究（P 18・C 21・L 37）
- 機械 UI を開いたまま中身が更新される録画を、UITK で通す（`.decisions/2026-10-18-液体UIは開いたままRefresh更新をunityプレイ録画テストで検証する.md`）
- マスタのアンカー（`recipe.craft-button` と研究ノード 7 件）が実ゲームで解決する
- UITK 側で掴みを扱うコードが Web に依存しない（`screenAllowsGrab` など Web の判定を読まない）。Web の掴みのコードは切替の日に消す（§2.6）
- インベントリと研究ツリーを開いたときの UITK の CPU 時間（`UIElementsUpdatePanels` と `UIElementsRepaintPanels` の合計）を台帳に 1 回記録する。目安は 1ms 以下（推測。PoC では CSS 表現 12 項目で 0.27ms）

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 最大の束なので、取りこぼしと退行の幅が大きい | 4-γ で全体スイッチの UITK 側の全録画を回す。配布環境での確かめは切替 PR に寄せる（§2.6・§6） |
| ブロック UI（11 種以上）の網羅漏れ | 4-γ の全録画（機械 UI の全族）と、切替の日に Web の `blockInventory` を消すときの台帳の行との突き合わせで捕まえる |
| 族の間で衝突する | 4-α でレジストリとセクションの枠を確定させてから 4-β に入る |
| 仮想化した一覧の行のアンカーが、画面外では存在しない | 研究ツリーは仮想化しない（transform で動かす）。仮想化する一覧は Bind 時にアンカーを付け替える |
| `MouseMove(from, to)` が途中のスロットに PointerEnter を出さない（L1） | 1 スロットずつ動かす（tests §5.5） |
| 切替の日まで、テスターはインベントリ系を含む CEF の不具合を被り続ける | 一気切替の帰結として受け入れる。CEF 由来の不具合は切替まで直さない（裁定）。master へ切り出す CEF 停止の記録で原因の値だけは届く |

**規模（推測）**: 100〜150 ファイル、8〜11k 行、テスト 300〜400 件。エージェント日数で 15〜20 日。

### 2.6 段階 5 最後の束と切替の日

**目的**: 最後の束を `feature/uitk` の上で作り、全画面を UITK でそろえる。続けて切替の日に、全画面の UITK への切替と、CEF・Kestrel・Web ホスト・Web 資産・全体スイッチの撤去を続けて入れ、完了の定義（§5.1）を満たす。Web へ戻すデバッグ切替は残さない（裁定）。

**最後の束の対象**
- ChallengePanel（研究の TreeView を再利用。カテゴリのボタン列）。見た目は Web 版と同じにする（様式化しない。§0「見た目」）
- EventLanguageGate（出展モード）: DI より前に出す全画面ゲート。`GameUiRoot` を起動の早い段で使えるようにするか、Starter が所有するゲート用のパネルを分けるかは、段階 5 の最初に決める（推測）。辞書を通さない文言（`DictionaryIndependentText`・`preDictionaryText.ts`）は C# の定数へ移す
- Web の横断層の残り: TutorialOverlay、ToastHost、DragGuide、再接続オーバーレイ、辞書エラー
- `AppErrorBoundary` の代わり（「UI の例外に誰が気づくか」）: UITK のビューの例外は、LogError とバグ報告の manifest に出す

**最後の束の完了条件**
- 共通条件（§2.0）を満たす。ラダー 5 `uitk-challenge-gate`（§4.4）が緑
- 全体スイッチの UITK 側で、DSL の Web ロケータが一度も要素を返さない（ログで確かめる）
- 台帳の全行が UITK で作られ、G1 の未記帳が全束で 0

**切替の日に消えるもの**（2026-11-06 までは段階ごとの削除 PR で消す予定だったものを、ここにまとめた）

| 元の段階 | 消えるもの |
|---|---|
| 1 | Web の `pauseMenu`・`settings` とテスト、ポーズ系の Web の ESC 経路 |
| 2 | Web の `commonHud`・`progress`・`keyHint`・`modeHud`・`trainHud`・`notification`・`skit`、CursorTooltip、WorldPinOverlay、KeyControlHintHud |
| 3 | Web の `buildMenu`・`hotbar`・`modal`、`BlueprintNameInputWebBridge` の Web 送り |
| 4 | Web の `inventory`・`recipe`・`blockInventory`・`research`、`shared/itemMove` などの掴み系 |
| 5 | `web/features` の残りすべて。CEF・Web ホスト・`moorestech_web`・Kestrel と AspNetCore 系 NuGet（裁定 3 で HttpListener）・DSL の Web 実装・全体スイッチ・master へ切り出した CEF 停止の記録・Web という名前 |

**切替 PR の作り方**（agent 既定。裁定は「切替と撤去を続けて入れる」「PR の中でだけビルドと検証機を通す」「マージはユーザー」）
- 最後の束が `feature/uitk` に入ったら、`feature/uitk` の上で下の撤去 5a〜5e をこの順にレーンとして積む。切替 PR は `feature/uitk` から master への 1 本で、切替と撤去を両方運ぶ（PR #1589 は閉じ、新しく作る）
- 撤去を積み始めたら、全体スイッチの Web 側は使えなくなる。積む前に、全体スイッチの Web 側と UITK 側で全録画を回しておく
- 切替 PR の中で、Windows と Mac のビルドを回し、Windows 検証機・Mac の Steam・`test-release-playtest-mac.sh` を通す。ラダー 6 本と全録画を CEF の存在しないビルドで流す（DSL は Editor 専用なので、CEF パッケージの無い HEAD の Editor で流す。A-259）。IME を CEF を含まない配布ビルドで一巡する（§4.9）。オーナーの目視もここで 1 回（§2.0）
- G1 は切替 PR で全束を `切替済み` にし、全行の所有者を `UITK` にする（§4.7）
- `feature/uitk` 上で Web を触ったコミット（§2.2 の休止の撤去・撤去 5c の `moorestech_web/` 削除・取り込み以外の Web 変更）も、G1 は各束の未記帳として数える。切替 PR の前に各束の「## 追従記録」へ `捨てた（理由）` で記帳し、ローカルで `.github/scripts/check-uitk-web-follow.py --base origin/master --head feature/uitk` を回して 0 件を見る。切替 PR は head=`feature/uitk` から出す（中継ブランチ禁止）。台帳と G1 の workflow は切替 PR の中では消さない（merge ref から workflow が消えると G1 が起動せず、required check が空のまま未記帳で切替がマージされうる）。切替 PR のマージ後に別 PR で両方まとめて消す（台帳だけ消すと G1 が落ちる）
- 最終検証中に master へ Web の更新が入ったら、取り込んで追従を記帳し、影響箇所を再検証してから切り替える（裁定）。撤去後の取り込みは、master 側の Web の変更が消したファイルとの衝突になる。消す側で解決し、中身は UITK 側へ写して記帳する
- ユーザーが切替 PR をマージしたら、moores-playtest-release で playtest へ反映する（§2.0）

**撤去の区分**（`feature/uitk` の上で直列。この順に積む）

| 区分 | 中身 |
|---|---|
| 5a 起動配線と共存機構 | 起動配線: `Client.Starter` の `WebUiStartup`、`InitializeScenePipeline.cs:73` の `KestrelPort`、`MainGameInitializationFinalizer` の `Bind`、`EventModeStartGate` の Hub 依存。共存機構: 全体スイッチ、DSL の Web ロケータ、`UiPointerHitTest` と `UiInputExclusivity` の Web の項、manifest の CEF の値と無応答の判定（master へ切り出した CEF 停止の記録）、Web ホストを起動しない Editor スイッチ |
| 5b CEF と配布工程 | `jp.juha.cefunity`（manifest 44 行目）。CEF の型を使う C#（Navigator・Playtest の 2 本・テストの BlockActionScene の 2 本）と asmdef 3 本。MainGameUI.prefab の子 `CefUnity`（RawImage・`CefUnityBrowserSample`・`WebUiCefNavigator`）と、ルートの Canvas・CanvasScaler・GraphicRaycaster・CanvasGroup のコンポーネント（GameObject と uGUI の描画部品を持たない子 4 本は残す）を `uloop execute-dynamic-code` で外す。`CefRuntimeBundler` と `BuildPipeline` からの呼び出し、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得。`scripts/playtest/lib/release-artifact.sh` の CEF helper 検査とその契約テスト（`scripts/playtest/tests/test-release-playtest-mac.sh`）、`scripts/playtest/README.md` の CEF raw input の確認。共用部品は消さずに整理する（`CefLfsPointer` は中立な名前へ改名。ffmpeg と Harmony の同梱検査が使う。`EnvironmentScrubOnEditorLoad` は `Client.ExternalProcess` 側へ移す） |
| 5c Web ホストと Web 資産 | `Client.WebUiHost`（asmdef ごと）、C# の通信契約テスト（131 件）、`WebUiActionInvoker`、`Client.Playtest/WebUi/PlaytestDomQuery`、`Client.Tests` のうち WebUiHost を参照する 67 ファイル（実測）、`moorestech_web/`、CI の `webui-test` ジョブ・`needs_webui`・pnpm の step、`WebUiProductionArtifactBuilder`、`StreamingAssets/WebUi` とその `.gitignore` |
| 5d NuGet | AspNetCore 系（裁定 3 で HttpListener の場合）と、使われなくなった `Microsoft.Extensions.*`。依存グラフを確かめてから消す。`packages.config` は 74 件で、47 件が `Microsoft.AspNetCore.*` か `Microsoft.Extensions.*`（実測）。`ModelContextProtocol 0.2.0-preview.3` は使い手 0（実測）。MCP の 13 件（client は Extensions 系 8 件と重なる）を同じ PR で外した（X-5d・A-256） |
| 5e 文書・スキル・改名・ADR・bd | 改名: `WebPinId`（13 か所。実測）、`LocalizationKeys.Ui.Blueprint.WebModal*`、`VanillaSchema/challenges.yml` の `web anchor id`（3 か所。実測）、`WebUiGateAuditTest`（名前を変え、対象を付け替えて残す）。スキル: `ref-webui-design` を廃止して `ref-uitk-design` を完成させ、moores-code-review の ts/tsx 観点 12 本を撤去し、DSL のスキル文書を更新する。文書: `docs/webui` を archive にして入口を `docs/uitk` にし、`docs/development/macmini/unity-editor.md` の CEF の節を消す。ADR と裁定の改訂か廃止の記録（§7.3）。bd: CEF 関連の open 課題 12 件（shipping §1.2）を、閉じるか、中身が同じなら題を直し、中身が変わるなら bd の新しい課題へ写す（台帳は X-cleanup で消えるので転記先にしない）。`moorestech-cgm` は §0 のとおり移行では閉じず、題を UITK のトーストに直して残す。台帳 `docs/uitk/TODO.md` と G1 の workflow・スクリプト・テストはこの区分に含めず、切替 PR のマージ後に別 PR で消す（切替 PR で消すと G1 が起動せず未記帳のままマージされうるため） |

- 5a を先にする。起動しなくなれば、残りは参照されないコードになる
- 5b を 5c より先にする。5c を先に出すと、`MainGameUI.prefab` に missing script（guid `592d2edb…`＝`WebUiCefNavigator`）が残ったまま CEF が起動し続ける
- 配布物が変わるのは 5b だが、ビルドと検証機は 5a〜5e を積んだ切替 PR で 1 回まとめて回す（裁定: 切替 PR の中でだけ回す）
- 5e は実体が消えてから書く

**規模と並列（推測）**: 最後の束は 25〜40 ファイル、1.5〜2.5k 行で、並列は 3 本（チャレンジ／言語ゲート／横断層と例外の経路）。撤去は 5 区分で、並列は 1〜2 本。最終検証は 1〜1.5 週で、master の取り込みが入るたびに延びる。削除の内訳は `moorestech_web` 約 28k 行（テスト込み）、`Client.WebUiHost` 10.5k 行、テスト 6〜8k 行、DSL の Web 部約 0.7k 行。

---

## 3. 共通基盤

### 3.1 置き場と構成

- **asmdef**: 描画の `Client.Ui` と画面状態の `Client.Ui.State`（段階 0 で新設済み）の 2 つにする。名前に技術名（Uitk）を入れない（Web という名前の改名を繰り返さないため）。参照の向きは `Client.Game` ← `Client.Ui.State` ← {`Client.Ui`・`Client.WebUiHost`}。`Client.Ui` は描画と操作だけを持ち、画面状態・ViewModel・UI コマンドは `Client.Ui.State` に置く。`Client.Ui.State` を `Client.Ui` に含めないのは、長命ブランチの中で `Client.Ui` が全体スイッチを通じて WebUiHost を触るため（WebUiHost も状態を読むので、同じアセンブリだと循環する）。`Client.Game` はどちらも参照せず、ゲーム側から UI へ頼む要求（モーダル等）はインターフェース越しにする。DI は既存の VContainer、イベントは UniRx
- **ディレクトリ**: 基盤 `Client.Ui/Foundation/{Root,Layers,Routing,Input,Motion,Tooltip,DragDrop,Modal,Anchor,Text,Theme}`、部品 `Client.Ui/Components/<部品>`、画面 `Client.Ui/Screens/<機能>/`。AGENTS.md の上限（1 ディレクトリ 10 ファイル、1 ファイル 200 行）に合わせる
- **3 層**: View は VisualElement を組むだけで、MonoBehaviour に依存しない。Presenter はモデルを購読して View へ push し、操作を型付きコマンドへ渡す。Mount は `GameUiRoot` に載せる薄い層。テストでは `UITestFixture` の `rootVisualElement` に同じ View を組める（tests §5.3）
- **アセットのロード**: USS は機能の横に置き、`GameUiTheme.tss` に `@import` で束ねる。部品の ctor は `StyleSheet` を受けない（F1 で段階 1 の `UiAssetCatalog` を撤去。設計書 [A-2]）。束ね漏れは L2（`UiThemeImportBundleTest`）で止める
- **実行時の形**: `GameUiRoot` は MainGameUI.prefab に事前配置し、シングルトンの規約どおり Awake で `_instance` を設定する。パネルは 1 枚で、Pick とフォーカスが 1 枚の中で閉じる

### 3.2 データ購読層

- **ゲームの正本には手を入れない**: `Client.Game`・`Client.Skit`・`Client.Input`・`Client.Localization`・サーバー（host §7.5 R2）。サーバー状態の同期（イベントパケット＋初期データ＋購読）も変えない。UITK 化はクライアントの内側だけの話。画面のための状態は正本ではないので `Client.Ui.State` に置く
- **topic を 2 つに分ける**: ①型付きのスナップショットと変化通知（`IReadOnlyReactiveProperty<XxxViewModel>`）を `Client.Ui.State` に置く。②それを JSON にするアダプタ（Web が使う）。分けるのは、その段で使う topic だけ。撤去の 5c で②を消す
- **フレーム末の畳み込みと間引き**: フレーム末に 1 回へ畳む処理（PostLateUpdate）は残す（中間状態を描かないため）。WS の帯域のための間引き（`ui.progress` の 100ms と歯車の `IntervalFrame(4)`）はアダプタ側へ移し、UITK では毎フレーム読む。`BlockInventoryTopic` はコメント（4tick）と実装（`IntervalFrame(4)`）が食い違っているので、移植するときに意図を確かめる
- **ViewModel**: 不変の sealed class にする。判別 union は抽象基底と sealed 派生で表す。zod の不変条件をこの型へ移す（FluidSlot、変換機の詳細、Tooltip、Hotbar の 9 枠、PlacementMode）
- **コマンド**: action ごとに型付きメソッドにし、`CommandResult`（Accepted か、Rejected と理由の enum）を返す。前提条件の検証（state の一致・解放判定・遷移許可表）はコマンド側に置き、全体スイッチのどちらの系も同じ検証を通る。拒否の理由は開発者ログへ出す（AGENTS.md の fail-closed）。良性の拒否（Web の `BENIGN_ERRORS`）は理由の性質として enum に持ち、トーストを出すかどうかをそれで決める
- **Presenter の規約**: `Update()` で毎フレームの同値判定をしない。画面は起動時に 1 回だけマウントし、表示・非表示はルータが切り替える。非表示の間は描画を止め、表示するときに最新値で 1 回描く

### 3.3 層と全体スイッチの規則

**層の並び**
- ルート直下に、層 enum の固定順で兄弟コンテナを並べる（USS に z-index は無い）。順序の正本は `web/app/tokens.css` の `--z-*` と `web/app/zLayerTokens.test.ts` で、C# の enum と不変条件テストへ写す
- 大筋の順（細部は写すときに確かめる）: backdrop → 背面（通知）→ stage（画面・stage 内のオーバーレイ）→ viewport の HUD → 掴み → モーダル → ツールチップ → トースト → チュートリアル → 全画面ゲート
- 層コンテナと backdrop は `PickingMode.Ignore` を既定にし、L1 で固定する。忘れると「パネル外の右短押し」（ADR 0046）とワールドのクリックが効かなくなる（input §3.1）
- stage 族・viewport 族・背面 viewport 族の三分法（ADR 0013・0017）はそのまま表す

**全体スイッチの規則**（2026-11-06 裁定「長命ブランチの中は全部 Web／全部 UITK の 1 つのデバッグスイッチ」。段階 1 の所有表・委譲・Editor 限定の上書きをこれへ作り直す）

| # | 規則 | 守り方 |
|---|---|---|
| R1 | 長命ブランチの中で描く系は、全体スイッチ 1 つで「全部 Web」か「全部 UITK」に決まる。画面ごとに系を振り分けない。UITK の間、UITK 側にまだ無い画面は使えない | スイッチは 1 つの値（enum）で、画面状態からは引かない。UITK 側では Web ホストを起動しない（0-10 の経路。agent 既定）ので、CEF の絵も入力も無い。Web 側では `GameUiRoot` に何も載せない。2026-10-26 の「UITK の主パネル表示中は Web を全面オフ」は、UITK 側で常に満たされる |
| R2 | 入力の裁き・DSL のロケータ・アンカーの解決は、スイッチの系だけを読む。両系の値を論理和にしない | `UiPointerHitTest`・`UiInputExclusivity`・`IUiElementLocator`・アンカーの登録簿が、スイッチの値で片方を選ぶ。片方だけを読むことを L0 で固定する |
| R3 | 長命ブランチの中では Web のコードを消さない。消すのは切替の日にまとめて（§2.6） | 全体スイッチの Web 側を master と同じ Web に保つ（取り込みの衝突も減る）。Web のコードへの変更は master の取り込みでだけ入れ、`feature/uitk` で直接 Web を書き換えない（agent 既定） |
| R4 | スイッチは Editor 限定。既定は Web、ラダーと UITK の録画は UITK を明示して始める | 切替 API は `#if UNITY_EDITOR` の中でファイル末尾に置く（配布ビルドは切替前に作らない。CI のプレイヤービルドは Web で通る）。既定を Web にするのは、段階 4 まで UITK 側でゲームが通しで遊べないため（agent 既定）。切替 PR（5a）でスイッチごと消す |

### 3.4 部品と作る段階

部品は、その段で使うものだけを作る。例外は、全画面が使う基盤（段階 1）。各部品は、作る段で装飾を含めた完成形にする。磨き込みの段は設けない（後回しの負債を作らないため）。

| 段階 | 部品 |
|---|---|
| 1 | 層マネージャ、ルータ、全体スイッチ（段階 2 の前に作り直す）、GamePanel（default）、PanelActionButton、横の ModeSwitch、IconButton（× は Painter2D）、スクロール領域、TextField（単行・複数行）、トースト、LocalizedLabel、KeyCap、UiMotion |
| 2 | CursorTooltip、FadeRule、GaugeBar、斜線帯、GamePanel（hud・skit）、通知の行、チュートリアルの枠とラベル、ワールドピン、スキットの選択肢の板とツールバーのアイコン |
| 3 | press ジェスチャ、D&D 層、HoverTooltip、SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、スクロールスパイ、モーダル層 |
| 4 | 掴み層、分割ドラッグ、GamePanel（craft）、RecipeRow、ProgressArrow、BarSeries、FluidSlot、SectionStackView、TreeView、四隅のブラケット、コンテナクエリの代わり |
| 5 | 全画面ゲート |

**GamePanel の描き方**（PoC で実証した部分と、未実証の部分がある。推測を含む）: 面のグラデと 4 辺のフェードは `generateVisualContent` の頂点カラーのメッシュで描く。inset の 2 層は border の入れ子にする。右下のグリップと下向きの三角は Painter2D で描く。

**面は Web 版と同じ枠にする**（§0「見た目」）。Web 版が GamePanel で描く画面は GamePanel を、それ以外の枠（ポーズ・設定・バグ報告の Mantine 系の枠、トーストの Notification など）で描く画面はその枠を UITK で写す。段階 1 では Web の枠（不透明に近い暗色・角丸 12px・大きい太字の題・節見出し）を写した。

### 3.5 テーマ USS とトークン

- **ファイル**: `Tokens.uss`（`tokens.css` 192 個、`skitTokens.css` 38 個、`slotTokens.css` 8 個、合わせて約 238 個を `:root` へ）。`Theme.uss`（ランタイムの既定コントロールを暗色にする。スライダーの入力部の高さが 0 になる癖への対処も含める）。`Typography.uss` と、部品ごとの USS
- **トークンの正本は USS**（§7.2）。C# が値を要るとき（Painter2D・配置の計算）は、`CustomStyleProperty` で解決後の値を読む。トークンを C# の定数に二重に持たない
- **CSS からの変換規則**（screens §5）

  | CSS の側 | UITK での扱い |
  |---|---|
  | rem・em | px にする（1rem=16px） |
  | `calc()` 70 か所 | 定数だけの式は計算済みのリテラルにし、導出式をコメントに残して L2 で検算する。ビューポートや要素の寸法・割合に依存する式（`App.module.css:62-65` の viewport 層の位置など。倍率と寸法は `App.tsx:39-47` が実行時に更新する）は、USS の配置指定（% や中央寄せ）か C# の幾何計算に置き換え、§4.6 の解像度確認で見る |
  | `color-mix`・`rgb(from …)` | 事前に計算する |
  | data 属性 40 か所 | `--modifier` クラスと `EnableInClassList` |
  | z-index 44 か所 | 層 enum |
  | `:first-child`・`:nth-child` など | C# でクラスを付ける |
  | `::before` | 子要素 |
  | grid・gap | flex-wrap と固定幅・margin |
  | コンテナクエリ | `GeometryChangedEvent` |

- **半透明（67 か所）は Web の値を流用しない**（PoC の実測: 同じ指定が UITK では `#664f29`、Chrome では `#3e311a`）。トークン表に「Web の値」「UITK の値」「決め方」の 3 列を持ち、UITK の値はその部品を作る段で実機で決める（§7.2）
- **L2 で落とす規約**: 色は `Tokens.uss` の外では `var()` だけ。構造擬似クラスと z-index は禁止。UXML の `text=` にリテラルを書かない。`name` の重複は禁止。ビットマップの参照は禁止（§7.2）。合成の太字（`-unity-font-style: bold`）は禁止

### 3.6 アニメーション

- **入退場とホバーは USS の transition** で書く。追加した直後にクラスを外すと補間されないので、`GeometryChangedEvent` を契機にする。`height: auto` は補間できないので、px に固定してから動かす。並べ替えの FLIP は `GeometryChangedEvent` の新旧の矩形で書く（`schedule.Execute` はレイアウトより先に走る。PoC の罠）
- **ループ・キーフレーム・smooth scroll・慣性は、自作の `UiMotion` で書く**（100〜150 行の見込み。段階 1 で作る。§7.2）
  - 使い手: 通知の出入り、チュートリアルの脈動、ドラッグガイドの往復、ビルドメニューの smooth scroll、研究ツリーの慣性
  - 持たせる API: 時計の注入、頭からの再生し直し（通知の集約で寿命を張り直す）、予約した退場の取り消し、完了の通知。要素がパネルから外れたら止まる
- **時間源は実時間**（パネルのスケジューラ、timeScale に依らない。§0 の確認）。テストでは `FrameUpdateMs` で進める
- **尺と曲線はトークンに置き、既存の値を参照する**: 通知の生存は `NOTIFICATION_DISPLAY_MS`（現在 7000ms）、入場 160ms・退場 200ms（ADR 0016）、脈動は 1200ms（ADR 0039）
- ref-webui-design §6 を継ぐ。テストのために尺を 0 にする抜け道は作らない。時計を進めるのは、尺を 0 にすることではない

### 3.7 ツールチップ・D&D・掴み・モーダル

- **CursorTooltip**: `MouseCursorTooltipState` を購読し、同じフレームのポインタから位置を決める。画面端では反転させる。カーソルがロックされている間は画面中央を基準にする（ロック中の UITK のポインタは画面外になるため。input §3.2）。書式は ADR 0019 の値（18px、padding 6/10、max-width 320）をトークンで持つ
- **HoverTooltip**: 祖先がスクロールしたら引っ込める。ドラッグの捕捉中は出さない（PoC の罠: 捕捉中も下の要素に PointerEnter が届く）
- **press ジェスチャ**: tap・drag・abort の 1 つの状態機械にし、`CapturePointer` を使う。D&D・パン・ドラッグスクロール・長押しで共通にする
- **D&D**: 自作する（PoC で約 140 行）。落とし先は、`panel.Pick` の結果の祖先から印のクラスを持つ要素を探して決める。ゴーストは掴み層に出す
- **掴み絵**は掴み層に 1 つだけ置き、同じフレームの `PointerMoveEvent` で動かす。今は CEF の mousemove 頼みで、カーソルのワープ後に追従しない経路があった（ADR 0020・0074）
- **モーダル**: 同時に 1 件だけ出す。幕を張り、フォーカスを中に閉じ込める。ESC は UIState が持ち、入力欄には自動でフォーカスする
- **右クリックメニュー**が要れば、`GenericDropdownMenu` を暗色のテーマで使う（PoC）

### 3.8 入力とフォーカス

入力の裁きは `UiPointerHitTest` と `UiInputExclusivity` の 2 か所だけで行う。長命ブランチの中では、全体スイッチの系の値だけを読む（R2。段階 1 で作った「UITK と Web の論理和」と「主パネル表示中は Web を読まない」は作り直しで外す）。最後の WS 受信から閾値を超えたら Web 側の値を無視する処理（§3.11）は、master の Web だけの世界の手当てとして切り出す。

| 項目 | 方針 | 根拠 |
|---|---|---|
| UI 用のアクション資産 | moorestech 専用の資産を作る。Point・Click・RightClick・MiddleClick・ScrollWheel は残し、Navigate は空、Submit と Cancel は使わない | `DefaultInputActions` は Navigate に WASD を含み、メニュー中も WASD で歩く仕様（`.decisions/2026-10-01-メニュー中の視点は回さず移動だけ解放する.md`）とぶつかる |
| フォーカス | 入力欄以外は `focusable=false`。Tab の既定のフォーカス移動はアクション資産では止められない（Unity の仕様で、Tab は入力モジュールを通らない）ので、ルートで打ち消す（Web の `activeLayer.ts:36` と同じ）。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを L1 で固定する | `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`、`…クラフトボタンはSpaceに反応させずジャンプ専用にする.md` |
| ポインタ | `UiPointerHitTest.IsPointerOverAnyUi` を次の形にする。ロック中は false。そうでなければ「スイッチの系の判定（UITK は Pick が層コンテナ以外を返す、Web は pointerOverUi）」か「EventSystem（デバッグ用の uGUI）」 | input §3.3 の案 A。呼び出し側の 23 ファイル・28 か所は変えない（実測）。EventSystem 判定は ADR 0052 の agent 前提 1 |
| ホイール | ホイールを通す印を USS のクラス（`wheel-passthrough`）にし、Pick の結果の祖先で判定する | 装備の循環は段階 4 で C# の 1 か所へ移す（UI の上なら止める、印のある HUD の上は通す、建築ツールが占有中なら止める、1 枠以下なら何もしない） |
| 文字入力 | ルートで `FocusInEvent`・`FocusOutEvent` を TrickleDown で受ける。フォーカス中の要素が TextField かを、`UiInputExclusivity` の ReactiveProperty へ同期で書く | 同じスレッド・同じフレームで書くので、lock も `ObserveOnMainThread` も要らない。`.decisions/2026-10-01-文字入力欄にフォーカス中は移動を止めたままにする.md` をテストで守る |
| ESC | 「このフレームの ESC は UI が使った」というラッチを、`InputKey` の抑止判定に足す。入力欄での ESC はフォーカスを外すだけにする。UITK の TextField の既定の ESC 処理（値をフォーカス時の値へ戻し、同じフレームで再フォーカスする。S9）も止める | 今は WS の遅れで偶然防げているだけ（`docs/webui/design/input-focus-exclusivity.md`） |
| キー処理の置き場 | 画面のキー処理（ESC・←→・Enter でのクラフトとスキット送り）は、C# の UIState か画面の Presenter に集める | Web の document 全域のキー処理は切替の日に消す（host §7.3） |
| IME | CEF は初期化時に 1 回だけ `imeCompositionMode=On` にする。UITK の TextField はフォーカスで On、外れると Auto にするため、一度使うと Web の入力欄で日本語が打てなくなり、未フォーカスのワールドでも IME が効く（S9）。モードは入力排他と同じ 1 か所で決め、UITK の入力欄にフォーカスがあるときだけ On にする（全体スイッチの UITK 側では CEF が起動しないので取り合いは起きない） | input §1・S9。CEF が消えれば Web 側の条件も消える |
| ゲームパッド | UI のパッド操作は作らない | ADR 0082（sprint-toggle）の 7 項で「ゲームパッドは正式対応外」 |
| `activeInputHandler` | Both（2）のまま。InputSystem だけにするかは、完了の後に別に判断する | IME の実装次第（推測） |

### 3.9 ローカライズ

- **C# の `Localize` に一本化する。** 辞書の合成、解決順（対象言語 → english → source → `[!key]`）、型付きキーはすでにある（input §4.1）。HTTP の辞書取得・再試行・409 は消える
- **足すもの**: 名前付き補間（0-6）。`LocalizedLabel`（キーと引数を持ち、`Localize.OnLanguageChanged` で解決し直す）。表示用の数値書式ヘルパー（InvariantCulture を明示する。de 列があるので `ToString()` の「,」が実害になる。Web の `toFixed`〈12 か所〉は常に「.」）。kbd の差し込み（KeyCap 部品と文字列の分割。`LocalizedShortcutHint` 相当）
- **DTO は Guid とキーだけを運び、ビューが解決する。** ADR 0006 の原則を保ち、決定 5 の「Web 側で解決」を「ビュー側で解決」へ改める（§7.3）。スキットは今どおり、C# が解決した文字列を push する
- **静的検査**: UXML の `text=` に生の文字を書かない（`noJsxVisibleLiteral` の後継）。全画面の文言がキーを通る（`allScreensI18n` の後継）

### 3.10 フォント

- **TextCore の SDF FontAsset を Editor で作ってコミットする**（不可なら実行時に生成。§2.2）。既存の `Assets/Dependencies/Font/*SDF*.asset` は TMP_FontAsset で型が違う。`FontDefinition.FromFont` はビットマップで描くので、拡大すると崩れる（PoC）
- **フォールバック**: NotoSans-Regular → NotoSansJP-Medium → NotoSansKR-Medium の順。ロケールごとに、ルートのクラスで順を切り替える（`:root[data-locale]` の代わり）。韓国語は、Web では OS のフォントへ落ちていた（推測）。UITK は自動では落ちないので、明示する
- **最小の文字寸法**: 11〜12px の和文は Chrome より細い（PoC）。段階 1 の実機の比べ（S7）を見て、12px 以下を 13px へ上げる。アイコン上の個数は `-unity-text-outline` で縁取るので、別に扱う
- **文中アイコンの SpriteAsset** は内部 API に頼る（PoC）。構築と描画のテストを置き、Unity の版上げで最初に壊れる箇所として台帳に書く

### 3.11 CEF 停止の記録

AGENTS.md「失敗の扱い」に従い、「誰がどこで気づくか」を先に決める。

**今の問題**
- Web ホストの起動に失敗すると、LogWarning だけを出して続行する（`Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs:19,24`。コメント「UIはWeb一本なので起動失敗時は非表示で続ける」）。配布物では開発者に届かない
- Web の入力欄にフォーカスしたまま JS が止まり、WS の接続だけ残ると、切断時のリセット（`WebSocketHub.cs:153-159`）を通らない。移動と Esc（`CloseUI` は Keyboard 抑止の対象。`InputManager.cs:110`）が抑止され続け、命綱の入口が塞がれる

**master へ先に切り出す**（2026-11-06 裁定）: 段階 1 で `feature/uitk` に入れた下の 1〜3 を、別 PR で先に master へ入れる。UITK の画面ではなく今の Web の世界の改善で、テスターの UI 全停止の報告に今から原因の値が載るため。master では UITK が無いので、3 は「Web 無応答時は Web 側の入力排他の値を無視して Esc が塞がらないようにする」形になる。切替の日に CEF と一緒に消える（5a）。`feature/uitk` は次の master の取り込みで同じものを受け、重複を解く

**それ以外の CEF 由来の不具合は切替まで直さない**（2026-11-06 裁定）: UI 全停止・メモリの伸びなど、起動不能・データ消失を含めて直さない。CEF 由来でない Web 画面の不具合は従来どおり master で直す（その変更は追従の記帳の対象になる）。直さない理由は「切替の日に CEF ごと消えるので捨て作業になる」（agent の推測。ユーザーは理由を述べていない）

**やること**（実装は `Client.WebUiHost`、バグ報告から見るインターフェースは `Client.Game`。撤去の 5a で消す）
1. バグ報告の manifest に生の値を 3 つ入れる: Web ホスト起動の成否、CEF プロセスの観測状態（`observedAfterBrowserStart` 等。生死は判定しない）、最後の WS 受信からの秒数。ADR 0057 の受け口から Mac mini に届き、プレイテストダッシュボードの報告詳細に出る（bug-report-auto-fix は `.decisions/2026-10-31-自動修正の基盤を消しプレイテスト生データの正本をR2だけにする.md` で削除済みで、読み手はダッシュボード）
2. `WebUiStartup.cs` の LogWarning を LogError にする
3. 最後の WS 受信から閾値（`webSocketClient.ts` の 15000ms を参照）を超えたら、入力排他の Web 側の値（文字入力フォーカス・pointerOverUi）を無視する（`feature/uitk` の段階 1 では UITK 側の値だけを使う形で作った）。L0 のテストで確かめる

ホストは今 pong を返すだけで最後の受信時刻を持たない（`WebSocketMessageDispatcher.cs:54-55`）ので、それを足す。停止の判定、テスターへの知らせ、CEF の絵を隠す処置、自動再起動は作らない（裁定 2 と 2026-11-06 の「CEF 由来の不具合は直さない」。残る負債は §6）。

**原因を CEF と決めつけない**: UI の全停止がすべて CEF 由来とは限らない。2026-10-14 の「破壊モードで橋脚にカーソル合わせるだけで…UI操作がたぶんできなくなる」は、cef-unity が 2 種とも生きていた（実測。Discord ミラー）。manifest に判定ではなく生の値を入れるのはこのため。

### 3.12 AI 向けの作法

- UXML と USS はテキストで持ち、UI Builder にしか無い状態を作らない。インラインの style は、計算で決まる幾何（位置・寸法）にだけ使う
- 見た目の調整は USS を中心に回す。USS と UXML の変更は、コンパイルもドメインリロードも起こさない。ビュー側の C# は薄く保ち、判断は Presenter とモデルに置く
- 名前: testid は `name` に同じ綴りで付ける（シナリオの testid を変えずに済む）。アンカーは USS クラスで付ける（`name` とは別の契約。`docs/webui/anchor-convention.md` の「testid とアンカーは別」を継ぐ）。状態はクラスで表す（`--selected` など）
- 見た目の自己確認は、ScreenCapture 系か window モードの撮影で行う。**`uloop screenshot` の rendering モードには UITK が写らない**（PoC の実測）。撮影にはフレーム番号か時刻の透かしを入れ、古いフレームを見ないようにする
- 規約違反は L2 で止める。moores-code-review に UITK の観点を足す（PickingMode の既定、全体スイッチの片側読み〈R2〉、トークンの直書き、`Func<>`・partial の禁止）
- `ref-uitk-design`（段階 1 で初版）: `ref-webui-design` の原則を写す（ホワイトリスト、スロットは共有部品だけ、装飾は 6 語彙、色はトークンだけ、族と層）。ただし「面は GamePanel だけ」は写さず、「面は Web 版のその画面と同じ枠」に直す（2026-11-06 裁定。§3.4）。見た目の正本は Web 版と参照キャプチャで、ホワイトリストは Web 版に無い新しい面を作るときにだけ効く（agent 既定）。§0 と §10（mock-host・Vite・Playwright・目視 QA）は、EditMode での組み立てと ScreenCapture に置き換える
- AGENTS.md の規約を守る: 200 行以下、1 ディレクトリ 10 ファイル、`#region Internal`、partial と `Func<>` の禁止、イベントは UniRx、日英 2 行のコメント

### 3.13 並列実行の設計

**作業の切り方**
1. **1 単位 = 1 レーン。** レーンは `feature/uitk` から切り、`feature/uitk` へマージする（§1.1）。単位は、ファイルの所有が重ならないように切る。新しく作るディレクトリの中で閉じるのが基本。
2. **共有ファイルは integrator が直列で所有する**: 層 enum、ルータと全体スイッチ、composition root（DI の Installer の登録）、`Tokens.uss`・`Theme.uss`、`AnchorIds`、DSL のロケータ本体、`Localization/localization.csv`、台帳、prefab とシーン（uloop で 1 本ずつ）。画面のルータ登録は integrator がレーンの取り込みで行う。master の取り込みと追従の記帳も integrator が持つ（agent 既定。台帳と衝突の解決が共有ファイルに集まるため）。
3. **部品の昇格**: 1 機能だけが使う部品は `Screens/<機能>/` に置く。2 機能目で使われたら、integrator が波の境で `Components/` へ移す。
4. **純ロジックの移植は別レーン**: vitest を仕様にして、C# の NUnit へ 1 対 1 で移す（約 4,000 行、L0 約 290 件）。レーンの先頭で、Unity の外で `dotnet test` が回るかを確かめる（S12。前例 `mooresmaster.Tests`）。回らなければ Editor のレーンだけで回す。AGENTS.md のコンパイル必須は、PR の前の `uloop compile` で守る。
5. **挙動を変えるのは grill を通したものだけ**: 移植はテスト名を写したパリティが基本。見た目は Web 版と同じにする（§0「見た目」）。影響が小さく非本質的な対応が入るのであれば推奨案で外してよく（§0「見た目」の原文どおり）、外したものは台帳に書く。各段階の着手前に、moores-writing-plans で詳細計画を切る。

**Editor の予算と役割**

| 項目 | 中身 |
|---|---|
| Mac mini | 実装に使える Editor ＝ 3 − apply 稼働数 − 配布ビルド中の 1。3 は `CLAUDE.local.macmini.md` の目安。pr-apply は apply のたびに Editor を起動し、配布ビルドは同じ機械で batchmode の Unity を回す。master の毎週の playtest（Web）は移行中も続くので、配布ビルド中の 1 も引き続き引く。`feature/uitk` の配布ビルドは切替 PR でだけ回す |
| macbook-pro | 起きているときだけ、1 本を足せる |
| CI | L1 を client shard に載せる（S13） |
| 指揮 | 波を起動し、完了を確かめ、レーンを `feature/uitk` へマージし、台帳を更新する |
| integrator | 共有ファイルの所有、部品の昇格、ルータ登録、全体スイッチの作り直し、master の取り込みと追従の記帳、切替 PR |
| 単位エージェント | 実装を担う（opus）。worktree は `moores-wt new` で切り、Editor を起動する前に `moores-wt status` で本数を確かめる。枠が無ければ、Editor の要らない作業（L0・テスト名の写し・UXML の下書き）を先に進める |

**レビューとマージ**
- レーンは moores-code-review を通してから、agent が `feature/uitk` へマージする（2026-11-06 裁定）
- pr-independent-review は、切替 PR と全体スイッチの作り直し（共有ファイルを持ち、段階 1 の検証を作り直すもの）だけに付ける。ほかのレーンは moores-code-review まで（agent 既定。2026-10-26 版の「切替 PR と段階 1 の基盤 PR だけ」と同じ考え方）
- master への PR は、CEF 停止の記録と切替 PR の 2 本。どちらも PR の作成で止まり、マージはユーザーが行う。律速はマージではなく、Editor の本数と追従

---

## 4. 検証

### 4.1 テストの移し方（仕様の正本）

- Web のテスト名を、そのまま NUnit のテスト名に写す。Playwright と vitest のテスト名はほぼすべて日本語の挙動文になっている（tests §3）
- 切替 PR（撤去の 5c）で、消えた Web テスト名と増えた NUnit テスト名をスクリプトで突き合わせる。束ごとの突き合わせは、その束の最後のレーンを `feature/uitk` へ入れるときに同じスクリプトで先に行う（agent 既定。切替の日に差分が一度に溜まらないため）。差分は `docs/uitk/disposition.md` 1 枚に理由付きで書く（目視で済ませるものは証跡のパス、捨てるものは理由）。見た目の意図は参照キャプチャ（0-8）で辿る
- 前例: uGUI から Web へ移ったときも、画面ごとの操作パリティを最終検証にし（`docs/webui/plans/phase-d-cutover.md:33-37`）、捨てるものを `docs/webui/disposition.md` に書いた

**Web のテストの行き先**（tests §2.1。件数は正規表現で数えた値で、議論の記録のランナー実測〈vitest 809・Playwright 211〉とは少しずれる）

| 種別 | 件数 | 残す見込み | 行き先 |
|---|---:|---:|---|
| ロジック（L） | 311 | 約 290 | L0（C# の純ロジックと NUnit） |
| コンポーネント（C） | 235 | 約 200 | L1 |
| 画面フロー（P） | 213 | 約 205 | L1。うちレイアウトの主張約 52 件は、L1 のレイアウト照合か L4 の目視 |
| 多言語（I） | 61 | 約 25 | L0 と L2。HTTP の辞書取得まわりは捨てる |
| 静的規約（S） | 91 | 約 35 | L2（意図だけ） |
| 通信契約（W） | 167 | 0 | 捨てる |
| C# の WebUi 系 | 285 | 154 | ビューモデル 90 とコマンド 36 は形を変えて残し、状態ストア 28 はそのまま残す。wire の 131 は捨てる（設定系の 10〜15 件は、設定サービスのテストへ移す） |

### 4.2 テストの層

| 層 | 中身 | 道具 | PlayMode |
|---|---|---|---|
| L0 純ロジック | スロットの計画、長押しクラフト、ビルドメニューの分類とスクロール追従、ビューポートの計算、ルーティング表、全体スイッチの片側読み、通知のストア、ビューモデル、コマンド、無応答時の入力排他 | NUnit（`dotnet test` が回れば Unity の外でも） | 不要 |
| L1 ビュー | UXML・USS・Presenter を偽モデルと記録用コマンドでつなぎ、操作をシミュレートして、発行コマンド・クラス・テキスト・レイアウトを照合する | UI Test Framework の `UITestFixture` と `PanelSimulator` | 不要 |
| L2 静的規約 | USS と UXML をテキストとして読む（§3.5、§3.12） | NUnit | 不要 |
| L3 結合 | ゲームを起動し、UI 状態の遷移、パネルの表示、コマンドのサーバー到達を確かめる。CEF 無しでの起動もここで見る | EditModeInPlayingTest（数は絞る） | 要 |
| L4 録画 | 実際の入力経路と見た目 | プレイテスト DSL | 要 |

Playwright は今 CI で走っていない（型検査だけ。`run_test.yml`）。L1 を CI に載せるので、移行後のほうが CI での検証面は広くなる。

### 4.3 EditMode の UI テスト（L1）

- **導入**: UI Test Framework 1.0.0（`com.unity.ui.test-framework`）は Unity 6000.3.8f1 に組み込まれているが、manifest には無い（実測）。段階 0 で manifest に足し、`Client.Tests.asmdef` から `Unity.UI.TestFramework.Runtime` と `.Editor` を参照する
- **本番に揃える**: `panelSize` は 1280×720、`themeStyleSheet` は本番の暗色テーマ、フォントは本番の FontAsset。文字の幅で決まるレイアウトがずれないようにするため
- **mock-host の代わり**: 画面ごとに `FakeXxxModel`（ReactiveProperty で組んだフィクスチャ）と `RecordingXxxCommands`（発行の記録）を用意する。Playwright の `setUiState` は偽モデルに、`payloadsOf` は記録リストになる
- **時間**: パネルのスケジューラか、注入した時計で動かす。`MonoBehaviour.Update` と `Time.deltaTime` で動かすと、`FrameUpdateMs` で時間を進められない
- **描かれない部分**: L1 は描画しないので、半透明の濃さや文字のにじみは L4 か ScreenCapture で見る
- **罠**（段階 1 で確かめる）: `MouseMove(from, to)` は途中の要素に PointerEnter を届けない可能性がある。ScrollView の子は、スクロールしても `GeometryChangedEvent` が発火しない可能性がある

### 4.4 録画プレイテスト（L4）と CEF 無し起動ラダー

**DSL**
- `IUiElementLocator` を Web 版と UITK 版の 2 本で持ち、全体スイッチの系の 1 本だけで探す（R2）。段階 1 で作った「UITK を先に探し、同じ testid が両系で見つかったら失敗」（tests §6.4）は作り直しで外す。Web 版は切替の日に消す
- UITK 版の手順
  1. ゲームのパネルから `Query(name: …)` で一致する要素を列挙する（`Q` は先頭の 1 件しか返さない）。0 件なら再試行し、2 件以上なら理由付きで失敗させる
  2. 自分と祖先の `resolvedStyle`（display・visibility・opacity）と、`worldBound` の面積で可視かを判定する
  3. `panel.Pick(中心)` が対象自身かその子孫かで、覆われていないかを判定する
  4. パネル座標を画面座標へ変換し、SemanticInput で注入する
- 文字は `SetText(testid, text)` で入れる（`TextField.value` を設定して ChangeEvent を流す。IME は通さない）
- UITK の画面では次のものが要らなくなる: WS の往復と「2 回続けて同じ矩形なら安定」の待ち、`CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery`、`PlaytestUiOps.cs:134-138` の pointerOverUi の待ち合わせ

**CEF 無し起動ラダー**（各段階の物差し）
- 全体スイッチの UITK 側（0-10 の経路。Web ホストを起動しない）で PlayMode を始める
- 段ごとにシナリオを足し、前の段のシナリオも毎回流す（退行を検知するため）。段階 2・3 のシナリオは、UITK 側にまだ無い画面に触らない形にする。切替 PR では、同じシナリオが CEF の存在しないビルドでそのまま通ること

| 段階 | シナリオ（仮称） | 内容 |
|---|---|---|
| 1 | `uitk-lifeline` | Esc → 設定で言語を切り替える → 日本語の本文でバグ報告を送る（本文は SetText）→ セーブして終了 |
| 2 | `uitk-opening-hud` | オープニングのスキットを選択肢まで進める → 歩いて採掘する（ゲージが出る）→ 数字キーで選んで設置する（ホットバーの表示は段階 3 から）→ 獲得通知と、目標 HUD を指すチュートリアル枠が出る |
| 3 | `uitk-build-bp` | B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける（検索語と BP 名は SetText） |
| 4 | `uitk-production-loop` | Tab → 長押しクラフト → 石窯に燃料と素材を入れる（掴み・Shift・右ドラッグ）→ 取り出す → 整理 |
| 4 | `uitk-research-train` | 研究を 1 つ完了 → 駅名を日本語で付ける（駅名は SetText）→ 時刻表を組んで自動運転を入れる |
| 5 | `uitk-challenge-gate` | チャレンジ一覧を開く → 出展モードの言語ゲートを通る |

本文と駅名は SetText で入れ、IME は §4.9 の合成キーで確かめる。

### 4.5 アンカーの付け替え

- **ID の文字列は変えない**: マスタに直書きされた ID は v8 で 9 件（実測: `challenge.current-hud` 1、`recipe.craft-button` 1、`research.node-<guid>` 7）。これに、`itemViewHighLight` から作る `recipe.item-<itemId>` が加わる。ID を保てば、マスタも `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` もそのまま成り立つ
- **語彙の正本は C# の `AnchorIds` 1 か所**（静的 19 個と動的 prefix 6 種）。`TutorialAnchorIdMapper` もここへまとめる。長命ブランチの中は、Web の `anchorIds.ts` と突き合わせるテストで同期を守り（master から Web のアンカーが増えたら取り込みで赤になる）、撤去で消す
- **付け方**: `TutorialAnchor.Mark(VisualElement, params string[] ids)` の 1 か所で、USS クラスとして付ける。`AttachToPanelEvent`・`DetachFromPanelEvent` で登録簿に出し入れする。Detach は同期で消すので、枠がアンカーより長生きしない
- **解決**: 0 件なら missing、2 件以上なら duplicate。ほかに非表示と画面外も判定し、開発者ログへ出す。全体スイッチで片方の系しか描かないので、系をまたぐ二重解決は起きない。同じ系の中の重複は duplicate としてログに出す（黙って先勝ちにしない）。位置は `GeometryChangedEvent` に加え、スクロールによる移動を拾うため、毎フレーム `worldBound` を見る
- **祖先のクリップ**（ADR 0024）: overflow を隠す祖先の `worldBound` との交差で求める。祖先の overflow を公開 API で読めなければ、クリップする容器に印のクラスを付ける（推測）
- **カバレッジテスト**: 全静的 ID と全 prefix が、どこかの画面の L1 マウントで登録されることを確かめる

### 4.6 見た目の確かめ方

- 画素のパリティは目標にしない。前回は uGUI の正本が消えて追従できず打ち切った（`.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`）。今回は参照キャプチャ（0-8）を残して意図を辿る
- 一致させるもの: 機能、トークン（色・寸法・層）、移したテスト。比べ方: 参照キャプチャと、UITK の ScreenCapture（透かし入り）を並べる
- 一致させる見た目は Web 版（§0「見た目」）。参照キャプチャと並べて差があれば、Web 版へ寄せる。影響が小さく非本質的な対応が入るのであれば（§0「見た目」の原文どおり）、推奨案のまま残して台帳に書く
- 撮る解像度: 720p と 21:9 の 2 種を、GamePanel の初出（段階 1）で 1 回
- 波ごとに画面の写真を外部監査（codex-audit、画像付き）にかける。ユーザーの目視は切替 PR の検証ビルドで 1 回（§2.0。段階の区切りでの staging の目視は棄却された）

### 4.7 決定論ゲート

| # | ゲート | 中身 | 入れる段階 |
|---|---|---|---|
| G1 | Web 追従 | 長命ブランチ側で持つ（下の「G1 の走り方」）。ワークフロー・スクリプト・台帳のファイルは master に残し、master の PR では G1 の job を skip する（ファイルを消すと feature/uitk への取り込みで削除衝突が起きるため。agent 既定）。最初の取り込み PR で束 1 を `暗転` へ戻す（§2.2 作業（画面）の 2 行目） | 0（最初の PR）。2026-11-06 に `feature/uitk` へ移した |
| G2 | uGUI 追加の禁止 | `WebUiGateAuditTest` を残す。撤去の 5e で名前を変え、対象を付け替える（`.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` の、両方で守る形を保つ） | 既存 |
| G3 | USS・UXML の規約 | §3.5 と §3.12 | 1 |
| G4 | 撤去後 | `WebUi`・`Cef`・`webui` のトークンが、許可リストの外で 0 | 5（撤去） |

**G1 の走り方**（2026-11-06 裁定「G1 と追従台帳は master から外し、長命ブランチ側で記帳する」）
- 走るのは `feature/uitk` への PR と、`feature/uitk` から master への切替 PR だけ。master への PR では走らないので、master の作業者は台帳を触らなくてよい。ワークフローの `if` は `github.base_ref == 'feature/uitk' || github.head_ref == 'feature/uitk'` で絞る
- 束の状態: 束のレーンを `feature/uitk` で始めるときに `暗転` にし、`暗転の起点` にそのとき `feature/uitk` に取り込んである最新の master のコミットを書く。作り終えた束も、切替 PR までは `暗転` のまま置く（`切替済み` にすると、master の取り込みで入る Web の変更を G1-2 が落とし、Web を凍結したのと同じになるため。agent 既定）
- `feature/uitk` への PR（レーンと master の取り込み）: G1 は台帳の網羅（`web/src` の全ファイルがどれかの行に入っている）を検査し、暗転中の束ごとに未記帳の件数を出す。未記帳では赤にならないので、取り込みの PR では件数が 0 であることを見てからマージする（agent 既定。§1.1）。ローカルでは同じスクリプト `.github/scripts/check-uitk-web-follow.py --base <取り込み前> --head <取り込み後>` で確かめられる
- 切替 PR: 全束を一度に `切替済み` にし、全行の所有者を `UITK` にする。base（master）の台帳は全束 `Web` なので全束が「今回切り替える束」になり、どれかの束の起点以降に未記帳の Web 変更が 1 件でもあれば赤になる。束ごとの暗転の起点と、切替済みの束の判定は、この PR で全部まとめて行う
- レーンと取り込みは `feature/uitk` 向けの PR として開き、G1 が緑なのを見てからマージする（ローカルマージで push すると G1 は切替 PR まで一度も走らない）
- 切替 PR の前に、`feature/uitk` 上で Web を触ったコミット（§2.2 の休止の撤去・撤去 5c の削除など）を各束の「## 追従記録」へ `捨てた（理由）` で記帳する。しないと切替 PR の G1-1 が全束の未記帳として赤にする（§2.6）
- 切替済みの束の Web パスへの変更を落とす規則（G1-2）は、切替の後に `feature/uitk` へ入る変更にだけ効く。切替 PR は Web を消すので、実際に働く場面はほぼ無い（推測）

### 4.8 実測（段階 0）

段階 0 で行うのは、段階 1 の前提になる 5 本。1 本 1 日で打ち切り、証拠付きで台帳に書く。PR #1558 の報告では S1・S2・S3 が合格、S15 が不可（下の表）。S9 は 2026-10-28 に OS 段の合成キーで確かめ、成立した（下の表。bd `moorestech-albof.8`）。

| # | 確かめること | 不可だったときの手 |
|---|---|---|
| S1 | UITK パネルと CEF の Canvas の前後を、PanelSettings の sortingOrder で決められるか（CEF の Canvas は Overlay・sortingOrder 0。実測）。背面パネルを足せるか | CEF の Canvas の sortingOrder を動かす。それでも駄目なら、委譲中に Web を空にする範囲を広げる |
| S2 | Unity Recorder（`PlaytestRecorder.cs:33` の GameViewInputSettings）、`PlaytestScreenshot`、バグ報告の `ScreenFrameReader`（`CaptureScreenshotIntoRenderTexture`）に、UITK が写るか | 撮影経路を ScreenCapture の連番へ直すまで、段階 1 に入らない（**赤信号**。§6） |
| S3 | `QueueStateEvent` で注入した入力が、`InputSystemUIInputModule` を経て UITK に届くか。パネル座標と画面座標の往復。`IsPointerOverGameObject` が UITK を拾うか | 入力資産・EventSystem・注入経路を直す。UITK のイベントの直接合成は L1 に限り、L4 の代わりにしない（L4 は実際の入力経路を確かめる層のため） |
| S9 | **日本語 IME**（Windows 検証機の配布ビルドと Mac）。単行・複数行で、変換・確定・取消・ESC を試す。フォーカスを往復した後に入力が戻るか。CEF を起動した状態と、CEF を初期化しない状態の 2 条件で同じ手順を行う。後者は S9 用の PoC ビルドを、CEF の起動を外してもう 1 本焼く（使い捨て。Editor 限定スイッチは配布ビルドで効かず、配布物に切替フラグも入れないため）。CEF の常時 `imeCompositionMode=On` が結果を変えるため。**成立**（2026-10-28。Mac mini と Windows 検証機で OS 段の合成キー）: 両条件・単行と複数行で、変換・確定・変換中の取消・フォーカス往復が通った。確定後の ESC・CEF との IME モードの取り合い・候補窓の位置などは段階 1 の要件にした（§4.9）。証拠 https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-28-uitk-s9-ime-result/ | **赤信号**（§6）。当たらなかった |
| S15 | 委譲中の Web を `pointer-events: none` にしたとき、CEF 経由の pointerOverUi が false になり、Web が action を出さないか。**不可**: action は止まったが、pointerOverUi が true のまま固まった（PR #1558） | 委譲中の状態では、Web の HUD も描かない（ポーズ中に HUD が消える）。2026-10-26 の裁定で、UITK が主パネルかモーダルを出しているあいだは Web を全面的にオフにする（R1） |

- S13（UI Test Framework が CI の batchmode で動くか）は、段階 1 の最初の L1 テスト（本番テーマの要素で schedule と transition を進める）を既存 CI に載せて確かめる。動かなければ §6 の 3
- ほかは作業の中で確かめる: S4・S5（入力）、S7（半透明と小さい文字）、S10（フォント）は段階 1 の各レーンの先頭（§2.2）。S12（`dotnet test`）は L0 レーンの先頭（§3.13）。S14（`HttpListener`）は 0-3 の完了条件

### 4.9 IME と実機

- IME は OS 段の合成キーで自動に確かめられる（S9 で実証）。InputSystem への注入（`QueueStateEvent`）は OS の IME を通らないので使えない。Mac は cua-driver の press_key か osascript の key code（入力ソースの切替は key code 104／102）、Windows は `SendInput` の仮想キーコード＋スキャンコード（`KEYEVENTF_UNICODE` は IME を通らない。対話セッションの scheduled task から送る）。手順と道具は S9 の報告（bd `moorestech-albof.8`）。確かめるのは配布ビルド（段階 1 は CI の開発ビルド）で 3 回
  - 段階 0: S9（2 条件）。2026-10-28 に済んだ
  - 段階 1（済み。台帳の R-15。2026-11-06 の裁定より前に Windows 検証機で CI の開発ビルドを使った）: バグ報告（本番で最初の入力欄。複数行）。S9 で見つかった次の要件を満たすことも見る（bd `moorestech-albof.12`）: 確定後の ESC で値を戻さずフォーカスだけ外す、IME のモードを入力排他と同じ 1 か所で決める（§3.8）、候補窓をキャレットの位置に出す（Mac は上下反転、Windows はキャレット座標の約 1.5 倍に出ていた）、変換中の文字に下線を付ける、クリックで全選択しない
  - 切替 PR: CEF を含まない配布ビルドで入力欄を一巡する（切替前のビルドを配布環境で回すのはここだけ）
- 人が見るのは、実キーボードで打ったときの体感（遅れ・候補窓のちらつき）と、全画面・他の解像度での候補窓の位置だけ。切替 PR の目視（§7.4）に含める
- 変換中の文字列も値として ChangeEvent に乗る（S9）。値を購読する側（文字数の検証・即時の絞り込み）は、未確定の文字列でも壊れないように作る
- 段階 3・4 の入力欄（検索・BP 名・駅名）は同じ TextField 部品を使うので、部品を変えない限り L1 と Editor の録画で済ませる。部品を変えたら、Editor の Mac の合成キーで 1 回足し、Windows は切替 PR でまとめて見る
- 実機で見る項目（切替 PR）: UI スケールと DPI、Steam オーバーレイ、言語 4 種（数値の書式とフォールバックを含む）、Ctrl+U・カットシーン・出展モード

---

## 5. 完了の定義

### 5.1 完了の定義（観測できる形）

| # | 条件 | 観測のしかた |
|---|---|---|
| 1 | manifest・prefab・配布ビルドに CEF が無い | manifest と lock に `jp.juha.cefunity` が無い。MainGameUI.prefab に CEF の Canvas・RawImage が無い。配布ビルドの Windows `Plugins/x86_64` と Mac `PlugIns/cef-unity-server.app` に CEF のファイルが 0（`release-artifact.sh` で検査） |
| 2 | G4 が緑 | `Client.WebUiHost`・`moorestech_web/`・Web 用の Kestrel が無いことを含む |
| 3 | 遠隔実行が検証機で動く | Windows 検証機で、ADR 0072 の完了条件 3 点（有効時に結果が返る、Harmony で差し込める、オプション無しでは開かない）を確かめる |
| 4 | 全録画とラダーが、CEF の存在しないビルドで緑 | ラダー 6 本と全録画（DSL は Editor 専用なので、CEF パッケージの無い HEAD の Editor で流す。A-259）。壊れている 10 本は直すか削除して bd `moorestech-wwxy` を閉じる |
| 5 | 切替ビルド（切替と撤去を含むビルド）が playtest で 1 周期通る | 1 周期 ＝ 切替ビルドを playtest へ反映してから 7 日以上、かつその間の Discord と受け口の報告を 1 回見た後（2026-10-26 版の R3 の定義をそのまま使う。agent 既定）。UI が原因で進行を阻む不具合が 0 か、全件修正済み。戻すときは切替前のビルドへ戻す（§7.4） |

文書・スキル・ADR・bd の後片付けは、撤去の 5e の作業項目に置く。

### 5.2 完了の定義に入れないもの（別に判断する）

- `com.unity.ugui` の撤去（ADR 0052 の例外 2〜4〈タイトルとロード、HP バー、デバッグ UI〉が残る）
- `activeInputHandler` を InputSystem だけにすること
- Mac の Universal 化と Linux クライアントの再開。CEF を消しても、ffmpeg も arm64 版だけを同梱しているので、arm64 固定は自動では解けない（ADR 0071）
- タイトルとロード画面の UITK 化（`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`）

---

## 6. リスク

| # | リスク | 兆候・検知 | 潰し方 | 赤信号か |
|---|---|---|---|---|
| 1 | 日本語 IME が UITK の TextField で成立しない | S9（2026-10-28 に成立を確認） | ユーザーへすぐ上げる。2 条件（CEF あり・なし）の結果を切り分けてから、IME の composition を自前で扱う TextField を作るか、文字入力のある画面（バグ報告・検索・駅名・BP 名）だけ Web に残すかを決める。後者は画面ごとに UITK と Web を混ぜることになり、2026-11-06 の一気切替の裁定に反するので、選ぶならユーザーの裁定が要る | **赤** |
| 2 | 録画・バグ報告の動画に UITK が写らない | S2 | 撮影経路を直すまで段階 1 に入らない。写らないまま進むと、録画での検証とテスターの報告動画が全部欠ける | **赤** |
| 3 | UI Test Framework が CI の batchmode で動かない | 段階 1 の最初の L1 テスト（S13） | 原因を調べて直す。直せないと分かった時点で、L1 をローカルの Editor レーンへ移すかを裁定に上げ、並列度を下げた日程に組み直す。transition が進まなければ UiMotion の時計を差し込む | 中 |
| 4 | 並行セッションとの衝突（液体 UI が `BlockInventoryTopic` を触る等） | マージ時の衝突 | integrator が共有ファイルを直列で持つ。topic 群ごとに短い作業窓を bd で告知する | 中 |
| 5 | Editor の本数と追従が律速になる | 待ちのレーン数・未記帳の件数 | 独立レビューは切替 PR と全体スイッチの作り直しだけに付ける。Editor の要らない作業（L0・テスト名の写し・UXML の下書き）を並べる | 中 |
| 6 | 規模の見積もりが外れる | 各段階の plan | 段の切り方は変わらず、切替の日だけが延びる。延びた分、CEF の不具合の放置期間（リスク 10）も延びる。切替の見込みが大きく延びたら（目安: 当初見込みの 1.5 倍。agent 既定）ユーザーへ知らせる | 中 |
| 7 | 移行中も Web が変わり続け、UITK 側が追いつかない（裁定 4 の帰結） | 取り込みごとの G1 の未記帳件数。台帳に数を書く | 取り込みのたびに記帳し、移し終えた画面の追従を未移植画面の実装より優先する（裁定）。そのぶん残り画面の完成と切替の日が延びる | 中 |
| 8 | 切替の日の退行の幅が全画面ぶんになる | 切替 PR の全録画・ラダー・検証機・Mac の Steam。切替後の Discord と受け口 | 段階 4 の終わりと切替 PR で全録画を流す。切替 PR の中で配布環境（Windows 検証機・Mac の Steam）を通す。オーナーの目視と IME の体感を切替 PR に集める。戻すときは切替前のビルドへ戻すしかない（Web へ戻すデバッグ切替は無い。裁定）ので、戻しの手順を切替の前に確かめておく（§7.4） | **高**（赤信号ではない。裁定の帰結として受け入れる） |
| 9 | 長命ブランチが master と乖離し、取り込みでマージ衝突が増える | 取り込みの衝突のファイル数。共有ファイル（composition root・prefab・シーン・`localization.csv`・テスト）の衝突 | 週 1 回と各レーンの分岐前に取り込む（agent 既定）。`feature/uitk` で Web のコードを直接書き換えない（R3）。prefab とシーンは integrator が uloop で 1 本ずつ持つ。段階 1 の Web 側の休止の変更は外す（§2.2） | 中 |
| 10 | CEF の不具合が切替の日まで放置される | Discord と受け口の UI 全停止・メモリの伸びの報告。master へ切り出す CEF 停止の記録の値 | 直さない（裁定。起動不能・データ消失を含む）。原因の値だけは CEF 停止の記録で集め、CEF 由来でない不具合と切り分けて、CEF 由来でないものは master で直す（§3.11） | 中（裁定の帰結として受け入れる） |

**負債が残るもの**: 切替の日まで、CEF が固まると最後のフレームが画面に残り（Discord 2026-10-03「インベントリ開いたままだったので閉じれなくなってました」の形）、Esc でのセーブと報告もできない場合がある。段階 1 の命綱は切替の日までテスターに届かない（2026-11-06 の帰結）。CEF 停止の記録で原因の値だけは届く。

---

## 7. 裁定と既定

### 7.1 着手前に聞くこと（段階 0 の前に 1 回で）

2026-10-26 時点の記録。「推奨」は agent の案。議論の記録が未裁定とした 5 点のうち、出荷形・第一弾はここで聞き、仕様の正本・完了の定義・AI 向けの作法は §7.2 の既定にした。**裁定 1 と様式化は 2026-11-06 に覆った**（今の裁定は §0、覆った経緯は §7.3）。裁定 1 は選択肢 (C)「移行ブランチに溜めて一括で入れる」に近い形になった。(C) の欠点として挙げた「約 2 万行が配布環境に触れないまま最後に出る」は、新しい方針でもそのまま残る（§6 のリスク 8）。

| # | 論点 | 選択肢 | 推奨 | 理由と帰結 | 既存裁定との関係 |
|---|---|---|---|---|---|
| 裁定 1（旧 D1。2026-10-26 裁定済み。**2026-11-06 に覆った**） | 移行中の出荷形 | (A) 束単位の strangler。束は暗転で作り、所有表の 1 行で切り替えて毎週出す。持ち物の束も束ごと一度に切り替える (B) UI スタックを丸ごと切り替える（起動オプション `--ui`。既定 Web で、最後に一括） (C) 移行ブランチに溜めて一括で入れる (D) (A) のうち持ち物の束だけ族ごとの小段階で出す | **A** | テスターの実害（CEF の停止とメモリの伸び）に、数週間で命綱が届く。配布環境の差を束ごとに浴びられ、どこで止めても出荷できる。持ち物の束を一度に切り替えるので、系をまたぐ掴みが起きず、InventoryPanel の二重実装も族ごとの所有表も要らない。帰結: 見た目の混在が届き、共存の仕組み（撤去で消す）を持つ。インベントリ系の CEF 不具合は段階 4 の終わりまで残る。(B) は配布物に切替が入り、テスターへの価値が最後まで 0。(C) は約 2 万行が配布環境に触れないまま最後に出る。(D) は早く届くが、途中で止めると InventoryPanel の二重実装が残る | 議論の記録の agent 案と同じ向き |
| 裁定 2（旧 D2。**2026-10-26 裁定済み**） | 第一弾の画面 | (A) 命綱（ポーズ・設定・バグ報告・トースト） (B) 入力の無い HUD (C) ビルドメニューとホットバー (D) 議論の記録の「インベントリかホットバー」を単独で | **A**。CEF の延命は §3.11 の 3 つだけ（manifest の生の値・LogError・無応答時に入力排他の Web 側を無視）。自動再起動や raw input の手当ては作らない | 失うものが最も大きい口（報告とセーブ）を先に守る（§2.2）。IME・ESC・フォーカスを、掴み抜きで先に通せる。(B) は CEF が死んだときに救えるものが少ない（段階 2 で続けて出す）。(D) のホットバー単独は建築の束を割り、インベントリは最大の束に未熟な基盤で挑むことになる。延命は撤去する対象への投資になる | 議論の記録の候補（未裁定）とは異なる。延命しないのは `.decisions/2026-09-13-CEF raw input応急処置は入れず…` と一致 |
| 裁定 3（旧 D6。**2026-10-26 に (a) で裁定済み**） | 遠隔実行の待受 | (a) `HttpListener` へ載せ替え、AspNetCore 系の NuGet を外す (b) 遠隔実行専用の最小 Kestrel として残す (c) 専用の TCP 待受 | **(a)**。配布ビルドで動かなければ (b)（0-3） | NuGet と Kestrel の保守面積が大きく減り、Web 用の Kestrel を消せる。今は Web ホストの起動失敗で遠隔実行も開かない。帰結 (b): Kestrel と NuGet が遠隔実行のためだけに残る | **ADR 0072 と矛盾**（「入口は既存の Web UI サーバーに 1 本足す」、棄却案「専用の待ち受けを新設」）。どれを選んでも改訂が要る。トークン・起動オプション・結果の 7 種は変えない |
| 裁定 4（旧 D7。**2026-10-26 に (c) で裁定済み**。UITK が安定稼働するまで Web の変更を続ける。G1 は追従ゲートへ変えた） | Web の凍結と新機能の行き先 | (a) G1 で `web/features` の新規ファイルを止める。Web はテスターを止める不具合だけ直す。新しい UI は段階 1 の基盤ができたら UITK にだけ作る。未移行の画面に機能が要れば、その束を前倒しする (b) 全面凍結（不具合も直さない） (c) 従来どおり | **(a)** | (b) は CEF 由来の致命的な不具合も直せない。(c) は移す対象が増え続け、二重作業になる。10-15 以降の 44 コミットは master 済み。凍結時に web を触る open PR は #1348（web 2 ファイル）だけで、許可リストに入れるか先にマージする。G1 は段階 0 の最初の PR で入れる | `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`（Web 専任）は 10-18 の裁定で覆っている |
| 確認（旧 D18。**2026-10-26 裁定済み**） | UI の演出の時間源 | (a) 実時間（パネルのスケジューラ） (b) GameUpdater のティック | **(a)** | ポーズ中もポーズ画面の演出は動くべき。AGENTS.md「経過時間は GameUpdater のティック加算だけ」は、サーバーのゲームロジックの規則と読む。この読み方の確認 | AGENTS.md の解釈 |
| 様式化（旧 D14。2026-10-26 裁定済み。**2026-11-06 に覆った**） | Mantine の素の見た目の画面（トースト・チャレンジ・列車 HUD・ポーズのボタン） | (a) 移すときに、ホワイトリストの語彙で様式化する (b) 今の見た目を再現する | **(a)**。4 画面を同じ語彙で一度に決める | Mantine の既定は再現の正本にならない。負債 `moorestech-cgm` も閉じられる。語彙は ref-webui-design のホワイトリストで共通 | ADR 0014 の「他の Button は今回触らない」を、移行の範囲で解く |

**段階 0 の中で出た論点（2026-10-26 裁定済み）**

| 論点 | 裁定 | 計画への反映 | 記録 |
|---|---|---|---|
| 旧 UITK の SkitUI | 削除し、会話 UI の予備を持たない | 0-12 | `.decisions/2026-10-26-旧UITKのSkitUIは削除し会話UIの予備を持たない.md` |
| 差し込み記法（旧 D8） | 名前付きに統一し、`GetFormatted` も名前付きに改める。全言語一致テストは残す | 0-6・§7.3 | `.decisions/2026-10-26-翻訳文の差し込みは名前付きに統一しGetFormattedも名前付きに改める.md` |
| Codex で進められない実装 | 段階 0 の遠隔実行・状態の移設・DSL の残りと、撤去・差し込みの統一は、Claude Code で進める | §7.3 | `.decisions/2026-10-26-UITK段階0でCodexで進められない実装はClaude-Codeで進める.md` |
| S15 の不可（委譲中の Web の入力） | UITK が主パネルを出しているあいだは、Web を全面的にオフにする。Web の HUD も消える | 2026-11-06 の全体スイッチで、UITK 側では常に Web がオフになる形に読み替えた（覆ってはいない）。R1・§3.8 | `.decisions/2026-10-26-UITKの主パネル表示中はWeb-UIを全面的にオフにする.md` |
| 段階 0 の PR の数 | 統合ブランチ `feature/uitk-stage0` へ全レーンを取り込み、PR は 1 本にする。独立レビューも統合 PR に 1 回 | §2.1 | `.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md` |

### 7.2 agent の既定として進め、台帳に記録するもの（大きく外れたら裁定に上げる）

- 仕様の正本（旧 D3）: Web のテスト名を NUnit へ写し、束の最後のレーンと切替 PR で突き合わせる（§4.1）。見た目の意図は参照キャプチャで辿る
- 完了の定義（旧 D4）: §5.1 の 5 項目。議論の記録の「Kestrel ホスト削除」は裁定 3 の結果に合わせる。1 周期の定義は 2026-10-26 版の R3 のものを使う
- AI 向けの作法（旧 D5）: §3.12 を `ref-uitk-design`・L2・moores-code-review の UITK 観点で固める
- ~~Web を全面オフにする範囲（S15 の裁定の読み方）~~: 2026-11-06 の全体スイッチで不要になった（UITK 側では Web ホストを起動しない）
- 半透明と小さい文字（旧 D10）: トークン表に UITK 列を持ち、部品を作る段で実機で決める。12px 以下は 13px へ上げる
- アニメーション基盤（旧 D11）: 自作の UiMotion（100〜150 行、時計を注入）を段階 1 で作る。要件が増えたら LitMotion v2.0.2
- 画像（旧 D12）: ビットマップは使わない。面と図形は Painter2D と頂点カラーのメッシュ。単純なアイコン（×・ピン・矢印）は Painter2D の命令で描き、既存の SVG を流用したいアイコン（スキットの 6 種など）は VectorImage を許す
- トークンの正本（旧 D13）: USS。C# は `customStyle` で読む
- パネルは 1 枚。背面パネルを足すのは S1 の結果次第。UITK は CEF の上に置く
- PanelSettings は Scale With Screen Size・1280×720・Expand・scale 1（倍率は `min(w/1280, h/720)` で、Web の `App.tsx:39` と同じ）
- ~~共存中、Web の全画面状態の間は UITK の通知層を隠す~~: 全体スイッチで UITK と CEF が重ならないので不要になった。通知は ADR 0017 の形（背面の層）で作る
- `name` は testid と同じ文字列にし、アンカーは USS クラスで付ける
- 入力欄での ESC は、フォーカスを外すだけ。入力欄以外は `focusable=false`。UI のパッド操作は作らない
- 長命ブランチの運用（2026-11-06 の裁定に付けた既定。§1.1）: master の取り込みは週 1 回と各レーンの分岐前。取り込みの PR は G1 の未記帳 0 を見てからマージする。取り込みと記帳は integrator が持つ
- 全体スイッチ（§3.3）: UITK 側では Web ホストを起動しない（0-10 の経路）。Editor 限定で既定は Web。`feature/uitk` で Web のコードを直接書き換えず、段階 1 で入れた Web 側の休止の変更は外す
- 台帳の束の状態: 作り終えた束も切替 PR までは `暗転` のまま置き、切替 PR で全束を `切替済み` にする（§4.7）。束 1 は `切替済み` から `暗転` へ戻す
- 切替 PR の形: 撤去 5a〜5e を `feature/uitk` の上で積み、`feature/uitk` から master への 1 本で切替と撤去を運ぶ（§2.6）
- 独立レビュー: 切替 PR と全体スイッチの作り直しだけ（§3.13）
- オーナーの目視: 切替 PR の検証ビルドで 1 回（§2.0）
- 見た目: Web 版と同じにするのが裁定。ホワイトリスト（ref-uitk-design）は Web 版に無い新しい面を作るときにだけ効く（§3.12）
- 閾値は既存の定数を参照する: 押下は `usePressGesture`（5px）、ツールチップは `OFFSET`（12）、通知は `NOTIFICATION_DISPLAY_MS`（7000ms）、無応答は `webSocketClient.ts`（15000ms）

### 7.3 改訂が要る既存の裁定・ADR

**2026-11-06 に覆った裁定**

| 裁定 | 覆した裁定 | 覆った範囲 |
|---|---|---|
| `.decisions/2026-10-26-UITK移行は束ごとに暗転で作り所有表の切り替えで毎週出す.md`（裁定 1） | `.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`、`.decisions/2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`、`.decisions/2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md` | 全部。束ごとの切替・所有表・毎週出荷・1 周期後の削除 PR（R3）・配布物に切替フラグを入れない（R4。全体スイッチは Editor 限定なので形は残る） |
| `.decisions/2026-10-26-素の見た目の4画面はUITKへ移すときに様式化する.md`（様式化） | `.decisions/2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`（トースト・ポーズのボタン）、`.decisions/2026-11-06-UITK段階2以降の画面もWeb版と同じ見た目にする.md`（チャレンジ・列車 HUD） | 全部（4 画面とも様式化しない） |
| 裁定 2 の帰結「CEF が死んでも Esc でセーブと報告ができる」 | `.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md` の帰結 | 第一弾を命綱にした裁定は残る。テスターに届くのが切替の日になった |
| `docs/superpowers/plans/2026-11-05-uitk-stage1-screens.md` の D-17・D-27（GamePanel の様式のまま） | `.decisions/2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md` | Web の枠へ戻す |

読み替えたもの（覆ってはいない）: `.decisions/2026-10-26-UITKの主パネル表示中はWeb-UIを全面的にオフにする.md` は、全体スイッチの UITK 側で常に満たされる。`.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md` はそのまま効き、追従の記帳が長命ブランチ側へ移った。

**矛盾するもの**

| 裁定・ADR | この計画での扱い | 時期 |
|---|---|---|
| ADR 0072（遠隔実行の入口は Web UI の Kestrel） | 待受を切り離す（裁定 3。2026-10-26 に HttpListener で裁定済み）。同じ PR で改訂する | 段階 0 |
| ADR 0006 決定 5（マスタ由来の表示名は Web の辞書で解決） | 「ビュー側（UITK）で解決」へ改める。DTO が Guid とキーだけを運ぶ原則は保つ | 段階 1 |
| 議論の記録「見た目は uloop screenshot で自己確認」「完了の定義に Kestrel 削除」 | ScreenCapture 系へ改める（rendering モードに UITK が写らない。PoC の実測）。Kestrel は裁定 3 の結果に合わせる | 段階 0 |
| `.decisions/2026-08-26-プレースホルダ規約は全言語一致テストで守る.md` | パリティテストは残す。「`GetFormatted` のシグネチャは変えない」は、0-6 の統一の時点で位置指定から名前付きへ改める（2026-10-26 裁定） | 段階 0 |
| `.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` | 段階 0 のうち Codex で進められない遠隔実行・状態の移設・DSL の残りは、例外として Claude Code で進める（2026-10-26 裁定） | 段階 0 |

**実装の節を書き換える ADR・裁定**（その段で触り、撤去の 5e で改訂か廃止をまとめて記録する）
- ADR 0013・0017（全体スイッチで通知を隠す逸脱は無くなった）、0016、0019、0020・0074、0021、0024、0052、0057、0071
- `.decisions/` の `2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md`、`2026-08-19-獲得通知の集約はWebUI側で行う.md`、`2026-08-19-カーソルツールチップの書式はWeb側が持つ.md`、`2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md`、`2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md`、`2026-08-23-操作ヒントの正はC#のUIStateに置きtopicで配る.md`、`2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md`、`2026-10-01-*`（Space の封じ、メニュー中の移動、入力欄の移動停止、クラフトボタンと Space）、`2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md`、`2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md`、`2026-09-13-CEF raw input応急処置は入れず…`、`2026-09-09-WebSocketHubをレジストリの単一所有者にする.md`
- AGENTS.md「イベント発火に Action を使わない」の既存の違反は 0-5 で解消する

### 7.4 ユーザーにしてもらう作業

- §0 の裁定は、2026-10-26 と 2026-11-06 に済んだ
- **CEF 停止の記録の PR のマージ**（master。§3.11）
- **独立レビューの裁定**（切替 PR と全体スイッチの作り直しだけ）
- **切替 PR の目視とマージ**: 切替 PR の検証ビルドで 1 回だけ目視する（§2.0）。日本語 IME の実キーボードでの体感もここで見る（IME の合否は §4.9 の合成キーで確かめる）。マージした後の playtest への反映は moores-playtest-release で agent が進める
- **切替後の戻しの判断**: 切替の後に UI が原因で進行を阻む不具合が出たら、切替前のビルドへ戻すか、master で前に進めて直すかを決める。戻すのは Steam の playtest ブランチに切替前のビルドを当て直す操作になる（推測。手順は moores-playtest-release で切替の前に確かめる）。Web へ戻すデバッグ切替は残さない（裁定）ので、ゲームの中で Web へ戻す手段は無い。master を revert して Web へ戻すかは、そのときに決める（裁定には無い）
- レーンの `feature/uitk` へのマージと master の取り込みは agent が行う。ユーザーの作業は無い
