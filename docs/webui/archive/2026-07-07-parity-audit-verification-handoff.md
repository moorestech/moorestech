# 申し送り: uGUI→WebUI パリティ監査の裏取り結果（2026-07-07 02:00）

3エージェント監査の全主張を tree2 の実コード＋v8実マスタと突き合わせた検証結果。
**監査はおおむね正確だが、そのまま台帳化すると誤った修正をする箇所が2つある**（下記「要訂正」1と2）。
TODO.md へ反映する際は本ドキュメントの訂正を織り込むこと。

## 裏取りで正しさを確認した主張（そのまま採用可）

- **画面カバレッジ 4/11**: `UIStateEnum`（Client.Game/InGame/UI/UIState/UIStateEnum.cs）は
  GameScreen / PlayerInventory / SubInventory / PauseMenu / DeleteBar / Story / PlaceBlock /
  ChallengeList / ResearchTree / Debug / TrainHUDScreen の11。
  web の `src/app/uiScreenRouting.ts` がパネルを出すのは PlayerInventory / SubInventory / ResearchTree のみ
  （GameScreen はパネル無し設計、未知stateは安全側フォールバック）。残り7ステートは丸ごと未対応で監査どおり。
- **ブロックスロット操作が左クリック全取り/全置きのみ**: `features/blockInventory/blockLogic.ts` に
  pickUpPayload / placePayload の2系統しか無い。右クリック半分/1個・Shift一括移動・ダブルクリック収集・ドラッグ全滅は事実。
  ※新発見ではなく all-code-review 時の Codex 指摘として
  `docs/webui/2026-07-06-all-code-review-progress.md` の実装漏れ種リストに記録済み（TODO.md には無い）。
- **ElectricToGearGenerator 未対応 = 本物の新発見**: v8マスタに1ブロック実在。uGUI は専用ビュー
  `ElectricToGearGeneratorBlockInventoryView` ＋出力モード選択（`ElectricToGearModeSelectUITest` まである）。
  web はレジストリ未登録で Generic 落ち＝モード変更が操作不能。
- **機械詳細に分間生産数表示なし**: `details/MachineSection.tsx` は進捗＋電力率（%と現在/要求）のみ。
- **クラフト長押し・連続クラフトなし**: recipe/ 配下に hold/長押しロジックなし（TODO.md FEAT-CRAFT-1 記録済み）。
- **アイテムリストのクラフト可能数バッジなし**: `craftLogic.craftable()` は CraftRecipeView の
  クラフトボタン活性判定にのみ使用。リスト側のバッジ/グレーアウトは無い。
- **ホイールのホットバー切替が±1固定**: `InventoryPanel` は deltaY の符号しか見ない（uGUIは入力量累積）。
- **電柱・列車PF系の未対応**: ElectricPole(3) / TrainStation(1) / TrainItemPlatform(1) / TrainFluidPlatform(1)
  はv8マスタに実在し、web レジストリに無い。TODO.md 記載どおり。
- **研究報酬の個数表示なし**: 既知（TODO.md 記載済み、ワイヤ型が個数未伝搬でC#変更要）。

## 要訂正（監査の誤り・過大表現）

1. **【最重要】「GearEnergyTransformer キーをレジストリに追加」は誤った処方箋**。
   `GearEnergyTransformer` という blockType はスキーマ（VanillaSchema/blocks.yml）にも v8 マスタにも存在しない。
   実態: v8マスタで **5ブロック（blockType = Shaft / Gear / GearChainPole 系）** が
   `blockUIAddressablesPath: "Vanilla/UI/Block/GearEnergyTransformerUI"` を指定してトルク/RPM UIを開く。
   web の `blockComponents` レジストリは blockType がキーなので、**登録すべきキーは Shaft / Gear / GearChainPole**
   （実装時は v8 blocks.json で当該addressablesPathを持つ blockType を再列挙して確定させること）。
   「TODO.md でギア系[x]完了扱いだが伝達器はGeneric落ち」という指摘自体は正しい。
2. **「プレイヤーインベントリは単発クリックのみ」は誤り**。`features/inventory/InventoryPanel/index.tsx` に
   右クリック半分取り（`inventory.split`）/ grab保持中の右クリック1個置き / ダブルクリック収集（`inventory.collect`）
   が実装済み。本当に欠けているのは**ドラッグ系のみ**（grabの複数スロット均等配分スプリットドラッグ・右ドラッグ連続1個配置）。
   Shift移動が main↔hotbar 限定（SubInventory対象外）は事実で種リスト記録済み。
3. **「素材の不足表示が無い」は半分誤り**。CraftRecipeView は不足素材を 40% 透過で減光する（uGUI準拠とコメント明記）。
   無いのは「所持数/必要数の数値テキスト（不足赤字）」と「ツールチップ内訳」。台帳にはこの粒度で書くこと。
4. **BaseCamp の優先度は過大**。blockType としてスキーマenumには在るが **v8 マスタに BaseCamp ブロックは0個**。
   「完成ボタン操作不能」は現行コンテンツでは発生しない。将来コンテンツ向けの低優先として記載。
5. **TankInventory は「要整理デッドコード」ではない**。`blockLogic.test.ts` に
   「実流体ブロック配線時に再登録する想定で温存」と意図が明記済み。かつ Tank という blockType もv8マスタに無い。整理不要。

## 未検証（監査の主張を鵜呑みにしないこと）

- A表の個別uGUIクラス名の網羅性（ChallengeListView / CraftTreeViewManager / Tutorial系等の存在は既知構造と整合するが個別未確認）
- 研究ノードの UIScale 未反映（低影響のため未検証）
- クラフト不可時のカーソルツールチップ文言（webにツールチップ基盤自体が無いのはほぼ確実だが個別未確認）

## 台帳一本化の注意

「C節はほぼ全てTODO.md未記載」は不正確。ブロック右クリ/Shift/ダブルクリック・SubInventoryへのShift移動・
13種ブロックビュー・blockInventory e2e拡充は `docs/webui/2026-07-06-all-code-review-progress.md` の
「実装漏れの徹底洗い出し」種リストに記録済み。台帳が TODO.md と進捗ドキュメントに二重化しているので、
TODO.md へ反映する際は種リスト側の項目も統合し、進捗ドキュメント側には「TODO.mdへ移設済み」と残すこと。

## 実用インパクト順（訂正反映後・監査の提案を修正）

1. ブロックスロットの操作パリティ（Shift一括移動・右クリック系・ダブルクリック収集）— チェスト/機械の日常操作
2. ElectricToGearGenerator（モード選択）＋ ギア伝達系の blockType キー登録（Shaft / Gear / GearChainPole）
3. 列車PF（モードトグル）・電柱ネットワーク情報
4. プレイヤーインベントリのドラッグ配分系（スプリットドラッグ・右ドラッグ連続配置）
5. クラフト長押し/可能数バッジ、機械の分間生産数表示
6. 大物画面（チャレンジ → ポーズ → 設置/破壊 → 列車HUD → スキット/チュートリアル）
   ※BaseCamp はマスタに実体が出るまで後回しで良い

## 検証に使った主な証拠

- web レジストリ: `moorestech_web/webui/src/features/blockInventory/blockLogic.ts`（blockComponents の9キー）
- v8 実マスタ blockType 分布: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
  （BeltConveyor 16 / GearBeltConveyor 12 / ElectricMachine 7 / Shaft 6 / GearMachine 5 / Chest 4 / Gear 3 / FluidPipe 3 /
   ElectricPole 3 / GearMiner 2 / GearChainPole 2 / FuelGearGenerator 2 / ElectricMiner 2 / Block 2 / 残り各1。
   SimpleGearGenerator は登録済みだがマスタ0個、Tank/BaseCamp/GearEnergyTransformer という blockType は不存在）
- ギアUI配線: 同 blocks.json 内で `Vanilla/UI/Block/GearEnergyTransformerUI` を指す5ブロック
- インベントリ操作: `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`
  （onLeftDown / onRightDown(split・1個置き) / onDoubleClick(collect) / directMove(main↔hotbar限定) / onHotbarWheel(±1)）
- クラフト表示: `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx`（不足素材 opacity 0.4）
- UIState: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateEnum.cs`（11値）と
  `moorestech_web/webui/src/app/uiScreenRouting.ts`（3+GameScreen）
