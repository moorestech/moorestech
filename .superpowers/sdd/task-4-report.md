# Task 4（＋Task 5）レポート: 接続消費のconnectToolマスタ駆動・複数素材化

## Status: DONE

main裁定（選択肢C）によりTask 4（サーバー）とTask 5（クライアント）を一括担当。`uloop compile` エラー0（client込み）、対象テスト `ElectricWire|Rail|GearChain|ConnectTool` **175/175 PASS**。

## コミット（範囲: 46651d1 → 2e173cc）
- `46651d1` wip: サーバー実装＋クライアント一部（未コンパイル中間チェックポイント）
- `f865c54` feat: 接続消費をconnectToolマスタ駆動の複数素材へ統一（サーバー＋クライアント）
- `0498367` test: 接続系テストをconnectTool複数素材へ追従（rail橋脚除く）
- `2e173cc` test: rail橋脚・電線自動接続・チェーン評価器テストをGREEN化

## 実装概要（サーバー）
- 新規: `Core.Master/ConnectToolMaterialCost.cs`（共有素材コスト構造体）、`Util/ConnectTool/`（`ConnectToolCostCalculator`=units×count算出, `ConnectToolMaterialConsumer`=検証/消費/返却, `ConnectToolSelector`=解放判定・ToolType別SortPriority昇順選択）
- ConnectionCost複数素材化: `ElectricWireConnectionCost`/`GearChainConnectionCost` を `IReadOnlyList<ConnectToolMaterialCost> Materials`（＋`HasMaterials`/`TotalCount`/`Empty`）へ。`IElectricWireConnector`/`IGearChainPole` の型はそのまま（構造体内部のみ変更）
- 永続化追従: `ElectricWireSaveDataJsonObject`/`GearChainPoleSaveDataJsonObject` を各接続が素材リスト（`ConnectToolMaterialSaveJsonObject`=ItemGuid保存）を持つ形へ。ロード時 `GetItemId(guid)` 解決。GetRefundItemsも複数素材展開
- 6プロトコルRequestの素材指定を `Guid ConnectToolGuid` に統一（電線/チェーン/レール接続編集・電線/チェーン延長・レール橋脚）
- 消費一般化: `ElectricWirePlacementEvaluator`/`GearChainPlacementEvaluator`/`RailConnectionEditProtocol.EvaluatePlacement` が connectToolGuid から複数素材コストを算出。延長系の建設コスト予約（同一素材の上乗せ判定）は温存
- 未解放拒否: 6経路すべてで `ConnectToolUnlockStateInfos[guid].IsUnlocked` を確認（延長系は設置前ガード）
- 電線自動接続 `ElectricWireAutoConnectService`: 解放済みelectricWire connectToolをSortPriority昇順で選択・複数素材消費。**未解放時は配線せず設置のみ許可**（設置自体はブロックしない）
- RailGraphの `RailTypeGuid` スロットにconnectToolGuidを格納（構造不変）。`RailConnectionEditProtocol.cs:83` の残存Debug.Log削除

## 実装概要（クライアント＝元Task 5）
- `ConnectToolPlacementTarget` を `Guid ConnectToolGuid` 保持へ。`BuildMenuEntryCatalog` を解放済みconnectTool単位表示（SortPriority順・未解放非掲載・アイコンは先頭requiredItem）
- `PlaceSystemSelector` はマスタのToolTypeで振り分け。各PlaceSystem/PreviewCalculator/RequestSender/VanillaApi をguid伝搬へ（電線/レール/チェーン）
- `ConnectToolCatalog` に ToolType↔enum写像・`ResolveDefaultConnectToolGuid`（ブロック設置延長時の種別解決）追加
- `PlacementTargetPickService`（吸取）・WebUI（`BuildMenuEntryDtoFactory`/`PlacementModeTopic`）をguid駆動へ
- ドラッグ設置プレビュー `ElectricWireAutoConnectPreview`＋VirtualInventory を複数素材モデルへ改修

## TDD Evidence
- RED: connectToolGuid化・複数素材化でシグネチャ/構造が変わり、全接続系テストがCS/実行時失敗（ItemId→Guid・`.Count`/`.ItemId`廃止・未解放拒否・MaxStack超過）
- GREEN: シグネチャ追従＋解放セットアップ＋期待消費数(units×count)更新で 175/175 PASS
- 新規テスト: 未解放connectTool拒否（電線接続・レール橋脚）、複数素材の距離比例消費（rail: 補強棒材12×units＋鉄板5×units＋橋脚コスト鉄板2の合算）、片素材不足での失敗＋ロールバック

## 実装分担
コード記述は多くを自分で直接実装。サーバーテスト14ファイルのシグネチャ追従＋新規テストはcodex(gpt-5.6-sol)へ委譲、自分がレビュー＋実行検証。失敗した6テスト（未解放セットアップ漏れ・期待値・MaxStack超過）は自分で修正。

## セルフレビュー・懸念
1. **auto-connectの解放ゲート（設計判断・要確認）**: ブロック設置時の電線自動接続は解放済みelectricWire connectToolが必要。未解放なら配線せず設置のみ（設置は失敗させない）。「解放済みentryのSortPriority最小を選択」の指示に沿うが、旧挙動（アイテム所持のみで配線）からの挙動変化。裁定が必要なら連絡を。
2. **クライアント既存auto-selector残置**: `ElectricWireItemAutoSelector`/`GearChainPoleItemFinder`/`TrainRailItemAutoSelector` は未使用化したが旧配列参照で残存（Task 6のスキーマ削除で露出・撤去予定）。
3. **プレビューの解放非考慮**: `ResolveDefaultConnectToolGuid`・`ElectricWireAutoConnectPreview` はSortPriority最小を解放状態非依存で選ぶ（サーバーが最終権威。プレビューは近似）。多connectTool/種別のUX厳密化は以降タスクで。
4. **wip中間コミット(46651d1)は非コンパイル**: bisect時の注意。必要なら `f865c54` へsquash可。
5. マスタ由来値(LengthPerUnit等)はセーブせず、揮発ItemIdも保存していない（ItemGuid保存・ロード時解決）。

---

## レビュー指摘修正（Fix 1 + Fix 2）2026-07-19

### Fix 1（CLIENT/Important）: connectToolのimagePathアイコンを実装
死にデータだった`connectTools.yml`の`imagePath`を実アイコン経路に接続。ItemTextureLoader/ItemImageContainer/ItemIconEndpointと同型で3層を新設し、素材アイコンフォールバックを撤去。
- 新規 `ConnectToolTextureLoader`（Client.Mod/Texture）: connectToolのimagePathからTexture2D→ItemViewDataを生成。imagePath空なら`assets/connectTool/{name}.png`へフォールバック。画像欠落時は`GetExtractedZipTexture.Get`がnull→`ToSprite`がnull安全のため落ちない。
- 新規 `ConnectToolImageContainer`（Client.Game/InGame/Context）: connectToolGuid→ItemViewData辞書。`ClientContext.ConnectToolImageContainer`で保持。ModAssetLoaderの並列ロードに`LoadConnectToolAssets`を追加。
- 新規 `ConnectToolIconEndpoint`（WebUI, `/api/connect-tool-icons/{guid}.png`）: TrainCarIconEndpointと同型。WebUiEndpointsにルート登録。
- `BuildMenuEntryCatalog`（uGUI）と`BuildMenuEntryDtoFactory.CreateIconUrl`（WebUI）をconnectToolGuid由来アイコンへ変更。
- 死んだ`ConnectToolCatalog.SelectIconItemGuid(ConnectToolMasterElement)`（素材アイコンフォールバック）を撤去。ConnectToolType overload・auto-selector群はTask 6へ委譲（スコープ非拡大）。

### Fix 2（SERVER/Important）: rail 2経路のGuid.Empty無料接続を是正
**調査結果**: `RailConnectionEditProtocol`/`RailConnectWithPlacePierProtocol`にEmptyを送る正当な内部呼び出し元は存在しない。station内部レール等は`RailComponentUtility.ConnectRailComponent`で直接生成し当プロトコルを通らない。クライアントは`target.ConnectToolGuid`（実マスタGuid）のみ送る。→escalate不要と判断し対称化。
- 両rail経路のガードを`if (!ConnectToolSelector.IsUnlocked(guid))`へ変更（`!= Guid.Empty &&`を除去）。Empty=`NotUnlocked`で拒否、electricWire/gearChainの4経路と対称。
- 拒否テスト追加: `RailConnectionEditProtocolTest.connectToolGuidがEmptyの接続要求は無料設置扱いされず拒否される`（NotUnlocked＋無消費）、`RailConnectWithPlacePierProtocolTest.connectToolGuidがEmptyの接続要求は無料設置扱いされず失敗応答を返す`（橋脚非設置＋無消費）。

### 検証
- `uloop compile --project-path ./moorestech_client`: Errors 0 / Warnings 0
- `uloop run-tests ... "RailConnectionEditProtocolTest|RailConnectWithPlacePierProtocolTest|ConnectToolCatalogTest"`: 15/15 passed
- `uloop run-tests ... "ElectricWire|Rail|GearChain|ConnectTool|BuildMenu"`: 179/179 passed

### 残懸念
- `RailConnectWithPlacePierProtocol`の`if (ConnectToolGuid != Guid.Empty)`ブロックとEvaluatePlacementのEmpty=無コスト分岐はEmpty拒否後に到達不能な残存コード。害はないためスコープ非拡大で残置（クライアントプレビューは実Guidのみ渡すため実挙動不変）。
