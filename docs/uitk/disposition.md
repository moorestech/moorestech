# 写さない Web テストの処遇

段階ごとの削除 PR で、消えた Web のテスト名（Playwright・vitest）と増えた NUnit のテスト名をスクリプトで突き合わせる（計画 `docs/uitk-migration/plan.md` §4.1）。束ごとの突き合わせは、その束の最後のレーン（段の締めの ladder）で行い、差分 0 を完了条件にする。見た目の意図は `docs/uitk/TODO.md` の参照キャプチャで辿る。

## 書式

- 写したテスト: NUnit のテストメソッドの直前（`[Test]` の上。`<summary>` があればその下）に、元の Web テスト 1 件につき 1 行の XML コメントを書く。`file` は `moorestech_web/webui/` からの相対パス、中身は Web のテスト名そのまま（`it(`／`test(` の第 1 引数。テンプレート文字列の `${…}` も展開しない。`<` `>` `&` は `&lt;` `&gt;` `&amp;`）。1 つの NUnit テストが複数の Web テストを写したら行を並べる。名前の空白の違い（連続・改行）は無視する

  ```csharp
  /// <web-test file="src/features/toast/toastStore.test.ts">emitToast で追加され 3秒後に消える</web-test>
  [Test]
  public void トーストは積まれ3秒後に消える()
  ```

- 写さないテスト: 下の「## 処遇」の表に 1 件 1 行で書く。列は、束（台帳「## 行」でそのファイルを持つ行の束。複数の束を持つ行は最大の束）／Web のファイル（`` ` `` で囲む）／Web のテスト名（`*` はそのファイルの全件。名前の中の `|` は `\|`）／処遇／証跡のパスか理由／PR（未定は `—`）
- 処遇は 4 つ。`目視`: L1 で主張できない見た目。証跡に参照キャプチャの撮影 id（台帳「参照キャプチャ」列の `1-toast` の形）と UITK 側のスクショのパスか URL（`.png`・`.jpg`）。どちらかが無い行は 2 で落ちる。`録画`: L1 ではなく録画シナリオで確かめる。証跡にシナリオのパス（`scenarios/…/*.cs`）。`捨てる`: UITK に同じものが無い（通信契約・DOM 固有・CEF 固有など）。理由を書く。`写す予定`: UITK に挙動はあるが NUnit がまだ無い。証跡に bd の ID（`moorestech-…`）。写す予定は差分として数える
- 実行: `node tools/uitk/test-name-parity.mjs --bundle <1-5|all>`。終了コード 0 = 差分 0、1 = 未対応か写す予定がある、2 = 書式・宛先の誤り。`--emit-missing` は未対応を表の行の形で出す（処遇の列が `要判断` のままだと 2 で落ちる）。Web を消した後は `--web-rev <消す前のコミット>`
- 自己テスト: `node --test tools/uitk/testNameParity/tests/*.test.mjs`（G1 の workflow でも走る）
- 突き合わせたら「## 突き合わせの記録」に 1 行足す（日付・束・その時の HEAD の 10 桁・各件数）

## 突き合わせの記録

| 日付 | 束 | コミット | Web | 写した | 処遇 | 写す予定 | 未対応 |
|---|---|---|---|---|---|---|---|
| 2026-11-06 | 1 | 1670aca187 | 66 | 41 | 9 | 16 | 0 |

## 処遇

| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |
|---|---|---|---|---|---|
| 1 | `e2e/tests/regression/actionErrors.spec.ts` | * | 捨てる | WS の action（block_inventory.split）の失敗コードに対するトーストの契約。UITK のコマンドは WS を通らず、通信失敗のトーストは撤去まで Web 側に残る（台帳「agent 既定」の通信失敗のトースト） | — |
| 1 | `e2e/tests/system/feedbackReportLabels.spec.ts` | 感想送信のボタンと完了通知: ${label.locale} | 写す予定 | moorestech-albof.21（4 言語の本番辞書で感想の送信ボタンと完了トーストの文言を見る。録画 `scenarios/misc/feedback-report-labels.cs` は日本語のボタンだけで完了通知と他言語を見ない） | — |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | PauseMenu遷移で表示しセーブとセーブして終了actionを送る | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs（Esc で PauseMenu へ遷移して pause-menu を待ち、セーブ・セーブして終了を押す。終了の結果は呼び出し側で save.json を見る） | — |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | トップから設定画面へ進み言語を選べ、戻るでトップへ戻る | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs（設定へ進み日本語を選んでロケール印を待ち、戻るでトップへ戻る） | — |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | トップからバグ報告画面へ進み送信すると成功を表示する | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs（バグ報告へ進み本文を入れて送り、トーストと outbox の新しい箱を確かめ、トップのセーブを待つ） | — |
| 1 | `e2e/tests/system/pauseMenuSettingsLayout.spec.ts` | 復旧表示は${viewport.width}x${viewport.height}で収まり同値操作と解除通知を受ける | 写す予定 | moorestech-albof.21（同値操作と解除は SettingsViewTest にあるが、ポーズの枠内に復旧表示が収まることを L1 で見るテストが無い） | — |
| 1 | `e2e/tests/system/pauseMenuSettingsLayout.spec.ts` | 設定画面は${viewport.width}x${viewport.height}で枠内に収まり本文だけスクロールする | 写す予定 | moorestech-albof.21（PauseMenuViewTest は 1280x720 だけを見る。Web が見る 2432x786 で枠内に収まり本文だけスクロールすることを見るテストが無い） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts` | 送信状況 %j と送信可否を同時に表示する | 写す予定 | moorestech-albof.21（文言は BugReportFormTextTest にあるが、配送の行と状態の行を同じ画面に同時に出す View のテストが無い） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts` | %s辞書で配送不能件数と復旧案内を出す | 写す予定 | moorestech-albof.21（4 言語の本番辞書。UITK は日本語だけ） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts` | %s辞書で件数不明を完了や0件にしない | 写す予定 | moorestech-albof.21（4 言語の本番辞書。UITK は日本語だけ） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/hostSubmissionLock.test.ts` | 応答待ちが終了してもホスト送信中は感想の種別と本文を保護する | 写す予定 | moorestech-albof.21（BugReportFormViewTest はホスト Submitting を Apply するだけ。送信→ホスト submitting→ローカル応答の終了後もロックが残り成功トーストを出さず、ready で解けることを見るテストが無い） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/productionSendingLabels.test.ts` | ${locale}: ${kind}のホスト送信表示は本番CSVを使う | 写す予定 | moorestech-albof.21（4 言語の本番辞書の文言。UITK はキーの選び分けだけを見る） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts` | %s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない | 写す予定 | moorestech-albof.21（4 言語。汎用の LocalizationDictionaryCoverageTest は空訳だけを見る） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts` | %s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない | 写す予定 | moorestech-albof.21（4 言語で差し込み値が残ること。UITK は日本語だけ） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts` | 感想でも欠損時は従来の通知を出す | 写す予定 | moorestech-albof.21（欠損のエラーのトーストは種別バグでしか見ていない） | — |
| 1 | `src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts` | 感想送信中の表示とロックを保ち、送信開始時の種別で完了を伝える | 写す予定 | moorestech-albof.21（送信中に下書きの種別が変わっても完了は送信開始時の種別で伝えることを見るテストが無い） | — |
| 1 | `src/features/pauseMenu/PauseMenuPanel.test.ts` | パネルを外して付け直すと書きかけは空になる | 写す予定 | moorestech-albof.21（ポーズを開くたびに PauseMenuScreenPresenter が書きかけを Reset するが、それを見るテストが無い） | — |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | 拒否理由と対象項目を表示文言へ解決する | 捨てる | WS の失敗コード（invalid_graphics_quality:invalid_type:…）を文言へ解く契約。UITK の SetGraphicsQuality は型付きの値を受け結果を返さず、拒否の経路が無い | — |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | topicを固定したまま連続操作とpreset直後に%sを送る | 写す予定 | moorestech-albof.21（モデルが更新されない間の連続操作とプリセット直後の操作で、前の操作を巻き戻さず今の項目を送ることを見るテストが無い） | — |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | %sの操作値を送る | 写す予定 | moorestech-albof.21（オンオフ 5 項目〈ssao・postProcessing・effects・mapObjects・ocean〉の testid と送る項目の対応〈GraphicsQualityItemRows の表〉を View で押して見るテストが無い。GraphicsQualityRowsTest は行を自作し表を通らず、SettingsViewTest は描画倍率と地形だけ） | — |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | topic未着では項目を描かない | 捨てる | UITK の SettingsModel は生成時に設定値を同期で持ち、topic 未着の状態が無い | — |
| 1 | `src/features/settings/LanguageSelect.test.ts` | 一覧が届くまでは辞書非依存リテラルの読み込み中を出しModeSwitchを描かない | 捨てる | UITK の言語一覧は LanguageCatalog の静的な表で、配信待ちの状態が無い | — |
| 1 | `src/features/settings/SprintModeSelect.test.ts` | holdの初期表示からtoggleを選択できる | 写す予定 | moorestech-albof.21（hold で開いた画面から toggle を押して SetSprintInputMode(Toggle) が出ることを見るテストが無い） | — |
| 1 | `src/features/settings/SprintModeSelect.test.ts` | topic未着では選択を描かない | 捨てる | UITK の SettingsModel は生成時に設定値を同期で持ち、topic 未着の状態が無い | — |
