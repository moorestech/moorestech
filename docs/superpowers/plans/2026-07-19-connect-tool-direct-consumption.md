# 接続ツール直接材料消費化（connectToolsマスタ化）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 電線・レール・歯車チェーンの専用アイテムを廃止し、guid付きconnectToolsマスタ（個別解放・個別アイコン・複数素材の距離比例消費）に置き換える。

**Architecture:** 新トップレベルマスタ`connectTools`を追加し、解放はunlockBlockと完全同型の新ドメイン（Holder＋GameAction＋既存3点セット同期の拡張）、消費は既存`ConstructionCostService`の一般化で実現。旧3配列（electricWireItems/gearChainItems/railItems）はスキーマ削除→コンパイルエラー駆動で全置換。

**Tech Stack:** Unity C# / Mooresmaster SourceGenerator（YAMLスキーマ→自動生成） / UniRx / MessagePackプロトコル / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-19-connect-tool-direct-consumption-design.md`（必読。決定事項・実データ値はここが正）

## Global Constraints

- 作業ディレクトリは `/Users/katsumi/moorestech-worktrees/tree1`（必ず最初に`pwd`確認）
- コンパイル: `uloop compile --project-path /Users/katsumi/moorestech-worktrees/tree1/moorestech_client`（.cs変更後は必ず実行。サーバーコードもクライアントプロジェクトに含まれる）
- テスト: `uloop run-tests --project-path /Users/katsumi/moorestech-worktrees/tree1/moorestech_client --filter-type regex --filter-value "<正規表現>"`
- **Unityは1インスタンスのみ（ポート11564固定）**。タスクは直列実行し、コンパイル/テストの並行実行禁止
- スキーマ(yml)編集はedit-schemaスキル（`.claude/skills/edit-schema/`）の手順に従う。Mooresmaster.Model.*は自動生成のため手書き禁止
- partial禁止・try-catch原則禁止・1ファイル200行以下・イベントはUniRx・デフォルト引数禁止
- 単純getter/setterプロパティ禁止（Setは`public void SetHoge`）
- コメントは日本語→英語の2行セット（約3〜10行ごと）
- 後方互換のためのoptional/フォールバック禁止。全JSON一括更新が正規手順
- 各タスク末で必ずコミット（git worktree運用のため作業消失防止）
- 本番マスタデータは `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/`（別リポジトリ。コミットもそちらで）
- テスト用マスタデータ: `moorestech_server/Assets/Scripts/Tests.Module.TestMod/`配下（ForUnitTestのJSON群。実配置はTask 1で確認）

## 配置と前例（spec-architecture-review済み）

| 新規物 | 配置先 | 前例（同役割） |
|---|---|---|
| ConnectToolMaster（生ロード・保持のみ） | Core.Master | BlockMaster / ItemMaster |
| ConnectToolUnlockStateHolder | Game.UnlockState/Holders | BlockUnlockStateHolder.cs（同ディレクトリ） |
| unlockConnectTool分岐 | Game.Action/GameActionExecutor.cs | 同ファイルのunlockBlock分岐(:145-152) |
| 解放同期 | UnlockedEventPacket＋InitialHandshake＋ClientGameUnlockStateDatastore | block/trainCarドメインの既存3点セット |
| 消費計算 | Server.Protocol/.../Util/ | ConstructionCostService / ElectricWireExtendService |
| ビルドメニュー枠 | BuildMenuEntryCatalog | 既存ブロックエントリ（IsBlockUnlockedフィルタ） |

新規パターン（裁定済み）: 接続ツールの解放を導出でなく個別`unlockConnectTool`にするのはユーザー裁定（レールv2独立解放のため）。

## 機能パリティ死活表（全操作が計画後も生存すること）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 電線の接続/切断/延長(空きに電柱自動設置) | 生存 | プロトコル引数の型統一のみ、機構不変 |
| レールの接続/切断(返却)/橋脚自動設置 | 生存 | RailTypeGuid=connectToolGuidに差し替えのみ |
| 歯車チェーン接続/ポール延長 | 生存 | 同上 |
| ミドルクリック吸取(電線) | 生存 | Task 5でconnectToolエントリ解決に追従 |
| ブロック設置時の電線自動接続+消費 | 生存 | Task 4でsortPriority最小の解放済みentryを選択 |
| 撤去時の素材返却 | 生存 | ConnectionCostの複数素材化で返却も追従 |
| 専用アイテムのクラフト | **廃止（仕様）** | ユーザー裁定・スペック決定事項5 |

---

### Task 1: connectToolsスキーマ・マスタ・gameAction enum追加（加算のみ・コンパイル維持）

**Files:**
- Create: `VanillaSchema/connectTools.yml`
- Modify: `VanillaSchema/ref/gameAction.yml`（enum optionsに`unlockConnectTool`＋switch caseを追加）
- Create: `moorestech_server/Assets/Scripts/Core.Master/ConnectToolMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`（静的プロパティ＋Load追加。既存Master群と同形）
- Modify: テスト用マスタJSON群のディレクトリに`connectTools.json`を追加（実配置はBlockMasterのテストJSONと同じ場所を`Grep: "blocks.json" Tests`で特定して倣う）

**Interfaces:**
- Produces: `MasterHolder.ConnectToolMaster`、生成型`Mooresmaster.Model.ConnectToolsModule.ConnectToolMasterElement { Guid ConnectToolGuid; string Name; string ToolType("electricWire"|"rail"|"gearChain"); string ImagePath; int SortPriority; bool InitialUnlocked; float LengthPerUnit; RequiredItems[] }`（正確な生成名はSourceGenerator出力に従い、以降のタスクはそれを使う）
- Produces: `ConnectToolMaster.GetElementOrNull(Guid connectToolGuid)` / `IReadOnlyList<ConnectToolMasterElement> All`

- [ ] **Step 1:** edit-schemaスキル本文を読む
- [ ] **Step 2:** `connectTools.yml`作成。スペックの表の8プロパティ。`requiredItems`はblocks.ymlの`requiredItems`(ConstructionRequiredItemElement, blocks.yml:167-181)と同構造（itemGuid: foreignKey→items, count）。`toolType`はenum。`initialUnlocked`はblocks.yml:195と同形
- [ ] **Step 3:** `gameAction.yml`のoptionsに`unlockConnectTool`を追加し、switch casesに`unlockConnectToolGuids`(uuid array, foreignKey→connectTools)のcaseを追加（unlockBlockのcaseを雛形にする）
- [ ] **Step 4:** SourceGenerator再生成をトリガー（edit-schemaスキル記載の方法）
- [ ] **Step 5:** `ConnectToolMaster.cs`をBlockMasterと同形で作成（JSONロード・Guid辞書化のみ。解釈ロジック禁止）。MasterHolderに登録
- [ ] **Step 6:** テスト用マスタに`connectTools.json`（3エントリ・スペックの表の値。テスト用itemGuidは既存テストmodのアイテムを使用）を追加
- [ ] **Step 7:** `uloop compile` → エラー0を確認
- [ ] **Step 8:** commit（`feat: connectToolsマスタとunlockConnectToolアクションのスキーマ追加`）

### Task 2: 解放ドメイン（Holder＋GameActionExecutor＋セーブロード）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/ConnectToolUnlockStateHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/States/ConnectToolUnlockStateInfo.cs`（＋JsonObject。BlockUnlockStateInfoと同形）
- Modify: `Game.UnlockState/GameUnlockStateDatastoreController.cs`＋`IGameUnlockStateDatastoreController.cs`（7番目のドメイン追加）
- Modify: `Game.Action/GameActionExecutor.cs`（unlockConnectTool分岐。unlockBlock分岐:145-152と同形）
- Test: 既存のGameUnlockState系テストの隣に`ConnectToolUnlockStateTest`（creating-server-testsスキル参照）

**Interfaces:**
- Consumes: `MasterHolder.ConnectToolMaster`（Task 1）
- Produces: `ConnectToolUnlockStateHolder { IObservable<Guid> OnUnlock; IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> Infos; void Unlock(Guid); Load/GetSaveJsonObject }`（BlockUnlockStateHolder.cs:11-58と同一シグネチャ形）
- Produces: `IGameUnlockStateDatastoreController.ConnectToolUnlockStateHolder`（後続タスクの解放判定用）

- [ ] **Step 1:** 失敗するテストを書く（unlockConnectToolアクション実行→Holder解放状態true＋OnUnlock発火、initialUnlocked=falseエントリの初期状態false、セーブ→ロード往復で状態維持）
- [ ] **Step 2:** `uloop run-tests --filter-value "ConnectToolUnlockState"` → FAIL確認
- [ ] **Step 3:** Holder/Info/Controller/Executor分岐を実装（BlockUnlockStateHolder.cs:11-58を写経しGuid元をConnectToolMasterに）。セーブJSONへの組み込みはblockのGetSaveJsonObject呼び出し箇所をGrepし同列に追加
- [ ] **Step 4:** テストPASS確認・`uloop compile`エラー0
- [ ] **Step 5:** commit（`feat: connectTool解放ドメインを追加`）

### Task 3: 解放のクライアント同期（既存3点セット拡張・新プロトコル禁止）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs`（UnlockEventTypeに`ConnectTool`追加・Block追加時の差分に倣う）
- Modify: InitialHandshakeResponseのUnlockState組み立て箇所（`Grep: "UnlockState" Server.Protocol`で特定、block/trainCarと同列に追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UnlockState/ClientGameUnlockStateDatastore.cs`（初期流し込み:73-91・Subscribe:93・更新:96-133にconnectTool分を追加）
- Test: 既存のUnlocked系PacketTestの隣にconnectTool分

**Interfaces:**
- Consumes: Task 2のHolder
- Produces: クライアント側 `ClientGameUnlockStateDatastore.IsConnectToolUnlocked(Guid)`＋解放変化のIObservable（既存block分と同形の公開名に合わせる）

- [ ] **Step 1:** 失敗するPacketTest（unlockConnectTool実行→イベントパケット受信、ハンドシェイクに解放状態が含まれる）
- [ ] **Step 2:** FAIL確認 → 実装（3箇所ともblockドメインの実装行を隣にコピーしてconnectToolへ置換） → PASS
- [ ] **Step 3:** `uloop compile`エラー0 → commit（`feat: connectTool解放をクライアントへ同期`）

### Task 4: サーバー消費ロジックのconnectToolGuid統一・複数素材化

**Files:**
- Modify: `Server.Protocol/PacketResponse/`の6プロトコル: `ElectricWireConnectionEditProtocol.cs` / `ElectricWireExtendProtocol.cs` / `RailConnectionEditProtocol.cs` / `RailConnectWithPlacePierProtocol.cs` / `GearChainConnectionEditProtocol.cs` / `GearChainPoleExtendProtocol.cs`（素材指定引数を`Guid ConnectToolGuid`に統一）
- Modify: `Server.Protocol/.../Util/ElectricWire/ElectricWireExtendService.cs`・`ElectricWirePlacementEvaluator.cs`・`Util/GearChain/GearChainSystemUtil.cs`（消費計算を`ceil(距離/LengthPerUnit)×RequiredItems各count`へ）
- Modify: `Game.Block.Interface/Component/GearChainConnectionCost.cs`・電線側`ElectricWireConnectionCost`（`(ItemId,int)`単体→`IReadOnlyList<(ItemId,int)>`化。GetRefundItems・セーブJSON追従）
- Modify: `Game.Block/Blocks/ElectricWire/ElectricWireConnectorComponent.cs`・`GearChainPole/GearChainPoleComponent.cs`（保存形式追従）
- Modify: 電線自動接続サービス（`Grep: ElectricWireAutoConnectService`）: 解放済みelectricWireエントリのうちSortPriority最小を選択して消費
- Test: 既存の電線/レール/チェーン接続テストの追従＋新規（複数素材消費・不足失敗・返却・未解放拒否）

**Interfaces:**
- Consumes: `MasterHolder.ConnectToolMaster`、`IGameUnlockStateDatastoreController.ConnectToolUnlockStateHolder`
- Produces: 各プロトコルRequestの`[Key(n)] Guid ConnectToolGuid`（旧ItemId/ChainItemId/RailTypeGuidを置換）。RailGraphの`RailTypeGuid`フィールドにはconnectToolGuidを格納（構造不変）

- [ ] **Step 1:** 未解放connectToolでの接続要求が拒否される失敗テストを書く → FAIL確認
- [ ] **Step 2:** 6プロトコルの引数統一＋消費計算一般化＋解放ガードを実装。延長系の建設コスト予約ロジック（ElectricWireExtendService.cs:102-110等）は温存
- [ ] **Step 3:** ConnectionCost複数素材化＋返却・セーブ追従。`RailConnectionEditProtocol.cs:83`の残存Debug.Logを削除。**永続化規約**: componentStatesへの保存は揮発int(ItemId)禁止・ItemGuidのJSONで保存し、ロード時に`MasterHolder.ItemMaster.GetItemId(guid)`で解決（正実装: ItemStackSaveJsonObject）。マスタ由来値(LengthPerUnit等)はセーブしない
- [ ] **Step 4:** 既存接続系テスト（regex `"ElectricWire|Rail|GearChain"`）を追従修正しPASS
- [ ] **Step 5:** `uloop compile`エラー0 → commit（`feat: 接続消費をconnectToolマスタ駆動の複数素材へ統一`）

### Task 5: クライアント（ビルドメニュー・PlaceSystem・アイコン・WebUI）

**Files:**
- Modify: `Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs:51-58`（3種固定ループ→解放済みconnectToolsエントリごとに1枠・SortPriority順・未解放非掲載）
- Modify: `Client.Game/InGame/BlockSystem/PlaceSystem/Targets/ConnectToolPlacementTarget.cs`（`ConnectToolType`→`Guid ConnectToolGuid`保持。toolTypeはマスタから解決）
- Modify: `PlaceSystemSelector.cs:80-86`＋各PlaceSystem（ElectricWireConnect/TrainRailConnect/GearChainPoleConnect）でguidをプロトコルへ伝搬
- Modify: `UIState/State/PlacementPick/PlacementTargetPickService.cs:37-46`（吸取→対応connectToolエントリ解決）
- Modify: アイコン: `ImagePath`からのロード（itemsのimagePathロード経路をGrepで特定し同経路を使用。ClientContextのImageContainer前例に倣う）
- Modify: WebUI: `Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`・`BuildMenuEntryDtoFactory.cs`・`Actions/BuildMenuActions.cs`追従
- 確認: レール描画のRailTypeGuid参照箇所（`Grep: RailTypeGuid client`）が新guidで動くこと

**Interfaces:**
- Consumes: `ClientGameUnlockStateDatastore.IsConnectToolUnlocked(Guid)`（Task 3）、各プロトコルの`ConnectToolGuid`（Task 4）
- Produces: `ConnectToolPlacementTarget(Guid connectToolGuid)`

- [ ] **Step 1:** 実装（未解放エントリ非掲載はブロックの`IsBlockUnlocked`フィルタ:24-44と同形）
- [ ] **Step 2:** `uloop compile`エラー0
- [ ] **Step 3:** commit（`feat: ビルドメニューをconnectToolsマスタ駆動化`）

### Task 6: 旧機構の削除（コンパイルエラー駆動）

**Files:**
- Modify: `VanillaSchema/blocks.yml`から`electricWireItems`(:1087-1100)・`gearChainItems`(:1019-1032)を削除、`VanillaSchema/train.yml`から`railItems`(:93-106)を削除 → SourceGenerator再生成
- Delete/Modify: コンパイルエラーになった全参照を新機構へ置換 or 削除: `ConnectToolCatalog.cs`のSelectIconItemGuid/TryGetPlaceBlockのアイテム依存部・`ElectricWireItemAutoSelector.cs`・`GearChainPoleItemFinder.cs`・`TrainRailItemAutoSelector`（TrainRailConnectSystem.cs:68-70）ほかエラー箇所全部
- Modify: テスト用マスタJSONからも旧3配列を削除

**Interfaces:**
- Consumes: Task 1-5の全成果（旧配列参照が残っているとここで全部露出する）

- [ ] **Step 1:** スキーマから3配列削除 → 再生成 → `uloop compile`でエラー一覧を取得
- [ ] **Step 2:** エラーを1件ずつ新機構へ置換（残すべき挙動: 空き延長時のアンカーブロック自動選択=TryGetPlaceBlockのsortPriority最小ブロック選択は素材と無関係なので温存）
- [ ] **Step 3:** エラー0＋全接続系テストPASS（regex `"ElectricWire|Rail|GearChain|ConnectTool"`）
- [ ] **Step 4:** commit（`refactor: 接続専用アイテム機構を削除`）

### Task 7: 本番マスタデータ変更（moorestech_masterリポジトリ）

**Files:** `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/`
- Create: `connectTools.json`（3エントリ。guidは新規採番。素材guid: 銅のワイヤー/補強棒材・鉄板/鉄のワイヤーはitems.jsonから実guidを引く。値はスペックの表）
- Modify: `items.json`（電線5b6e76b8-・レール5be3a22c-・歯車チェーン8412fa32-の3件削除）
- Modify: `craftRecipes.json`（レシピb8a34c15-・019e1f0b-の2件削除）
- Modify: `machineRecipes.json`（歯車チェーン機械レシピ1件削除）
- Modify: `research.json`（「鉄道の時代」「鉄のチェーンポール」のunlockItemRecipeViewをunlockConnectToolへ置換、「加工装置」に電線のunlockConnectTool追加）
- Create: `imagePath`用アイコン画像3枚（既存アイテム画像アセットの電線/レール/歯車チェーン画像を流用しパス設定。無ければ暫定で既存の類似画像パス）

- [ ] **Step 1:** connectTools.json作成＋4ファイル修正（旧itemGuid参照が他に残っていないことを`grep -r <guid>`で全ファイル確認）
- [ ] **Step 2:** Unityでマスタロードが通ることを確認（`uloop get-logs --log-type Error`でMooresmasterLoaderException無し）
- [ ] **Step 3:** moorestech_master側でcommit

### Task 8: セーブ移行スクリプト（save_1）

**Files:**
- Create: `moorestech_master/design/`または`tools/`配下に移行スクリプト（moorestech-save-migrationスキルの手順・配置に従う）

対象変換（スペック§5）: ①インベントリ内 レール1→補強棒材12＋鉄板5 / 歯車チェーン1→鉄のワイヤー10 ②電線・チェーンのcomponentStates内返却コスト→新requiredItems形式 ③レールセグメントRailTypeGuid→新connectToolGuid

- [ ] **Step 1:** moorestech-save-migrationスキル本文を読む
- [ ] **Step 2:** バックアップ作成（`save_1.json.bak-connect-tool`）→ スクリプト実装・実行
- [ ] **Step 3:** Unity実ロード検証（エラーログ0・レール/電線/チェーンの既設接続が生存）
- [ ] **Step 4:** commit

### Task 9: 総合QA

- [ ] **Step 1:** 全体テスト実行（接続系＋UnlockState＋Packet系regex）で全PASS
- [ ] **Step 2:** `uloop compile`エラー0・警告増加なし
- [ ] **Step 3:** 機能パリティ死活表（本ファイル冒頭）を1行ずつ実コードで裏取り
- [ ] **Step 4:** moores-code-reviewスキルで最終レビュー1パス → 指摘修正 → commit
