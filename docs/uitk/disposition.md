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
| 2026-11-06 | 1 | ef796d7551 | 66 | 40 | 8 | 18 | 0 |
| 2026-11-06 | 3 | ca052a7c04 | 99 | 55 | 0 | 0 | 44 |
| 2026-11-06 | 4 | e23c6fd597 | 457 | 87 | 5 | 0 | 365 |
| 2026-11-10 | 4 | 91e3f7f5c5 | 457 | 189 | 9 | 1 | 258 |
| 2026-11-10 | 5 | 91e3f7f5c5 | 528 | 132 | 4 | 0 | 392 |

## 処遇

| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |
|---|---|---|---|---|---|
| 1 | `e2e/tests/regression/actionErrors.spec.ts` | * | 捨てる | WS の action（block_inventory.split）の失敗コードに対するトーストの契約。UITK のコマンドは WS を通らず、通信失敗のトーストは撤去まで Web 側に残る（台帳「agent 既定」の通信失敗のトースト） | #1596 |
| 1 | `e2e/tests/system/feedbackReportLabels.spec.ts` | 感想送信のボタンと完了通知: ${label.locale} | 写す予定 | moorestech-albof.21（4 言語の本番辞書で感想の送信ボタンと完了トーストの文言を見る。録画 `scenarios/misc/feedback-report-labels.cs` は日本語のボタンだけで完了通知と他言語を見ない） | #1596 |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | PauseMenu遷移で表示しセーブとセーブして終了actionを送る | 写す予定 | moorestech-albof.21（PauseMenu の UI 状態へ遷移して画面が出て、セーブとセーブして終了のコマンドが出ることを一続きで見るテストが無い。PauseMenuViewTest は表示済みの View でコマンドの種類だけを見て遷移を通らず、録画 `scenarios/uitk/uitk-lifeline.cs` は 2 つを押すだけで結果を assert しない） | #1596 |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | トップから設定画面へ進み言語を選べ、戻るでトップへ戻る | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs（設定へ進み日本語を選んでロケール印を待ち、戻るでトップへ戻る） | #1596 |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | トップからバグ報告画面へ進み送信すると成功を表示する | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs（バグ報告へ進み本文を入れて送り、トーストと outbox の新しい箱を確かめ、トップのセーブを待つ） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts` | 送信状況 %j と送信可否を同時に表示する | 写す予定 | moorestech-albof.21（文言は BugReportFormTextTest にあるが、配送の行と状態の行を同じ画面に同時に出す View のテストが無い） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts` | %s辞書で配送不能件数と復旧案内を出す | 写す予定 | moorestech-albof.21（4 言語の本番辞書。UITK は日本語だけ） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts` | %s辞書で件数不明を完了や0件にしない | 写す予定 | moorestech-albof.21（4 言語の本番辞書。UITK は日本語だけ） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/hostSubmissionLock.test.ts` | 応答待ちが終了してもホスト送信中は感想の種別と本文を保護する | 写す予定 | moorestech-albof.21（BugReportFormViewTest はホスト Submitting を Apply するだけ。送信→ホスト submitting→ローカル応答の終了後もロックが残り成功トーストを出さず、ready で解けることを見るテストが無い） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/productionSendingLabels.test.ts` | ${locale}: ${kind}のホスト送信表示は本番CSVを使う | 写す予定 | moorestech-albof.21（4 言語の本番辞書の文言。UITK はキーの選び分けだけを見る） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts` | %s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない | 写す予定 | moorestech-albof.21（4 言語。汎用の LocalizationDictionaryCoverageTest は空訳だけを見る） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts` | %s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない | 写す予定 | moorestech-albof.21（4 言語で差し込み値が残ること。UITK は日本語だけ） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts` | 感想でも欠損時は従来の通知を出す | 写す予定 | moorestech-albof.21（欠損のエラーのトーストは種別バグでしか見ていない） | #1596 |
| 1 | `src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts` | 感想送信中の表示とロックを保ち、送信開始時の種別で完了を伝える | 写す予定 | moorestech-albof.21（送信中に下書きの種別が変わっても完了は送信開始時の種別で伝えることを見るテストが無い） | #1596 |
| 1 | `src/features/pauseMenu/PauseMenuPanel.test.ts` | パネルを外して付け直すと書きかけは空になる | 写す予定 | moorestech-albof.21（ポーズを開くたびに PauseMenuScreenPresenter が書きかけを Reset するが、それを見るテストが無い） | #1596 |
| 1 | `src/features/pauseMenu/PauseMenuPanel.test.ts` | 設定画面は言語選択と戻るボタンを出し、戻るはtopへのshow_pageを送る | 写す予定 | moorestech-albof.21（PauseMenuViewTest は言語選択と戻るだけを見る。Web が見る sprint-mode-select と graphics-quality-settings が設定画面に出ることを見るテストが無い） | #1596 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | 拒否理由と対象項目を表示文言へ解決する | 捨てる | WS の失敗コード（invalid_graphics_quality:invalid_type:…）を文言へ解く契約。UITK の SetGraphicsQuality は型付きの値を受け結果を返さず、拒否の経路が無い | #1596 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | topicを固定したまま連続操作とpreset直後に%sを送る | 写す予定 | moorestech-albof.21（モデルが更新されない間の連続操作とプリセット直後の操作で、前の操作を巻き戻さず今の項目を送ることを見るテストが無い） | #1596 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | %sの操作値を送る | 写す予定 | moorestech-albof.21（オンオフ 5 項目〈ssao・postProcessing・effects・mapObjects・ocean〉の testid と送る項目の対応〈GraphicsQualityItemRows の表〉を View で押して見るテストが無い。GraphicsQualityRowsTest は行を自作し表を通らず、SettingsViewTest は描画倍率と地形だけ） | #1596 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | topic未着では項目を描かない | 捨てる | UITK の SettingsModel は生成時に設定値を同期で持ち、topic 未着の状態が無い | #1596 |
| 1 | `src/features/settings/LanguageSelect.test.ts` | 一覧が届くまでは辞書非依存リテラルの読み込み中を出しModeSwitchを描かない | 捨てる | UITK の言語一覧は LanguageCatalog の静的な表で、配信待ちの状態が無い | #1596 |
| 1 | `src/features/settings/SprintModeSelect.test.ts` | holdの初期表示からtoggleを選択できる | 写す予定 | moorestech-albof.21（hold で開いた画面から toggle を押して SetSprintInputMode(Toggle) が出ることを見るテストが無い） | #1596 |
| 1 | `src/features/settings/SprintModeSelect.test.ts` | topic未着では選択を描かない | 捨てる | UITK の SettingsModel は生成時に設定値を同期で持ち、topic 未着の状態が無い | #1596 |
| 4 | `src/features/blockInventory/blockInventoryKeydown.test.ts` | blockInventoryレイヤーのEscapeでGameScreen遷移を要求する | 捨てる | UITK では UIState（`SubInventoryState` の `CloseUI`）が ESC で閉じる。Web は CEF がキーを取るために持つ判断（L0-s4b D-7） | #1599 |
| 4 | `src/features/blockInventory/blockInventoryKeydown.test.ts` | modalレイヤーではEscapeでも遷移を要求しない | 捨てる | 同上。モーダル上の ESC は UIState の遷移規則が持つ（L0-s4b D-7） | #1599 |
| 4 | `src/features/blockInventory/blockInventoryKeydown.test.ts` | gameレイヤーではEscapeでも遷移を要求しない | 捨てる | 同上（L0-s4b D-7） | #1599 |
| 4 | `src/features/blockInventory/blockInventoryKeydown.test.ts` | blockInventoryレイヤーでもEscape以外では遷移を要求しない | 捨てる | 同上。テキスト欄フォーカス中の ESC は albof.12（L0-s4b D-7） | #1599 |
| 4 | `src/shared/itemMove/planDirectMoves.test.ts` | maxStack 不明時（マスタ未ロード）は同種探索をスキップし空スロットのみ使う | 捨てる | UITK はマスタを同期で持ち、スタック上限は `IItemStackLevelLookup.GetMaxStack` で常に引ける。Web の「マスタ未ロード（HTTP 取得前）」の状態が UITK に無い（設計書 [A-13] の ConnectingPlaceholder と同じ理由）。L0-s4a 判断記録 D-5 | #1598 |
| 4 | `src/shared/placementTarget/selectableTarget.test.ts` | 辞書解決kindはidをguidとして写す | 捨てる | 配信の kind 文字列を表示種別へ写す処理は Web の配信契約だけのもの。C# は `IPlacementTarget` の型そのものが種別で、Kind→型の対応は既存 `Client.Tests/PlaceSystem/PlacementTargetFactoryTest` の「TryCreateはKindごとに対応する型とIdを持つターゲットを生成する」が持つ | #1595 |
| 4 | `src/shared/placementTarget/selectableTarget.test.ts` | blueprintCopyはidを持たない共通種別になる | 捨てる | 同上。C# の `BlueprintCopyPlacementTarget` は表示名を辞書キー 1 つで解き（`PlacementTargetDisplayNameTest`）、id の有無は表示名に効かない | #1595 |
| 4 | `src/shared/placementTarget/selectableTarget.test.ts` | 保存BPだけが原文labelのrawになる | 捨てる | 同上。原文の名前を返すのは `BlueprintPlacementTarget.DisplayName` だけで、`PlacementTargetDisplayNameTest`「保存したブループリントは利用者が付けた名前をそのまま返す」が固定する | #1595 |
| 4 | `src/shared/placementTarget/selectableTarget.test.ts` | ホットバー枠のDTO形状もそのまま受け付ける | 捨てる | UITK のホットバーは配信 DTO を経ず `IPlacementTarget` を直接持つので、DTO の形の互換が無い | #1595 |
| 2 | `e2e/tests/progress.spec.ts` | ラベル付き汎用進捗を維持する | 捨てる | C# の `ProgressTopic` は Label を常に null で送り、ラベルの源が無い（`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ProgressTopic.cs:66`。D-12） | — |
| 2 | `src/features/commonHud/alwaysOnHudWheel.test.ts` | クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ | 写す予定 | moorestech-albof.30（ホットバーのスロット列が UITK に入る S3-hotbar-modal で、クリックを持つスロット列だけが入力を取りホイールを素通すことを NUnit で見る） | — |
| 5 | `src/shared/ui/BlockSlot/index.test.ts` | nameが無ければホバーTooltipを出さない | 捨てる | UITK の `BlockSlot.SetBlock(UiIcon, UiText name)` は名前を必須で受ける（唯一の使い手の研究も常に名前を渡す。設計書 A-50）。名前の無い BlockSlot が作れない | — |
| 5 | `src/shared/ui/iconEndpointArchitecture.test.ts` | * | 捨てる | UITK はアイコンを HTTP（`/api/*-icons/`）で配らず、Content 層（`GameContentPresentation`）がテクスチャを直に `UiIcon` へ渡す（設計書 A-4）。エンドポイントの構造が無い | — |
| 2 | `src/shared/tooltip/CursorTooltip.test.ts` | resolves a content key from the dictionary without a raw-text fallback | 捨てる | UITK の CursorTooltip は `MouseCursorTooltipState` の `TooltipLine`（型付きの `LocalizationKey` だけ）を受け、Web の topic の contentKey（文字列のキー）の経路が無い。内容辞書の解決は `UiText.FromContent` と `LocalizationTextResolver` の既存 L1 が持つ | #1611 |
| 2 | `src/features/modeHud/modeHudDesign.test.ts` | 警告色のコントラスト改善を既存consumerへ共有する | 捨てる | UITK の操作モード HUD は置けない理由の警告の行を持たない（C# に書き手が無く、Web の橋も常に空文字。Web の mock だけが流す。D-7）。他の consumer（通知・列車の網・不足の強調）はそれぞれの画面レーンが写す | #1611 |
| 5 | `src/features/tutorial/overlay/TutorialOverlay.test.ts` | item master未ロード中は所持アンカーのackを送らず、到着後に送り直す | 捨てる | ack は越境の通知で、C# の受け手 `TutorialAnchorAckAction` は検証して返すだけ（store・サーバー・ログに効果が無い）。UITK は解決する側が C# 自身なので ack を作らない。加えて UITK は `MasterHolder` がゲーム開始前に読み終わっており、マスタ未ロードの状態が無い | #1627 |
| 5 | `src/features/tutorial/overlay/TutorialOverlay.test.ts` | 同一anchorを共有する全highlightへackを配る | 捨てる | ack は越境の通知で、C# の受け手 `TutorialAnchorAckAction` は検証して返すだけ（store・サーバー・ログに効果が無い）。UITK は解決する側が C# 自身なので ack を作らない。同じアンカーを共有する全ての枠を描くことは L1 `TutorialOverlayAnchorTest` と `TutorialStageCueTest` が見る | #1627 |
| 5 | `src/shared/ui/FluidSlot/FluidSlot.icon.test.ts` | 液体マスタ未取得の間はフィルを描かない（フォールバック色を使わない） | 捨てる | UITK にマスタ未取得の時間が無く、液体の色はサーバーのマスタのロード時に `#RRGGBB` を検証して起動で止める（`FluidMasterUtil.Validate`。C4-a 計画 D-15）ので、色の欠けた中身が作れない | #1633 |
| 5 | `src/shared/ui/FluidAmountSlot/index.test.ts` | 液体名が辞書に無ければツールチップのラベルを持たない | 捨てる | UITK の液体名は `GameContentPresentation.Fluid` が `UiText.FromContent` で作り、辞書はマスタの name を原文に合成する（`MasterSourceTextCollector`）ので、解けない名前が無い（C4-a 計画 D-9） | #1633 |
| 4 | `src/features/inventory/EquipmentPanel/index.test.ts` | action失敗時はその要求をpendingから除く | 捨てる | C# の装備の選択（`LocalPlayerEquipment.SetSelectedIndex` → `SendOnly.SetSelectedEquipment`）は送りっぱなしで失敗が返らず、外す要求が生まれない。未確認の列はサーバーのエコーの世代だけで縮む（`EquipmentWheelSelectorTest`。S4-inventory D-5） | — |
| 4 | `src/features/inventory/EquipmentPanel/index.test.ts` | 再接続開始時にpendingを破棄して復元snapshotを起点にする | 捨てる | UITK は同じプロセスの C# を直接読み、WebSocket の切断・再接続が無い。確定の世代 `SelectionConfirmationRevision` は加算だけで戻らない（`LocalPlayerEquipment.cs:63,100`）ので、世代が戻ったら未確認を捨てる規則は写さない（D-28）。確定値を起点にすることは `EquipmentWheelSelectorTest`「未確認が全部確定した後はサーバーの確定値から次を計算する」が見る（S4-inventory D-5） | — |
| 4 | `e2e/tests/inventory/equipment.spec.ts` | ホットバーHUDの上でもホイールで装備が切り替わる | 写す予定 | moorestech-albof.47（S3-hotbar-modal へ回送。HotbarHudView が未マージで、ホットバーの枠の上のホイールが装備を切り替えることを NUnit で見られない。素通しの印 `WheelPassthroughMark` は S4-inventory が先に作った） | — |
| 4 | `e2e/tests/inventory/equipment.spec.ts` | ホットバー帯のスロット列外でもホイールで装備が切り替わる | 写す予定 | moorestech-albof.47（S3-hotbar-modal へ回送。HotbarHudView が未マージで、ホットバー帯のスロット列の外のホイールが装備を切り替えることを NUnit で見られない） | — |
| 4 | `e2e/tests/research/research.spec.ts` | translate後のグリップ矩形だけに重なる境界buttonをexpectCraftGripが検出する | 捨てる | Playwright の補助関数 expectCraftGrip 自身の検査で、DOM の translate とボックスの境界に依る。UITK に同じ補助が無く、写す対象の挙動が無い | #1641 |
| 4 | `e2e/tests/research/research.spec.ts` | padding box外の境界buttonをexpectCraftGripが重なりなしと判定する | 捨てる | 同上（expectCraftGrip の自己テスト） | #1641 |
| 4 | `e2e/tests/research/research.spec.ts` | 研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-tree.cs（研究画面を開いたまま、判定の間だけ HUD の根を拾う設定にして challenge-hud・key-hints の中心の Pick が各 HUD を返す。層の順序の主張） | #1641 |
| 4 | `e2e/tests/research/tmp-icon-click.spec.ts` | * | 捨てる | tmp の付いた一時の調査テストで、実行計画 §1.4 の完了条件が写す数から除いた | #1641 |
| 4 | `src/features/research/ResearchTreePanel.test.ts` | インベントリtopic未受信中は詳細ペインへ所持数をnullで渡す(D4) | 捨てる | UITK は持ち物を同期で持ち、所持数の未受信の状態が無い（設計書 [A-13] の ConnectingPlaceholder と同じ理由） | #1641 |
| 4 | `src/features/research/ResearchDetailPane.test.ts` | 所持数未受信中(owned=null)は不足表示も所持/必要も出さない | 捨てる | 同上（[A-13]。所持数は常に有る） | #1641 |
| 4 | `src/features/blockInventory/details/machine/ModuleSlotStackLimitText.test.ts` | 上限の配信が届くまでは何も描かない | 捨てる | UITK は `IModuleSlotStackLimitLookup.CurrentStackLimit` を同期で読み、配信待ちの状態が無い（S4-machine D-10） | — |
| 4 | `e2e/mock-host/detailActions.test.ts` | setはrecipeGuidを選択状態へ反映する | 捨てる | mock-host 自身の契約。UITK は `MachineRecipeSelectionProtocol` を直に送り、解放の検査は `MachineRecipeSelectCommandTest` が持つ（S4-machine D-11） | — |
| 4 | `e2e/mock-host/detailActions.test.ts` | recipeGuidなしsetとclearは空GUIDへ戻す | 捨てる | 同上（S4-machine D-11） | — |
| 4 | `e2e/mock-host/detailActions.test.ts` | 対象machineがないblockと未知operationを拒否する | 捨てる | 同上（S4-machine D-11） | — |
