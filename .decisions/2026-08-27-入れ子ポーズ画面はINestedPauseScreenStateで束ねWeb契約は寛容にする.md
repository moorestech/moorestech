# 入れ子ポーズ画面はINestedPauseScreenStateで束ね、Web契約は寛容にする

- 日付: 2026-08-27（PR feature/skit-pause-menu の最終レビュー裁定）
- 決定:
  1. skitPause中はWebのスキット表示層(SkitPresentation)の入力だけ止める（pointer-events:none・表示と自動送りは継続）。ポーズパネルのz順は変えない
  2. 「入れ子ポーズを持つ画面」を `INestedPauseScreenState { SubStateName; OnPresentationChanged; bool RequestClosePauseMenu() }` で束ね、TrainHUDScreenState/SkitStateに実装。UiStateTopic/UiStateActionsは1分岐に畳み、閉じ要求は実際に閉じたかをboolで返して拒否(transition_not_allowed)を復活。サブステートコントローラ/ポーズサブステートは列車HUDと共通化しGetKeyHints委譲も復活
  3. Web `UiStateDataSchema.subState` は `z.string().optional()`（stateと対称・未知値はscreenForUiStateのfail-safe）
  4. 到達不能なuGUI会話UI復帰枝（SkitUITools.IsUIHidden/ShowUI・SkitUI.ShowHiddenUI）は削除しstore一本に。スキット側サブステート語彙は列車HUDと同じ GameScreen/PauseMenuScreen に統一
- 棄却案: ポーズパネルを最前面に上げる（全ポーズ画面のレイヤ規約見直しを伴う）／複製のまま現状維持／subStateをstate別constで型付け・discriminatedUnionで厳格化／uGUI枝・語彙とも現状維持
- 理由: 入力遮断はskitPause経路に限定でき裁定「背後で流し続ける」と両立する。抽象化は3画面目の更新漏れ事故（メニューが出ない/閉じない）を型で防ぐ。閉じたenumは語彙追加でWebが固着する。uGUIモードは存在しない（IsWebUiMode恒久true）
- 出所: ユーザー裁定 2026-08-27 AskUserQuestion 4問すべて推奨案を選択
- リンク: docs/adr/0035-skit-pause-menu-nested-substate.md、moorestech_logs harness/moores-code-review/runs/2026-08-26-1859/design.md
