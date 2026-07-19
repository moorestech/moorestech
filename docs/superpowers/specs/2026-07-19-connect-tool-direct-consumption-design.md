# 接続ツールの直接材料消費化（connectToolsマスタ化）設計

日付: 2026-07-19 / 対象ブランチ: tree1

## 背景と目的

電線・レール・歯車チェーンは現在「専用アイテム」（電線・レール・歯車チェーン）をクラフトして敷設時に消費する。
これを廃止し、ブロック設置と同様に**素材アイテムをインベントリから直接消費**する形に変える。

現状調査で判明している前提:

- 3接続ツールは既にホットバー非依存で、ビルドメニューの専用エントリから起動する
  （`BuildMenuEntryCatalog.cs` / `ConnectToolCatalog.cs`。後者の11行目に「非アイテム化したら消す」コメントあり）
- 専用アイテムの残存役割は「メニューアイコン」「距離比例の消費素材」の2つのみ
- 消費はマスタ定義（blocks.jsonの`electricWireItems[]`/`gearChainItems[]`、train.jsonの`railItems[]`）を
  参照し、サーバーが`ConstructionCostService`でメインインベントリから直接消費している
- 解放は`unlockBlock`（v8導入）でブロック単位に制御。接続ツール3種のみ解放と無関係に常時表示
- 電線アイテムはv8マスタに入手手段が存在せず、電線接続は実質不能（本変更で修復される）

## 決定事項（ユーザー裁定済み）

1. 解放は導出ではなく**個別設定**。unlockBlockと同型の`unlockConnectTool`を新設する
   （理由: レールv2等の追加時に独立した解放タイミングを持たせるため）
2. アイコン画像はマスタに**個別指定**（`imagePath`）できるようにする
3. 電線の素材は**銅のワイヤー×1 / 長さ1**
4. 既存セーブ（save_1等）は**移行スクリプトで対応**する
5. 専用アイテム3種と関連レシピは完全削除する

## 1. マスタデータ

### 新スキーマ `VanillaSchema/connectTools.yml`（トップレベル・edit-schemaスキル手順で追加）

| プロパティ | 型 | 説明 |
|---|---|---|
| connectToolGuid | uuid | 一意ID。レールでは`RailTypeGuid`としてそのまま永続化に使う |
| name | string | 表示名 |
| toolType | enum | electricWire / rail / gearChain（挙動の束縛） |
| imagePath | string | アイコン画像パス（items.jsonの`imagePath`と同じロード経路） |
| sortPriority | integer | ビルドメニュー表示順 |
| initialUnlocked | boolean | blocks.ymlの`initialUnlocked`と同型 |
| lengthPerUnit | number | N長さごとに材料1セット消費（消費数 = ceil(距離/lengthPerUnit)×count） |
| requiredItems[] | array | {itemGuid(foreignKey→items), count}。複数素材可。既存の建設コスト型を再利用 |

blocks.ymlの`electricWireItems`・`gearChainItems`、train.ymlの`railItems`は**スキーマから削除**し、
SourceGeneratorのコンパイルエラー駆動で参照を全置換する。

### 実データ（connectTools.json、3エントリ）

| エントリ | toolType | 材料 | lengthPerUnit | 解放研究ノード |
|---|---|---|---|---|
| 電線 | electricWire | 銅のワイヤー×1 | 1 | 「加工装置」（電柱unlockBlockと同ノード） |
| レール | rail | 補強棒材×12＋鉄板×5 | 5 | 「鉄道の時代」 |
| 歯車チェーン | gearChain | 鉄のワイヤー×10 | 4 | 「鉄のチェーンポール」 |

消費量は現行クラフトレシピの実効コストの展開であり、ゲームバランスは変わらない（電線のみ新規設定）。

### マスタ実データの変更（../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master）

- items.json: 旧3アイテム（電線 5b6e76b8- / レール 5be3a22c- / 歯車チェーン 8412fa32-）を削除
- craftRecipes.json: レール・歯車チェーンのレシピ2件を削除
- machineRecipes.json: 歯車チェーンの機械レシピ1件を削除
- research.json: `unlockItemRecipeView`（レール=「鉄道の時代」、歯車チェーン=「鉄のチェーンポール」）を
  `unlockConnectTool`に置換。電線の`unlockConnectTool`を「加工装置」ノードに追加
- connectTools.json: 上表3エントリを新規作成

## 2. サーバー: 解放ドメイン（unlockBlockと完全同型）

- `VanillaSchema/ref/gameAction.yml`のenumに`unlockConnectTool`を追加
- `GameActionExecutor`に分岐追加（`unlockBlock`分岐と同型）
- `Game.UnlockState/Holders/ConnectToolUnlockStateHolder`を新設（7番目のホルダー）。
  全connectToolsを`initialUnlocked`で初期化し、`IObservable<Guid> OnUnlock`と`Infos`辞書を公開
- クライアント同期は既存3点セットの拡張のみ（**新プロトコル不要**）:
  - `UnlockedEventPacket`のUnlockEventTypeに`ConnectTool`を追加
  - `InitialHandshakeResponse.UnlockState`にconnectTool解放状態を追加
  - クライアント`ClientGameUnlockStateDatastore`に購読・保持を追加
- MasterHolderにConnectToolMasterを追加しJSONロード（既存Masterと同パターン）

## 3. サーバー: 消費ロジック

- 対象プロトコル: `va:electricWireConnectionEdit` / `va:electricWireExtend` / `va:railConnectionEdit` /
  `va:railConnectWithPlacePier` / `va:gearChainConnectionEdit` / `va:gearChainPoleExtend`
- 各プロトコルの素材指定引数（現状 `int ItemId` / `ItemId ChainItemId` / `Guid RailTypeGuid` と不統一）を
  **`connectToolGuid`（Guid）に統一**する
- 消費計算: `ceil(距離/lengthPerUnit) × requiredItems各count` に一般化。
  既存の`ConstructionCostService`によるインベントリ検証・消費を継続利用
- `ElectricWireConnectionCost` / `GearChainConnectionCost`（現状 `(ItemId,int)` 単体）を
  **複数素材リスト**に拡張（撤去時返却`GetRefundItems`も追従）
- サーバー側でも**未解放connectToolの接続要求を拒否**する
  （`PlaceBlockProtocol`の未解放ブロックスキップと同型のガード）
- ブロック設置時の電線自動接続（`ElectricWireAutoConnectService`）は
  「解放済みelectricWireエントリのうちsortPriority最小」を自動選択して同様に消費する
- レールグラフの`RailTypeGuid`には`connectToolGuid`をそのまま格納（データ構造変更なし）。
  延長系（電柱/橋脚/ポール自動設置）の建設コスト予約による二重消費防止ロジックは現行を維持

## 4. クライアント

- `BuildMenuEntryCatalog`: `ConnectToolType`3種固定ループを廃止し、
  **解放済みconnectToolsエントリごとに1枠**を表示（未解放は非掲載。ブロックと同一挙動）。
  sortPriority昇順。アイコンは`imagePath`から取得
- `ConnectToolPlacementTarget`が`connectToolGuid`を運び、各PlaceSystem
  （ElectricWireConnect / TrainRailConnect / GearChainPoleConnect）はプロトコルへそのguidを渡す。
  挙動の振り分けはマスタの`toolType`で行う
- 削除対象: `ConnectToolCatalog`のアイテム選択部（`SelectIconItemGuid`等）、
  `ElectricWireItemAutoSelector`、`GearChainPoleItemFinder`、`TrainRailItemAutoSelector`
- ミドルクリック吸取（`PlacementTargetPickService`）は対象のconnectToolエントリ解決に追従
- WebUI側（`BuildMenuTopic` / `BuildMenuEntryDtoFactory` / `BuildMenuActions`）も同追従
- レール描画がRailTypeGuid（旧レールアイテムguid）を参照している箇所があれば新guidに追従（実装時に確認）

## 5. セーブ移行スクリプト

save_1等を対象に、moorestech-save-migrationスキルの手順で作成（バックアップ必須）:

1. インベントリ内の旧3アイテムを素材換算で置換
   （レール1→補強棒材12＋鉄板5、歯車チェーン1→鉄のワイヤー10。電線は入手不能のため実質対象なし）
2. 電線・チェーンのブロックcomponentStatesに保存済みの返却コスト（旧アイテム参照）を新requiredItems相当へ変換
3. レールセグメントの`RailTypeGuid`（旧レールアイテムguid）を新connectToolGuidへ引き換え

## 6. テスト

- サーバー: 解放前の接続要求拒否 / `unlockConnectTool`実行と解放イベント同期 /
  複数素材の消費・不足時失敗・撤去時返却 / 各プロトコルのconnectToolGuid受け渡し
- 既存の接続系テスト（電線・レール・歯車チェーン）のマスタ参照追従修正
- ロード復元: 完了済み研究の`ExecuteUnlockActions`再実行でconnectTool解放が復元されること（冪等）

## 自己反証済みの論点

- **同一toolTypeの複数エントリ**（レールv2等）: ビルドメニュー選択→`connectToolGuid`が
  プロトコルまで一気通貫で流れるため、消費素材・レール種別は一意に決まる
- **未解放guidの直接送信**: サーバー側解放チェックで拒否
- **検証範囲の注記**: 「電線が現在入手不能」はv8実データのgrep確認による
  （craftRecipes/machineRecipes/research全走査でヒットなし）。ゲーム内動作の実プレイ確認はしていない
