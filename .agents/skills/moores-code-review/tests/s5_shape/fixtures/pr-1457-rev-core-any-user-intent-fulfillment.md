# rev-core-any-user-intent-fulfillment (PR #1457)

Critical: なし

## 依頼動詞ごとの達成判定（patch の + 行と、未変更の最終利用点を cwd で Read して確認）
- 開いたまま歩ける（インベントリ・ブロックインベントリ・研究・ビルド・チャレンジ）: PlayerInventoryState / SubInventoryState / ResearchTreeState / BuildMenuState / ChallengeListState の LocksPlayerMovement が true→false。最終利用点 UIStateControl.cs:109 ApplyMovementLock -> PlayerObjectController.SetMovementLock(Ui,..) -> controller.SetControllable(_movementLocks.Count==0 && !riding) まで読んで接続を確認。未変更の PauseMenuState.cs:29 と PauseMenuNestedSubState.cs:41 は true のまま。DeleteObject / DebugBlockInfo / PlaceBlock は元から false。達成。
- ポーズだけ止める: 上記のとおり。列車HUD・スキットのサブステート宣言は据え置き（WASD を画面が奪う画面。PR 側の IUIState doc コメントにも明記）。達成。
- 距離で自動クローズ（距離は同じ定数）: InteractOverlap.InteractDistance を一元化し、InteractTargetSelector の候補選定と InteractReachQuery の到達判定が同じ定数・同じ LayerMask・同じ OverlapNearby を使う。SubInventoryState.GetNextUpdate が毎フレーム IsOutOfReach を評価し GameScreen へ遷移する。InteractableResolver.TryResolve は BlockGameObjectChild.Interactable -> BlockGameObject.Interactable を返すので ReferenceEquals 比較が成立する（BlockSubInventorySource が返す参照と同一）。ブロック撤去時は TryGetReachTarget=false で閉じる。達成。
- 列車も閉じる（自機と対象の双方の移動を拾う）: TrainSubInventorySource が ID から TrainCarObjectDatastore.TryGetEntity で毎回今の表示を引き直し、毎フレーム判定する。ファクトリ->datastore->TrainCarInteractable->Action へ datastore を配線済み。コード上は達成（実機は未検証。下記 Warning）。
- Space のブラウザ既定動作を文字入力欄以外で封じる: useWebInputExclusivity.ts の keydown/keyup が suppressesBrowserDefaultKey（Tab 常時、Space は !isTextInputElement）で preventDefault。他に Space を処理する webui コードは grep で無し。達成。
- クラフトボタンは Space 非反応（Enter・マウス長押しは従来どおり）: CraftRecipeEntry.tsx の onKeyDown/onKeyUp が Enter のみ。onPointer* は無変更。達成。
- TextInput 新停止理由: PlayerMovementLockReason.TextInput 追加、TextInputMovementLockApplier を PlayerSystemContainer.Construct で配線（購読後に現在値を適用）。WebUiInputExclusivity.OnTextInputFocusedChanged を新設し ObserveOnMainThread。達成。

## 非目標の侵犯確認
視点操作・Shift 排他・自動クローズのトースト・外部 repo 変更は patch に無し。侵犯なし。

## Warning
- moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarObjectDatastore.cs: 「列車が発車して離れた場合も閉じる」（R4）はコード経路（毎フレーム再解決）の存在までしか確認できず、実機未検証。コンテキストの許容トレードオフ欄にも [agent前提] として記載済み。未確認として列挙する。
- moorestech_web/webui/src/features/skit/SkitPresentation.tsx:68 と useWebInputExclusivity.ts:32: Space で台詞送りができなくなる副作用。ADR「Web UI全体で封じる」の範囲内だが、依頼文（ゴール欄）は「ボタンのクリック・スクロール」を封じる趣旨で、スキット送りの Space 廃止までは明示されていない。意図確認が望ましい未確認事項（Critical にはしない）。
- moorestech_web/webui/src/shared/uiState/useWebInputExclusivity.ts: 文字入力欄が focusout 無しで unmount された場合に TextInput 停止が残る可能性。ただし Move キー自体が元から Keyboard 抑止対象で、新規退行ではない。未確認。

## Info
- .decisions/2026-10-01-サブインベントリの自動クローズ距離...md と docs/superpowers/plans/2026-10-01-walk-in-menus-except-pause.md は `InteractTargetSelector.InteractDistance` を名指しするが、定数は本 PR で InteractOverlap.InteractDistance へ移動済み。文書が陳腐化している（ゴールの「同じ定数を参照」自体は満たす）。

## §5 裁定引用の含意チェック
対象: あり（決定 10 件）
- メニュー中の移動停止はESCメニューだけ — 引用「ESCのメニューだけ開いている途中動かないようにしたい」「インベントリ、研究、ビルドメニュー等が開いていある間も動いていたい」 / 読み1: ESC メニューのみ移動停止、他メニューは歩ける → 両立 / 読み2: ESC メニューのみ停止だが列車HUD等の画面は別枠で停止が残る → 両立（決定は「メニュー」5種を列挙） / 棄却案「ブロックインベントリだけ止めたまま」→ no（引用は「インベントリ…等」を歩けるとしている）
- ブロックインベントリは一定距離離れたら閉じる — 引用「指定距離離れたら自動で閉じる」 / 読み1: 距離超過で閉じる → 両立 / 読み2: 距離は別途指定値 → 両立（距離は別決定で扱う） / 棄却案「開いたままにする」→ no
- ブロックインベントリは離れても開いたまま（上記で覆される旧決定）— 引用は質問+選択「開いたまま」 / 覆された決定であり現行は上の決定が正。実装は覆し後に従う → 両立 / 棄却案「離れたら閉じる」→ yes（旧決定自身が棄却した案が後の決定で採用された。覆し履歴として説明されており転記歪みではない。Critical にしない）
- 自動クローズ距離は定数参照 — 引用「ちゃんと同じパラメーターを参照する」+復唱への「はい」 / 読み1: 開く距離の定数を共有 → 両立 / 読み2: 値を同じ 2 に揃えるだけ → 両立（決定は定数参照で包含） / 棄却案 5m・10m → no
- 列車も距離で閉じる — 質問「自動で閉じる対象に列車も含めますか？」→「列車も同じく閉じる」 / 棄却案「ブロックだけ」→ no
- Web UI全体で Space 封じ — 質問+選択「Web UI全体で封じる」 / 棄却案「クラフトボタンだけ」→ no
- クラフトボタンは Space ジャンプ専用 — 質問+選択「Spaceはジャンプ専用」 / 棄却案「今のまま」→ no
- メニュー中の Shift は両方効く — 質問+選択「両方効く」 / 棄却案「Web UI上は一括移動専用」→ no
- メニュー中の視点は回さない — 質問+選択「今のまま、移動だけ解放」 / 棄却案「右ドラッグ等で視点」→ no
- 文字入力中は止める — 質問+選択「入力中は止める（現状維持）」 / 棄却案「入力中も歩く」→ no
