# UI Toolkit 全面移行 計画の補足（採点と訂正）

- `docs/uitk-migration/plan.md` の補足。計画の中身ではない（3 草案から軸を選んだ記録と、事実の訂正の記録）。
- 「7. 採点表」（7.1〜7.4）は plan-v2 の §7 をそのまま移した。中の節番号・段階番号・裁定番号（D1〜D20）は plan-v2 のもの（段階 0〜6）。plan-v3 では段階を 0〜5 に畳み、着手前の裁定を 4 件に絞った。
- 末尾の「レビューでの訂正（2026-10-23）」は、2026-10-23 のレビュー（Fable 4 観点・Codex gpt-6-astra 2 観点）で見つかった訂正。

---

## 7. 採点表

### 7.1 3 案の採点（1〜5）

| 軸 | strangler（リスク先行・画面単位） | foundation（基盤先行・一括切替） | player（テスター体験先行） |
|---|---|---|---|
| 途中で止めても出荷できるか | **5**: 段も小段階も出荷でき、止めた所の状態が束単位で一貫している | **3**: 出荷はできる。ただし P4 より前に止めると、テスターへの価値が 0 のまま、暗転のコードと `--ui` の切替が配布物に残る | **4**: 段階 1 で止めても命綱が残り、価値は大きい。ただし状態ごとの描画者の分割（ホットバーは BuildMenu だけ Web など）で、止めた所の見た目が画面内で切り替わる |
| 難所を早く踏むか | **4**: P1 で共存の物理を実測し、P2 で D&D・ツールチップ・IME・入力の取り合いを本番で踏む。掴みと TreeView は P4 | **4**: P0 の実測 13 本と P2 のパイロットで、技術の難所を全部踏む（最良）。ただし配布環境の差（移行の理由そのもの）を浴びるのは P4 | **3**: IME と ESC は段階 1 で踏む。D&D と掴みは段階 3、スクロールスパイは段階 4、TreeView は段階 5 |
| 共存期間の短さ | **2**: 実行時の共存が P1〜P5 の全期間。P4 では InventoryPanel が両系に 2 つある | **4**: テスターの前で両系が並ばない。ただしコードの上の共存（P2〜P4 と退避の 1 周期）と、凍結中のバグ修正の二重化がある | **2**: 実行時の共存が段階 1〜5 の全期間。状態ごとの描画者の分割が 3 つある |
| 検証の確かさ | **4**: 段ごとに配布環境（検証機・Mac）を通し、差分が小さいので原因を追いやすい。参照キャプチャが無い | **4**: 実測・参照キャプチャ・ゲート G1〜G7・17 項目の完了の定義・4 解像度の撮影と、成果物は最も厚い。ただし UI 全体が配布環境に出るのは P4 に集中する | **5**: CEF 無し起動ラダーで、移行の目的（CEF が無くても遊べる）を段ごとに自動・累積で測れる。CEF 停止の検知が開発者に届く経路も持つ。実際に CEF を落とす確認もある |
| AI エージェント並列での実行しやすさ | **3**: 段の中で 3 本まで。束を段ごとに直列に進める | **5**: integrator・殻・波・1 単位 1 PR・依頼文の型・純ロジックの別レーン | **3**: 段の中で 2〜4 本。段階 3 と 4 は並走できるが、所有表が熱い共有ファイルになる |
| 既存裁定との整合 | **4**: 実行時の切替フラグを作らない（AGENTS.md・前例に沿う）。ADR 0072 と 0006 の改訂は、どの案でも避けられない | **3**: 配布物に `--ui` の切替が入る（AGENTS.md「デバッグ/テスト専用 public をプロダクションに残さない」と緊張）。議論の記録の agent 案と逆。最小 Kestrel も ADR 0072 の棄却案「専用の待ち受けを新設」にあたる | **3**: 装飾を段階 7 へ回すのは、ユーザー規約「推奨案に、既知の負債を据え置く・後で一括改修を置かない」に反する。ADR 0017 との一時的な逸脱がある |
| **合計** | **22** | **23** | **20** |

### 7.2 軸の選び方

- 合計は僅差だった（foundation 23、strangler 22、player 20）。そこで「長所を他の案へ移せるか」で軸を決めた。
- strangler を軸にした。理由は 3 つ。
  - strangler の長所（配布物に切替を入れない、毎週テスターと配布環境へ出る）は、出荷形そのもの。foundation へ移すと、foundation の核（スタック丸ごとの切替）を捨てることになる。
  - foundation の長所（並列の仕組み・実測・参照キャプチャ・ゲート）と player の長所（命綱が先・停止の検知・ラダー）は、出荷形と独立していて、strangler にそのまま移せる。
  - 裁定 10-18 の理由「数万の環境で UI が壊れない」と、テスターの実害（§2.2）に、最も早く効く。
- strangler の弱い 2 軸は、統合の中で手当てした。
  - 共存期間: 束の中を暗転で並列に作り、所有表の 1 行で切り替える。小段階ごとに InventoryPanel を二重に出すのをやめ、族ごとの所有表のキーも要らなくした。
  - 並列: integrator・殻・波・純ロジックの別レーンを入れた。

統合案の見込み（自己評価で、参考）:

| 軸 | 見込み |
|---|---|
| 止めても出荷できるか | 5 |
| 難所を早く踏むか | 4 |
| 共存期間の短さ | 3 |
| 検証の確かさ | 5 |
| 並列のしやすさ | 4 |
| 整合 | 4 |

### 7.3 各案から取り込んだもの

| 出どころ | 取り込んだもの |
|---|---|
| strangler | 束単位の所有表と共存の規則 R1〜R7、段ごとの共通条件（配布環境の通し検証）、委譲の伝え方、段階 6 の PR の分け方、既存裁定との対応表の大半、HttpListener の推奨 |
| foundation | 次のとおり |
| | ・実測スパイクの型（1 日で打ち切り、不可のときの手を先に書く）と、S6・S10〜S13 |
| | ・参照キャプチャ、integrator・殻・波・1 単位 1 PR、依頼文の型、純ロジックの別レーン |
| | ・ゲート G1〜G7、完了の定義の観測方法、性能の記録、`UiAssetCatalog` の起動時の検査、外部監査の回し方 |
| player | 次のとおり |
| | ・命綱を第一弾にすること（Discord の実害の根拠つき） |
| | ・WebUiHealth（AGENTS.md の「誰が気づくか」） |
| | ・CEF 無し起動ラダー、実際に CEF を落とす確認 |
| | ・HUD・通知・スキットを早く出す段 |
| 統合で新しく決めたこと | 次のとおり |
| | ・束の中を暗転で作り、Editor 限定の上書きで検証して一度に切り替える（R9） |
| | ・切替 PR → 1 周期 → 削除 PR（R3） |
| | ・委譲中の Web は表示だけで入力を受けない（R1、S15） |
| | ・研究と列車族を持ち物の束に入れる |
| | ・装飾を後回しにしない |
| | ・S14・S15 を足す |

### 7.4 調査・草案の事実誤りと訂正（本書では訂正済み）

| 誤り | どこにあったか | 実測・正しい形 |
|---|---|---|
| Web UI に依存する録画シナリオは「27 本・約 143 か所」（「24〜29 本」とも） | 調査 tests、草案 3 本 | 90 本中 41 本（実測）。内訳は 3 つ。(1) DSL の Web 操作を使う 32 本・151 か所（ClickWebUi 67 など）。(2) `PlaytestDomQuery.Query` を直接呼ぶ 19 本・27 か所。(3) `WebUiInputExclusivity` を参照する 3 本。直接呼び出しの 19 本はロケータの照会 API への置き換えが要るので、DSL の作業は見積もりより大きい |
| シナリオが使う testid は約 45 種 | 調査 tests | 文字列で 53 種（実測） |
| `Client.WebUiHost` は 134 ファイル（約 10.1k 行） | 議論の記録、調査 host | 140 の .cs ファイル・10,565 行（実測） |
| topic は「28」「約 30」 | 草案 | `protocol.ts` に 29（`playtest.dom_query` を含む。除くと 28）。action は 44 で正しい（実測） |
| Web を参照する `Client.Tests` は 65 ファイル | 草案 strangler・player | 67 ファイル（実測。`WebUiHost` で grep） |
| Web の本番コードは 15.4k 行 | 調査 screens | 15,609 行（実測。features 8,482・shared 4,002・app 922・bridge 2,203）。10-15 以降の 44 コミットで増えた |
| 裁定のファイル名「メインメニューとロード画面はuGUI現状維持（UI Toolkit 化は後日）.md」 | 調査 screens §0 | そのファイルは無い。正しくは `.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md` |
| 「見た目は uloop screenshot で自己確認」 | 議論の記録 | PoC の実測で、rendering モードには UITK が写らない。ScreenCapture 系か window モードを使う |
| 完了の定義の「Kestrel ホスト削除」 | 議論の記録 | 遠隔実行が Kestrel に相乗りしている（ADR 0072、`WebUiEndpoints.cs:97-101`）。遠隔実行の載せ替え（D6）の後でだけ成り立つ |
| 破壊モードの UI 停止を、CEF 停止の実害として挙げた | 草案 player | 2026-10-14 の同種の報告では、cef-unity が 2 種とも生きていた（実測。Discord ミラー）。UI の全停止を CEF と決めつけない（§3.11） |
| `PocControls.uss` を repo のパスとして引用した | 草案 3 本 | master には無い。PoC ブランチ `poc/uitk-hard-cases`（`de6c68621c`）の `Client.UitkPoc/Resources/UitkPoc/PocControls.uss` |
| NuGet の `Microsoft.Extensions.*` を ModelContextProtocol が共有しているかもしれない（未確認） | 調査 host、草案 3 本 | `ModelContextProtocol 0.2.0-preview.3` は packages.config にあるが、client と server の scripts に `using` が無い（実測）。未使用なら一緒に外せる（推測） |
| 草案 foundation の期間 9〜12 週 | 草案 foundation | 統合案は 12〜16 週（推測）。段ごとの配布検証とマージ待ちを含めた |

次の点は、正しいことを確かめた。

- `UiPointerHitTest` の呼び出し側は 23 ファイル・28 か所
- 差し込み記法は位置指定が 32 キー・190 か所、名前付きが 44 キー。mod の CSV には 0 件
- マスタのアンカーは 9 件
- 引用した `.decisions` と ADR（0006・0013・0016・0017・0019・0020・0021・0024・0035・0039・0046・0052・0057・0071・0072・0074・0082）は実在する
- ADR 0082 の「ゲームパッドは正式対応外」
- UI Test Framework は組み込み済みで、manifest には未記載
- 配布ビルドの scripting backend は Mono


---

## レビューでの訂正（2026-10-23）

plan-v2 に対するレビュー（`review/fable-facts.md`・`review/codex-premortem.md`・`review/codex-tech.md`）のうち、事実と技術前提の訂正。plan-v3 には反映済み。上の §7.4 の 1 行目（41 本）と 6 行目（15,609 行の内訳）は、下の F6・F8 で改める。

- F1 進行中の Web の PR: 「画質設定などの Web の PR が進行中」は誤り。10-15 以降の `moorestech_web` へのコミット 44 本はすべて master HEAD の祖先（画質設定 `65e61835dd` を含む）。open PR は #1348（`feature/belt-conveyor-replace-impl`）の 1 本だけで、`moorestech_web` に 2 ファイルの差分がある。凍結時は G1 の許可リストに入れるか、先にマージする
- F2 壊れている録画シナリオ: 8 本ではなく 10 本。bd `moorestech-wwxy` の本文は 8 本だが、NOTES に `research-node-state-label.cs`・`server-notification-smoke.cs` の 2 本が追記されている
- F3 差し込み記法と既存裁定: `.decisions/2026-08-26-プレースホルダ規約は全言語一致テストで守る.md` が §8.3 に抜けていた。この裁定は「`Localize.GetFormatted` のシグネチャは変えない」とし、「引数の個数を型へ持ち上げる生成器」を別タスクとして棄却している。旧 D8 (a) の「型付きの書式メソッド」はこの棄却案そのもの。plan-v3 は生成器を作らず、パリティテストを残し、`GetFormatted` の位置指定シグネチャは統一の時点で名前付きへ改める
- F4 裁定の期限の矛盾: 「D1〜D9 は段階 0 の前」と §8.1 表の期限（D2・D5 は段階 1、D9 は段階 2 の終わり）が合っていなかった。plan-v3 は着手前に聞くものを 4 件に絞ったので消えた
- F5 managed stripping: 「managed stripping は Standalone で 3＝High（実測）」は読み違い。`ProjectSettings.asset:921-933` の `managedStrippingLevel` に Standalone のキーは無い（未設定＝既定）。`Standalone: 3` は 944 行目の `apiCompatibilityLevelPerPlatform` の値。scripting backend が Mono なのは正しい
- F6 Web UI に依存する録画シナリオ: 「41 本（32 本・151 か所＋19 本・27 か所＋3 本）」は再現できない。`*WebUi*(` の呼び出しがあるのは 23 本・139 か所（ClickWebUi 66）、`PlaytestDomQuery.Query` は 17 本・27 か所（`RegisterHandler`・`ResetPending` を含めると 19 本）、`WebUiInputExclusivity` は 3 本。plan-v3 は数え方を `grep -rlE 'WebUi|DomQuery|Cef' .agents/skills/unity-playmode-recorded-playtest/scenarios --include='*.cs'` に決め、「約 40 本（式で確定）」とした（`e8e611f1c0` で 42 本。うち 4 本は `CefUnity.Runtime` を直接使う vein-terrain 系）
- F7 画素パリティの前例: 「parity-check 47/47 が足かせになって打ち切った」は誤引用。08-17 裁定の理由は「uGUI の描画は恒久停止済みで、参照すべき実物がもう無い」。47/47 は `docs/webui-parity/iteration-log.md` の機械チェックの値
- F8 React の本番コードの行数: 15.6k 行（features 8,482・shared 4,002・app 922・bridge 2,203）は `.ts/.tsx/.css` の合計。TS/TSX だけなら 12.2k 行（features 6,623・shared 3,154・app 222・bridge 2,203）、CSS が 3.4k 行
- F9 段階 2 の HUD の数: §1 の「HUD 10 種」に対し、§2.3 の列挙は 9 種。9 種に揃えた
- ADR 0071 の ffmpeg: 「ffmpeg も arm64 しか無い」ではなく「ffmpeg も arm64 版だけを同梱している」（選択の結果）。「しか無い」のは CEF の Mac ランタイム（`osx-arm64` のみ）
- Codex（premortem 1・tech 1）CEF 無応答時の入力排他: WS の接続だけ残って JS が止まると、切断時のリセット（`WebSocketHub.cs:153-159`）を通らず、Web 側の文字入力フォーカスが残って移動と Esc（`InputManager.cs:110` の `CloseUI` は Keyboard 抑止の対象）が塞がれる。plan-v2 の Dead 時の処置（RawImage を隠す）と、CEF のプロセスを止める人手の試験では捕まらない。plan-v3 は「最後の WS 受信から 15000ms を超えたら入力排他の Web 側を無視する」を L0 のテストで確かめる形にした（推測される故障で、再現はしていない）
- Codex（tech 7）S8・S13 の探索: 同梱の `com.unity.ui.test-framework` のコード（`UITestFixture.cs:98-136`・`AbstractUITestFixture.cs:229-236`・`PanelSimulator.cs`）で、EditMode の fixture・batchmode・パネル時計の差し替えはあると確認できる。別立ての実測にせず、段階 1 の最初の L1 テストを既存 CI に載せて確かめる
