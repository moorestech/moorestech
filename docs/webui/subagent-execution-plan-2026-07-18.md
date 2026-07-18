# web-ui 設計負債解消 実行計画 (2026-07-18)

`design-debt-audit-2026-07-17.md`（45件、2026-07-18鮮度更新済み）をsubagent駆動で解消するための実行計画。監査が診断、本書が処方箋。ワークユニット（WU）はファイル衝突しない単位に束ねてあり、同一WU内は1エージェント直列、WU間は依存順を守れば並列可。

## 決定事項（2026-07-18 ユーザー決裁）

1. **zod導入: 採用。** ワイヤ契約（型・バリデータ・topic定義の3ファイル5箇所分散）はzodスキーマ単一定義＋`z.infer`型導出へ全面移行する。C#⇔TS間のドリフトは引き続きWireFixturesが防波堤（zodの守備範囲外）。
2. **スロット部品API: 案B（分解路線）。** 枠＋data属性＋ジェスチャ配線を`SlotFrame`へくくり出し、中身は`GameIcon`（ItemIcon/BlockIcon統合）＋`SlotBadge`。ItemSlotは薄い合成に再編する。
3. **方針: まず動くものを作る。uGUIとの視覚一致は後回し。** parity-check 47/47は必須ゲートにしない（リファクタ時の「見た目を壊していないか」のスモークチェックとしての利用は可）。視覚判断が必要な項目は保留リストへ。
4. **B3（パネル寸法336/445）: 凍結。** 監査時の数値は消滅し、パリティ較正のサブピクセル値がパネルごとに別値で存在する。較正が安定するまで整理対象にしない。整理系エージェントがこれらの値を「マジックナンバー掃除」しないこと。

## ワークユニット

| WU | 内容 | 対象指摘 | 主なファイル領域 | 依存 |
|----|------|---------|----------------|------|
| WU1 | itemMasterStoreの修正（成功でループ終了・shapeガード）＋BENIGN_ERRORSにblock_inventory.split追加 | A1, A2, X1 | bridge/store/itemMasterStore.*, transport/actions.ts | なし |
| WU2 | 接続開始をinitBridge()化しmain.tsxから呼ぶ。5テストファイルのvi.mock儀式を撤去 | A5 | bridge/transport/webSocketClient.ts, main.tsx, 各test | なし |
| WU3 | 常時購読topicのpin一元管理＋unsubscribe時のclearTopic＋useTopicSelectorのequality検査 | A3, F1, F3 | bridge/store/useTopic.ts, subscriptionManager.ts, shared/uiState/activeLayer.ts, App.tsx | なし |
| WU4 | ESLint導入＋no-restricted-importsで境界強制。深いimport約40ファイルを@/bridge一本へ機械的置換。barrel迂回残件も | A6, G1 | eslint.config新設＋全features/sharedのimport行 | WU1〜3の後（同一行の衝突回避） |
| WU5 | zod移行: スキーマ単一定義、validators.ts/payloadTypes.ts置換、craft_recipesフィクスチャ＋テスト追加、ui_state型の二重定義解消 | A4, G2 | bridge/contract/*, protocol.ts | WU4の後 |
| WU6 | スロット部品分解（案B）: SlotFrame/GameIcon/SlotBadge新設、BlockSlotでC2の越境import解消、BuildMenuSlot載せ替え、FluidSlot意匠統一、SlotGrid API整理、名前解決のItemSlot自己解決化、slotActionsのstale closure解消、ステータステキスト共通化 | C2, E1, E2, E3, E4, E5, E7, D2, F2, F4, E6 | shared/ui/*, features/inventory, buildMenu, recipe/views | WU1（F4がA1修正前提）とWU4の後。現在の見た目を維持する（ピクセル変更はしない） |
| WU7 | CSS正規化: z-indexレイヤトークン一列化、同一グラデボタンの統合（鮮度更新で完全同一と確定）、.viewerCol削除、CSS命名流儀統一、色記法統一＋stylelint、decoLine明示クラス化、現行値のままのトークン昇格 | B2, D1, D3, D4, D5, D6, B1(構造のみ) | 各module.css, index.css | WU6の後（同一CSSファイルの衝突回避） |
| WU8 | 構造統合: viewsのSectionStackView統合、uiStateの独立モジュール昇格＋playerInventoryレイヤ陽性化、App.tsxのクローム整理、アイコンURLワイヤ面の集約 | C1, F5, F6, G4, G6 | blockInventory/views, shared/uiState, app/ | WU3の後 |
| WU9 | 掃除: デッドコード削除、パススルーLogic整理、小型ユーティリティ統合、未使用props削除、connecting...共通化、recipe/直下11ファイルの10ファイル上限違反解消（H1鮮度更新で発生） | G3, G5, H1, H2, H3, H4, H5, H6, E8 | 各所（削除中心） | WU6〜8の後 |

推奨実行順: **WU1・WU2・WU3を並列 → WU4 → WU5とWU6を並列 → WU7・WU8を並列 → WU9**。

## 保留リスト（着手禁止）

- **B3**: パネル寸法。パリティ較正安定まで凍結（決定事項4）。
- **B4**: GamePanelへの外装統一。buildMenu/researchのuGUI正本の見た目が未確定のため、視覚一致フェーズ再開時に判断。
- **B1の色値統廃合**: 28種のhexをuGUI正本と突き合わせて統合する作業は視覚一致フェーズへ。WU7では「現行値のままトークンに昇格」までに留める。
- **E1/E6のuGUI意味論**: 充足/不足の視覚表現をuGUIのどの表現に合わせるかの判断。WU6では現行の見た目のまま部品化だけ行う。

## 各WUの完了条件

- `npx vitest run` グリーン＋`npx tsc --noEmit`（またはvite build）成功
- WU6・WU7は加えてparity-check.pyを実行し、既存スコアから後退していないことを確認（後退した場合は報告。47/47達成は不要 — 決定事項3）
- 変更をコミットしてから完了報告（AGENTS.md規約）
- 監査ドキュメントの該当指摘IDに対応済みマークを付ける

## エージェント運用の注意

- 各WUのプロンプトには監査ドキュメントの該当セクション（指摘ID）を参照させ、**推奨対応の記述を仕様として渡す**。鮮度更新（2026-07-18）の段落がある指摘は、そちらの記述が正。
- WU間の並列実行時は対象ファイル領域が重ならないことを本表で確認してから起動する。
- モデル振り分け: 機械的置換・掃除系（WU4の置換部・WU9）はSonnet、設計判断を含むWU5・WU6・WU8はOpus以上を明示指定する。
