# プレイヤー身元とプレイヤーID連番採番 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 既定プレイヤーID（1）と `PlayerPrefs` のランダム採番を撤去し、クライアントが名乗るプレイヤー身元からサーバーがワールド単位で連番のプレイヤーIDを払い出し、以後はハンドシェイクで接続に紐づけたIDだけを信じるようにする。

**Architecture:** サーバーに新アセンブリ `Game.PlayerIdentity`（身元→プレイヤーIDの対応表。ワールドのセーブの新節 `players` に永続）を足し、ハンドシェイクで採番・紐づけを行う。振り分け口 `PacketResponseCreator` が未紐づけ接続の要求を一括で拒否し、各プロトコルはペイロードの playerId を捨てて `PacketResponseContext.PlayerId` を使う。セーブは V2→V3 の変換で旧ランダムIDを連番へ振り直し、持ち主未定＋結びつけ候補を作る。クライアントは起動時に身元（`steam:` / `device:`）を解決し、ハンドシェイク応答で受け取ったIDで `PlayerConnectionSetting` を作る。

**Tech Stack:** Unity (C#), MessagePack-CSharp（int Key 配列形式）, Newtonsoft.Json（セーブ）, UniTask, Steamworks.NET, NUnit（EditMode）, bash/python3（バグ報告の再現スクリプト）

## Requirements

裁定の正本は `docs/adr/0073-server-assigns-sequential-player-ids-from-player-identity.md` と `.decisions/2026-09-27-*.md`（7件）。以下はそれを受入基準つきで並べたもの。

- R1 既定プレイヤーIDの撤去: `InitializeProprieties.DefaultPlayerId`・`InitializeProprieties.PlayerId`・`SetPlayerId`（スクリプトとMainMenuシーン上のコンポーネント）・`PlayerPrefsKeys.PlayerIdKey` が存在しない。受入: `grep -rn "DefaultPlayerId\|PlayerIdKey\|class SetPlayerId" moorestech_client/Assets/Scripts` が0件。
- R2 身元の決め方: Steam配布ビルド（build-info の `steamBuildLabel` が非空。`RepositoryStateProbe.ReadBuildOrigin()` 経由）は `steam:<SteamID64>`、それ以外（Editor・開発・展示ビルド）は `device:<SystemInfo.deviceUniqueIdentifierのSHA-256小文字16進64桁>`。受入: `LocalPlayerIdentityResolverTest` が両分岐とハッシュ値を検査して緑。
- R3 Steam配布ビルドでSteamIDが読めないときは開始を止め、`ui.loading.steamIdentityUnavailable` を表示しログに理由を出す。端末値へ切り替えない。受入: 同テストの失敗分岐。
- R4 Steamを使わないビルドで端末値が空または `SystemInfo.unsupportedIdentifier` のときは開始を止め、`ui.loading.deviceIdentityUnavailable` を表示する。代わりのGUIDは作らない。受入: 同テストの失敗分岐。
- R5 サーバーはワールド単位で 1 始まりの連番を払い出し、欠番を再利用しない。既知の身元には同じIDを返す。受入: `PlayerIdentityRegistryTest`。
- R6 対応表・次のID・結びつけ候補はセーブの `players` 節に永続する（JSON、エントリは playerId 昇順）。受入: `PlayerIdentityRegistrySaveLoadTest`・`SaveOrderCanonicalTest` の `players` ケース。
- R7 ハンドシェイク要求は身元文字列を運び、応答は採番したIDか拒否コードを返す。形式不正の身元は `InvalidIdentity` で拒否。受入: `InitialHandshakeIdentityTest`。
- R8 接続中と同じ身元の二重接続は後から来た方を `AlreadyConnected` で拒否しログに出す。受入: 同テスト。
- R9 ハンドシェイクで接続への紐づけに失敗（切断済み）したら登録も初期装備付与も行わず `ConnectionClosed` で拒否。受入: 同テスト。
- R10 未紐づけ接続からのハンドシェイク以外の要求は `PacketResponseCreator` の1か所で理由をログに出して無視する。受入: `PacketResponseCreatorUnboundGateTest`。
- R11 全プロトコル（下表27本＋`InventoryIdentifierMessagePack`）のリクエストから playerId を外し、サーバーは `context.PlayerId.Value` を使う。後続の Key は詰め直す。受入: `grep -n "PlayerId" moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/*.cs` でリクエストクラスのフィールドが0件（イベント・レスポンス用の表示IDは除く、Task 6 の確認コマンド参照）。
- R12 パケットログの各レコードに送り手のプレイヤーID（未紐づけは0）を記録し、再生は記録IDごとに紐づけ済みコンテキストで流す。受入: `ReceivedPacketLogTest`・`SnapshotReplayDeterminismTest` 緑。
- R13 セーブ V2→V3 変換: 旧IDを昇順に 1..N へ振り直し7節（`playerInventory`・`entities` の `va:Player`・`playerRidingStates`・`hotbarAssignments`・`remainingPlacementCounts`・`constructionPayers`・`miningCooldowns`）を書き換える。全員持ち主未定、候補は持ち物総数最大（同数→スポーンから遠い→新IDが小さい）。受入: `SaveMigrationStepV2ToV3Test`（テスターのセーブ相当の fixture で ID1623179277→2 が候補）。
- R14 結びつけ候補がある状態で未知の身元が初めて来たら候補に結びつけ候補を消す。以後の未知の身元は新規ID。残りの持ち主未定は自動で割り当てない。受入: `PlayerIdentityRegistryTest`。
- R15 バグ報告の再現: `prepare-run.sh` が複製した `save.json` の報告者のプレイヤー（manifest `steamId` → `steam:<id>`）を持ち主未定に戻し候補にする。steamId が無い報告は結びつき済みのうち持ち物総数最大を候補にしログを出す。受入: `test-prepare-run.sh` の新ケース。
- R16 AGENTS.md「既知の制約」の playerId 自己申告の項を削除し、`.decisions/2026-08-14-プロトコルのplayerId自己申告は既存多数派として放置する.md` に ADR 0073 で置き換えた旨を追記、moores-code-review の該当レンズ注記を更新。受入: grep で旧記述が残らない。
- R17 実機（unityプレイ録画テスト）で、新規ワールドでID1が払い出され、再起動後も同じID、テスターのV2スナップショットを読むと変換され開発機が候補（旧1623179277＝原木612個）を受け取る。受入: Task 10 の手順と警告ログ0件。

**やらないこと（スコープ境界）:**
- リモート接続での身元の検証（Steam認証チケット）→ bd moorestech-3eonp
- プレイヤーとベルト上アイテムの EntityInstanceId 空間の分離 → bd moorestech-pejkw
- 開発用の身元上書き（起動引数等）・Steam身元と端末身元の結びつけ・身元の結び直しUI
- 旧形式パケットログ（`packets_*.bin`）の読み込み互換
- 全データリセットの挙動変更（身元は PlayerPrefs に無いので影響しない）

## Global Constraints

- AGENTS.md 全規約: 1ファイル200行未満、partial禁止、`Func<>`禁止、try-catchは外部境界のみ、デフォルト引数禁止、日英2行コメント（各1行）、`#region Internal` はメソッド内ローカル関数のみ、null チェックは外部データのみ、1ディレクトリ10ファイルまで、イベントは UniRx。
- fail-closed の拒否・無視は必ず理由をログへ出す（`Debug.LogWarning`/`LogError`）。
- 身元の書式: `steam:` + 10進1〜20桁、`device:` + `[0-9a-f]{64}`。これ以外は不正。
- プレイヤーIDは 1 以上。0 はパケットログの「未紐づけ」、-1 は既存のブロードキャスト（`NotificationService.BroadcastPlayerId`）で、採番に使わない。
- セーブ形式: `WorldSaveAllInfo.CurrentVersion = 3`、変換は `SaveMigrationStepV2ToV3`（moorestech-save-migration スキル準拠。`MasterHolder` 不使用・`Failed(reason)` で返す・1行ログ）。
- 新規 `.cs` を足したら Unity の再起動が要る（スキル記載）。`.meta` は手で作らない。シーンは `uloop execute-dynamic-code` 経由でのみ編集。
- ローカライズ CSV を足したら force-recompile（メモリ: localization.csv は force-recompile が要る）。
- コンパイル: `uloop compile --project-path ./moorestech_client`。テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（v3 既定は EditMode）。TestResults.xml はマシン共通パスなので start-time で自分の結果か確認する。
- 作業は worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/player-identity-sequential-ids`（ブランチ `feature/player-identity-sequential-ids`）で行う。各タスク末尾でコミット。

---

## File Structure

### 新規（サーバー）

| パス | 責務 | 既存部品との関係 |
|---|---|---|
| `moorestech_server/Assets/Scripts/Game.PlayerIdentity/Game.PlayerIdentity.asmdef` | 新アセンブリ | 前例 `Game.Hotbar/Game.Hotbar.asmdef` と同形。references: `Common.Debug` のみ |
| `.../Game.PlayerIdentity/PlayerIdentityText.cs` | 身元文字列の書式検査 | 新規（同役割の既存部品なし） |
| `.../Game.PlayerIdentity/PlayerIdentityRegistry.cs` | 対応表・採番・候補の結びつけ・セーブ入出力 | 前例 `Game.Hotbar/HotbarAssignmentDatastore.cs`（辞書＋`GetSaveJsonObject`/`Load`） |
| `.../Game.PlayerIdentity/IPlayerIdentityRegistry.cs` | プロトコル側から見える口 | 前例 `IHotbarAssignmentLookup` |
| `.../Game.PlayerIdentity/PlayerIdAssignment.cs` | 採番結果（ID・新規か・候補を結んだか） | 新規 |
| `.../Game.PlayerIdentity/PlayersSaveJsonObject.cs` | `players` 節のJSON | 前例 `PlayerHotbarSaveJsonObject` |
| `.../Game.SaveLoad/Migration/Steps/SaveMigrationStepV2ToV3.cs` | 変換本体（振り直し表の作成と適用の指揮） | 前例 `SaveMigrationStepV1ToV2.cs` |
| `.../Game.SaveLoad/Migration/Steps/V2ToV3/PlayerIdRenumbering.cs` | 7節の旧ID収集と書き換え | 新規（200行制限のため本体から分割） |
| `.../Game.SaveLoad/Migration/Steps/V2ToV3/PlayerClaimCandidateSelector.cs` | 持ち物総数による候補選定 | 新規 |
| `.../Server.Protocol/PacketResponse/Handshake/HandshakeRejection.cs` | 拒否コード enum | 新規 |

### 新規（クライアント）

| パス | 責務 | 既存部品との関係 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Starter/Identity/LocalPlayerIdentityResolver.cs` | 身元の解決（純関数＋この起動用の薄い入口） | Steam は既存 `PlaytestLocalSteamIdReader` を**呼ぶ**。build-info は既存 `RepositoryStateProbe.ReadBuildOrigin()` を**呼ぶ**。SHA-256 は `WebUiArtifactValidator.cs:78-81` と同じ3行だが別アセンブリの private なので新規（理由: 共通化先が無い） |
| `.../Client.Starter/Identity/PlayerStartRefusedException.cs` | 開始拒否（ローカライズキー付き） | 新規。既存の初期化失敗経路 `InitializeScenePipeline.cs:124-143` が捕まえる |
| `.../Client.Network/API/PlayerHandshakeRejectedException.cs` | サーバー拒否の例外 | 新規 |

### 新規（スクリプト・テスト）

| パス | 責務 |
|---|---|
| `scripts/bugreport/unclaim-reporter.py` | 複製セーブの報告者を持ち主未定＋候補に戻す（`read-manifest.py` と同じ流儀の外部境界） |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlayerIdentity/PlayerIdentityRegistryTest.cs` | R5・R14 |
| `.../Tests/UnitTest/Game/PlayerIdentity/PlayerIdentityTextTest.cs` | 書式 |
| `.../Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV2ToV3Test.cs` | R13 |
| `.../Tests/CombinedTest/Server/PacketTest/InitialHandshakeIdentityTest.cs` | R7〜R9 |
| `.../Tests/CombinedTest/Server/PacketTest/PacketResponseCreatorUnboundGateTest.cs` | R10 |
| `.../Tests/Util/BoundPacketContext.cs` | テスト用の紐づけ済みコンテキスト生成 |
| `moorestech_client/Assets/Scripts/Client.Tests/Starter/LocalPlayerIdentityResolverTest.cs` | R2〜R4 |

### 変更（主要）

- サーバー: `InitialHandshakeProtocol.cs`・`PacketResponseCreator.cs`・`PacketResponseContext.cs`（変更なし・読むだけ）・Task 6 の27プロトコル・`Server.Util/MessagePack/InventoryIdentifierMessagePack.cs`・Resolver 6本・`WorldSaveAllInfo.cs`・`AssembleSaveJsonText.cs`・`WorldSaveDataRestorer.cs`・`WorldLoaderFromJson.cs`・`MissingMasterPruner.cs`・`MoorestechServerDIContainerGenerator.cs`・`ReceivedPacketLog.cs`/`ReceivedPacketLogReader.cs`/`ReceivedPacketRecord.cs`/`ReceiveQueueProcessor.cs`/`SnapshotReplayer.cs`/`PacketLogJsonDumper.cs`・各 asmdef（`Game.SaveLoad`・`Server.Protocol`・`Server.Boot` に `Game.PlayerIdentity` を追加）。
- クライアント: `InitializeProprieties.cs`・`LocalGameLauncher.cs`・`ConnectServer.cs`・`InitializeScenePipeline.cs`・`ServerConnectionInitializer.cs`・`VanillaApi.cs`・`VanillaApiSendOnly.cs`・`VanillaApiWithResponse.cs`・`ElectricWireExtendRequestSender.cs`・`MachineRecipeSelectionActions.cs`・`InventoryMoveServerDispatcher.cs`・`LocalPlayerInventoryController.cs`・`PlayerPrefsKeys.cs`・`Client.MainMenu/SetPlayerId.cs`（削除）・`MainMenu.unity`（コンポーネント除去は uloop 経由）・`Localization/localization.csv`。

### レイヤリング制約（spec-architecture-review 済み）

| 項目 | 配置 | 前例 |
|---|---|---|
| 対応表 datastore | `Game.PlayerIdentity`（Game 層・新アセンブリ） | `Game.Hotbar/HotbarAssignmentDatastore.cs`。Core には置かない（プレイヤーというドメイン語彙を持つ） |
| セーブ入出力 | `AssembleSaveJsonText`/`WorldSaveDataRestorer` に具象で注入 | Hotbar と同じ（`AssembleSaveJsonText.cs:55,109`、`WorldSaveDataRestorer.cs:56,130`） |
| 未紐づけ拒否 | `PacketResponseCreator.GetPacketResponse` の1か所 | 各プロトコルに分散させない（同種の条件分岐は文脈が集まる側に揃える規約） |
| 身元の解決 | `Client.Starter`（既に `Client.PlaytestReceiver`・`Client.Game` を参照） | Steam 読み取りは `PlaytestLaunchProfile` と同じ `IPlaytestLocalSteamIdReader` を呼ぶ |
| 開始拒否の表示 | 既存の初期化失敗経路（例外→メニューへ戻る） | `InitializeScenePipeline.cs:124-143` |
| 拒否コード | enum（文字列でない） | 新規パターン。クライアントがローカライズキーへ写すため |

### ユーザー操作を恒久に失敗させる経路

| 状態 | 通常運用で起きる時機・起こす人 | その間ユーザーに見えるもの | 操作なしで解消するか | 出所 |
|---|---|---|---|---|
| Steam配布ビルドでSteamIDが読めない | Steamクライアント未起動・オフライン・ログアウトでゲームを起動したとき（テスター） | 「Steamに接続できないため開始できません」→メインメニュー | しない。Steamを起動して再度開始で解消 | ユーザー裁定 2026-09-27「開始を止める」 |
| 端末値が取れない（空・unsupported） | 配布対象（Windows/macOS）では Unity が常に値を返すため通常運用では起きない。未対応プラットフォームでの起動時のみ | 「この端末の識別子を取得できないため開始できません」→メインメニュー | しない | agent判断（ADR 0073 agent前提。通常運用で到達しないため裁定外とした。配布対象を増やすときは要裁定） |
| 同じ身元が接続中 | 同じSteamアカウントで2台同時に起動／同一PCで2クライアント（開発者） | 「同じプレイヤーが既に接続しています」→メインメニュー | 先の接続が切れれば解消（サーバーが切断を検知するまで。正常終了なら即時、回線断はTCPの切断検知まで） | ユーザー裁定 2026-09-27「後から来た方を拒否」 |
| 紐づけ中に切断 | ハンドシェイク途中の回線断 | 接続断として既存の初期化失敗表示 | 再接続で解消 | agent前提（ADR 0073） |

---

### Task 1: `Game.PlayerIdentity` — 身元の書式と対応表

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerIdentity/Game.PlayerIdentity.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerIdentity/PlayerIdentityText.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerIdentity/PlayersSaveJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerIdentity/PlayerIdAssignment.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerIdentity/IPlayerIdentityRegistry.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerIdentity/PlayerIdentityRegistry.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlayerIdentity/PlayerIdentityTextTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlayerIdentity/PlayerIdentityRegistryTest.cs`
- Modify: テストアセンブリの asmdef（`moorestech_server/Assets/Scripts/Tests/Tests.asmdef`。実名は `find moorestech_server/Assets/Scripts/Tests -maxdepth 1 -name "*.asmdef"` で確認）の references に `Game.PlayerIdentity` を追加

**Interfaces:**
- Produces:
  - `static bool PlayerIdentityText.IsValid(string identity, out string reason)`
  - `const string PlayerIdentityText.SteamPrefix = "steam:"`, `const string PlayerIdentityText.DevicePrefix = "device:"`
  - `class PlayersSaveJsonObject { int NextPlayerId; int? ClaimCandidatePlayerId; List<PlayerIdentityEntryJsonObject> Entries; }`、`class PlayerIdentityEntryJsonObject { int PlayerId; string Identity; }`（Identity null＝持ち主未定）
  - `readonly struct PlayerIdAssignment { int PlayerId; PlayerIdAssignmentKind Kind; }`、`enum PlayerIdAssignmentKind { Known, ClaimedCandidate, NewlyAssigned }`
  - `interface IPlayerIdentityRegistry { bool TryGetPlayerId(string identity, out int playerId); PlayerIdAssignment Assign(string identity); }`
  - `class PlayerIdentityRegistry : IPlayerIdentityRegistry { void InitializeForNewWorld(); PlayersSaveJsonObject GetSaveJsonObject(); void Load(PlayersSaveJsonObject save); }`

- [ ] **Step 1: asmdef を作る**

```json
{
    "name": "Game.PlayerIdentity",
    "rootNamespace": "",
    "references": [
        "Common.Debug"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 失敗するテストを書く（書式）**

```csharp
using Game.PlayerIdentity;
using NUnit.Framework;

namespace Tests.UnitTest.Game.PlayerIdentity
{
    public class PlayerIdentityTextTest
    {
        [TestCase("steam:76561198319362448")]
        [TestCase("device:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void 正しい書式の身元は受け入れるTest(string identity)
        {
            Assert.IsTrue(PlayerIdentityText.IsValid(identity, out var reason), reason);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("76561198319362448")]
        [TestCase("steam:")]
        [TestCase("steam:12a")]
        [TestCase("steam:123456789012345678901")]
        [TestCase("device:0123")]
        [TestCase("device:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
        [TestCase("mac:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void 不正な書式の身元は理由付きで拒否するTest(string identity)
        {
            Assert.IsFalse(PlayerIdentityText.IsValid(identity, out var reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }
    }
}
```

- [ ] **Step 3: 失敗するテストを書く（対応表）**

```csharp
using System.Collections.Generic;
using Game.PlayerIdentity;
using NUnit.Framework;

namespace Tests.UnitTest.Game.PlayerIdentity
{
    public class PlayerIdentityRegistryTest
    {
        private const string SteamA = "steam:1";
        private const string SteamB = "steam:2";
        private const string SteamC = "steam:3";

        [Test]
        public void 新しいワールドでは1から連番で払い出すTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.InitializeForNewWorld();

            Assert.AreEqual(1, registry.Assign(SteamA).PlayerId);
            Assert.AreEqual(2, registry.Assign(SteamB).PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.NewlyAssigned, registry.Assign(SteamC).Kind);
        }

        [Test]
        public void 既知の身元には同じIDを返すTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.InitializeForNewWorld();
            registry.Assign(SteamA);

            var again = registry.Assign(SteamA);
            Assert.AreEqual(1, again.PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.Known, again.Kind);
            Assert.IsTrue(registry.TryGetPlayerId(SteamA, out var id));
            Assert.AreEqual(1, id);
            Assert.IsFalse(registry.TryGetPlayerId(SteamB, out _));
        }

        [Test]
        public void 欠番は再利用しないTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(5, null, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, SteamA),
            }));

            Assert.AreEqual(5, registry.Assign(SteamB).PlayerId);
        }

        [Test]
        public void 候補は最初の未知の身元だけに結びつき以後は新規IDTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(3, 2, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, null),
                new(2, null),
            }));

            var first = registry.Assign(SteamA);
            Assert.AreEqual(2, first.PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.ClaimedCandidate, first.Kind);

            var second = registry.Assign(SteamB);
            Assert.AreEqual(3, second.PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.NewlyAssigned, second.Kind);

            var save = registry.GetSaveJsonObject();
            Assert.IsNull(save.ClaimCandidatePlayerId);
            Assert.AreEqual(3, save.Entries.Count);
            Assert.IsNull(save.Entries[0].Identity, "候補でない持ち主未定は残る");
            Assert.AreEqual(SteamA, save.Entries[1].Identity);
        }

        [Test]
        public void セーブ出力はプレイヤーID昇順Test()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(4, null, new List<PlayerIdentityEntryJsonObject>
            {
                new(3, SteamC),
                new(1, SteamA),
            }));
            registry.Assign(SteamB);

            var entries = registry.GetSaveJsonObject().Entries;
            CollectionAssert.AreEqual(new[] { 1, 3, 4 }, entries.ConvertAll(e => e.PlayerId));
            Assert.AreEqual(5, registry.GetSaveJsonObject().NextPlayerId);
        }
    }
}
```

- [ ] **Step 4: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerIdentity(Text|Registry)Test"`
Expected: コンパイルエラー（`Game.PlayerIdentity` の型が無い）。新規 .cs / asmdef 追加のため Unity を再起動してから実行する。

- [ ] **Step 5: 実装する**

`PlayerIdentityText.cs`:
```csharp
namespace Game.PlayerIdentity
{
    // プレイヤー身元の文字列書式。種別の接頭辞で出どころを区別する
    // The player identity text format; the prefix tells where it came from
    public static class PlayerIdentityText
    {
        public const string SteamPrefix = "steam:";
        public const string DevicePrefix = "device:";
        private const int MaxSteamIdDigits = 20;
        private const int DeviceHashLength = 64;

        public static bool IsValid(string identity, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(identity))
            {
                reason = "身元が空";
                return false;
            }

            // Steamは10進のSteamID64、端末はSHA-256の小文字16進
            // Steam carries a decimal SteamID64; a device carries a lowercase hex SHA-256
            if (identity.StartsWith(SteamPrefix)) return IsSteamBody(identity.Substring(SteamPrefix.Length), out reason);
            if (identity.StartsWith(DevicePrefix)) return IsDeviceBody(identity.Substring(DevicePrefix.Length), out reason);

            reason = $"未知の身元種別: {identity}";
            return false;

            #region Internal

            bool IsSteamBody(string body, out string steamReason)
            {
                steamReason = null;
                if (body.Length == 0 || body.Length > MaxSteamIdDigits) steamReason = $"SteamIDの桁数が不正: {identity}";
                foreach (var c in body)
                {
                    if (c >= '0' && c <= '9') continue;
                    steamReason = $"SteamIDに数字以外が含まれる: {identity}";
                }
                return steamReason == null;
            }

            bool IsDeviceBody(string body, out string deviceReason)
            {
                deviceReason = null;
                if (body.Length != DeviceHashLength) deviceReason = $"端末値の長さが不正: {identity}";
                foreach (var c in body)
                {
                    if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) continue;
                    deviceReason = $"端末値に小文字16進以外が含まれる: {identity}";
                }
                return deviceReason == null;
            }

            #endregion
        }
    }
}
```

`PlayersSaveJsonObject.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.PlayerIdentity
{
    // セーブの players 節。身元とプレイヤーIDの対応・次のID・結びつけ候補
    // The save's players section: identity-to-id entries, the next id, and the claim candidate
    public class PlayersSaveJsonObject
    {
        [JsonProperty("nextPlayerId")] public int NextPlayerId;
        [JsonProperty("claimCandidatePlayerId")] public int? ClaimCandidatePlayerId;
        [JsonProperty("entries")] public List<PlayerIdentityEntryJsonObject> Entries;

        public PlayersSaveJsonObject(int nextPlayerId, int? claimCandidatePlayerId, List<PlayerIdentityEntryJsonObject> entries)
        {
            NextPlayerId = nextPlayerId;
            ClaimCandidatePlayerId = claimCandidatePlayerId;
            Entries = entries;
        }
    }

    // 身元が null のエントリは持ち主未定のプレイヤー
    // An entry whose identity is null is an unclaimed player
    public class PlayerIdentityEntryJsonObject
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("identity")] public string Identity;

        public PlayerIdentityEntryJsonObject(int playerId, string identity)
        {
            PlayerId = playerId;
            Identity = identity;
        }
    }
}
```

`PlayerIdAssignment.cs`:
```csharp
namespace Game.PlayerIdentity
{
    public enum PlayerIdAssignmentKind
    {
        Known,
        ClaimedCandidate,
        NewlyAssigned,
    }

    // ハンドシェイクでの採番結果
    // The id assignment made at handshake
    public readonly struct PlayerIdAssignment
    {
        public readonly int PlayerId;
        public readonly PlayerIdAssignmentKind Kind;

        public PlayerIdAssignment(int playerId, PlayerIdAssignmentKind kind)
        {
            PlayerId = playerId;
            Kind = kind;
        }
    }
}
```

`IPlayerIdentityRegistry.cs`:
```csharp
namespace Game.PlayerIdentity
{
    public interface IPlayerIdentityRegistry
    {
        bool TryGetPlayerId(string identity, out int playerId);

        // 既知ならそのID、未知なら候補か新規IDを払い出す
        // Returns the known id, or claims the candidate / issues a new id for an unknown identity
        PlayerIdAssignment Assign(string identity);
    }
}
```

`PlayerIdentityRegistry.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.PlayerIdentity
{
    // ワールド単位の身元→プレイヤーIDの対応表。連番で払い出し欠番は再利用しない
    // Per-world identity-to-player-id table; ids are sequential and never reused
    public class PlayerIdentityRegistry : IPlayerIdentityRegistry
    {
        private const int FirstPlayerId = 1;

        private readonly Dictionary<string, int> _idByIdentity = new();
        private readonly SortedSet<int> _unclaimedPlayerIds = new();
        private int _nextPlayerId = FirstPlayerId;
        private int? _claimCandidatePlayerId;

        public void InitializeForNewWorld()
        {
            _idByIdentity.Clear();
            _unclaimedPlayerIds.Clear();
            _nextPlayerId = FirstPlayerId;
            _claimCandidatePlayerId = null;
        }

        public bool TryGetPlayerId(string identity, out int playerId)
        {
            return _idByIdentity.TryGetValue(identity, out playerId);
        }

        public PlayerIdAssignment Assign(string identity)
        {
            if (_idByIdentity.TryGetValue(identity, out var knownId)) return new PlayerIdAssignment(knownId, PlayerIdAssignmentKind.Known);

            // 旧セーブ・再現用の候補は最初の未知の身元にだけ渡す
            // The legacy/repro candidate goes only to the first unknown identity
            if (_claimCandidatePlayerId.HasValue)
            {
                var candidate = _claimCandidatePlayerId.Value;
                _claimCandidatePlayerId = null;
                _unclaimedPlayerIds.Remove(candidate);
                _idByIdentity[identity] = candidate;
                Debug.Log($"[PlayerIdentity] 持ち主未定のプレイヤー{candidate}を身元{identity}へ結びつけました");
                return new PlayerIdAssignment(candidate, PlayerIdAssignmentKind.ClaimedCandidate);
            }

            var newId = _nextPlayerId++;
            _idByIdentity[identity] = newId;
            Debug.Log($"[PlayerIdentity] 身元{identity}へ新しいプレイヤーID{newId}を払い出しました");
            return new PlayerIdAssignment(newId, PlayerIdAssignmentKind.NewlyAssigned);
        }

        public PlayersSaveJsonObject GetSaveJsonObject()
        {
            // 比較器が添字で突き合わせるためID昇順で正準化する
            // Canonicalize by ascending id because the comparer matches by index
            var entries = _idByIdentity.Select(pair => new PlayerIdentityEntryJsonObject(pair.Value, pair.Key))
                .Concat(_unclaimedPlayerIds.Select(id => new PlayerIdentityEntryJsonObject(id, null)))
                .OrderBy(entry => entry.PlayerId)
                .ToList();
            return new PlayersSaveJsonObject(_nextPlayerId, _claimCandidatePlayerId, entries);
        }

        public void Load(PlayersSaveJsonObject save)
        {
            InitializeForNewWorld();
            foreach (var entry in save.Entries)
            {
                if (entry.Identity == null) _unclaimedPlayerIds.Add(entry.PlayerId);
                else _idByIdentity[entry.Identity] = entry.PlayerId;
            }
            _nextPlayerId = save.NextPlayerId;
            _claimCandidatePlayerId = save.ClaimCandidatePlayerId;

            // 候補が持ち主未定に居ないのは壊れたセーブ。結びつけずに理由を出す
            // A candidate missing from the unclaimed set means a broken save; drop it and log why
            if (_claimCandidatePlayerId.HasValue && !_unclaimedPlayerIds.Contains(_claimCandidatePlayerId.Value))
            {
                Debug.LogError($"[PlayerIdentity] 結びつけ候補{_claimCandidatePlayerId.Value}が持ち主未定の一覧に無いため候補を破棄します");
                _claimCandidatePlayerId = null;
            }
        }
    }
}
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0。続けて Step 4 と同じテストコマンド。
Expected: 全件 PASS。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerIdentity moorestech_server/Assets/Scripts/Tests
git commit -m "feat(identity): 身元の書式とワールド単位の連番プレイヤーID対応表を追加 (ADR 0073)"
```

---

### Task 2: セーブの `players` 節と V2→V3 変換

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（`CurrentVersion = 3`、`[JsonProperty("players")] public PlayersSaveJsonObject Players { get; }` とコンストラクタ末尾引数 `PlayersSaveJsonObject players`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`（`PlayerIdentityRegistry` を注入し `Capture()` で `_playerIdentityRegistry.GetSaveJsonObject()` を渡す）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveDataRestorer.cs`（`ThrowIfRequiredFieldMissing` に `players` を追加、`LoadPlayerInventory` の直前に `_playerIdentityRegistry.Load(save.Players)`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`（`WorldInitialize` に `_playerIdentityRegistry.InitializeForNewWorld()`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs:40-58`（`SectionsWithoutMasterPruning` に `"players"`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（references に `Game.PlayerIdentity`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`services.AddSingleton<PlayerIdentityRegistry>(); services.AddSingleton<IPlayerIdentityRegistry>(provider => provider.GetRequiredService<PlayerIdentityRegistry>());` を Hotbar 登録 L212-214 の隣に、L281 の配列に `new SaveMigrationStepV2ToV3()`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`（`Game.PlayerIdentity`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV2ToV3.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V2ToV3/PlayerIdRenumbering.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V2ToV3/PlayerClaimCandidateSelector.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs:35`・`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs:137`（手書きのステップ配列へ `new SaveMigrationStepV2ToV3()`）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerVersionTest.cs`（L116/189/237 の「版2」の名前・コメントを現行版へ）、`SaveTickAndRandomStateTest.cs:119,138`（CurrentVersion 前提の確認）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveOrderCanonicalTest.cs`（`players.entries` 昇順ケース）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV2ToV3Test.cs`

**Interfaces:**
- Consumes: Task 1 の `PlayerIdentityRegistry`・`PlayersSaveJsonObject`
- Produces: `sealed class SaveMigrationStepV2ToV3 : ISaveMigrationStep`（`FromVersion => 2`）、`static bool PlayerIdRenumbering.TryBuildMap(JObject save, out Dictionary<long,int> map, out string reason)`、`static void PlayerIdRenumbering.Apply(JObject save, Dictionary<long,int> map)`、`static int? PlayerClaimCandidateSelector.Select(JObject save, Dictionary<long,int> map)`

- [ ] **Step 1: 変換の失敗するテストを書く**

```csharp
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV2ToV3Test
    {
        // テスターのセーブ相当: 幽霊1（装備のみ・スポーン）と本人1623179277（原木612）
        // Tester-like save: ghost 1 (equipment only, at spawn) and the owner 1623179277 (612 logs)
        private const string TesterLikeSave = @"{
          ""setting"": {""SpawnX"":500,""SpawnY"":13.4,""SpawnZ"":500},
          ""playerInventory"": [
            {""PlayerId"":1,""MainInventoryItems"":[],""GrabInventoryItems"":{""itemGuid"":""00000000-0000-0000-0000-000000000000"",""count"":0},""EquipmentInventoryItems"":[{""itemGuid"":""4c5fefbd-60a4-42ea-b70a-38a83b96e25e"",""count"":1}],""SelectedEquipmentIndex"":0},
            {""PlayerId"":1623179277,""MainInventoryItems"":[{""itemGuid"":""aafce615-6c30-48c4-a29e-3c5b3266748f"",""count"":612}],""GrabInventoryItems"":{""itemGuid"":""00000000-0000-0000-0000-000000000000"",""count"":0},""EquipmentInventoryItems"":[{""itemGuid"":""4c5fefbd-60a4-42ea-b70a-38a83b96e25e"",""count"":1}],""SelectedEquipmentIndex"":0}
          ],
          ""entities"": [
            {""InstanceId"":1,""Type"":""va:Player"",""X"":500,""Y"":13.4,""Z"":500},
            {""InstanceId"":1623179277,""Type"":""va:Player"",""X"":485,""Y"":13.4,""Z"":442}
          ],
          ""playerRidingStates"": [{""PlayerId"":1623179277,""RidableType"":""train"",""IdentifierState"":""{}"",""SeatIndex"":0}],
          ""hotbarAssignments"": [{""PlayerId"":1623179277,""Assignments"":[]}],
          ""remainingPlacementCounts"": [{""PlayerId"":1,""Entries"":[]}],
          ""constructionPayers"": [{""BlockInstanceId"":7,""PlayerId"":1623179277}],
          ""miningCooldowns"": [{""playerId"":1623179277,""lastAttackTick"":10}]
        }";

        [Test]
        public void 旧IDは昇順で1からの連番に振り直され全節が書き換わるTest()
        {
            var save = Convert(JObject.Parse(TesterLikeSave));

            Assert.AreEqual(1, (int)save["playerInventory"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["playerInventory"][1]["PlayerId"]);
            Assert.AreEqual(2L, (long)save["entities"][1]["InstanceId"]);
            Assert.AreEqual(2, (int)save["playerRidingStates"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["hotbarAssignments"][0]["PlayerId"]);
            Assert.AreEqual(1, (int)save["remainingPlacementCounts"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["constructionPayers"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["miningCooldowns"][0]["playerId"]);
        }

        [Test]
        public void 全員持ち主未定で持ち物総数最大が候補になるTest()
        {
            var players = Convert(JObject.Parse(TesterLikeSave))["players"];

            Assert.AreEqual(3, (int)players["nextPlayerId"]);
            Assert.AreEqual(2, (int)players["claimCandidatePlayerId"]);
            Assert.AreEqual(2, ((JArray)players["entries"]).Count);
            foreach (var entry in (JArray)players["entries"]) Assert.AreEqual(JTokenType.Null, entry["identity"].Type);
        }

        [Test]
        public void 持ち物が同数ならスポーンから遠い方が候補Test()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["playerInventory"][1]["MainInventoryItems"] = new JArray();
            var players = Convert(save)["players"];

            Assert.AreEqual(2, (int)players["claimCandidatePlayerId"], "同数(装備1)なら遠い旧1623179277");
        }

        [Test]
        public void 持ち物も距離も同じならIDが小さい方が候補Test()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["playerInventory"][1]["MainInventoryItems"] = new JArray();
            save["entities"][1]["X"] = 500;
            save["entities"][1]["Z"] = 500;

            Assert.AreEqual(1, (int)Convert(save)["players"]["claimCandidatePlayerId"]);
        }

        [Test]
        public void プレイヤーが居ないセーブは空の表で候補無しTest()
        {
            var players = Convert(JObject.Parse(@"{""setting"":{""SpawnX"":0,""SpawnY"":0,""SpawnZ"":0},""playerInventory"":[],""entities"":[]}"))["players"];

            Assert.AreEqual(1, (int)players["nextPlayerId"]);
            Assert.AreEqual(JTokenType.Null, players["claimCandidatePlayerId"].Type);
            Assert.AreEqual(0, ((JArray)players["entries"]).Count);
        }

        [Test]
        public void 既にplayers節があるセーブは変換不能として返るTest()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["players"] = new JObject();
            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("players", result.FailureReason);
        }

        [Test]
        public void 節が配列でないセーブは変換不能として返るTest()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["hotbarAssignments"] = new JObject();

            Assert.IsFalse(new SaveMigrationStepV2ToV3().Migrate(save).IsConverted);
        }

        [Test]
        public void FromVersionは2であるTest()
        {
            Assert.AreEqual(2, new SaveMigrationStepV2ToV3().FromVersion);
        }

        private static JObject Convert(JObject save)
        {
            var result = new SaveMigrationStepV2ToV3().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            return result.Save;
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigrationStepV2ToV3Test"`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 3: 変換を実装する**

`V2ToV3/PlayerIdRenumbering.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V2ToV3
{
    // 旧ランダムIDを集めて昇順に1..Nへ写す表を作り、プレイヤーIDを持つ7節へ適用する
    // Collects legacy random ids, maps them ascending to 1..N, and applies the map to the seven player-keyed sections
    public static class PlayerIdRenumbering
    {
        private const string PlayerEntityType = "va:Player";

        // 節名とID欄名。miningCooldowns だけ小文字キー
        // Section name and id key; only miningCooldowns uses a lowercase key
        private static readonly (string section, string key)[] PlayerIdSections =
        {
            ("playerInventory", "PlayerId"),
            ("playerRidingStates", "PlayerId"),
            ("hotbarAssignments", "PlayerId"),
            ("remainingPlacementCounts", "PlayerId"),
            ("constructionPayers", "PlayerId"),
            ("miningCooldowns", "playerId"),
        };

        public static bool TryBuildMap(JObject save, out Dictionary<long, int> map, out string reason)
        {
            map = null;
            var oldIds = new SortedSet<long>();
            foreach (var (section, key) in PlayerIdSections)
            {
                if (!TryCollect(section, key, false, oldIds, out reason)) return false;
            }
            if (!TryCollect("entities", "InstanceId", true, oldIds, out reason)) return false;

            map = new Dictionary<long, int>();
            var next = 1;
            foreach (var oldId in oldIds) map[oldId] = next++;
            reason = null;
            return true;

            #region Internal

            bool TryCollect(string section, string key, bool onlyPlayerEntities, SortedSet<long> into, out string collectReason)
            {
                collectReason = null;
                var token = save[section];
                if (token == null || token.Type == JTokenType.Null) return true;
                if (token is not JArray array)
                {
                    collectReason = $"{section} が配列でない";
                    return false;
                }
                foreach (var element in array)
                {
                    if (element is not JObject obj) continue;
                    if (onlyPlayerEntities && (string)obj["Type"] != PlayerEntityType) continue;
                    if (obj[key]?.Type != JTokenType.Integer)
                    {
                        collectReason = $"{section} の {key} が整数でない: {obj}";
                        return false;
                    }
                    into.Add((long)obj[key]);
                }
                return true;
            }

            #endregion
        }

        public static void Apply(JObject save, Dictionary<long, int> map)
        {
            foreach (var (section, key) in PlayerIdSections) Rewrite(section, key, false);
            Rewrite("entities", "InstanceId", true);

            #region Internal

            void Rewrite(string section, string key, bool onlyPlayerEntities)
            {
                if (save[section] is not JArray array) return;
                foreach (var obj in array.OfType<JObject>())
                {
                    if (onlyPlayerEntities && (string)obj["Type"] != PlayerEntityType) continue;
                    obj[key] = map[(long)obj[key]];
                }
            }

            #endregion
        }
    }
}
```
（`TryBuildMap` で全要素の型検査を済ませてから `Apply` するので、`Apply` は失敗しない。）

`V2ToV3/PlayerClaimCandidateSelector.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V2ToV3
{
    // 持ち物総数が最大の旧プレイヤーを結びつけ候補に選ぶ。同数はスポーンから遠い方、次に新IDが小さい方（ユーザー裁定 2026-09-27）
    // Picks the legacy player with the most items; ties go to the one farther from spawn, then the smaller new id (user ruling 2026-09-27)
    public static class PlayerClaimCandidateSelector
    {
        public static int? Select(JObject save, Dictionary<long, int> map)
        {
            if (map.Count == 0) return null;

            var spawn = save["setting"] as JObject;
            var spawnX = (double?)spawn?["SpawnX"] ?? 0;
            var spawnZ = (double?)spawn?["SpawnZ"] ?? 0;

            return map.Values
                .Select(newId => (newId, items: CountItems(newId), distance: DistanceFromSpawn(newId)))
                .OrderByDescending(c => c.items)
                .ThenByDescending(c => c.distance)
                .ThenBy(c => c.newId)
                .First().newId;

            #region Internal

            // 変換後の節を読むので新IDで引く。スタックは itemGuid/count 形
            // Reads the already-renumbered sections by new id; stacks are itemGuid/count
            long CountItems(int newId)
            {
                var inventory = (save["playerInventory"] as JArray)?.OfType<JObject>().FirstOrDefault(p => (int)p["PlayerId"] == newId);
                if (inventory == null) return 0;
                var stacks = new List<JToken>();
                if (inventory["MainInventoryItems"] is JArray main) stacks.AddRange(main);
                if (inventory["EquipmentInventoryItems"] is JArray equipment) stacks.AddRange(equipment);
                if (inventory["GrabInventoryItems"] is JObject grab) stacks.Add(grab);
                return stacks.OfType<JObject>().Sum(stack => (long?)stack["count"] ?? 0);
            }

            double DistanceFromSpawn(int newId)
            {
                var entity = (save["entities"] as JArray)?.OfType<JObject>()
                    .FirstOrDefault(e => (string)e["Type"] == "va:Player" && (long)e["InstanceId"] == newId);
                if (entity == null) return 0;
                var dx = ((double?)entity["X"] ?? spawnX) - spawnX;
                var dz = ((double?)entity["Z"] ?? spawnZ) - spawnZ;
                return dx * dx + dz * dz;
            }

            #endregion
        }
    }
}
```

`SaveMigrationStepV2ToV3.cs`:
```csharp
using System.Linq;
using Game.SaveLoad.Migration.Steps.V2ToV3;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // 版2→3: 旧ランダムなプレイヤーIDを連番へ振り直し、持ち主未定の players 節を作る（ADR 0073）
    // V2→V3: renumbers legacy random player ids and builds an all-unclaimed players section (ADR 0073)
    public sealed class SaveMigrationStepV2ToV3 : ISaveMigrationStep
    {
        public int FromVersion => 2;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 版2に players 節は無い。あるなら想定外の形なので変換しない
            // V2 has no players section; its presence is an unexpected shape, so refuse
            if (save["players"] != null) return SaveMigrationStepResult.Failed("版2のセーブに players 節が既にある");

            if (!PlayerIdRenumbering.TryBuildMap(save, out var map, out var reason)) return SaveMigrationStepResult.Failed(reason);
            PlayerIdRenumbering.Apply(save, map);

            // 候補は振り直し後の節から選ぶ
            // Choose the candidate from the renumbered sections
            var candidate = PlayerClaimCandidateSelector.Select(save, map);
            var entries = new JArray(map.Values.OrderBy(id => id).Select(id => new JObject { ["playerId"] = id, ["identity"] = null }));
            save["players"] = new JObject
            {
                ["nextPlayerId"] = map.Count + 1,
                ["claimCandidatePlayerId"] = candidate.HasValue ? candidate.Value : null,
                ["entries"] = entries,
            };

            Debug.Log($"セーブを版2から版3へ変換しました。プレイヤーID振り直し={map.Count}件 結びつけ候補={(candidate.HasValue ? candidate.Value.ToString() : "なし")}");
            return SaveMigrationStepResult.Converted(save);
        }
    }
}
```
（`["claimCandidatePlayerId"] = candidate.HasValue ? candidate.Value : null` がコンパイルしない場合は `candidate.HasValue ? new JValue(candidate.Value) : JValue.CreateNull()` と書く。）

- [ ] **Step 4: `WorldSaveAllInfo`・保存・復元・新規ワールド・剪定分類・DI を配線する**

`WorldSaveAllInfo.cs`:
```csharp
        public const int CurrentVersion = 3;
        ...
        [JsonProperty("players")] public PlayersSaveJsonObject Players { get; }
```
コンストラクタ末尾に `PlayersSaveJsonObject players` を足し `Players = players;`（`MiningCooldowns` と同じく null のまま受け、欠損を検出させる）。`AssembleSaveJsonText.Capture()` の `new WorldSaveAllInfo(...)` 末尾へ `_playerIdentityRegistry.GetSaveJsonObject()`。コンストラクタ注入は Hotbar（`AssembleSaveJsonText.cs:55`）の隣に `PlayerIdentityRegistry playerIdentityRegistry`。

`WorldSaveDataRestorer.cs` の `ThrowIfRequiredFieldMissing` に:
```csharp
            if (save.Players == null) missing.Add("players");
```
（既存の書き方に合わせる。`missing` の名前は既存コードに従う。）`LoadPlayerInventory` の直前に:
```csharp
            // 身元の対応表はプレイヤー状態より先に戻す
            // Restore the identity table before any player state
            _playerIdentityRegistry.Load(save.Players);
```
`WorldLoaderFromJson.WorldInitialize` に `_playerIdentityRegistry.InitializeForNewWorld();`。`MissingMasterPruner.SectionsWithoutMasterPruning` の先頭グループに `"players"`。DI は File 節のとおり。

`SaveOrderCanonicalTest.cs` に、登録順と逆に `Assign` した3身元の `players.entries` が playerId 昇順で出るケースを追加（既存ケースと同じく `AssembleSaveJsonText` の JSON を読む）。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`（ErrorCount 0）→ `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoadPreparer|SaveOrderCanonical|SaveTickAndRandomState|MissingMasterSectionCoverage|WorldSave|PlayerIdentity"`
Expected: 全件 PASS。

- [ ] **Step 6: 実ロード確認（スキル必須）**

`.agents/skills/moorestech-save-migration/references/load_test.cs` を `uloop execute-dynamic-code` で実行し、テスターのスナップショット（`../moorestech_logs/harness/playtest/reports/76561198319362448/20260926_145808_e35a893b/snapshots/tick_21742.json`）を複製したワールドで `LOAD OK` を確認。ログに `セーブを版2から版3へ変換しました。プレイヤーID振り直し=2件 結びつけ候補=2` が出ること。

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_server
git commit -m "feat(save): players節を追加しV2→V3でプレイヤーIDを連番へ振り直す (ADR 0073)"
```

---

### Task 3: ハンドシェイクで身元から採番し接続へ紐づける

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Handshake/HandshakeRejection.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`（`Game.PlayerIdentity`）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/InitialHandshakeProtocolTest.cs`・`TrainResyncProtocolTest.cs`（身元で送る形へ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/InitialHandshakeIdentityTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/Util/BoundPacketContext.cs`

**Interfaces:**
- Consumes: `IPlayerIdentityRegistry.Assign/TryGetPlayerId`、`PlayerIdentityText.IsValid`
- Produces:
  - `enum HandshakeRejection { None = 0, InvalidIdentity = 1, AlreadyConnected = 2, ConnectionClosed = 3 }`（namespace `Server.Protocol.PacketResponse.Handshake`）
  - `RequestInitialHandshakeMessagePack(string playerIdentity)`: `[Key(2)] string PlayerIdentity`（`PlayerId`・`PlayerName` は削除）
  - `ResponseInitialHandshakeMessagePack`: 既存 Key 2〜8 に加え `[Key(9)] int PlayerId`・`[Key(10)] HandshakeRejection Rejection`。`static ResponseInitialHandshakeMessagePack Rejected(HandshakeRejection rejection)`
  - テスト用: `static PacketResponseContext BoundPacketContext.Handshake(PacketResponseCreator creator, string identity, out int playerId)`（実ハンドシェイクで紐づける）と `static PacketResponseContext BoundPacketContext.Bind(int playerId)`（`TryBindPlayerId` だけ。ハンドシェイクを経ないテスト用）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InitialHandshakeIdentityTest
    {
        private const string SteamA = "steam:76561198319362448";

        [Test]
        public void 新しいワールドの最初の身元はID1を受け取り同じ身元は同じIDTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var first = Handshake(packet, new PacketResponseContext(new CapturedEventSink()), SteamA);
            Assert.AreEqual(HandshakeRejection.None, first.Rejection);
            Assert.AreEqual(1, first.PlayerId);
        }

        [Test]
        public void 接続中の身元の二重接続は後から来た方を拒否するTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Handshake(packet, new PacketResponseContext(new CapturedEventSink()), SteamA);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("接続中"));
            var second = Handshake(packet, new PacketResponseContext(new CapturedEventSink()), SteamA);
            Assert.AreEqual(HandshakeRejection.AlreadyConnected, second.Rejection);
        }

        [Test]
        public void 書式不正の身元は拒否するTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("身元"));
            var response = Handshake(packet, new PacketResponseContext(new CapturedEventSink()), "1");
            Assert.AreEqual(HandshakeRejection.InvalidIdentity, response.Rejection);
        }

        [Test]
        public void 切断済みの接続は初期装備も登録もせず拒否するTest()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var context = new PacketResponseContext(new CapturedEventSink());
            context.MarkClosedAndGetPlayerId();

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("切断"));
            var response = Handshake(packet, context, SteamA);
            Assert.AreEqual(HandshakeRejection.ConnectionClosed, response.Rejection);

            var checker = provider.GetService(typeof(Game.PlayerConnection.IPlayerConnectionChecker)) as Game.PlayerConnection.IPlayerConnectionChecker;
            Assert.IsFalse(checker.IsConnected(1));
        }

        private static InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack Handshake(PacketResponseCreator packet, PacketResponseContext context, string identity)
        {
            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(identity));
            return MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(packet.GetPacketResponse(payload, context)[0]);
        }
    }
}
```
（`CapturedEventSink` は `Tests/CombinedTest/Server/PacketTest/Event/EventTestUtil.cs` の既存型。`TestModDirectory`・DI生成の呼び方は既存 `InitialHandshakeProtocolTest.cs` の書き方に合わせる。ずれていたら既存テストの形を正とする。）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InitialHandshakeIdentityTest"`
Expected: コンパイルエラー

- [ ] **Step 3: 実装する**

`Handshake/HandshakeRejection.cs`:
```csharp
namespace Server.Protocol.PacketResponse.Handshake
{
    // ハンドシェイクの拒否理由。クライアントが表示文言へ写す
    // Handshake rejection reasons; the client maps them to display text
    public enum HandshakeRejection
    {
        None = 0,
        InvalidIdentity = 1,
        AlreadyConnected = 2,
        ConnectionClosed = 3,
    }
}
```

`InitialHandshakeProtocol.GetResponse` を次の順序へ置き換える（`CreateResponse`・`GetPlayerPosition` のローカル関数は `data.PlayerId` を `playerId` 変数に置き換えて残す）:
```csharp
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload);

            // 身元の書式を検査する。不正なら何も登録しない
            // Validate the identity format; register nothing when invalid
            if (!PlayerIdentityText.IsValid(data.PlayerIdentity, out var invalidReason))
            {
                Debug.LogWarning($"[InitialHandshake] 身元が不正なため拒否: {invalidReason}");
                return ResponseInitialHandshakeMessagePack.Rejected(HandshakeRejection.InvalidIdentity);
            }

            // 接続中の身元は後から来た方を拒否する（採番前に見る）
            // Refuse the later connection of an identity already connected (checked before assignment)
            if (_playerIdentityRegistry.TryGetPlayerId(data.PlayerIdentity, out var existingId) && _connectionRegistry.IsConnected(existingId))
            {
                Debug.LogWarning($"[InitialHandshake] 身元{data.PlayerIdentity}(プレイヤー{existingId})は接続中のため後からの接続を拒否");
                return ResponseInitialHandshakeMessagePack.Rejected(HandshakeRejection.AlreadyConnected);
            }

            var playerId = _playerIdentityRegistry.Assign(data.PlayerIdentity).PlayerId;

            // 紐づけに失敗したら切断済み。登録も付与もしない
            // A failed bind means the connection closed; neither register nor grant
            if (!context.TryBindPlayerId(playerId))
            {
                Debug.LogWarning($"[InitialHandshake] ハンドシェイク中に切断されたためプレイヤー{playerId}の接続を拒否");
                return ResponseInitialHandshakeMessagePack.Rejected(HandshakeRejection.ConnectionClosed);
            }
            _connectionRegistry.Register(playerId);
            _eventProtocolProvider.RegisterPlayer(playerId, context.EventSink);

            // 初期装備は新規プレイヤーの接続確定時にだけ配る
            // Grant the initial equipment only when a brand-new player connects
            _playerInventoryDataStore.GrantInitialEquipmentIfNewPlayer(playerId);

            return CreateResponse();
            ...
```
`CreateResponse()` の `return new ResponseInitialHandshakeMessagePack(playerPos, ridingTarget, ridingSeatIndex, itemStackLevels, hotbarAssignments, remainingPlacementCounts);` に `playerId` を足す（コンストラクタ末尾引数 `int playerId`、`Rejection = HandshakeRejection.None`）。

リクエスト・レスポンスのクラス:
```csharp
        [MessagePackObject]
        public class RequestInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public string PlayerIdentity { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestInitialHandshakeMessagePack() { }

            public RequestInitialHandshakeMessagePack(string playerIdentity)
            {
                Tag = ProtocolTag;
                PlayerIdentity = playerIdentity;
            }
        }
```
（既存クラスの `{ get; set; }` 形・`[Obsolete]` 空コンストラクタの書き方に揃える。レスポンスは Key(9) `int PlayerId`・Key(10) `HandshakeRejection Rejection` を追加し、`Rejected` は他フィールドを null/空配列、`RidingSeatIndex = -1` で作る。）

コンストラクタで `_playerIdentityRegistry = serviceProvider.GetService<IPlayerIdentityRegistry>();`。

`Tests/Util/BoundPacketContext.cs`:
```csharp
using MessagePack;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;

namespace Tests.Util
{
    // テストで紐づけ済みの接続コンテキストを作る。実ハンドシェイク版と紐づけのみ版
    // Builds bound connection contexts for tests: via a real handshake, or bind-only
    public static class BoundPacketContext
    {
        public static PacketResponseContext Handshake(PacketResponseCreator creator, string identity, out int playerId)
        {
            var context = new PacketResponseContext(null);
            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(identity));
            var response = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(creator.GetPacketResponse(payload, context)[0]);
            NUnit.Framework.Assert.AreEqual(HandshakeRejection.None, response.Rejection, $"テスト用ハンドシェイクが拒否された: {response.Rejection}");
            playerId = response.PlayerId;
            return context;
        }

        public static PacketResponseContext Bind(int playerId)
        {
            var context = new PacketResponseContext(null);
            context.TryBindPlayerId(playerId);
            return context;
        }
    }
}
```

- [ ] **Step 4: 既存のハンドシェイク系テストを身元へ置き換える**

`InitialHandshakeProtocolTest.cs:114-116`・`TrainResyncProtocolTest.cs:21-27` の `new RequestInitialHandshakeMessagePack(0, ...)` 等を `new RequestInitialHandshakeMessagePack("steam:1")` にし、playerId 前提の比較はレスポンスの `PlayerId` を使う。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop compile ...`（0件）→ `uloop run-tests ... --filter-value "InitialHandshake|TrainResync"`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_server
git commit -m "feat(handshake): 身元からプレイヤーIDを採番し接続へ紐づけ、二重接続・不正身元・切断済みを拒否する (ADR 0073)"
```

---

### Task 4: 未紐づけ接続の要求を振り分け口で拒否する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:78`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PacketResponseCreatorUnboundGateTest.cs`

**Interfaces:**
- Consumes: `PacketResponseContext.PlayerId`、`InitialHandshakeProtocol.ProtocolTag`
- Produces: 以後、全プロトコルの `GetResponse` は `context.PlayerId.HasValue` を前提にしてよい（Task 5・6 が依存）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PacketResponseCreatorUnboundGateTest
    {
        [Test]
        public void 未紐づけ接続のハンドシェイク以外の要求は無視してログを出すTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var payload = MessagePackSerializer.Serialize(new GetChallengeInfoProtocol.RequestChallengeMessagePack());

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("未紐づけ"));
            var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));

            Assert.AreEqual(0, responses.Count);
        }

        [Test]
        public void 紐づけ済み接続の要求は処理されるTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var payload = MessagePackSerializer.Serialize(new GetChallengeInfoProtocol.RequestChallengeMessagePack());

            var responses = packet.GetPacketResponse(payload, Tests.Util.BoundPacketContext.Bind(1));

            Assert.AreEqual(1, responses.Count);
        }
    }
}
```
（`GetChallengeInfoProtocol` のリクエスト型名は実ファイルで確認して合わせる。プレイヤーIDを持たず応答を返す既存プロトコルなら何でもよい。）

- [ ] **Step 2: 実行して失敗を確認する**（1件目が FAIL: 応答が返る）

- [ ] **Step 3: 実装する**

`PacketResponseCreator.GetPacketResponse` の、`request.Tag` を読んだ直後・辞書引きの前へ:
```csharp
            // 身元の確定前に届いたハンドシェイク以外の要求は、どのプレイヤーの操作か決まらないので捨てる
            // Drop any non-handshake request before identity is settled, since no player owns it yet
            if (request.Tag != InitialHandshakeProtocol.ProtocolTag && !context.PlayerId.HasValue)
            {
                Debug.LogWarning($"[PacketResponseCreator] 未紐づけの接続からの要求を無視しました tag:{request.Tag}");
                return new List<byte[]>();
            }
```

- [ ] **Step 4: 既存テストを紐づけ済みコンテキストへ移す**

`Tests/` 配下で `new PacketResponseContext(null)` を使って**ハンドシェイク以外**を送っている全呼び出し（調査時点70ファイル）を `BoundPacketContext.Bind(<そのテストが使っていたplayerId>)` に置き換える。ペイロードの playerId と同じ値を渡すこと（この時点ではまだペイロードにもIDがあるので、両者一致で従来どおり動く）。確認:
```bash
grep -rln "new PacketResponseContext(null)" moorestech_server/Assets/Scripts/Tests | xargs grep -L "RequestInitialHandshakeMessagePack"
```
の出力が、`BoundPacketContext.cs` 自身と、未紐づけを意図して検査するテスト（本タスクの Gate テスト）だけになるまで置き換える。

- [ ] **Step 5: 全サーバーテストで回帰が無いことを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\." --timeout-seconds 1500`
Expected: 本ブランチ着手前（Task 1 開始前に同じコマンドで取った基準）と同じ失敗集合。新しい失敗が0件。

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_server
git commit -m "feat(protocol): 未紐づけ接続のハンドシェイク以外の要求を振り分け口で拒否する (ADR 0073)"
```

---

### Task 5: パケットログに送り手のプレイヤーIDを記録し、再生で紐づける

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/ReceivedPacketRecord.cs`（`int PlayerId` を追加）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/ReceivedPacketLog.cs:64-86`（`Append(ulong tick, int playerId, byte[] payload)`、書き込みを tick→playerId→length→payload）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/ReceivedPacketLogReader.cs`（`ReadInt32()` で playerId を読む）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/ReceiveQueueProcessor.cs:49`（`_receivedPacketLog.Append(GameUpdater.CurrentTick, _packetResponseContext.PlayerId ?? 0, packet);`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Replay/SnapshotReplayer.cs:57-78`（記録IDごとの紐づけ済みコンテキスト）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Replay/PacketLogJsonDumper.cs`（出力行に `playerId`）
- Modify: テスト `Tests/UnitTest/Game/SaveLoad/ReceivedPacketLogTest.cs`・`Tests/CombinedTest/Server/Replay/PacketLogJsonDumperTest.cs`・`Tests/CombinedTest/Server/Replay/SnapshotReplayDeterminismTest.cs:246`

**Interfaces:**
- Produces: `ReceivedPacketRecord(ulong tick, int playerId, byte[] payload)`、`ReceivedPacketLog.Append(ulong tick, int playerId, byte[] payload)`

- [ ] **Step 1: 失敗するテストを書く** — `ReceivedPacketLogTest` に追加:

```csharp
        [Test]
        public void 送り手のプレイヤーIDが記録され読み戻せるTest()
        {
            var directory = CreateTempDirectory();
            var log = CreateLog(directory);
            log.StartSegment(0);
            log.Append(1, 2, new byte[] { 1 });
            log.Append(2, 0, new byte[] { 2 });
            log.Close();

            var records = ReceivedPacketLogReader.ReadAll(SegmentFiles(directory));
            Assert.AreEqual(2, records[0].PlayerId);
            Assert.AreEqual(0, records[1].PlayerId, "未紐づけは0");
        }
```
（`CreateTempDirectory`/`CreateLog`/`StartSegment`/`SegmentFiles` は同テストファイル内の既存の組み立て方に合わせる。既存テストの `Append(tick, payload)` 呼び出しはすべて `Append(tick, 1, payload)` へ。）

- [ ] **Step 2: 実行して失敗を確認する**（コンパイルエラー）

- [ ] **Step 3: 実装する**

`ReceivedPacketLog.Append` の書き込み部:
```csharp
                    _writer.Write(tick);
                    _writer.Write(playerId);
                    _writer.Write(payload.Length);
                    _writer.Write(payload);
```
`ReceivedPacketLogReader`:
```csharp
                    var tick = reader.ReadUInt64();
                    var playerId = reader.ReadInt32();
                    var length = reader.ReadInt32();
                    ...
                    result.Add(new ReceivedPacketRecord(tick, playerId, payload));
```
`SnapshotReplayer` の `var context = new PacketResponseContext(null);` を置き換え:
```csharp
            // 記録された送り手ごとに紐づけ済みの接続を用意する。0は未紐づけ（ハンドシェイク前）
            // Prepare a bound connection per recorded sender; 0 is unbound (before handshake)
            var contexts = new Dictionary<int, PacketResponseContext>();
            PacketResponseContext ContextFor(int playerId)
            {
                if (contexts.TryGetValue(playerId, out var existing)) return existing;
                var created = new PacketResponseContext(null);
                if (playerId != 0) created.TryBindPlayerId(playerId);
                contexts[playerId] = created;
                return created;
            }
```
（ローカル関数は既存の `#region Internal` へ入れる。）Enqueue を `new ReplayPacketEntry(packetResponseCreator, ContextFor(records[next].PlayerId), records[next].Payload)` に。

`PacketLogJsonDumper` の1行JSONに `["playerId"] = record.PlayerId` を追加。

- [ ] **Step 4: テストを実行する**

Run: `uloop run-tests ... --filter-value "ReceivedPacketLog|PacketLogJsonDumper|SnapshotReplay|BugReportBundle"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_server
git commit -m "feat(replay): パケットログへ送り手のプレイヤーIDを記録し再生で接続へ紐づける (ADR 0073)"
```

---

### Task 6: 全プロトコルのペイロードから playerId を外す（サーバー）

**Files:** 下表の27ファイル、`moorestech_server/Assets/Scripts/Server.Util/MessagePack/InventoryIdentifierMessagePack.cs`、`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/Resolver/*.cs`（`IInventoryIdentifierResolver`・Main/Grab/Equipment/Block/Train・`OpenableInventoryResolver`）、`InventoryItemMoveProtocol.cs`・`SortInventoryProtocol.cs`・`InventoryRequestProtocol.cs`・`InventoryItemMoveRejectionReporter.cs`、それらを呼ぶ全テスト。

**Interfaces:**
- Consumes: Task 4 の前提（`context.PlayerId.HasValue`）
- Produces: 各リクエスト MessagePack のコンストラクタ／ファクトリから `int playerId` 引数が消える（Task 7 のクライアントが依存）。`IInventoryIdentifierResolver.Resolve(InventoryIdentifierMessagePack identifier, int requesterPlayerId)`、`OpenableInventoryResolver.Resolve(InventoryIdentifierMessagePack identifier, int requesterPlayerId)`、`InventoryIdentifierMessagePack.CreateMainMessage()`/`CreateGrabMessage()`/`CreateEquipmentMessage()`（引数なし）

**変換規則（全行共通）:**
1. リクエストクラスから `PlayerId` フィールドを削除し、それより後ろの `[Key(n)]` を1ずつ詰める。コンストラクタ・`Create*` ファクトリから `int playerId` 引数を削除。
2. `GetResponse` 内の `data.PlayerId`（変数名は各ファイルのもの）を、先頭で `var playerId = context.PlayerId.Value;` として置き換える。
3. レスポンス・イベントの表示用 playerId（`PlayerInventoryResponseProtocolMessagePack.PlayerId`・`RidingStateEventMessagePack`・`ResearchCompleteEventMessagePack`・`CraftCompletedEventMessagePack`）は残す（受け手が誰のことか判別するため）。

例（`SetSelectedEquipmentIndexProtocol.cs`）:
```csharp
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SetSelectedEquipmentIndexMessagePack>(payload);

            // 送り手は接続に紐づいたプレイヤー
            // The sender is the player bound to this connection
            var playerId = context.PlayerId.Value;
            _playerInventoryDataStore.GetInventoryData(playerId).EquipmentInventory.SetSelectedIndex(data.SelectedIndex);
            return null;
        }

        [MessagePackObject]
        public class SetSelectedEquipmentIndexMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int SelectedIndex { get; set; }
            ...
            public SetSelectedEquipmentIndexMessagePack(int selectedIndex)
            {
                Tag = ProtocolTag;
                SelectedIndex = selectedIndex;
            }
        }
```
（上は形の例。実ファイルのメソッド名・フィールド名が違えば実ファイルを正とする。）

| # | ファイル（`Server.Protocol/PacketResponse/`） | クラス | 旧PlayerId Key | 後続Keyの詰め |
|---|---|---|---|---|
| 1 | `RequestWorldDataProtocol.cs` | `RequestWorldDataMessagePack` | 2 | 後続なし |
| 2 | `PlayerInventoryResponseProtocol.cs` | `RequestPlayerInventoryProtocolMessagePack` | 2 | 後続なし（レスポンスの Key2 PlayerId は残す） |
| 3 | `SetPlayerCoordinateProtocol.cs` | `PlayerCoordinateSendProtocolMessagePack` | 2 | 3→2 |
| 4 | `SetSelectedEquipmentIndexProtocol.cs` | `SetSelectedEquipmentIndexMessagePack` | 2 | 3→2 |
| 5 | `PlaceBlockProtocol.cs` | `SendPlaceBlockProtocolMessagePack` | 2 | 3→2 |
| 6 | `RemoveBlockProtocol.cs` | `RemoveBlockProtocolMessagePack` | 2 | 以降を1ずつ |
| 7 | `CompleteBaseCampProtocol.cs` | `CompleteBaseCampProtocolMessagePack` | 2 | 以降を1ずつ（PlayerId は未使用だったので削除のみ） |
| 8 | `SubscribeInventoryProtocol.cs` | `SubscribeInventoryRequestMessagePack` | 2 | 未使用の Key3 も削除し 4→2, 5→3 |
| 9 | `OneClickCraft.cs` | `RequestOneClickCraftProtocolMessagePack` | 2 | 3→2 |
| 10 | `MiningProtocol.cs` | `MiningProtocolMessagePack` | 2 | 以降を1ずつ |
| 11 | `GetResearchInfoProtocol.cs` | `RequestResearchInfoMessagePack` | 2 | 後続なし |
| 12 | `CompleteResearchProtocol.cs` | `RequestCompleteResearchMessagePack` | 2 | 3→2 |
| 13 | `RegisterPlayedSkitProtocol.cs` | `RegisterPlayedSkitMessagePack` | 2 | 3→2 |
| 14 | `HotbarProtocol.cs` | `HotbarProtocolMessagePack` | 2 | 以降を1ずつ |
| 15 | `RideActionProtocol.cs` | `RequestRideActionMessagePack` | 2 | 以降を1ずつ |
| 16 | `TrainCarRidingInputProtocol.cs` | `TrainCarRidingInputMessagePack` | 2 | 以降を1ずつ |
| 17 | `RemoveTrainCarProtocol.cs` | `RemoveTrainCarRequestMessagePack` | 3 | 以降を1ずつ |
| 18 | `PlaceTrainCarOnRailProtocol.cs` | `PlaceTrainOnRailRequestMessagePack` | 4 | 以降を1ずつ |
| 19 | `AttachTrainCarToUnitProtocol.cs` | `AttachTrainCarToUnitRequestMessagePack` | 5 | 6→5, 7→6 |
| 20 | `ElectricWireDisconnectProtocol.cs` | `ElectricWireDisconnectRequest` | 4 | 以降を1ずつ |
| 21 | `ElectricWireExtendProtocol.cs` | `ElectricWireExtendRequest` | 6 | 7→6, 8→7 |
| 22 | `GearChainConnectionEditProtocol.cs` | `GearChainConnectionEditRequest` | 5 | 6→5 |
| 23 | `GearChainPoleExtendProtocol.cs` | `GearChainPoleExtendRequest` | 5 | 6→5, 7→6 |
| 24 | `RailConnectionEditProtocol.cs` | `RailConnectionEditRequest` | 7 | 8→7 |
| 25 | `RailConnectWithPlacePierProtocol.cs` | `RailConnectWithPlacePierRequest` | 5 | 6→5, 7→6 |
| 26 | `MachineRecipeSelectionProtocol.cs` | `MachineRecipeSelectionRequest` | 5 | 後続なし |
| 27 | `InitialHandshakeProtocol.cs` | （Task 3 で済） | — | — |

`InventoryIdentifierMessagePack`: `[Key(3)] int PlayerId` を削除し後続を詰める。`CreateMainMessage()`/`CreateGrabMessage()`/`CreateEquipmentMessage()` は引数なしに。Resolver は `Resolve(identifier, requesterPlayerId)` で `requesterPlayerId` を使い、Block/Train は引数を受けて無視する。`InventoryItemMoveProtocol`・`SortInventoryProtocol`・`InventoryRequestProtocol` は `context.PlayerId.Value` を渡す。`InventoryItemMoveRejectionReporter.cs:60` はログに `requesterPlayerId` を出す。

- [ ] **Step 1: 失敗するテストを書く** — 送り手判定が接続由来であることを固定する回帰テストを `SetSelectedEquipmentIndexProtocolTest`（既存が無ければ `Tests/CombinedTest/Server/PacketTest/` に新規）へ:

```csharp
        [Test]
        public void 接続に紐づいたプレイヤーの装備選択だけが変わるTest()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var contextA = BoundPacketContext.Handshake(packet, "steam:1", out var playerA);
            BoundPacketContext.Handshake(packet, "steam:2", out var playerB);

            packet.GetPacketResponse(MessagePackSerializer.Serialize(new SetSelectedEquipmentIndexProtocol.SetSelectedEquipmentIndexMessagePack(1)), contextA);

            var store = provider.GetService<IPlayerInventoryDataStore>();
            Assert.AreEqual(1, store.GetInventoryData(playerA).EquipmentInventory.SelectedIndex);
            Assert.AreEqual(0, store.GetInventoryData(playerB).EquipmentInventory.SelectedIndex);
        }
```
（装備の選択インデックスの取得名は実APIに合わせる。）

- [ ] **Step 2: 実行して失敗を確認する**（コンストラクタ引数が合わずコンパイルエラー）

- [ ] **Step 3: 表の27本と InventoryIdentifier を変換規則どおりに直す**（1ファイルずつ、直したらコンパイル）

- [ ] **Step 4: サーバーテストの呼び出しを直す** — ペイロードへ playerId を渡していた箇所（調査時点50ファイル・103か所）から引数を消し、そのテストの `BoundPacketContext.Bind(id)` と同じIDで検証していることを確認する。確認コマンド:

```bash
grep -rnE "new (SendPlaceBlockProtocolMessagePack|RequestOneClickCraftProtocolMessagePack|MiningProtocolMessagePack|HotbarProtocolMessagePack|RequestRideActionMessagePack|SetSelectedEquipmentIndexMessagePack)\(" moorestech_server/Assets/Scripts/Tests | head
```

- [ ] **Step 5: ペイロードに playerId が残っていないことを確認する**

```bash
grep -nE "\[Key\([0-9]+\)\] public int PlayerId" moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/*.cs moorestech_server/Assets/Scripts/Server.Util/MessagePack/*.cs
```
Expected: `PlayerInventoryResponseProtocolMessagePack`（レスポンス）とイベント用の3クラスだけ。

- [ ] **Step 6: 全サーバーテストで回帰が無いことを確認する**（Task 4 Step 5 と同じコマンド・同じ基準）

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_server
git commit -m "refactor(protocol): 全リクエストからplayerIdを外し接続に紐づいたIDを使う (ADR 0073)"
```

---

### Task 7: クライアント — 身元の解決・ハンドシェイク後の採番・既定IDの撤去

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Identity/LocalPlayerIdentityResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Identity/PlayerStartRefusedException.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Network/API/PlayerHandshakeRejectedException.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeProprieties.cs`（`PlayerId`・`DefaultPlayerId` 削除、`CreateLocalServer()`/`CreateRemoteConnection(ip, port)`/`TryCreateRemoteConnection(ipText, portText, out ..., out ...)`）
- Modify: `LocalGameLauncher.cs:37-38`、`Client.MainMenu/ConnectServer.cs:57-58`、`Client.Playtest/PlaytestWorldBootSession.cs:46`、`Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs:75`、`InitializeScenePipeline.cs:34,113-148`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/ServerConnectionInitializer.cs`（身元解決→接続→ハンドシェイク→`PlayerConnectionSetting` 作成）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs`・`VanillaApiSendOnly.cs`・`VanillaApiWithResponse.cs`（`PlayerConnectionSetting` を受け取らない）
- Modify: `ElectricWireExtendRequestSender.cs`・`Client.WebUiHost/Game/Actions/MachineRecipeSelectionActions.cs`・`InventoryMoveServerDispatcher.cs`・`LocalPlayerInventoryController.cs`（playerId 引数を外す）
- Delete: `moorestech_client/Assets/Scripts/Client.MainMenu/SetPlayerId.cs`（と `.meta`）。MainMenu.unity の "System" 上のコンポーネントは削除前に uloop で外す
- Modify: `moorestech_client/Assets/Scripts/Client.Common/PlayerPrefsKeys.cs`（`PlayerIdKey` 削除）
- Modify: `Localization/localization.csv`（3行追加）
- Modify: クライアントテスト（`InitializeProprietiesTest.cs`・`GeneratedWorldPlayModeSettingsTest.cs`・`SkipSaveLoadPlayModeSettingsTest.cs`・`DirectPlayAlwaysOnCaptureSettingsTest.cs`・`EditModeInPlayingTestUtil.cs:82`・`EditModeInPlayingTestUtilTest.cs:63`・`LocalPlayEmbeddedServerBootTest.cs:178`・`PlacementPacketCapture.cs:45`・`LocalPlayerEquipmentTest.cs:31`・`InventoryMoveServerCoordinateTest.cs:14`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Starter/LocalPlayerIdentityResolverTest.cs`

**Interfaces:**
- Consumes: Task 3 のハンドシェイク型、Task 6 の引数なしリクエスト
- Produces:
  - `static PlayerIdentityResolution LocalPlayerIdentityResolver.Resolve(bool isSteamDistributionBuild, IPlaytestLocalSteamIdReader steamReader, string deviceUniqueIdentifier)`（純関数・テスト対象）
  - `static PlayerIdentityResolution LocalPlayerIdentityResolver.ResolveForThisProcess()`（本番入口）
  - `readonly struct PlayerIdentityResolution { bool Succeeded; string Identity; string RefusalLocalizationKey; string RefusalLogReason; }`（`LocalPlayerIdentityResolver.cs` 内に置く）
  - `class PlayerStartRefusedException : Exception { string LocalizationKey; }`
  - `class PlayerHandshakeRejectedException : Exception { HandshakeRejection Rejection; }`
  - `UniTask<InitialHandshakeResponse> VanillaApiWithResponse.InitialHandShake(string playerIdentity, CancellationToken ct)`（`InitialHandshakeResponse` に `int PlayerId` を追加）
  - `ServerConnectionResult.PlayerConnectionSetting`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.PlaytestReceiver.Steam;
using Client.Starter.Identity;
using NUnit.Framework;

namespace Client.Tests.Starter
{
    public class LocalPlayerIdentityResolverTest
    {
        private sealed class FakeSteamReader : IPlaytestLocalSteamIdReader
        {
            private readonly string _steamId;
            public FakeSteamReader(string steamId) { _steamId = steamId; }

            public bool TryRead(out string steamId, out string failureReason)
            {
                steamId = _steamId;
                failureReason = _steamId == null ? "no steam" : null;
                return _steamId != null;
            }
        }

        [Test]
        public void Steam配布ビルドはsteam身元になるTest()
        {
            var result = LocalPlayerIdentityResolver.Resolve(true, new FakeSteamReader("76561198319362448"), "abc");
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("steam:76561198319362448", result.Identity);
        }

        [Test]
        public void Steam配布ビルドでSteamIDが読めなければ端末値へ落ちず拒否Test()
        {
            var result = LocalPlayerIdentityResolver.Resolve(true, new FakeSteamReader(null), "abc");
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("ui.loading.steamIdentityUnavailable", result.RefusalLocalizationKey);
        }

        [Test]
        public void それ以外は端末値のSHA256小文字16進になるTest()
        {
            var result = LocalPlayerIdentityResolver.Resolve(false, new FakeSteamReader("1"), "abc");
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("device:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Identity);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("n/a")]
        public void 端末値が取れなければ拒否Test(string device)
        {
            var result = LocalPlayerIdentityResolver.Resolve(false, new FakeSteamReader("1"), device);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("ui.loading.deviceIdentityUnavailable", result.RefusalLocalizationKey);
        }
    }
}
```
（`"n/a"` は `SystemInfo.unsupportedIdentifier` の値。実装は定数ではなく `SystemInfo.unsupportedIdentifier` と比較する。テストの `TestCase` は定数しか取れないため値で書く。`Client.Tests` は `Client.PlaytestReceiver` の InternalsVisibleTo を持つ。）

- [ ] **Step 2: 実行して失敗を確認する**（コンパイルエラー）

- [ ] **Step 3: 身元の解決を実装する**

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.PlaytestReceiver.Steam;
using UnityEngine;

namespace Client.Starter.Identity
{
    // 起動ごとのプレイヤー身元を決める。Steam配布ビルドはSteam、それ以外は端末値（ADR 0073）
    // Resolves this process's player identity: Steam on Steam distribution builds, the device value otherwise (ADR 0073)
    public static class LocalPlayerIdentityResolver
    {
        public const string SteamUnavailableKey = "ui.loading.steamIdentityUnavailable";
        public const string DeviceUnavailableKey = "ui.loading.deviceIdentityUnavailable";

        public static PlayerIdentityResolution ResolveForThisProcess()
        {
            // 配布の種別は焼き込み値で決め、実行時のSteamの状態では決めない
            // The build kind comes from the baked value, never from runtime Steam state
            var origin = RepositoryStateProbe.ReadBuildOrigin();
            var isSteamDistribution = origin.Kind == BuildOriginKind.BakedBuild && !string.IsNullOrEmpty(origin.BuildInfo.SteamBuildLabel);
            return Resolve(isSteamDistribution, new PlaytestLocalSteamIdReader(), SystemInfo.deviceUniqueIdentifier);
        }

        public static PlayerIdentityResolution Resolve(bool isSteamDistributionBuild, IPlaytestLocalSteamIdReader steamReader, string deviceUniqueIdentifier)
        {
            if (isSteamDistributionBuild)
            {
                if (steamReader.TryRead(out var steamId, out var failureReason)) return PlayerIdentityResolution.Success("steam:" + steamId);
                return PlayerIdentityResolution.Refused(SteamUnavailableKey, $"Steam配布ビルドでSteamIDを読めないため開始しない: {failureReason}");
            }

            if (string.IsNullOrEmpty(deviceUniqueIdentifier) || deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier)
            {
                return PlayerIdentityResolution.Refused(DeviceUnavailableKey, $"端末の識別子を取得できないため開始しない: '{deviceUniqueIdentifier}'");
            }
            return PlayerIdentityResolution.Success("device:" + Sha256Hex(deviceUniqueIdentifier));

            #region Internal

            // 生の端末識別子をサーバーやセーブへ出さないためハッシュ化する
            // Hash so the raw device identifier never reaches a server or a save
            string Sha256Hex(string text)
            {
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            #endregion
        }
    }

    public readonly struct PlayerIdentityResolution
    {
        public readonly bool Succeeded;
        public readonly string Identity;
        public readonly string RefusalLocalizationKey;
        public readonly string RefusalLogReason;

        private PlayerIdentityResolution(bool succeeded, string identity, string refusalLocalizationKey, string refusalLogReason)
        {
            Succeeded = succeeded;
            Identity = identity;
            RefusalLocalizationKey = refusalLocalizationKey;
            RefusalLogReason = refusalLogReason;
        }

        public static PlayerIdentityResolution Success(string identity) => new(true, identity, null, null);
        public static PlayerIdentityResolution Refused(string localizationKey, string logReason) => new(false, null, localizationKey, logReason);
    }
}
```
（`RepositoryStateProbe`・`BuildOriginKind` の namespace は実ファイル（`Client.Game/InGame/BugReport/RepositoryStateProbe.cs`・`BuildOrigin/BuildOriginReading.cs`）の namespace に合わせる。`PlaytestLocalSteamIdReader` にコンストラクタ引数があれば `PlaytestLaunchProfile.Resolve()` の生成の仕方に合わせる。）

`PlayerStartRefusedException.cs`:
```csharp
using System;

namespace Client.Starter.Identity
{
    // 開始前にプレイヤー身元を決められなかった。表示はローカライズキーで行う
    // The player identity could not be settled before start; display goes through the localization key
    public class PlayerStartRefusedException : Exception
    {
        public readonly string LocalizationKey;

        public PlayerStartRefusedException(string localizationKey, string logReason) : base(logReason)
        {
            LocalizationKey = localizationKey;
        }
    }
}
```

`Client.Network/API/PlayerHandshakeRejectedException.cs`:
```csharp
using System;
using Server.Protocol.PacketResponse.Handshake;

namespace Client.Network.API
{
    // サーバーがハンドシェイクを拒否した
    // The server rejected the handshake
    public class PlayerHandshakeRejectedException : Exception
    {
        public readonly HandshakeRejection Rejection;

        public PlayerHandshakeRejectedException(HandshakeRejection rejection) : base($"ハンドシェイクが拒否されました: {rejection}")
        {
            Rejection = rejection;
        }
    }
}
```

- [ ] **Step 4: 接続の流れを組み替える**

`ServerConnectionInitializer.RunAsync()` の先頭（接続より前。Steam 不在で内蔵サーバーを起動しないため）:
```csharp
            // 身元が決まらなければ接続もサーバー起動もしない
            // Neither connect nor boot a server when the identity cannot be settled
            var identity = LocalPlayerIdentityResolver.ResolveForThisProcess();
            if (!identity.Succeeded) throw new PlayerStartRefusedException(identity.RefusalLocalizationKey, identity.RefusalLogReason);
```
`new VanillaApi(exchangeManager, packetSender, serverCommunicator)`（第4引数を削除）。ハンドシェイクを:
```csharp
            var handshakeResponse = await vanillaApi.Response.InitialHandShake(identity.Identity, _exitToken);
            var playerConnectionSetting = new PlayerConnectionSetting(handshakeResponse.PlayerId);
```
とし、`ServerConnectionResult` に `PlayerConnectionSetting = playerConnectionSetting` を足す。コンストラクタの `PlayerConnectionSetting` 引数とフィールドは削除。

`VanillaApiWithResponse.InitialHandShake(string playerIdentity, CancellationToken ct)`: ハンドシェイク要求を送り、`Rejection != HandshakeRejection.None` なら `throw new PlayerHandshakeRejectedException(response.Rejection)`。受理されたら、既存の `WhenAll`（`GetWorldData`・`GetMyPlayerInventory`）を続けて `InitialHandshakeResponse` に `PlayerId = response.PlayerId` を入れる。`VanillaApiWithResponse`/`VanillaApiSendOnly` のコンストラクタから `PlayerConnectionSetting` を外し、全リクエスト生成から playerId 引数を外す（Task 6 の変換後コンストラクタに合わせる。`GetPlayerInventory(int playerId)` は削除し `GetMyPlayerInventory()` に一本化。呼び出し元が無い `DisconnectRailAsync(int playerId, …)` は削除）。

`InitializeScenePipeline.cs`: L113 の `new PlayerConnectionSetting(_proprieties.PlayerId)` を削除し、`ServerConnectionInitializer` のコンストラクタ呼び出しから外す。L148 の `ClientContext` への受け渡しは `serverResult.PlayerConnectionSetting` を使う。L124-143 の catch で表示する文言を:
```csharp
                var messageKey = e switch
                {
                    PlayerStartRefusedException refused => refused.LocalizationKey,
                    PlayerHandshakeRejectedException { Rejection: HandshakeRejection.AlreadyConnected } => "ui.loading.playerAlreadyConnected",
                    _ => LocalizationKeys.Ui.Loading.InitializationFailed,
                };
```
として既存の `loadingProgressLog.Append(...)` へ渡す（`Append` がキー型を取る場合は、`LocalizationKeys.Ui.Loading.SteamIdentityUnavailable` 等の生成キーを使う。生成は Step 6 の CSV 追加と force-recompile の後）。

`InitializeProprieties.cs`: `PlayerId` フィールド・`DefaultPlayerId`・全ファクトリの `playerId` 引数を削除。`LocalGameLauncher.cs:37-38` は `starter.SetProperty(InitializeProprieties.CreateLocalServer());`。`ConnectServer.cs:57-58` は `PlayerPrefs.GetInt` 行を削除し `TryCreateRemoteConnection(ipText, portText, out var properties, out var denyReason)`。

その他の送信元: `ElectricWireExtendRequestSender.cs:65-78`・`MachineRecipeSelectionActions.cs:57,74,79`・`InventoryMoveServerDispatcher.cs:21-46`・`LocalPlayerInventoryController.cs:182` から playerId 引数を削除。`TrainHUDScreenState.cs:123`・`TrainRidingTopic.cs:58`（イベントの受け手判定）と `give`/`clearInventory` コマンド文字列（`ItemGetDebugSheet.cs:29`・`DebugSheetController.cs:47`・`PlaytestItemOps.cs:49`・`EditModeInPlayingTestUtil.cs:134-135`）は `ClientContext.PlayerConnectionSetting.PlayerId` のまま残す（サーバー採番の値が入る）。

- [ ] **Step 5: `SetPlayerId` を撤去する**

`uloop execute-dynamic-code --project-path ./moorestech_client` で MainMenu シーンを開き、"System" GameObject から `Client.MainMenu.SetPlayerId` コンポーネントを `Object.DestroyImmediate` で外し、`EditorSceneManager.SaveScene`。その後 `SetPlayerId.cs` と `.meta` を削除し、`PlayerPrefsKeys.PlayerIdKey` を削除。

- [ ] **Step 6: ローカライズを足す**

`Localization/localization.csv` に3行（列: key,Source,english,japanese,german）:
```
ui.loading.steamIdentityUnavailable,Could not connect to Steam. Start Steam and try again.,Could not connect to Steam. Start Steam and try again.,Steamに接続できないため開始できません。Steamを起動してからもう一度お試しください。,Keine Verbindung zu Steam. Starte Steam und versuche es erneut.
ui.loading.deviceIdentityUnavailable,Could not identify this device. The game cannot start.,Could not identify this device. The game cannot start.,この端末の識別子を取得できないため開始できません。,Dieses Gerät konnte nicht identifiziert werden. Das Spiel kann nicht starten.
ui.loading.playerAlreadyConnected,The same player is already connected.,The same player is already connected.,同じプレイヤーが既に接続しています。,Derselbe Spieler ist bereits verbunden.
```
追加後に force-recompile（`uloop compile --project-path ./moorestech_client --force-recompile`）。

- [ ] **Step 7: クライアントテストを直す**

`InitializeProprietiesTest.cs` の `PlayerId` 検査（L27,35,48,60）を削除し、`CreateLocalServer()`/`CreateRemoteConnection(ip, port)` の新シグネチャで他の検査を残す。File 節の他テストは新シグネチャへ。`PlacementPacketCapture.cs:45` の `new PlayerConnectionSetting(1)` はそのまま（クライアント側の設定オブジェクトは残る）。

- [ ] **Step 8: テストを実行する**

Run: `uloop compile --project-path ./moorestech_client`（0件）→ `uloop run-tests ... --filter-value "LocalPlayerIdentityResolverTest|InitializeProprieties|PlayModeSettings|EditModeInPlayingTestUtil|PlacementPacket|LocalPlayerEquipment|InventoryMoveServer"`
Expected: PASS。続けて `grep -rn "DefaultPlayerId\|PlayerIdKey\|class SetPlayerId" moorestech_client/Assets/Scripts` が0件。

- [ ] **Step 9: コミットする**

```bash
git add -A moorestech_client Localization
git commit -m "feat(client): 身元をSteam/端末値から解決しハンドシェイク応答のIDで接続を作る。既定IDとSetPlayerIdを撤去 (ADR 0073)"
```

---

### Task 8: バグ報告の再現で報告者のプレイヤーを受け取れるようにする

**Files:**
- Create: `scripts/bugreport/unclaim-reporter.py`
- Modify: `scripts/bugreport/prepare-run.sh:148-150`（コピー成功後に呼ぶ）
- Modify: `scripts/bugreport/tests/test-prepare-run.sh`（ケース追加）

**Interfaces:**
- Consumes: Task 2 の `players` 節の形（`nextPlayerId`・`claimCandidatePlayerId`・`entries[{playerId, identity}]`）
- Produces: `python3 scripts/bugreport/unclaim-reporter.py <save.json> <manifest.json>`（終了コード0。変更内容と理由を stderr に1行以上）

- [ ] **Step 1: 失敗するテストを書く** — `test-prepare-run.sh` の既存ケースの組み立て方に合わせ、`players` 節を持つ `tick_*.json` と `steamId: "76561198319362448"` の manifest で run を作り、次を検査する:

```bash
# 報告者のプレイヤーが持ち主未定の候補に戻る
# The reporter's player goes back to an unclaimed candidate
python3 - "$WORLD_DIR/save.json" <<'PY'
import json, sys
players = json.load(open(sys.argv[1]))["players"]
reporter = [e for e in players["entries"] if e["playerId"] == 2][0]
assert reporter["identity"] is None, players
assert players["claimCandidatePlayerId"] == 2, players
PY
```
fixture の `players`: `{"nextPlayerId":3,"claimCandidatePlayerId":null,"entries":[{"playerId":1,"identity":"steam:1"},{"playerId":2,"identity":"steam:76561198319362448"}]}`。

- [ ] **Step 2: 実行して失敗を確認する**

Run: `bash scripts/bugreport/tests/test-prepare-run.sh`
Expected: FAIL（identity が残る）

- [ ] **Step 3: 実装する**

`scripts/bugreport/unclaim-reporter.py`:
```python
# 再現用に複製したセーブで、報告者のプレイヤーを持ち主未定の結びつけ候補へ戻す（ADR 0073）。開発機の最初の接続がそれを受け取る
# In the save copied for reproduction, turn the reporter's player back into an unclaimed claim candidate (ADR 0073); the developer's first connection receives it
import json, sys

save_path, manifest_path = sys.argv[1], sys.argv[2]

def note(message):
    sys.stderr.write("[unclaim-reporter] " + message + "\n")

# 外部JSONのパースは外部境界。失敗は再現の成否を告げて終える
# Parsing external JSON is a boundary; a failure reports the reproduction impact and stops
try:
    with open(save_path) as handle:
        save = json.load(handle)
except Exception as error:
    note("save.json を読めないため報告者の付け替えをしない（%s: %s）" % (type(error).__name__, error))
    sys.exit(0)
try:
    with open(manifest_path) as handle:
        manifest = json.load(handle)
except Exception as error:
    note("manifest.json を読めない（%s: %s）。報告者の身元なしとして扱う" % (type(error).__name__, error))
    manifest = {}

players = save.get("players")
if not isinstance(players, dict) or not isinstance(players.get("entries"), list):
    note("save.json に players 節が無い（版3より前のスナップショット）。ロード時の変換が候補を選ぶので付け替えは不要")
    sys.exit(0)

entries = players["entries"]
steam_id = manifest.get("steamId") if isinstance(manifest, dict) else None
target = None
if isinstance(steam_id, str) and steam_id:
    identity = "steam:" + steam_id
    target = next((e for e in entries if e.get("identity") == identity), None)
    if target is None:
        note("報告者の身元 %s に結びつくプレイヤーが無い。持ち物総数で選ぶ" % identity)
else:
    note("manifest に steamId が無い（端末値のビルドからの報告）。持ち物総数で選ぶ")

if target is None:
    def item_count(player_id):
        inventory = next((p for p in save.get("playerInventory", []) if p.get("PlayerId") == player_id), None)
        if not inventory:
            return 0
        stacks = list(inventory.get("MainInventoryItems") or []) + list(inventory.get("EquipmentInventoryItems") or [])
        grab = inventory.get("GrabInventoryItems")
        if isinstance(grab, dict):
            stacks.append(grab)
        return sum(int(s.get("count", 0)) for s in stacks if isinstance(s, dict))
    bound = [e for e in entries if e.get("identity") is not None]
    if not bound:
        note("結びつき済みのプレイヤーが居ないため付け替えしない")
        sys.exit(0)
    target = sorted(bound, key=lambda e: (-item_count(e["playerId"]), e["playerId"]))[0]

note("プレイヤー%d（身元 %s）を持ち主未定の結びつけ候補へ戻す" % (target["playerId"], target.get("identity")))
target["identity"] = None
players["claimCandidatePlayerId"] = target["playerId"]
with open(save_path, "w") as handle:
    json.dump(save, handle)
```

`prepare-run.sh` の `cp ... "$WORLD_DIR/save.json" || log ...` の直後を次に置き換える:
```bash
  if cp "$RUN/snapshots/tick_$LATEST_TICK.json" "$WORLD_DIR/save.json"; then
    # 開発機が報告者のプレイヤーとして入れるよう、報告者を持ち主未定の候補へ戻す
    # Turn the reporter back into an unclaimed candidate so the developer enters as the reporter's player
    python3 "$SCRIPT_DIR/unclaim-reporter.py" "$WORLD_DIR/save.json" "$RUN/manifest.json" 2>&1 | while read -r line; do log "$line"; done
  else
    log "save.json のコピーに失敗した。固定ワールド起動はできない"
  fi
```
（`SCRIPT_DIR`・`log`・manifest のパス変数名は prepare-run.sh の既存のものに合わせる。）

- [ ] **Step 4: テストを実行する**

Run: `bash scripts/bugreport/tests/test-prepare-run.sh`
Expected: 全ケース PASS

- [ ] **Step 5: コミットする**

```bash
git add scripts/bugreport
git commit -m "feat(bugreport): 再現用セーブの報告者を持ち主未定の結びつけ候補へ戻す (ADR 0073)"
```

---

### Task 9: 規約・裁定・レビュー観点の更新

**Files:**
- Modify: `AGENTS.md`（「既知の制約」の「プロトコルのplayerIdはクライアント自己申告で偽造容易だが現時点で許容…」の行を削除）
- Modify: `.decisions/2026-08-14-プロトコルのplayerId自己申告は既存多数派として放置する.md`（末尾に `置き換え: 2026-09-27 ADR 0073 で接続に紐づいたIDだけを信じる形へ一括是正した（.decisions/2026-09-27-サーバーは接続に紐づいたプレイヤーIDだけを信じる.md）` を追記）
- Modify: `.agents/skills/moores-code-review/lenses/server-state-sync.md`（playerId 自己申告を「既存多数派」の例に挙げている注記を、「リクエストに playerId を載せない。送り手は `context.PlayerId`（ADR 0073）。ペイロードで playerId を受け取る新規プロトコルは Critical」へ差し替え）
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/electric-wire-tool-pole-cycle-rotate-via-ui.cs:68`（`ClientContext.PlayerConnectionSetting.PlayerId` はそのまま動くが、コメントに ID の出どころが書かれていれば更新）

- [ ] **Step 1: 旧記述を検索する**

```bash
grep -rn "自己申告" AGENTS.md .agents/skills/moores-code-review .decisions | grep -v 2026-09-27
```

- [ ] **Step 2: 上記ファイルを編集する**（内容は Files 節のとおり）

- [ ] **Step 3: 再検索して旧前提が残っていないことを確認する**（Step 1 のコマンドの出力が、置き換え済みの追記行だけ）

- [ ] **Step 4: コミットする**

```bash
git add AGENTS.md .decisions .agents/skills
git commit -m "docs: playerId自己申告の既知制約を撤去しレビュー観点をADR 0073へ更新"
```

---

### Task 10: 実機確認（unityプレイ録画テスト）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/player-identity-sequential-id.cs`（スキルの既存シナリオの書き方に従う）

- [ ] **Step 1: シナリオを書く** — unity-playmode-recorded-playtest スキルを読み、次を1本のシナリオにする:
  1. 新規ワールドでローカル開始 → `ClientContext.PlayerConnectionSetting.PlayerId == 1` を記録
  2. 終了して同じワールドで再開始 → 再び 1（同じ身元に同じID）
  3. テスターのスナップショット `tick_21742.json`（`../moorestech_logs/harness/playtest/reports/76561198319362448/20260926_145808_e35a893b/snapshots/`）をワールドの `save.json` に置いて開始 → `PlayerConnectionSetting.PlayerId == 2`、メインインベントリの原木 `aafce615-...` が612個
- [ ] **Step 2: 実行する**（スキルの手順どおり。PlayModeは `--test-platform`/`--test-mode` の指定がスキルにある）
- [ ] **Step 3: 合否をログの警告・拒否で判定する** — 実行区間の Editor ログ（スキルが示す固定パス）を次で引き、**0件**であること:

```bash
grep -nE "拒否|無視しました|変換できません|候補を破棄|Failed|unclaimed|mismatch|refus" <ログファイル>
```
あわせて肯定行として `セーブを版2から版3へ変換しました。プレイヤーID振り直し=2件 結びつけ候補=2` と `持ち主未定のプレイヤー2を身元device:` が各1回あること。
- [ ] **Step 4: 録画と結果を `../moorestech_logs/harness/` の所定位置へ保存し、コミットする**（シナリオファイルのみ本repoへ）

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/connect/player-identity-sequential-id.cs
git commit -m "test(playtest): 身元からの連番採番と旧セーブの結びつけを録画シナリオで確認"
```

---

### Task 11: 全ブランチのコードレビュー（省略不可）

- [ ] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** moores-code-review スキルで `feature/player-identity-sequential-ids` の全差分をレビューする。
- [ ] **Step 2:** 指摘の反映がソース（判定経路・条件式・その評価時点）に触れたら、反映後のバイナリで Task 10 を再実施してから完了とする。
- [ ] **Step 3: 残課題の起票** — plan・Task 10 の記録・進捗台帳に書いた「未検証」「未確認」「残差」を1件ずつ `bd create --deps=discovered-from:moorestech-5quqr` で起票し、完了報告には issue 番号を列挙する（要約で済ませない）。既知の別件: moorestech-3eonp（Steam認証チケット検証）、moorestech-pejkw（EntityInstanceId 空間）。
- [ ] **Step 4:** PR を作成（本文日本語、ADR 0073 と報告ID `76561198319362448/20260926_145808_e35a893b` を記載）→ `moores-wt rm player-identity-sequential-ids` → `bd close moorestech-5quqr --reason="PR #<番号>"`。

---

## Self-Review 結果

1. **Requirements coverage:** R1→Task7 / R2〜R4→Task7 / R5・R14→Task1 / R6・R13→Task2 / R7〜R9→Task3 / R10→Task4 / R11→Task6 / R12→Task5 / R15→Task8 / R16→Task9 / R17→Task10。漏れなし。
2. **Placeholder scan:** 「実ファイルに合わせる」注記は、既存コードの名前（テストユーティリティ・namespace）を実装者が実物で確認する指示であり、挙動は本文に確定している。TBD/TODO なし。
3. **Type consistency:** `PlayerIdentityRegistry.Assign`/`TryGetPlayerId`/`InitializeForNewWorld`/`GetSaveJsonObject`/`Load`、`HandshakeRejection`、`BoundPacketContext.Handshake/Bind`、`ReceivedPacketLog.Append(tick, playerId, payload)`、`LocalPlayerIdentityResolver.Resolve/ResolveForThisProcess` は全タスクで同名。
4. **保留・縮退経路:** (a) 結びつけ候補は「最初の未知の身元の接続」で解消。最小構成: プレイヤー0人の旧セーブ→候補 null（テスト有）、1人→その1人が候補、候補が壊れている→破棄してログ（Registry.Load）。候補の証拠はセーブ内にあり、候補を持つプロセス（サーバー）と同じ寿命。(b) 未紐づけ拒否は「ハンドシェイク成功」で解消し、その経路はハンドシェイクテストで通る。「解かない」側（Gate テスト）と「解く」側（紐づけ済みは処理される）を対で指定済み。(c) 二重接続拒否は先の接続の切断で解消（既存の `UserPacketHandler.Cleanup` が `Unregister` する）。恒久失敗の表は File Structure 節に記載。
5. **決定的な選択規則:** 候補の同点規則（持ち物数→距離→ID）はユーザー裁定。同点 fixture のテストを Task 2 に2件指定済み。
6. **外部出力の規則化:** `SystemInfo.deviceUniqueIdentifier` の失敗値は Unity の `SystemInfo.unsupportedIdentifier` 定数と空の2分岐をテスト指定。SHA-256 は既知ベクトル（"abc"）で検査。
7. **共有サーバー状態:** 本planはUIの新設を含まない（ローディングの文言のみ）。プレイヤーIDの保持者はクライアントの `ClientContext.PlayerConnectionSetting`（ハンドシェイク応答から1回作る。書き換え操作なし）とサーバーの `PlayerIdentityRegistry`（セーブで永続）。
8. **既存部品の写し:** Steam 読み取り・build-info 読み取りは既存を呼ぶ。SHA-256 の3行のみ別アセンブリの private なため新規（File Structure 節に理由）。

## 判断記録（ADR）

- 設計の正本: `docs/adr/0073-server-assigns-sequential-player-ids-from-player-identity.md`（ユーザー裁定12件＋agent前提。出所はADR本文の各項のとおりで、ここでは書き換えない）。関連: `.decisions/2026-09-27-*.md` 7件。
- planning 中の追加判断:
  - タスク分割を「サーバー基盤（1〜2）→ハンドシェイク（3）→振り分け口（4）→パケットログ（5）→全プロトコル（6）→クライアント（7）→再現（8）→文書（9）→実機（10）」とし、Task 4 でテストを紐づけ済みコンテキストへ先に移してから Task 6 でペイロードを外す順にした。出所: agent判断（ペイロードと接続の両方にIDがある期間を作り、各段で全テストが緑のまま進めるため）
  - 未紐づけの判定をプロトコル個別でなく `PacketResponseCreator` の1か所に置いた。出所: agent判断（ADR 0073 agent前提「振り分け口の1か所で行う」をそのまま実装へ）
  - 拒否理由を文字列でなく enum `HandshakeRejection` で返す。出所: agent判断（クライアントがローカライズキーへ写すため。既存に同役割の前例なし＝新規パターン）
  - 変換の本体を `SaveMigrationStepV2ToV3` と `V2ToV3/` の2ファイルへ分けた。出所: agent判断（200行制限と `Migration/` の10ファイル制限）
  - `PlayerIdRenumbering` の対象絞り込みは `bool onlyPlayerEntities` 引数で表す（`Predicate` 引数を使わない）。出所: agent判断（AGENTS.md「`Func<>` 禁止・コールバックに逃げない」の趣旨）
