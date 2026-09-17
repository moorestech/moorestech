# ADR 0060: plan G 最終レビュー（moores-code-review）の設計判断10件の裁定

- 状態: 採用
- 日付: 2026-09-14
- 文脈: plan G ブランチ `feature/playtest-client-report` の最終ブランチ全体レビュー（run `2026-09-14-0616`・56系統＋Codex 3本）が
  `integrated.md` に残した「設計判断」10件と「系統間の矛盾」2件
- 裁定者: 実装セッション（ユーザー就寝中の無人実行。質問禁止の指示下で本体が裁定した）

レビューの指摘本文・故障シナリオ・却下案の詳細は
`../moorestech_logs/harness/moores-code-review/runs/2026-09-14-0616/integrated.md` が正本。
本 ADR は **どれを採り、何を却下し、なぜか** だけを残す。

## 裁定

### 1. クラッシュ報告の書き出し失敗の返し方（C1・C22） → 案A

`CrashReportResponseResult` に `WriteFailed` を足し、`send` かつ `CrashBundleWriter.Write` が `null` を返したときは
待機状態へ戻して `ActionResult.Fail("bundle_write_failed")`（ポーズメニュー経路と同じ理由コード）を返す。
「送らない」は常に押せるので起動が恒久停止することはない。`LastWrittenBundleDirectory` はこの変更で不要になり削除する。

- 却下: 案B（`finally` で待機解除のまま `WriteFailed` を返す）— `waiting:false` が先に飛んでゲートが消え、失敗文言が1フレームも見えない。enum だけ正しくなって症状が残る。
- 却下: 案C（結果を topic の discriminated union に載せて2段ゲートにする）— 共有契約とゲート UX の変更まで波及する。案A で「失敗したら閉じない」が成立すれば、結果の運搬は現在の `respondState="failed"` 表示で足りる。plan D/H が部分欠損（録画だけコピー失敗）を運ぶ必要を示した時点で改めて採る。

### 2. マシン共通ツリーの割り方（C2） → 案A（pid スコープ）

退避・削除・回収の単位を `pid_<PID>` にし、生存 PID のディレクトリは触らず理由をログと `missing` に残す。
`CLEAN_EXIT` と `ProgressRecords/current/` も同じ識別子で割る。pid の綴りは録画側から配る。

- 却下: 案B（単一インスタンスロック）— 並列 Editor が日常のこの環境（CLAUDE.local.md）で2本目以降が常に記録無しになり、録画前提のテストが片肺になる。
- 却下: 案C（単一インスタンス前提を明文化して受理）— 壊れ方が無音（`clean:True` のログだけ残してクラッシュ録画が消える）で、本 plan の目的と正面から衝突する。

### 3. 契約日時フォーマットの置き場（C4） → 案A

`Game.Paths/BugReportBundleLayout.cs` に `Utc8601Format` を置き、client/server 全箇所が参照する。

- 却下: 案B（`BugReportManifest` に置く）— サーバーの `GetWorldPlaySessionInfoProtocol` から参照できず、サーバー側にリテラルが1つ残る＝ドリフトの再発経路が残る。

### 4. 起動ゲートの `IPlaytestSessionIdentity` 解決（C6） → 案A（静的プロバイダ）

`PlaytestSessionIdentityProvider.Current` を唯一の差し替え点にし、DI 登録もゲートもそこを見る。

- 却下: 案D（差込口ごと削除して `""` 直書き）— AGENTS.md「将来の拡張性は考慮不要」に最も忠実だが、plan D（ADR 0061 の受け口）は着手済みの並行ブランチであり「来るか分からない将来」ではない。ここで消すと plan D が同じ差込口を作り直す。
- 却下: 案B（送信直前に manifest へ注入）— 依存方向は最もきれいだが「書いた直後の manifest は未完成」という状態を持ち込み、READY マーカーの意味（＝完成した箱）が崩れる。
- 却下: 案C（ゲートを DI 確立後へ移す）— 「オープニングとチュートリアルが走り出す前に聞く」という順序（plan G の要件）が崩れる。

### 5. CLEAN_EXIT 書き手の置き場と終了口（C13・C14） → 案A

`CleanExitMarker` の設置と `ProgressSessionRecovery` を、消費と同じ起動時1箇所（`RunAtStartup` 直後）へ並べる。
あわせて ①`Client.MainMenu/QuitGame.cs` の `Application.Quit` 直呼びを `GameShutdownEvent` 経由へ
②初期化失敗経路（`InitializeScenePipeline` の `Forget` ハンドラ）では CLEAN_EXIT を書かせない（終了理由を持たせて分岐）。

- 却下: 案B（`Client.Starter` に常駐 DI スコープを新設して entry point 化）— そのためだけに新しいライフサイクルスコープを増やす。`IDisposable` の形は保てるがゲーム寿命オブジェクトの dispose は本 repo の割り切り上そもそも不要（AGENTS.md 既知の制約）。

### 6. 欠損・取得失敗の表明（C25） → 案A（欠損列の一元表明）

`ProgressRecordHeader` に `Missing`（`BugReportManifest.MissingItem` と同型）を足し、`headerMissing` をその特殊形へ畳む。
`FillWorldPlayTimeAsync` / `CreateHeaderForLostHeader` / `BuildInfoJson.Parse` / `ProgressEventEntry.FromJsonLine` の欠損が理由付きで積まれる。

- 却下: 案B（各フィールドの nullable 化）— 欠損の**理由**がどこにも残らず、`?? ""` を `null` に置き換えただけになる。無音の縮退禁止を満たさない。
- plan H への影響: 共有契約 §3 に列が1本増える。plan H は `.get()` 読みで未知キー拒否が無いことを確認済み（ADR 0059 と同じ根拠）。

### 7. `BugReport/` 直下12ファイルの解消（dir-file-limit） → 案A

`BugReport/BuildOrigin/` を新設して `BuildInfo.cs` / `BuildInfoJson.cs` を移す（12→10）。

- 却下: 案B（`BugReport/Playtest/` へ移す）— `BuildInfo` は crash 箱・bug 箱・進行記録の共通概念で「プレイテスト固有」ではない。
- 却下: 案C（`RepositoryStateProbe` へ統合）— 200行制限に抵触する。

### 8. 全画面ゲートの外殻と辞書フォールバックの集約（webui） → 案A

`shared/ui/FullScreenGate` を1つ置き、3ゲート（EventLanguage / CrashReport / PlaytestConsent）が呼ぶ。
z-index・face トークンは既存の同値トークンへ集約する。あわせて `createTranslator` に fallback 引数を足し、
10箇所に散った `status === "ready" ? ... : ...` の分岐を `t(key, {}, fallback)` の1回呼びへ畳む。

- 却下: 案B（`useGateWaiting` だけ共有し JSX の複製は残す）— Overlay の見た目・z-index・Title 属性の分裂（すでに発生済み）が解消しない。
- 却下: 案C（現状維持）— 4つ目のゲートごとに36行＋トークン2つ＋z-layer テスト1本を複製し続ける。
- context の「ゲートは ADR 0040 の `EventLanguageGate` と同型」は共有外殻を否定しない（同じ抽象を共有する方が「同型」を強く保証する）。

### 9. 進行記録の `blockPlaced` の粒度 → 案A（集約）

`{"type":"blockPlaced","data":{"count":N}}` へ集約し、flush 点は UI 状態変化の追記直前と終了時。
あわせて追記口を `ProgressSessionWriter` が保持する1本の `StreamWriter`（Append・毎行 `Flush()`）へ寄せる。

- 却下: 案B（追記口の常時オープン化のみ）— mkdir/open/close は消えるが `events.jsonl` の行数肥大が残る。
- 却下: 案C（現状維持）— 1ブロック＝1回の `Directory.CreateDirectory`+open/write/close がメインスレッドで走る。サーバー側 `PlaceBlockProtocol` は同じ増幅をすでに回避しており、クライアントで作り直している形。
- 1ブロックごとの時刻・tick は捨てる。plan G の観測項目（設置数・建築モードで設置したか）はどちらも集約後に復元できる。

### 10. ゲートの多言語表示 → 案A

`LocalizationTopic` の登録をゲート開始前へ移し、後段の重複登録を除く。
これで `localization.csv` に足したドイツ語がゲートに実際に表示される。

- 却下: 案B（`DictionaryIndependentText` に3言語を持たせる）— 表示は直るが文言の正本が3言語ぶん二重化し、「csv を編集しても表示へ届かない」構造が残る。
- 背景: 現状はゲート待ちが `WebUiGameBinder.Bind()` より前にあるため辞書が届かず、ゲート文言は常に日英のみの fallback が出ていた。
  「追加文言は ja/en/de の3言語すべて」という制約が、実装上は de だけ死んでいた。

## 系統間の矛盾の裁定

### A. ディスクIO の try-catch（決定論チェック `try-catch-forbidden` 8件 vs C3）

**隔離を増やす側（C3）を採る。** AGENTS.md の例外条項は「外部境界の隔離目的に限り使用可」という一般則を先に置いており、
ディスクIO（他プロセスのロック・権限・満杯）はプロセス外資源としてその趣旨に含まれる。
実装側は理由コメントを日英2行で明記し、失敗を `Debug.LogError` と `manifest.Missing` の両方へ残している（無音でない）。
catch を外すと C3 の故障シナリオ（起動不能・終了不能）が現実化する。

あわせて **AGENTS.md の例外列挙に「ディスクIO」を明記する**（文書側の是正）。列挙が実態と食い違ったままだと、
決定論チェックが毎回同じ8件を confirmed に挙げ続け、レビューの signal が摩耗する。

### B. `ProgressRecorder.Deserialize<T>` の try-catch

**外して前例に揃える。** 同じ payload を `ChallengeManager` / `SkitFireManager` ほか25箇所超が catch 無しで復号しており、
壊れたパケットは同フレームで先にそちらで例外になる＝この catch は実際には何も防げていない。
復号失敗件数を記録へ残す話は裁定6（欠損列）で別途拾う。

- 追記（refix round1）: 「先に本来の購読者が例外にする」は**購読順に依存する前提**だったので裏を取った。
  `VanillaApiEvent` は UniRx の `Subject<byte[]>` を逐次配信しており、1購読者の例外は後続購読者への配信を止める。
  裁定自体（catch を外す）は維持し、配信側 `VanillaApiEvent.SubscribeEventResponse` で購読者ごとに隔離した。
  これで前提が購読順に依存しなくなり、catch 無しで復号している他25箇所も同時に守られる。
- 追記（refix round2）: この隔離が新しい前提になったのに回帰テストが1本も無く、行が落ちても誰も赤にならない状態だった。
  隔離とタグ別配信を `EventResponseDispatcher`（`Client.Network/API/`）へ切り出し、`VanillaApiEvent` は通信の配線だけを持つ形にした
  （`PacketExchangeManager` を ctor で要求する `VanillaApiEvent` は無限 UniTask ループのため EditMode で立てられない）。
  `Client.Tests/Network/EventResponseDispatcherTest` が「1人目が例外でも2人目へ届く」「例外を投げた購読者が購読解除されない」を固定する。

### C. `ProgressRecorder` の `IDisposable`

レビュー側で解決済み（`MainGameStarter.OnDestroy` の `_resolver?.Dispose()` により `Dispose` は到達可能）。
`Dispose` は残し、`CancellationTokenSource` のキャンセルを足す。

## 免責（suppressed）3件の裁定

レビューは「context.md の出所ラベルが解決不能なので、免責されていた3件は通常の Warning として再統合が必要」と指摘した（C30）。
context.md はレビュー run の**凍結済み入力**（何をどう測ってその結論に至ったかの証跡）なので後から書き換えず、ここで3件を裁定する。

1. `BuildInfo.SteamBuildLabel` / `BuiltAt` / `Target` が常に null で成果物へ出る
   → **免責を維持。** `build-info.json` の生成と Windows 配布ビルドは plan E の範囲で、値を埋める側がまだ存在しない。
   欠損は裁定6 の `missing` 列で理由付きに表明されるので、無音ではない。

2. `BuildInfoJson.MasterDataCommit` が `masterCommit` からのみ読み、共有契約 §1 の `masterDataCommit` を見ない
   → **免責を維持。** 出所は ADR 0059（実在する。台帳項目は「JSON キーの不一致について」）。
   実装済みの焼く側が出すキーを正とし、両キーを見るフォールバックは採らない。

3. `EmptyPlaytestSessionIdentity` が常に空文字を返し `manifest.steamId` が識別子として機能しない
   → **免責は不要になった。** 裁定4（案A・静的プロバイダ）で差し替え点が1つに畳まれたので、
   plan D が `PlaytestSessionIdentityProvider.SetCurrent` を呼べばゲートも DI も同じ実体を見る。
   「差込口を名乗っているが DI 差し替えでは切り替わらない」という中途半端な状態は解消済み。

なお `context-source-label` の confirmed 8件は、この凍結入力に対する指摘なので残り続ける。
将来の run で同じ指摘を出さないために、plan G 側のラベルは `ead0551bf` で訂正済み。
