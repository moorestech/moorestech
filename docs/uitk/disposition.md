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
| 2026-11-11 | 4 | 9e2228c658 | 457 | 371 | 22 | 2 | 62 |
| 2026-11-12 | 3 | 6028360d76 | 99 | 99 | 0 | 0 | 0 |
| 2026-11-12 | 2 | 080539a95d | 115 | 111 | 4 | 0 | 0 |
| 2026-11-13 | 4 | c440f807cd | 457 | 434 | 23 | 0 | 0 |
| 2026-11-14 | 4 | f5d0e71e61 | 457 | 434 | 23 | 0 | 0 |
| 2026-11-15 | 1 | 4fc636d56d | 66 | 57 | 9 | 0 | 0 |
| 2026-11-15 | 2 | 4fc636d56d | 115 | 111 | 4 | 0 | 0 |
| 2026-11-15 | 3 | 4fc636d56d | 99 | 99 | 0 | 0 | 0 |
| 2026-11-15 | 4 | 4fc636d56d | 457 | 434 | 23 | 0 | 0 |
| 2026-11-15 | 5 | 4fc636d56d | 528 | 243 | 285 | 0 | 0 |
| 2026-11-16 | 1 | 0beb563f74 | 66 | 57 | 9 | 0 | 0 |
| 2026-11-16 | 2 | 0beb563f74 | 115 | 111 | 4 | 0 | 0 |
| 2026-11-16 | 3 | 0beb563f74 | 99 | 99 | 0 | 0 | 0 |
| 2026-11-16 | 4 | 0beb563f74 | 457 | 433 | 24 | 0 | 0 |
| 2026-11-16 | 5 | 0beb563f74 | 528 | 235 | 293 | 0 | 0 |
| 2026-11-16 | 1 | a4988176fb（--web-rev 6248097123） | 66 | 57 | 9 | 0 | 0 |
| 2026-11-16 | 2 | a4988176fb（--web-rev 6248097123） | 115 | 111 | 4 | 0 | 0 |
| 2026-11-16 | 3 | a4988176fb（--web-rev 6248097123） | 99 | 99 | 0 | 0 | 0 |
| 2026-11-16 | 4 | a4988176fb（--web-rev 6248097123） | 457 | 433 | 24 | 0 | 0 |
| 2026-11-16 | 5 | a4988176fb（--web-rev 6248097123） | 528 | 235 | 293 | 0 | 0 |

## 処遇

| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |
|---|---|---|---|---|---|
| 1 | `e2e/tests/regression/actionErrors.spec.ts` | * | 捨てる | WS の action（block_inventory.split）の失敗コードに対するトーストの契約。UITK のコマンドは WS を通らず、通信失敗のトーストは撤去まで Web 側に残る（台帳「agent 既定」の通信失敗のトースト） | #1596 |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | トップから設定画面へ進み言語を選べ、戻るでトップへ戻る | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs（設定へ進み日本語を選んでロケール印を待ち、戻るでトップへ戻る） | #1596 |
| 1 | `e2e/tests/system/pauseMenu.spec.ts` | トップからバグ報告画面へ進み送信すると成功を表示する | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs（バグ報告へ進み本文を入れて送り、トーストと outbox の新しい箱を確かめ、トップのセーブを待つ） | #1596 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | 拒否理由と対象項目を表示文言へ解決する | 捨てる | WS の失敗コード（invalid_graphics_quality:invalid_type:…）を文言へ解く契約。UITK の SetGraphicsQuality は型付きの値を受け結果を返さず、拒否の経路が無い | #1596 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | topicを固定したまま連続操作とpreset直後に%sを送る | 捨てる | UITK は画質の全項目を送り、送った同じ呼び出しの中で SettingsModel から View の Apply まで回るので、モデルが追いつかない間の連続操作が起きない（`GraphicsQualityPreference.cs:41` の Subject.OnNext → `SettingsModel.cs:23,32-34` → `PauseMenuView.cs:98,117-119` → `GraphicsQualitySettingsView.Apply`。偽のモデルで連続操作を写した NUnit は 9 件とも落ちた〈winunity run 20261115-141909〉が、本番では起きない。S5-ladder D-14） | #1674 |
| 1 | `src/features/settings/GraphicsQualitySettings.test.ts` | topic未着では項目を描かない | 捨てる | UITK の SettingsModel は生成時に設定値を同期で持ち、topic 未着の状態が無い | #1596 |
| 1 | `src/features/settings/LanguageSelect.test.ts` | 一覧が届くまでは辞書非依存リテラルの読み込み中を出しModeSwitchを描かない | 捨てる | UITK の言語一覧は LanguageCatalog の静的な表で、配信待ちの状態が無い | #1596 |
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
| 4 | `e2e/tests/research/research.spec.ts` | translate後のグリップ矩形だけに重なる境界buttonをexpectCraftGripが検出する | 捨てる | Playwright の補助関数 expectCraftGrip 自身の検査で、DOM の translate とボックスの境界に依る。UITK に同じ補助が無く、写す対象の挙動が無い | #1641 |
| 4 | `e2e/tests/research/research.spec.ts` | padding box外の境界buttonをexpectCraftGripが重なりなしと判定する | 捨てる | 同上（expectCraftGrip の自己テスト） | #1641 |
| 4 | `e2e/tests/research/research.spec.ts` | 研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/research/uitk-research-tree.cs（研究画面を開いたまま、判定の間だけ HUD の根を拾う設定にして challenge-hud・key-hints の中心の Pick が各 HUD を返す。層の順序の主張） | #1641 |
| 4 | `e2e/tests/research/tmp-icon-click.spec.ts` | * | 捨てる | tmp の付いた一時の調査テストで、実行計画 §1.4 の完了条件が写す数から除いた | #1641 |
| 4 | `src/features/recipe/logic/recipeSlotLayout.test.ts` | スロット寸法は列数ぶんのgapを差し引いて上限で頭打ちになる | 捨てる | Web は CSS の calc 文字列でスロット寸法を列幅から縮めるが、UITK は寸法をつまみの上限のまま置く（S4-recipe A-121・LX-91）ので寸法の式が無い。列数の式は `RecipeSlotLayoutTest` が持つ | — |
| 4 | `src/features/research/ResearchTreePanel.test.ts` | インベントリtopic未受信中は詳細ペインへ所持数をnullで渡す(D4) | 捨てる | UITK は持ち物を同期で持ち、所持数の未受信の状態が無い（設計書 [A-13] の ConnectingPlaceholder と同じ理由） | #1641 |
| 4 | `src/features/research/ResearchDetailPane.test.ts` | 所持数未受信中(owned=null)は不足表示も所持/必要も出さない | 捨てる | 同上（[A-13]。所持数は常に有る） | #1641 |
| 4 | `src/features/blockInventory/details/machine/ModuleSlotStackLimitText.test.ts` | 上限の配信が届くまでは何も描かない | 捨てる | UITK は `IModuleSlotStackLimitLookup.CurrentStackLimit` を同期で読み、配信待ちの状態が無い（S4-machine D-10） | — |
| 4 | `e2e/mock-host/detailActions.test.ts` | setはrecipeGuidを選択状態へ反映する | 捨てる | mock-host 自身の契約。UITK は `MachineRecipeSelectionProtocol` を直に送り、解放の検査は `MachineRecipeSelectCommandTest` が持つ（S4-machine D-11） | — |
| 4 | `e2e/mock-host/detailActions.test.ts` | recipeGuidなしsetとclearは空GUIDへ戻す | 捨てる | 同上（S4-machine D-11） | — |
| 4 | `e2e/mock-host/detailActions.test.ts` | 対象machineがないblockと未知operationを拒否する | 捨てる | 同上（S4-machine D-11） | — |
| 2 | `src/features/notification/notificationStore.test.ts` | 退場アニメが発火しなくても保険タイマーで消える | 捨てる | UITK の通知ホストは背面の層に常駐し、外れるのは Dispose だけなので退場の終わりが必ず来る。保険タイマーを持たない（Web の保険は React の unmount で onAnimationEnd が来ない場合のもの）。外れた場合は NotificationHostView の LogWarning が知らせる | #1649 |
| 4 | `src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx` | accepts panel data and narrows inside the dedicated view | 捨てる | TS の型検査（`expectTypeOf`）。C# は型がコンパイルで閉じる | S4-special |
| 5 | `src/features/eventLanguageGate/EventLanguageGate.test.ts` | 一覧が届くまでは辞書非依存リテラルの読み込み中を出す | 捨てる | UITK の言語の一覧は生成済みの `LanguageCatalog.Languages` を同期で読むので、読み込み中の状態が無い（Web は WS の topic で一覧を受ける）。S5-gate D-5 | #1661 |
| 5 | `src/features/eventLanguageGate/EventLanguageGate.test.ts` | 選択が受理されなければ辞書非依存リテラルの失敗行を出す | 捨てる | UITK の言語の一覧はどれも選べる（一覧と辞書の言語の列が一致しないと生成でビルドが止まり、mod は知らない言語を例外で拒否する）ので、本番の押下で `UnknownLanguage` は返らず失敗の 1 行の状態が無い。拒否が返れば View がログに残す。S5-gate D-6 | #1661 |
| 5 | `src/features/eventLanguageGate/EventLanguageGate.test.ts` | 選択済みの拒否は受け付け済みとして押下不可のまま辞書非依存の1行を出す | 捨てる | UITK は受理の同期の通知でその押下の中でゲートを外すので、選び終えたゲートへ押下が届かず `LanguageAlreadySelected` は本番で返らない（Web は action の往復の間に二度目の押下が届く）。S5-gate D-6・D-8 | #1661 |
| 5 | `src/features/eventLanguageGate/useLanguageSelectionAnswer.test.ts` | アンマウント後に受理が解決しても閉じない見張りを張らない | 捨てる | UITK の選択は同じプロセスで同期に受理し、受理の通知（`OnWaitingChanged`）でその場でゲートを外すので、応答待ちも 10 秒の閉じない見張りも無い（Web は action の往復と topic の配信が別経路なので見張りを持つ）。S5-gate D-6・D-8 | #1661 |
| 5 | `e2e/tests/regression/connection.spec.ts` | 切断後は全topic snapshotをrestoring中に復元し旧世代snapshotを破棄する | 捨てる | UITK には Web↔C# の WS が無く切断・復元の状態が起きない（設計書 A-38・実行計画 E-17） | #1673 |
| 5 | `e2e/tests/regression/connection.spec.ts` | inventory未受信中はconnecting placeholderを表示する | 捨てる | UITK に「未受信」の状態が無い（設計書 A-13） | #1673 |
| 5 | `e2e/tests/regression/connection.spec.ts` | WS切断時は再接続バナーをz-reconnect層に表示する | 捨てる | 同 A-38・E-17 | #1673 |
| 5 | `src/shared/i18n/allScreensI18n.test.ts` | fetches fallback only once while also loading source for the fallback locale | 捨てる | UITK の辞書は起動時に同期で読み込み、HTTP 取得・取得の世代・topic 待ちが無い（設計書 A-38） | #1673 |
| 5 | `src/shared/i18n/allScreensI18n.test.ts` | waits for the localization topic before the first dictionary fetch | 捨てる | 同上（A-38） | #1673 |
| 5 | `src/shared/i18n/allScreensI18n.test.ts` | keeps the newest locale when StrictMode cleanup aborts an older dictionary generation | 捨てる | 同上（A-38） | #1673 |
| 5 | `src/bridge/contract/__tests__/blockInventoryValidators.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/actionNames.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/contentGuidContracts.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/eventLanguageGateContract.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/__tests__/buildMenu.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/__tests__/contentIdentity.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/__tests__/guidValidation.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/__tests__/inventory.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/__tests__/presentation.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/__tests__/ui.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/rocketLaunchPad/index.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/schemas/spaceElevator/index.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/validators.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/wireContract.machineUpgrade.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/wireContract.research.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/wireContract.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/contract/wireContract.trainTimetable.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/playtest/domQueryResponder.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/playtest/domSetTextResponder.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/playtest/playtestTopicResponder.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/store/fluidMasterStore.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/store/itemMasterStore.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/store/languageList/languageListStore.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/store/topicEventDelivery.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/store/topicStore.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/transport/actions.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/transport/httpEndpoints.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/transport/subscriptionManager.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `src/bridge/transport/tests/webSocketClient.test.ts` | * | 捨てる | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える | #1674 |
| 5 | `e2e/mock-host/tests/httpHandler.test.ts` | * | 捨てる | Web の検証基盤（mock-host の HTTP 応答）。UITK は mock-host を使わない | #1674 |
| 5 | `e2e/mock-host/tests/spaceElevatorConnect.test.ts` | * | 捨てる | mock-host の模擬サーバーの挙動。UITK は本物のサーバーにつなぎ、mock-host を使わない | #1674 |
| 5 | `e2e/tests/reference/captureReadiness.spec.ts` | * | 捨てる | Web の参照キャプチャの撮影の準備の検査。UITK の撮影は `UiRuntimePanelCapture`・`PlaytestGameViewOps`（compare-procedure.md §3） | #1674 |
| 5 | `e2e/uitk-reference/captureLifecycle.test.ts` | * | 捨てる | 同上（Web の撮影ツールの後始末と manifest） | #1674 |
| 5 | `src/shared/i18n/localizationKeysFreshness.test.ts` | * | 捨てる | TS の生成物 `localizationKeys.ts` の鮮度と CSV の構文解析。C# 側は SourceGenerator が毎ビルド CSV から生成し古くならない | #1674 |
| 5 | `src/shared/i18n/provider/dictionaryRetry.test.ts` | * | 捨てる | 辞書の HTTP 取得の再試行。UITK は辞書を同じプロセスで同期に読み、取得が無い（設計書 A-38） | #1674 |
| 5 | `src/shared/i18n/provider/dictionaryGeneration.test.ts` | * | 捨てる | 同上（取得の世代の入れ替え） | #1674 |
| 5 | `src/shared/i18n/noJsxVisibleLiteral.test.ts` | * | 捨てる | JSX の見える文字の lint。UITK の同じ規則は UXML 側の `Client.Tests/Ui/Text/UxmlLiteralTextTest` が持ち、JSX の構文は無い | #1674 |
| 5 | `src/app/App.architecture.test.ts` | * | 捨てる | React の App の構成の制約（辞書エラーのリロード・画面固有のクロームを持たない）。UITK は辞書エラーを作らず（設計書 A-38）、層の向きは `Client.Tests/Ui/Conventions/UiLayerDependencyDirectionTest` が持つ | #1674 |
| 5 | `src/shared/uiState/activeLayer.test.ts` | Enterなど他のキーは封じない | 捨てる | ブラウザ既定動作の封じ対象を Tab・Space に絞る DOM の許可リストの主張で、UITK にはブラウザ既定動作が無く、ルートは Enter 由来の決定を含むナビゲーションを全部打ち消し Enter は各画面が自前で扱う（`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs` の Attach） | #1674 |
| 5 | `src/shared/uiState/activeLayer.test.ts` | 文字を打てるinput typeは文字入力欄として数える | 捨てる | DOM の要素の種類の判定。UITK の文字入力は `TextField` のフォーカスだけで決まる（`GameUiInputRules.IsInsideTextField`） | #1674 |
| 5 | `src/shared/uiState/activeLayer.test.ts` | 押すだけのinput typeと未知のtypeは文字入力欄にしない | 捨てる | DOM の要素の種類の判定。UITK の文字入力は `TextField` のフォーカスだけで決まる（`GameUiInputRules.IsInsideTextField`） | #1674 |
| 5 | `src/shared/uiState/activeLayer.test.ts` | contenteditableは有効な値のときだけ文字入力欄になる | 捨てる | DOM の要素の種類の判定。UITK の文字入力は `TextField` のフォーカスだけで決まる（`GameUiInputRules.IsInsideTextField`） | #1674 |
| 5 | `src/shared/i18n/contentKeys.test.ts` | merges content keys into the translation key contract | 捨てる | TS の型の和（ContentLocalizationKey を TranslationKey へ代入できること）の検査で、UITK は導出キーを C# の型 `ContentLocalizationKey`（`Mooresmaster.Localization.Generated`）で持ち型の和の契約が無い | #1674 |
| 5 | `src/shared/i18n/contentKeys.test.ts` | re-exports the generated builders through the i18n barrel | 捨てる | TS のモジュール構成（i18n の barrel からの再輸出）の検査で、UITK の生成コードは `ContentLocalizationKeys` 1 か所から直に使い再輸出の層が無い | #1674 |
| 5 | `src/shared/i18n/contentKeys.test.ts` | rejects strings outside the content key grammar | 捨てる | `@ts-expect-error` による TS の型レベルの文法検査で、UITK は文字列を受けず Guid を引数に取る生成メソッド（`ContentLocalizationKeys.ItemName(Guid)` 等）だけでキーを作るため文法外の文字列を渡す口が無い | #1674 |
| 5 | `src/shared/i18n/diagnostics/translationWarnings.test.ts` | warns once per missing key until dictionaries change | 捨てる | React の再描画で同じ警告が繰り返される問題への世代ごとの重複抑止で、UITK は解決を描画のたびに呼ばず、欠けたキーの警告自体も持たない（`Client.Localization/LocalizationTextResolver.cs` は印を返すだけ） | #1674 |
| 5 | `src/shared/i18n/diagnostics/translationWarnings.test.ts` | keeps missing-key warnings isolated between old and current translators | 捨てる | React の translator の世代（旧翻訳器と現行翻訳器）ごとの警告の分離で、UITK には translator の世代が無く `Localize` の単一の公開 snapshot から解く | #1674 |
| 5 | `src/shared/i18n/diagnostics/translationWarnings.test.ts` | deduplicates each key and placeholder across renders and isolates generations | 捨てる | React の再描画で同じ警告が繰り返される問題への重複抑止で、UITK は解決を描画のたびに呼ばない（値の無い枠は `LocalizationTextInterpolator` が呼ばれるたびに LogError し、その挙動は `LocalizeGetFormattedTest` が持つ） | #1674 |
| 5 | `src/shared/i18n/itemName/useItemName.test.ts` | マスタ未着ではnullを返す | 捨てる | UITK にマスタ未受信の状態が無い（マスタは起動時に同期で持つ。設計書 A-13） | #1674 |
| 5 | `src/shared/i18n/itemName/useItemName.test.ts` | 辞書とマスタが同じ再描画ではresolver参照を維持する | 捨てる | React の再描画の最適化（hook が返す resolver の参照の同一性）で、UITK に再描画ごとに作り直す resolver が無い | #1674 |
| 5 | `src/shared/i18n/itemName/useItemName.test.ts` | マスタ未着ではid表示へ落とす | 捨てる | UITK にマスタ未受信の状態が無い（設計書 A-13） | #1674 |
| 5 | `src/shared/i18n/useI18n.test.ts` | starts uninitialized with no dictionary content | 捨てる | React の辞書の取得状態。UITK は起動時に同期で辞書を持ち、取得の状態が無い（A-38） | #1674 |
| 5 | `src/shared/i18n/useI18n.test.ts` | does not warn or show a missing marker before the first dictionary generation is ready | 捨てる | React の辞書の取得状態。UITK は起動時に同期で辞書を持ち、取得の状態が無い（A-38） | #1674 |
| 5 | `src/shared/i18n/useI18n.test.ts` | exposes an error status when the very first dictionary load fails | 捨てる | React の辞書の取得状態。UITK は起動時に同期で辞書を持ち、取得の状態が無い（A-38） | #1674 |
| 5 | `src/shared/i18n/useI18n.test.ts` | keeps translations empty and silent after the very first load failed | 捨てる | React の辞書の取得状態。UITK は起動時に同期で辞書を持ち、取得の状態が無い（A-38） | #1674 |
| 5 | `src/shared/i18n/useI18n.test.ts` | keeps the last ready generation while exposing a later load failure | 捨てる | React の辞書の取得状態（言語切替時の非同期取得の失敗）。UITK は全言語の辞書を起動時に同期で持ち、言語切替に取得が無い（A-38） | #1674 |
| 5 | `src/shared/i18n/useI18n.test.ts` | narrows only generated keys at an external string boundary | 捨てる | TS の型の絞り込み（`isTranslationKey` の type guard）で、UITK は生成キーを C# の `LocalizationKeys` の型で持ち、外部文字列を生成キーへ絞る実行時の判定が無い | #1674 |
| 5 | `src/app/iconTextOutlineDesign.test.ts` | 縁を真のストロークで描く共有クラスを2本だけ持つ | 捨てる | CSS の共有クラスの数（paint-order・-webkit-text-stroke の 2 本）の規則で、UITK は `OutlinedCountLabel` 1 つが縁を持ち色は `--outlined-count-label-stroke` のつまみで切り替える（`Client.Ui/Components/Slots/OutlinedCountLabel/OutlinedCountLabel.uss`）ので同じ構造が無い | #1674 |
| 5 | `src/shared/tutorialAnchor/tutorialAnchor.test.ts` | creates only the tutorial contract attribute | 捨てる | DOM の属性（data-tutorial-anchor 1 つだけ）の書式で、UITK はクラス名で印を付ける（`Client.Ui/Foundation/Anchor/TutorialAnchor.cs`。印付けは `TutorialAnchorRegistryTest.MarkedElementResolvesAsUnique` が見る） | #1674 |
| 5 | `src/shared/tutorialAnchor/tutorialAnchor.test.ts` | builds a whitespace token match selector | 捨てる | CSS の属性セレクタ（`~=`）の組み立てで、UITK はクラス名の照会で解決しセレクタ文字列を作らない（`Client.Ui/Foundation/Anchor/TutorialAnchorRegistry.cs`） | #1674 |
| 5 | `src/shared/ui/IconButton/index.test.ts` | aria-label、testId、tutorial向け残余propsをbuttonへ渡す | 捨てる | DOM の属性（aria-label・data-testid・残余 props・type=button）の受け渡しで、UITK の IconButton は残余の属性を受けない（testid の name と押下は `IconButtonTest.クリックで押下を1回流しキーと決定では押されずフォーカスを持たない` が見る） | #1674 |
| 4 | `src/features/blockInventory/registry/blockComponentRegistry.test.ts` | 網羅e2eが参照する登録blockType一覧と実レジストリを一致させる | 捨てる | Web の registeredBlockTypes との照合。Web の撤去で相手が無くなる（UITK の登録の正しさは同じファイルの残りのテストと `BlockRegistryCoverageTest` が見る） | — |
| 5 | `src/shared/tutorialAnchor/anchorIds.test.ts` | static IDs match the shared fixture exactly | 捨てる | Web の anchorIds.ts との照合。Web の撤去で相手が無くなる（一覧が `AnchorIds` の定数を過不足なく含むことは `AnchorIdsFormatTest.StaticIdsListEveryNonPrefixConstant` が見る） | — |
| 5 | `src/shared/tutorialAnchor/anchorIds.test.ts` | dynamic ID prefixes match the shared fixture exactly | 捨てる | Web の anchorIds.ts との照合。Web の撤去で相手が無くなる（`AnchorIdsFormatTest.DynamicPrefixesListEveryPrefixConstant` が見る） | — |
| 5 | `src/app/zLayerTokens.test.ts` | 通知が沈む背面層はディムより前・stageより後ろに立つ | 捨てる | Web の `--z-*` トークンの順との照合。Web の撤去で相手が無くなる（UITK の層の上下は `UiLayer` の宣言順で、`UiLayerStackTest.LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks` と `UiLayerStackTest.FrontLayerElementIsPickedOverBackLayerElement` が見る） | — |
| 5 | `src/app/zLayerTokens.test.ts` | stageとディムの層序は生値でなくトークンを参照する | 捨てる | Web の `--z-*` トークンの順との照合。Web の撤去で相手が無くなる（UITK の層の上下は `UiLayer` の宣言順で、`UiLayerStackTest.LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks` と `UiLayerStackTest.FrontLayerElementIsPickedOverBackLayerElement` が見る） | — |
| 5 | `src/app/zLayerTokens.test.ts` | 常駐HUD層はscreen層より前に立ち、ツールチップはさらに前に立つ | 捨てる | Web の `--z-*` トークンの順との照合。Web の撤去で相手が無くなる（UITK の層の上下は `UiLayer` の宣言順で、`UiLayerStackTest.LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks` と `UiLayerStackTest.FrontLayerElementIsPickedOverBackLayerElement` が見る） | — |
| 5 | `src/app/zLayerTokens.test.ts` | ポーズメニューは.viewportOverlay一式より前・ツールチップより後ろに立つ | 捨てる | Web の `--z-*` トークンの順との照合。Web の撤去で相手が無くなる（UITK の層の上下は `UiLayer` の宣言順で、`UiLayerStackTest.LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks` と `UiLayerStackTest.FrontLayerElementIsPickedOverBackLayerElement` が見る） | — |
| 5 | `src/app/zLayerTokens.test.ts` | モーダルはトーストより前に立つ | 捨てる | Web の `--z-*` トークンの順との照合。Web の撤去で相手が無くなる（UITK の層の上下は `UiLayer` の宣言順で、`UiLayerStackTest.LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks` と `UiLayerStackTest.FrontLayerElementIsPickedOverBackLayerElement` が見る） | — |
| 5 | `src/app/zLayerTokens.test.ts` | 全画面ゲートは再接続オーバーレイより前に立つ | 捨てる | Web の `--z-*` トークンの順との照合。Web の撤去で相手が無くなる（UITK の層の上下は `UiLayer` の宣言順で、`UiLayerStackTest.LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks` と `UiLayerStackTest.FrontLayerElementIsPickedOverBackLayerElement` が見る） | — |
