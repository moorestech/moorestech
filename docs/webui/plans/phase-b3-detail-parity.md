# Phase B3 実行計画: 細部パリティ（独立小粒タスク群）

親: `../MIGRATION.md` / 進捗: `../TODO.md`
全タスク独立・並行可能。**1件ずつ完結**させ、都度コミットする。
既実装に注意: プレイヤーインベントリの右クリック半分取り/1個置き/ダブルクリック収集・
ブロック側の同ジェスチャ・Shift 直接移動・40%透過減光は**実装済み。触らない**。

## 操作系

1. **スプリットドラッグ**: grab 中アイテムを複数スロットへドラッグして均等配分。
   uGUI 参照: `InGame/UI/Inventory/Main/PlayerInventorySplitDragHandler.cs`。
   配分計算はホスト側（`block_inventory.split` と同思想。クライアント床計算を作らない）
2. **右ドラッグ連続1個配置**: grab 中の右ボタンドラッグで通過スロットに1個ずつ置く。
   uGUI 参照: `PlayerInventorySlotInteraction.cs`
3. **クラフト長押し**: uGUI `CraftButton` は CraftTime 分の長押し + 進捗表示 + 離すとキャンセル
   （`_buttonDownElapsed >= _currentCraftTime` で確定）。Web の即時ワンクリック送信を
   uGUI 準拠の長押し + 進捗 + キャンセル + 連続クラフトへ
4. **ホイールのホットバー切替**: 現状 deltaY 符号のみ（±1固定）→ uGUI 準拠の入力量累積へ
   （`src/features/inventory/` の該当ハンドラ）

## 表示系

5. **CraftRecipeView の素材充足表示**: 所持数/必要数の数値テキスト（不足は赤字）+
   ツールチップで内訳
6. **アイテムリストのクラフト可能数バッジ/グレーアウト**: `craftLogic.craftable()` を
   ボタン活性以外（バッジ・グレーアウト）へ拡張
7. **機械詳細の分間生産数**: `details/MachineSection.tsx` にレシピ時間×倍率からの算出を追加
   （算出ロジックはマスタのレシピ時間と機械倍率から。uGUI 側表示があれば準拠、無ければ新規表示として設計）

## 品質フォロー

8. **`ui_state.request` ホワイトリスト**: 現 state を問わず受理されるため、Story/PauseMenu 中の
   遅延要求で強制遷移し得る。C# 側で「現 state → 受理可能 intent」の許可表を持つ
9. **itemMaster の WS 再接続後リフレッシュ**（一度ロード成功後の再接続で再取得しない。
   `bridge/store/itemMasterStore.ts`）+ **crafting validator の堅牢化**（壊れ payload での
   React クラッシュ耐性）

## 完了条件・検証

各タスクごと: vitest（ロジック）+ 該当 e2e（ジェスチャ系は必ず e2e を足す）+ C# 変更時は
契約テスト + `uloop compile`。完了ごとにコミット + `../TODO.md` のチェック更新。
