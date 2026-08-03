# ホットバーの建築ショートカット化と装備スロットの新設

ホットバーを Satisfactory 型の「建築するもののショートカット」に変え、斧・石器といったツールは新設する装備スロットで使う。あわせて、ビルドメニューに並ぶもの（設置対象）の識別子を Guid 1本に統一する。

関連ADR: [0001](../adr/0001-placement-target-id-as-single-guid.md) / [0002](../adr/0002-hotbar-holds-placement-targets-not-items.md) / [0003](../adr/0003-equipment-slots-as-independent-inventory.md) / [0004](../adr/0004-server-authoritative-mining.md)
用語: [CONTEXT.md](../../CONTEXT.md)

## 現状（調査結果）

- 設置系はすでにホットバーから独立している。`BuildMenuView` → `IPlacementTarget` → `PlaceBlockState` の経路で、建設コストは `PlaceBlockProtocol` がメインインベントリ全体から消費する。
- ホットバーはメインインベントリ末尾9スロットの別名（`PlayerInventoryConst`）。選択アイテムの用途は「手持ち3Dモデル」と「採掘ツール判定」の2つだけ。
- 採掘はクライアント権威。`MapObjectMiningFocusState` がツールを照合し、`attackDamage` をサーバへ送る。サーバは無検証。
- ツールは 石の斧 / 石器 の2種のみ。`miningTools` は mapObject 側に damage / attackSpeed を持つ。アイテム側に「ツールである」情報は無い。
- `PlaceBlockState` はカーソル表示・カメラ回転オフ、`GameScreenState` はカーソルロック・回転オンで、建築モードと通常モードは操作系が別物。
- ホイールは通常モードのホットバー切替（`HotBarView` / web `HotbarPanel`）と、建築モードのBPコピー枠高さ（`BlueprintCopySystem`）にしか使われていない。
- スキル文書 `hotbar-driven-systems.md` が説明する `usePlaceItems`（手持ちアイテム駆動の設置システム）は**コードにもマスタにも既に存在しない**。文書が陳腐化している。
- ビルドメニューのエントリ組み立ては uGUI 用（`BuildMenuEntryCatalog`）と web 用（`WebBuildMenuEntryCatalog`）の2本が並存している。

## 設計

### 設置対象IDの統一

| 設置対象 | 設置対象ID |
|---|---|
| ブロック | ブロックGUID（マスタ） |
| 列車車両 | 車両GUID（マスタ） |
| 接続ツール | 接続ツールGUID（マスタ） |
| ビルドツール（BPコピー） | `buildMenu.yml` の `buildTools` に新設するGUID |
| ブループリント | 作成時に発行するGUID |

- Web契約の `entryType + entryKey` は廃止し、設置対象ID（Guid文字列）ひとつにする。表示・振る舞い用の種別 `kind` は残すが**識別子ではない**。実行時 `BlockId` は永続・通信に使わない。
- 設置対象カタログを共有アセンブリ `Game.PlacementTarget` に1本置く。マスタ由来のエントリは共有コードで列挙し、ブループリントの供給元だけ `IBlueprintCatalogSource` で差し替える（サーバ=`BlueprintDatastore` / クライアント=`ClientBlueprintLibrary`）。
- カタログは「ビルドメニューに並びうるもの」の集合であり、ベルトの坂ブロックのようにメニューに出ないものは含まない。アンロック状態はカタログの関心ではない。
- ブループリント名は表示名。削除・参照はGUID。同名を許容し `" (2)"` の連番付与は廃止。
- 設置の向きは設置対象の一部にしない。`BlockPlacementTarget.PickedDirection` は設置操作中の一時状態に留め、ホットバー割当には保存しない。

### ホットバー

- 9枠が設置対象IDへの参照を持つ。アイテムは持たない。
- 1〜9キーでその設置対象を持って建築モードへ即遷移。同キー再押下で通常モードへ戻る。**空枠を押した場合も建築モードを抜ける**。
- 割当の入口は2つ: 建築モード中（ビルドメニュー選択後・スポイト直後を含む）の数字キー長押し、および Web UI のビルドメニューからHUDへのドラッグ&ドロップ。
- 割当はプレイヤー単位でセーブに永続。プロトコルは1本にモード分岐（assign / clear / swap）。
- ロード時、設置対象カタログで解決できない割当は削除する。マスタに存在するが未解放のものは削除しない。
- 割当と選択中の枠は非MonoBehaviourのクライアントモデルが所有し、uGUI `HotBarView` / `HotBarItem` は削除する。

### 装備スロット

- `InventoryType.Equipment` を追加し、`PlayerInventoryData` に独立した `IOpenableInventory` を持たせる。既存のアイテム移動・整理プロトコルで操作する。
- スロット数はマスタ定義の固定値（`items.yml` の `equipmentSlotCount`、初期値3）。受入制限は持たず、メインと同じ普通のスロットとして扱う（2026-07-29 裁定で「ツール限定」「1枠1個」の両方を撤回）。
- `items.yml` のトップレベルに `tools` 配列を新設する。採掘性能（damage / attackSpeed）は従来どおり mapObject 側。受入制限の撤回により `tools` の利用先は現状無く、デッドデータとして残置している。
- 採掘可否は「選択中アイテムが mapObject の `miningTools` に含まれるか」だけで決まるため、装備側に受入制限は要らない。
- 通常モードのホイールで循環選択（空も含む）。HUD右端に3枠常設し、選択中をハイライト。
- 手持ち3Dモデルと採掘アニメーションの参照元を装備スロットへ移す。

### 採掘のサーバ権威化

- クライアントは「instanceId を掘った」だけ送る。`attackDamage` は送らない。
- サーバが選択中の装備ツール × mapObject の `miningTools` からダメージを算出。ツール未装備・非対応ツールなら無視。
- 前回打撃から `attackSpeed` 秒未満の打撃は無視する（プレイヤー×mapObject単位のクールダウン）。
- 選択中の装備インデックスはサーバ保持＋セーブ保存。
- `MapObjectSuperMine` デバッグはサーバ側 `DebugParameters` で処理。
- PickUp 系（小石など）はツール不要のまま。ツール未装備時の「このアイテムが必要です」表示は維持。

### Web

- 装備は `inventory` トピックへ（`hotbarSlots` / `selectedHotbar` を置換）。ホットバーは新トピック `local_player.hotbar`。
- アクションは `inventory.select_hotbar` を廃止し、ホットバートピック側に選択・割当（select / assign / clear / swap）を、装備側に選択（`inventory.select_equipment`）を置く。
- HUDは「左：ホットバー9枠」「右端：装備3枠」。

## 実装計画の分割

逐次依存はあるが、それぞれ単体で出荷・テスト可能な3本に分ける。B が C より先なのは、ホットバーからアイテムが消えると採掘ツールを選べなくなるためと、ホイールの奪い合いを解消するため。

| plan | 内容 |
|---|---|
| [A](../superpowers/plans/2026-07-28-placement-target-id-unification.md) | 設置対象IDの統一。ゲーム挙動は不変 |
| [B](../superpowers/plans/2026-07-28-equipment-slot-and-server-authoritative-mining.md) | 装備スロット＋採掘サーバ権威化。ホイールが装備切替へ移り、ホットバーはキーのみになる |
| [C](../superpowers/plans/2026-07-28-hotbar-build-shortcut.md) | ホットバーの建築ショートカット化 |

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `buildTools` 配列 | `VanillaSchema/buildMenu.yml` のトップレベル | 同ファイルの `connectTools`（新yamlを作らず既存へ統合） |
| `BuildToolMaster` / `ToolMaster` | `moorestech_server/Assets/Scripts/Core.Master/` | `ConnectToolMaster.cs`（既存yamlの一部配列だけを読むラッパーMaster） |
| `PlacementTargetCatalog` 他 | 新アセンブリ `Game.PlacementTarget` | `Game.Blueprint` / `Game.UnlockState`（小さな単一責務asmdef） |
| `EquipmentInventoryData` | `Game.PlayerInventory/ItemManaged/` | `GrabInventoryData.cs`（`OpenableInventoryItemDataStoreService` 委譲の薄いIOpenableInventory） |
| `EquipmentInventoryIdentifierResolver` | `Server.Protocol/.../InventoryService/Resolver/` | `GrabInventoryIdentifierResolver.cs` |
| ホットバー割当プロトコル | `Server.Protocol/PacketResponse/` | `BlueprintProtocol.cs`（1プロトコル内のOperation enum分岐） |
| 装備・ホットバーの購読同期 | `Client.WebUiHost/Game/Topics/` | `InventoryTopic.cs`（PostLateUpdateでまとめてpublish） |
| 装備選択の変化通知 | UniRx `Subject<T>` | `ConnectToolUnlockStateHolder`（`Subject`+`IObservable`） |

新機構は1つ: 設置対象カタログ（Guid→設置対象の解決。既存は種別ごとの個別解決）。spec で新規パターンとして明示する。

（当初は `IItemAcceptanceInventory`（受入制限）も新機構として数えていたが、2026-07-29 の裁定で撤去したため新機構ではなくなった。装備インベントリの前例は `GrabInventoryData` である）

## 機能パリティ死活表

計画が触れる機構（ホットバー・プレイヤーインベントリ移動・採掘・ビルドメニュー選択）にぶら下がる全操作:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| ホットバー 1〜9 キー | 生きる（意味が変わる） | plan C で建築ショートカットへ |
| ホットバーのホイール切替 | **死ぬ** | ユーザー裁定「切替は通常モードのホイールで装備循環」。ホットバーはキーのみ |
| ホットバー ⇄ メインの Shift 配分 | **死ぬ** | ユーザー裁定「旧ホットバーの特別扱いは完全撤廃」。hotbar エリア自体が消滅 |
| 拾得時のホットバー優先挿入 | **死ぬ** | 同上 |
| インベントリソートのホットバー除外 | **死ぬ** | 同上 |
| BP名の重複連番 `" (2)"` | **死ぬ** | ユーザー裁定「同名BP許容・識別はGUID」 |
| 手持ち3Dモデル | 生きる（出所が装備へ） | plan B |
| 採掘（ツール／PickUp とも） | 生きる | plan B。ツールの出所が装備スロットへ |
| デバッグ高速採掘 | 生きる（サーバ側へ移設） | plan B |
| ミドルクリックのスポイト | 生きる | 変更なし |
| ビルドメニュー選択・BP削除 | 生きる（キーがGUIDへ） | plan A |
| チュートリアルのアイテムハイライト | 生きる（解決先が装備HUD／インベントリへ） | plan B の検証項目 |

死ぬ操作はすべてユーザー裁定済み。裁定なしで落とす操作はない。

## 検証・QA観点

- ホットバー割当の検証はアンロック状態のロード順に依存しないこと（未解放を誤って削除しない）。
- ベルトの坂などビルドメニューに出ないブロックの設置対象IDが割当に入り込まないこと。
- 装備スロットへスタックを移動したとき、1個だけ入り残りがメインへ戻ること。
- 装備中のツールをインベントリへ戻した直後の打撃がサーバで拒否されること。
- 連打連送でクールダウンを超えて採掘できないこと。
- チュートリアルの `itemViewHighLight`（石の斧・石器）のDOMアンカー解決先が装備HUD／インベントリへ移ること。
- 旧セーブのマイグレーションは、ブループリントGUIDの発行を除いて不要（末尾9スロットは通常スロットになるだけ、装備インベントリは空で開始）。それが実際に成り立つことを確認する。

## 判断記録（ADR）

- **ホットバーは設置対象IDの参照のみを持ちアイテムを保持しない**（出所: ユーザー裁定 AskUserQuestion 2026-07-27。詳細 ADR-0002）
- **数字キーで即建築モード・同キーで解除・空枠は建築モードを抜ける**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **ホットバー割当はサーバ永続（save の player 単位）**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **割当の入口はキー長押しと Web D&D の両方**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **解決不能な割当はロード時に削除する**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）。マスタに存在するが未解放のものは保持（出所: agent前提（拒否権つき））
- **メインインベントリ末尾9スロットの特別扱いを完全撤廃**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **uGUI `HotBarView` を削除し非MonoBehaviourモデルへ移す**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **装備スロットは独立した `IOpenableInventory`（`InventoryType.Equipment`）**（出所: ユーザー裁定 AskUserQuestion 2026-07-27。詳細 ADR-0003）。メイン末尾レンジ案は、メインが `playerInventorySlotLevels` で可変長のためインデックスがサイズ依存になる（旧ホットバーと同じ罠）ので不採用
- **スロット数はマスタ定義の固定値・空も循環に含む**（出所: ユーザー裁定「スロット数はマスタ。個数は固定」2026-07-27）。~~1枠1個~~ は 2026-07-29 の受入制限撤去で撤回（下記参照）
- **切替は通常モードのホイール循環・HUD右端に3枠**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **`items.yml` トップレベルに `tools` 配列を新設し、装備可能アイテムを列挙する**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）。採掘性能は mapObject 側に残す
- **採掘はサーバ権威。打撃イベント＋サーバ側 `attackSpeed` クールダウン**（出所: ユーザー裁定 AskUserQuestion 2026-07-27。詳細 ADR-0004）。距離検証はしない（座標がクライアント申告値のため防御力が乏しい）
- **設置対象IDを Guid 1本に統一する**（出所: ユーザー裁定「統合的にビルドメニューにおくもののキー、ID的なものの仕組みを策定したほうが良い」2026-07-27。詳細 ADR-0001）
- **BPコピーツールは `buildMenu.yml` の `buildTools` でマスタ化する**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **設置の向きは割当に保存しない**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **カタログは共有アセンブリに1本・BP供給だけ差し替え**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- **BP名は表示名へ格下げし同名を許容、`" (2)"` 連番は廃止**（出所: agent前提（拒否権つき）2026-07-27。ユーザーに提示済みで異議なし）
- **永続キーは BlockGuid（実行時 BlockId は不使用）**（出所: agent前提（拒否権つき）2026-07-27）
- **Webトピックは装備＝`inventory`、ホットバー＝新トピック**（出所: ユーザー裁定 AskUserQuestion 2026-07-27）
- ~~**`IItemAcceptanceInventory` を `Core.Inventory` に新設して移動サービスが尊重する**（出所: agent前提（拒否権つき））。マシンのモジュールスロットは投入無制限で前例にならないため新機構となる~~ → **撤回**（下記参照）
- **受入制限そのものを撤去し、装備スロットは普通の `IOpenableInventory` にする**（出所: ユーザー裁定 AskUserQuestion 2026-07-29「一旦受け入れ制限自体をやめる。現状は実害が無い。（別に非ツールアイテムを装備スロットに設定したところでなにかあるわけでもない）。無用な複雑性を課すオーバーエンジニアリングになるので、計画を変更する」）。「ツール限定」「1枠1個」の**両方**を撤回し、`IItemAcceptanceInventory` と `UnrestrictedItemAcceptance` および強制経路（Insert/Replace/移動/整理）を全て削除する。採掘可否の判定は「選択中アイテムが mapObject の `miningTools` に含まれるか」だけで成立するため影響しない
- **装備枠のクリックは常にアイテム移動、選択はホイール専用**（出所: ユーザー裁定 AskUserQuestion 2026-07-29）。クリックに「選択」と「移動」の2義を持たせない
- **装備選択の楽観更新は維持し、同値時の送信抑止だけ外す**（出所: ユーザー裁定 AskUserQuestion 2026-07-29）。`clamped == SelectedIndex` の早期returnがあると、一度サーバとズレた際に同値再送が握り潰されて恒久的にズレたままになるため、常に送信する
- **装備へのアイテム移動経路は plan B 内で実装する**（出所: ユーザー裁定 AskUserQuestion 2026-07-29）。ブランチ全体レビューで5系統が「移動経路が全層に無く実プレイで採掘が成立しない」と一致指摘したことを受けた裁定
- **常時表示HUDのクリック可否と `GrabOverlay` の描画は同一の述語 `screenAllowsGrab` を読む**（出所: ユーザー裁定「ロジックをgrab itemの表示と完全に共通化で処理できないの？」2026-07-30）。両者が別々の画面名リテラルを持っていたため、`pauseMenu` / `buildMenu` / `challengeList` / `trainHud` では「クリックは通るが掴んだ絵が出ない」不可視grabが起きていた。どちらの集合に寄せるかを選ぶのではなく、リテラルが2箇所ある構造自体を潰す
- **装備インベントリは `ISortExcludedSlots` で整理対象から外す**（出所: レビュー指摘の受諾 2026-07-30）。受入制限撤去でソート除外も消えたが、`SortInventoryProtocol` は identifier を無条件に解決するためプロトコル上は到達し、スロットが詰め直されて選択インデックスが別のツールを指す。役割同型の前例 `ISortExcludedSlots`（`VanillaMachineBlockInventoryComponent`）が既にあるのでそれに従う
- **`Game.PlacementTarget` を新規アセンブリとして作る**（出所: agent前提（拒否権つき））。`Core.Master` へ置く案は「共有層へのドメインロジック混入」に該当するため不採用
- **既存セーブのブループリントはロード時にGUIDを発行する**（出所: agent前提（拒否権つき））。ユーザー生成データであり、マスタ由来値のフォールバックとは別物
- **実装計画は A/B/C の3本に分割し、今回すべて執筆する**（出所: ユーザー裁定 AskUserQuestion 2026-07-28）

plan執筆時（2026-07-28）の追加判断（詳細は各planの「判断記録（ADR）」。各行末は改修対象のレンズ該当ファイル）:

- **設置対象IDは生 `Guid`（ラッパー型なし）・`IPlacementTarget` に `Guid Id` を追加**（出所: agent前提（拒否権つき）。前例: マスタ識別子は生Guid）
- **`buildTools` は `BuildToolMaster` が読み、`tools`/`equipmentSlotCount` は `ToolMaster` が読む**（出所: ユーザー裁定のマスタ化2件の具体化。前例: `ConnectToolMaster` の「同一JSONの自配列だけ読むラッパーMaster」）— 対象: `BuildToolMaster.cs` / `ToolMaster.cs` / `MasterHolder.cs`
- **BPのGUID発行・同名許容・GuidベースDelete/TryGetへのAPI変更**（出所: ユーザー裁定「同名BP許容・識別はGUID」の具体化）— 対象: `IBlueprintDatastore.cs` / `BlueprintDatastore.cs` / `BlueprintProtocol.cs`
- ~~**受入制限 `IItemAcceptanceInventory` は移動先が宣言し移動サービス2種が尊重する**（出所: ユーザー裁定「items.ymlにツール定義」＋新機構裁定の具体化）— 対象: `InventoryItemMoveService.cs` / `InventoryItemInsertService.cs`~~ → **撤回**（2026-07-29。移動サービス2種は master とバイト一致まで復元済み）
- **装備インベントリは `PlayerInventoryData` 配下で生成・セーブし、Resolverは grab 前例に追随**（出所: ADR-0003の具体化）— 対象: `PlayerInventoryDataStore.cs` / `EquipmentInventoryIdentifierResolver.cs`
- **装備同期は3点セット標準で新設**（イベント＋初期データ同梱＋選択プロトコル。1プロトコル=1 VanillaApiメソッド）（出所: `.claude/rules/server-protocol.md` 標準）— 対象: `EquipmentUpdateEventPacket.cs` / `PlayerInventoryResponseProtocol.cs` / `EquipmentProtocol.cs` / `PacketResponseCreator.cs` / `VanillaApiSendOnly.cs`
- **選択中の装備は `EquipmentInventoryData` が所有し `-1`＝素手を正式値とする**（出所: agent前提（拒否権つき））
- **採掘のダメージ解決＋クールダウンは `Game.Map/MapObjectMiningService` が担い、プロトコルは `AttackDamage` を廃してinstanceIdのみ受ける**（出所: ADR-0004の具体化。時刻は `DateTime.UtcNow`・揮発Dictionary保持は agent前提（拒否権つき））— 対象: `MapObjectAcquisitionProtocol.cs` / `VanillaApiSendOnly.cs`
- **`HotbarAssignmentDatastore` は新asmdef `Game.Hotbar` に置き、書き込み時もカタログ検証する。クライアント側は非MonoBehaviourの `ClientHotbarDatastore` が割当と選択枠を所有**（出所: ADR-0002の具体化＋agent前提（拒否権つき）。前例: `Game.UnlockState` の小asmdef・`PlayerInventoryDataStore` のplayer別Dictionary）— 対象: `HotbarAssignmentDatastore.cs` / `ClientHotbarDatastore.cs`
- **ホットバー同期は3点セット標準で新設・操作は `va:hotbar` 1本のOperation分岐**（出所: `.claude/rules/server-protocol.md` 標準＋ユーザー裁定「プロトコルは1本にモード分岐」）— 対象: `HotbarProtocol.cs` / `GetHotbarProtocol.cs` / `HotbarUpdateEventPacket.cs` / `PacketResponseCreator.cs`
- **旧ホットバーの特別扱い撤廃はソート除外の削除を含む**（出所: ユーザー裁定「完全撤廃」の具体化）— 対象: `SortInventoryProtocol.cs`
- **選択中のホットバー枠はクライアントのみの状態（サーバ非保持・非セーブ）**（出所: ADR-0002の具体化）
- **チュートリアル `itemViewHighLight` はレシピパネルアンカー解決でありホットバー非依存と判明、対応不要**（出所: 現状調査 2026-07-28。死活表の当該行を「変更不要の確認」に読み替え）
