# ブループリントアンロックシステム Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ブループリント機能（コピーツール・保存済みBP選択/ペースト・作成/削除）をゲーム開始時ロックとし、研究ノードのclearedActionsから解放できる単一フラグのアンロックを実装する。

**Architecture:** ConnectToolアンロックの先行パターンを単一bool版で踏襲する。`Game.UnlockState`に`BlueprintUnlockStateHolder`（単一フラグ）を追加し、既存の同期3点セット（`va:event:unlocked`イベント／`va:getGameUnlockState`初期データ／クライアントミラー購読）に載せる。クライアントの唯一のアンロック判定点`PlacementTargetCatalog.UnlockedEntries`のBPハードコードtrueをフラグ参照へ置換することで、ビルドメニュー・ホットバー・配置解決へ一括波及させる。サーバーは`BlueprintProtocol`のCreate/Deleteとホットバーへの新規BP系割当を拒否する。設計はADR 0015が正。

**Tech Stack:** Unity C#（moorestech_server / moorestech_client）/ Mooresmaster SourceGenerator（VanillaSchemaのYAML）/ UniRx / MessagePack / NUnit / React + TypeScript（moorestech_web/webui、通知トーストのみ）/ uloop

## Requirements

ADR: `docs/adr/0015-blueprint-feature-unlock-single-flag.md`（実装前に必読）

1. ブループリント機能全体を単一のアンロック状態で束ねる。受け入れ基準: `IGameUnlockStateData.IsBlueprintUnlocked`の1値で、コピーツール・保存済みBPのビルドメニュー表示/選択/ペースト・作成/削除の可否がすべて決まる。
2. 新gameAction `unlockBlueprint`（パラメータ無し）を追加し、研究・チャレンジ双方のclearedActionsから呼べる。受け入れ基準: `GameActionExecutor`経由で`UnlockBlueprint()`が呼ばれるサーバーテストが通る。
3. 初期状態はマスタでシードする: `buildMenu.yml`のルートへ`blueprintInitialUnlocked`（boolean, default false）を追加し、Holderが`BuildToolMaster`からシードする。受け入れ基準: テストマスタ（blueprintInitialUnlocked:false）で新規サーバーの`IsBlueprintUnlocked`がfalse。
4. 同期は3点セット: `UnlockEventType.Blueprint`を`va:event:unlocked`へ追加／`va:getGameUnlockState`応答に`IsBlueprintUnlocked`を追加／`ClientGameUnlockStateData`がシード＋イベント反映する。受け入れ基準: 各拡張点のテスト（サーバー保存ロード・クライアントカタログ判定）が通る。
5. 未解放中、ビルドメニューにBPコピーツール・保存済みBPエントリは出ない（除外方式）。受け入れ基準: `PlacementTargetCatalog.UnlockedEntries`が未解放時にBlueprintCopy/Blueprint種別を返さないテストが通る。BP系は`showAllPlaceable`（無料設置デバッグ）の対象外（接続ツール同様）。
6. サーバー側拒否: 未解放時、`BlueprintProtocol`のCreate/Deleteは`BlueprintFailureReason.NotUnlocked`で失敗し、GetAllは成功のまま。ホットバーへの新規BP系割当（コピーツールGuid・BP Guid）は無視される。受け入れ基準: それぞれのサーバーテストが通る。
7. 旧セーブ（アンロック項目欠損）は未解放としてシードどおり扱い、既存BPデータ・ホットバーの既存BP割当は消さない（`LoadHotbar`の解決可否は従来どおり存在チェックのみ）。受け入れ基準: 欠損JSONロード後もfalse、かつ`LoadHotbar`がBP割当を保持するテストが通る。
8. 解放時にachievementトーストを出す: `achievement.unlockedBlueprint`を`AchievementNotificationWiring`から全員へ通知し、webuiの`notificationMessages.ts`とローカライズキーを追加する。受け入れ基準: vitestの通知テーブル整合テストが通る。
9. やらないこと: 研究詳細ペインへの`unlockBlueprint`ラベル表示（研究UI改修 bd:moorestech-23s の解放物セクション実装に追従する後続タスク bd:moorestech-c0y）／実マスタ（moorestech_master）への`unlockBlueprint`配置とconsumeItems設計（後続タスク bd:moorestech-fy6）／グレーアウト＋錠アイコン等の新規ロック表示語彙／`BuildMenuTopic`のアンロックイベント購読追加（既存どおりメニュー再入場で反映）。

## Global Constraints

- 作業ブランチ: `feature/blueprint-unlock`（origin/master起点。`moores-wt new feature/blueprint-unlock`で作った使い捨てworktreeで作業する）
- AGENTS.md全規約に従う。特に: partial禁止 / `Func<>`禁止 / 1ファイル200行以下 / コメントは日本語・英語2行セット / 単純getter/setter禁止（`{ get; private set; }`は許容）/ .cs変更後は必ず `uloop compile --project-path ./moorestech_client`
- VanillaSchemaのYAML編集時は必ず`edit-schema`スキルを読み込むこと。`Mooresmaster.Model.*`は自動生成のため手動編集禁止（スキーマ変更→コンパイルで再生成される）
- Core.Masterルール: ローダーでのJSON改変・欠損プリフィル禁止。`?? Default`フォールバック禁止。欠損はスキーマの`default`で解決する（connectTools `initialUnlocked`前例）
- サーバー可変状態のクライアント同期は3点セット標準（`.claude/rules/server-protocol.md`）。他プロトコル応答から状態を推測合成するApplierは禁止
- Unityテストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>" --test-mode EditMode`（既定はPlayModeなのでEditMode明示必須）。「Unity is reloading」エラー時は45秒待ってリトライ
- webuiのコマンドは `moorestech_web/webui/` で実行: `npm run test`（vitest）/ `npm run gen:i18n`。ローカライズ正本はリポジトリ直下 `Localization/localization.csv`
- MessagePackの`[Key(N)]`は既存番号を変更せず末尾追加のみ。enum（`UnlockEventType`・`BlueprintFailureReason`）も既存値を変えず末尾追加のみ
- タスク完了ごとにコミットする（作業消失防止）

---

### Task 1: 設計文書の持ち込み

**Files:**
- Copy+Commit（メインクローンから本worktreeへ。worktree作成時に未追跡ファイルはコピーされないため）:
  - `docs/adr/0015-blueprint-feature-unlock-single-flag.md`
  - `.decisions/2026-08-18-ブループリントは機能全体を1フラグでロックする.md`
  - `.decisions/2026-08-18-ブループリント解放は研究ノードのclearedActionsで行う.md`
  - `.decisions/2026-08-18-ロック中のBPエントリはビルドメニューに非表示とする.md`
  - `.decisions/2026-08-18-BP未解放はサーバー側でも拒否する.md`
  - `.decisions/2026-08-18-旧セーブのBPは未解放扱いでデータは保持する.md`
  - `docs/superpowers/plans/2026-08-18-blueprint-unlock.md`（本ファイル）
  - `CONTEXT.md`（「ブループリント解放」用語と「解放物」更新分。メインクローン版をそのままコピー）

**Interfaces:**
- Consumes: なし（先頭タスク）
- Produces: 以後の全タスクが参照するADR・裁定・用語

- [x] **Step 1: メインクローンから設計文書8点をコピーする**

```bash
SRC=<メインクローンの絶対パス>   # moores-wt newの元ディレクトリ
cp "$SRC/docs/adr/0015-blueprint-feature-unlock-single-flag.md" docs/adr/
cp "$SRC"/.decisions/2026-08-18-ブループリント*.md "$SRC"/.decisions/2026-08-18-BP*.md "$SRC"/.decisions/2026-08-18-ロック中のBP*.md "$SRC"/.decisions/2026-08-18-旧セーブのBP*.md .decisions/
cp "$SRC/docs/superpowers/plans/2026-08-18-blueprint-unlock.md" docs/superpowers/plans/
cp "$SRC/CONTEXT.md" CONTEXT.md
```

注意: `CONTEXT.md`のコピーで研究UIセッション由来の「### 研究」節も一緒に入る（メインクローンの現行内容が正）。

- [x] **Step 2: コミットする**

```bash
git add docs/adr .decisions CONTEXT.md docs/superpowers/plans
git commit -m "docs: ブループリントアンロックのADR 0015と裁定・用語を持ち込む"
```

---

### Task 2: スキーマ拡張（blueprintInitialUnlocked / unlockBlueprint）

**Files:**
- Modify: `VanillaSchema/buildMenu.yml`（ルートへblueprintInitialUnlocked追加）
- Modify: `VanillaSchema/ref/gameAction.yml`（unlockBlueprint追加）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/buildMenu.json`

**Interfaces:**
- Consumes: なし
- Produces: 自動生成される `BuildMenu.BlueprintInitialUnlocked: bool`（Task 3が使う）と `GameActionElement.GameActionTypeConst.unlockBlueprint`（Task 4が使う）

- [x] **Step 1: `edit-schema`スキルを読み込む**

Skillツールで`edit-schema`を起動し、スキーマ編集規約に従う。

- [x] **Step 2: `VanillaSchema/buildMenu.yml`のルートへblueprintInitialUnlockedを追加する**

ルートの`properties`先頭（`- key: categories`の直前）に追加:

```yaml
- key: blueprintInitialUnlocked
  type: boolean
  default: false
```

- [x] **Step 3: `VanillaSchema/ref/gameAction.yml`へunlockBlueprintを追加する**

`gameActionType`のenum optionsの`unlockConnectTool`の後に`- unlockBlueprint`を追加し、`cases:`へ以下を追加（`playBackgroundSkit`のパラメータ無し前例と同型）:

```yaml
    - when: unlockBlueprint
      type: object
      isDefaultOpen: true
      properties: []
```

- [x] **Step 4: テストマスタJSONへblueprintInitialUnlockedを明示する**

`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/buildMenu.json`のルートへ`"blueprintInitialUnlocked": false`を追加する（connectToolsが全JSONで明示している前例に合わせる）。実マスタ（moorestech_master repo）の更新は後続タスクbd:moorestech-fy6のスコープ。

- [x] **Step 5: コンパイルして自動生成を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`BuildMenu.BlueprintInitialUnlocked`と`GameActionTypeConst.unlockBlueprint`が生成される（生成物は手で編集しない）。

- [x] **Step 6: コミットする**

```bash
git add VanillaSchema moorestech_server/Assets/Scripts/Tests.Module
git commit -m "feat: buildToolsへinitialUnlocked、gameActionへunlockBlueprintをスキーマ追加する"
```

---

### Task 3: Game.UnlockState拡張（単一フラグHolderと保存）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/States/BlueprintUnlockStateInfo.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/BlueprintUnlockStateHolder.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.UnlockState/IGameUnlockStateDatastoreController.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.UnlockState/GameUnlockStateDatastoreController.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintUnlockStateTest.cs`（新規。`ConnectToolUnlockStateTest.cs`と同型）

**Interfaces:**
- Consumes: `MasterHolder.BuildToolMaster.BlueprintInitialUnlocked`（Task 2の`blueprintInitialUnlocked`）
- Produces: `IGameUnlockStateData.IsBlueprintUnlocked: bool` ／ `IGameUnlockStateDataController.OnUnlockBlueprint: IObservable<Unit>`・`void UnlockBlueprint()` ／ `GameUnlockStateJsonObject.BlueprintUnlockState: BlueprintUnlockStateInfoJsonObject`（Task 4〜6が使う）

- [x] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintUnlockStateTest.cs`を新規作成:

```csharp
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class BlueprintUnlockStateTest
    {
        [Test]
        public void テストマスタのblueprintInitialUnlockedがfalseなので初期状態は未解放()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(controller.IsBlueprintUnlocked);
        }

        [Test]
        public void ブループリント解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 解放イベントの発火も同時に検証する
            // Verify the unlock event also fires
            var unlockEventCount = 0;
            controller.OnUnlockBlueprint.Subscribe(_ => unlockEventCount++);
            controller.UnlockBlueprint();

            Assert.AreEqual(1, unlockEventCount);
            Assert.IsTrue(controller.IsBlueprintUnlocked);

            // 二重解放はイベントを再発火しない
            // Unlocking twice never re-fires the event
            controller.UnlockBlueprint();
            Assert.AreEqual(1, unlockEventCount);

            // 別サーバーで状態引継ぎ確認
            // Load into another server instance and check the state carries over
            var saveJson = controller.GetSaveJsonObject();
            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.IsBlueprintUnlocked);
        }

        [Test]
        public void 旧セーブのように項目が欠損していればシード値のまま未解放()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 旧セーブ相当: BlueprintUnlockStateがnullのJSONをロードする
            // Old-save equivalent: load JSON whose BlueprintUnlockState is null
            var saveJson = controller.GetSaveJsonObject();
            saveJson.BlueprintUnlockState = null;
            controller.LoadUnlockState(saveJson);

            Assert.IsFalse(controller.IsBlueprintUnlocked);
        }

        private static (global::Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL（`IsBlueprintUnlocked`が未定義のコンパイルエラー）

- [x] **Step 3: State/Holderを実装する**

`States/BlueprintUnlockStateInfo.cs`:

```csharp
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class BlueprintUnlockStateInfo
    {
        public bool IsUnlocked { get; private set; }

        public BlueprintUnlockStateInfo(bool isUnlocked)
        {
            IsUnlocked = isUnlocked;
        }

        public BlueprintUnlockStateInfo(BlueprintUnlockStateInfoJsonObject jsonObject)
        {
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class BlueprintUnlockStateInfoJsonObject
    {
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public BlueprintUnlockStateInfoJsonObject() { }

        public BlueprintUnlockStateInfoJsonObject(BlueprintUnlockStateInfo blueprintUnlockStateInfo)
        {
            IsUnlocked = blueprintUnlockStateInfo.IsUnlocked;
        }
    }
}
```

`Holders/BlueprintUnlockStateHolder.cs`:

```csharp
using System;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class BlueprintUnlockStateHolder
    {
        public IObservable<Unit> OnUnlock => _onUnlock;
        public bool IsUnlocked => _info.IsUnlocked;

        private readonly Subject<Unit> _onUnlock = new();
        private BlueprintUnlockStateInfo _info;

        public BlueprintUnlockStateHolder()
        {
            // 機能全体の単一フラグ。buildMenuルートのblueprintInitialUnlockedからシードする（ADR 0015）
            // Single feature-wide flag seeded from buildMenu's root blueprintInitialUnlocked (ADR 0015)
            _info = new BlueprintUnlockStateInfo(MasterHolder.BuildToolMaster.BlueprintInitialUnlocked);
        }

        public void Unlock()
        {
            // 複数の研究/チャレンジから重複解放されてもイベントは一度だけ
            // Unlocks from multiple researches/challenges fire the event only once
            if (_info.IsUnlocked) return;
            _info.Unlock();
            _onUnlock.OnNext(Unit.Default);
        }

        public void Load(BlueprintUnlockStateInfoJsonObject jsonObject)
        {
            // 旧セーブは項目欠損＝シード値（未解放）のまま
            // Old saves lack this field, so the seed value (locked) stays
            if (jsonObject == null) return;
            _info = new BlueprintUnlockStateInfo(jsonObject);
        }

        public BlueprintUnlockStateInfoJsonObject GetSaveJsonObject()
        {
            return new BlueprintUnlockStateInfoJsonObject(_info);
        }
    }
}
```

- [x] **Step 4: インターフェースとコントローラへ配線する**

`IGameUnlockStateDatastoreController.cs`: `IGameUnlockStateData`へ`public bool IsBlueprintUnlocked { get; }`を、`IGameUnlockStateDataController`へ以下を追加（`using UniRx;`が必要）:

```csharp
public IObservable<Unit> OnUnlockBlueprint { get; }
void UnlockBlueprint();
```

`GameUnlockStateDatastoreController.cs`（クラス名`GameUnlockStateDataController`）:
- フィールド追加: `private readonly BlueprintUnlockStateHolder _blueprint = new();`
- プロパティ/メソッド追加（`_connectTool`ブロックの直後）:

```csharp
public IObservable<Unit> OnUnlockBlueprint => _blueprint.OnUnlock;
public bool IsBlueprintUnlocked => _blueprint.IsUnlocked;
public void UnlockBlueprint() => _blueprint.Unlock();
```

- `LoadUnlockState`へ`_blueprint.Load(stateJsonObject.BlueprintUnlockState);`を追加
- `GetSaveJsonObject`の初期化子へ`BlueprintUnlockState = _blueprint.GetSaveJsonObject(),`を追加
- `GameUnlockStateJsonObject`へ`[JsonProperty("blueprintUnlockState")] public BlueprintUnlockStateInfoJsonObject BlueprintUnlockState;`を追加
- `using UniRx;`を追加

注意: `IGameUnlockStateData`の実装は他にもある。`grep -rn "IGameUnlockStateData" --include="*.cs" moorestech_client moorestech_server`で全実装を洗い出し、`ClientGameUnlockStateData`（Task 5で対応）と`Client.Tests`内のスタブ（`AllPlacementTargetsUnlockedStateData`＝`BuildMenuEntryDtoFactoryTest.cs:186`。`public bool IsBlueprintUnlocked => true;`を追加）を同時に直さないとコンパイルが通らない。このタスクの時点で最小修正（スタブはtrue、`ClientGameUnlockStateData`は一旦`IsBlueprintUnlocked { get; private set; }`の追加だけ）を入れてよい。

- [x] **Step 5: コンパイルとテストを実行して通す**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintUnlockStateTest" --test-mode EditMode`
Expected: PASS（3件）

- [x] **Step 6: コミットする**

```bash
git add moorestech_server moorestech_client
git commit -m "feat: ブループリント単一フラグのアンロック状態を追加する"
```

---

### Task 4: gameAction実行・イベント・初期データ・達成通知

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Action/GameActionExecutor.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetGameUnlockStateProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Notification/AchievementNotificationWiring.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintUnlockStateTest.cs`（テスト追加）

**Interfaces:**
- Consumes: Task 2の`GameActionTypeConst.unlockBlueprint`、Task 3の`UnlockBlueprint()`/`OnUnlockBlueprint`/`IsBlueprintUnlocked`
- Produces: `UnlockEventType.Blueprint`（enum末尾）、`ResponseGameUnlockStateProtocolMessagePack.IsBlueprintUnlocked`（`[Key(16)]`）。Task 5のクライアントミラーが使う

- [x] **Step 1: 失敗するテストを書く（executor経由の解放）**

`BlueprintUnlockStateTest.cs`へ追加:

```csharp
[Test]
public void unlockBlueprintのgameActionで解放される()
{
    var (_, serviceProvider) = CreateServer();
    var controller = serviceProvider.GetService<IGameUnlockStateDataController>();
    var executor = serviceProvider.GetService<Game.Action.IGameActionExecutor>();

    // unlockBlueprintアクションを直接組み立てて実行する
    // Build and execute the unlockBlueprint action directly
    var action = new Mooresmaster.Model.GameActionModule.GameActionElement(
        Mooresmaster.Model.GameActionModule.GameActionElement.GameActionTypeConst.unlockBlueprint,
        new Mooresmaster.Model.GameActionModule.UnlockBlueprintGameActionParam());
    executor.ExecuteUnlockActions(new[] { action });

    Assert.IsTrue(controller.IsBlueprintUnlocked);
}
```

注意: 生成される`GameActionElement`/`UnlockBlueprintGameActionParam`のコンストラクタシグネチャは生成コードに合わせる（`Mooresmaster.Model.GameActionModule`の既存パラメータ型の生成形を確認して調整する。プロパティ0個のcaseなので引数無しの想定）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintUnlockStateTest" --test-mode EditMode`
Expected: FAIL（executorがunlockBlueprintを処理しないため`IsBlueprintUnlocked`がfalse）

- [x] **Step 3: GameActionExecutorへcaseを追加する**

`ExecuteUnlockActions`のswitchの`unlockPlayerInventorySlotLevel`の後へ`case GameActionElement.GameActionTypeConst.unlockBlueprint:`を追加（`ExecuteAction(action, context); break;`へ落ちる既存グループに加える）。
`ExecuteAction`のswitchへ追加:

```csharp
case GameActionElement.GameActionTypeConst.unlockBlueprint:
    _gameUnlockStateDataController.UnlockBlueprint();
    break;
```

（パラメータ無しのためローカル関数は作らない）

- [x] **Step 4: イベントパケット・初期データ・通知を配線する**

`UnlockedEventPacket.cs`:
- enum `UnlockEventType`の末尾へ`Blueprint,`を追加（既存値の順序変更禁止）
- `Load()`へ追加: `_unlockState.OnUnlockBlueprint.Subscribe(_ => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.Blueprint, Guid.Empty)));`
- `UnlockEventMessagePack`のGuid付きコンストラクタのswitchへ追加:

```csharp
case UnlockEventType.Blueprint:
    // 単一フラグのためGuidを持たない
    // Single flag; carries no GUID
    break;
```

`GetGameUnlockStateProtocol.cs`:
- `GetResponse`の`return`直前で値を取り、コンストラクタ引数の末尾へ`gameUnlockStateData.IsBlueprintUnlocked`を追加
- `ResponseGameUnlockStateProtocolMessagePack`へ`[Key(16)] public bool IsBlueprintUnlocked { get; set; }`を追加し、コンストラクタへ`bool isBlueprintUnlocked`引数（末尾）と代入を追加

`AchievementNotificationWiring.cs`の`Load()`末尾へ追加:

```csharp
_unlockState.OnUnlockBlueprint.Subscribe(_ => _notificationService.NotifyAll(
    NotificationMessagePack.CreateAchievement("achievement.unlockedBlueprint", Array.Empty<string>())));
```

- [x] **Step 5: コンパイルとテストを実行して通す**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintUnlockStateTest|GetGameUnlockStateProtocol" --test-mode EditMode`
Expected: PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_server
git commit -m "feat: unlockBlueprintアクションと解放イベント・初期データ・達成通知を配線する"
```

---

### Task 5: クライアントミラーとカタログゲート

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UnlockState/ClientGameUnlockStateDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetCatalog.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetCatalogUnlockTest.cs`（テスト追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`（スタブがTask 3で未対応ならここで確定）

**Interfaces:**
- Consumes: Task 3の`IsBlueprintUnlocked`、Task 4の`UnlockEventType.Blueprint`と応答`IsBlueprintUnlocked`
- Produces: 未解放時に`PlacementTargetCatalog.UnlockedEntries`がBP系を除外する挙動。ビルドメニュー（WebUI含む）・ホットバー表示・配置解決は`PlacementTargetResolver`経由で自動追従する（個別UI変更なし）

- [x] **Step 1: 失敗するテストを書く**

`PlacementTargetCatalogUnlockTest.cs`へ追加:

```csharp
[Test]
public void ブループリント未解放ならBP系エントリは列挙されず解放後に現れる()
{
    var catalog = new PlacementTargetCatalog();
    var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
    var blueprintGuid = Guid.Parse("70000000-0000-4000-8000-000000000002");
    var blueprints = new[] { (blueprintGuid, "locked-base") };

    // 未解放: コピーツールも保存済みBPも出ない。showAllPlaceableでも出ない（接続ツール同様）
    // Locked: neither the copy tool nor saved blueprints appear, even with showAllPlaceable
    var lockedIds = catalog.UnlockedEntries(unlockState, false, blueprints).Select(entry => entry.Id).ToHashSet();
    var lockedShowAllIds = catalog.UnlockedEntries(unlockState, true, blueprints).Select(entry => entry.Id).ToHashSet();
    var blueprintCopyIds = catalog.CreateEntries(blueprints).Where(entry => entry.Kind == PlacementTargetKind.BlueprintCopy).Select(entry => entry.Id).ToList();
    Assert.IsNotEmpty(blueprintCopyIds);
    foreach (var copyId in blueprintCopyIds)
    {
        Assert.IsFalse(lockedIds.Contains(copyId));
        Assert.IsFalse(lockedShowAllIds.Contains(copyId));
    }
    Assert.IsFalse(lockedIds.Contains(blueprintGuid));

    // 解放後: 両方現れる
    // Unlocked: both appear
    unlockState.UnlockBlueprint();
    var unlockedIds = catalog.UnlockedEntries(unlockState, false, blueprints).Select(entry => entry.Id).ToHashSet();
    foreach (var copyId in blueprintCopyIds) Assert.IsTrue(unlockedIds.Contains(copyId));
    Assert.IsTrue(unlockedIds.Contains(blueprintGuid));
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalogUnlockTest" --test-mode EditMode`
Expected: 新テストFAIL（現状ハードコードtrueのため未解放でも列挙される）

- [x] **Step 3: PlacementTargetCatalogのハードコードtrueを置換する**

`PlacementTargetCatalog.cs`の`IsUnlocked`ローカル関数:

```csharp
case PlacementTargetKind.BlueprintCopy:
case PlacementTargetKind.Blueprint:
    // BP機能は単一フラグで判定。無料設置デバッグの対象外（接続ツール同様・ADR 0015）
    // Blueprints gate on the single feature flag, excluded from free placement like connect tools (ADR 0015)
    return unlockState.IsBlueprintUnlocked;
```

- [x] **Step 4: クライアントミラーを実装する**

`ClientGameUnlockStateDatastore.cs`の`ClientGameUnlockStateData`:
- `public bool IsBlueprintUnlocked { get; private set; }`を追加（Task 3で追加済みならシードと反映のみ）
- コンストラクタの接続ツール初期化の後へ:

```csharp
// ブループリント機能の解放状態を初期化
// Initialize the blueprint feature unlock state
IsBlueprintUnlocked = unlockState.IsBlueprintUnlocked;
```

- `OnUpdateUnlock`のswitchへ:

```csharp
// ブループリント機能の解放をイベントから反映する
// Reflect the blueprint feature unlock from the event
case UnlockEventType.Blueprint:
    IsBlueprintUnlocked = true;
    break;
```

- [x] **Step 5: コンパイルと関連テストを実行して通す**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalogUnlockTest|BuildMenuEntryDtoFactoryTest" --test-mode EditMode`
Expected: PASS（`BuildMenuEntryDtoFactoryTest`はスタブが常時true=従来挙動のまま通る）

- [x] **Step 6: コミットする**

```bash
git add moorestech_server moorestech_client
git commit -m "feat: BP系設置対象の解放判定を単一フラグ参照へ置換しクライアントへ同期する"
```

---

### Task 6: サーバー側拒否（BlueprintProtocol・ホットバー割当）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintPacketDto.cs`（FailureReason追加）
- Modify: `moorestech_server/Assets/Scripts/Game.Hotbar/HotbarAssignmentDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Hotbar/Game.Hotbar.asmdef`（`Game.UnlockState`参照追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs`（既存2テストの解放前置き＋拒否テスト追加）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotbarAssignmentDatastoreTest.cs`（構築引数対応＋ロック時テスト追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/HotbarSaveLoadTest.cs`（構築引数対応が必要なら）

**Interfaces:**
- Consumes: Task 3の`IsBlueprintUnlocked`/`UnlockBlueprint()`
- Produces: `BlueprintFailureReason.NotUnlocked = 6`（クライアント側は既存の失敗ハンドリングで受ける。UIは未解放中導線が消えているため専用文言は追加しない）

- [x] **Step 1: 失敗するテストを書く**

`BlueprintProtocolTest.cs`へ追加:

```csharp
[Test]
public void 未解放時はCreateとDeleteが拒否されGetAllは成功する()
{
    var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
        .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

    // 未解放: Create/DeleteはNotUnlocked、GetAllは読み取り専用のため成功
    // Locked: Create/Delete fail with NotUnlocked while the read-only GetAll succeeds
    var createResponse = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
    Assert.IsFalse(createResponse.Success);
    Assert.AreEqual(BlueprintFailureReason.NotUnlocked, createResponse.FailureReason);

    var deleteResponse = Send(BlueprintRequest.CreateDeleteRequest(Guid.NewGuid()));
    Assert.IsFalse(deleteResponse.Success);
    Assert.AreEqual(BlueprintFailureReason.NotUnlocked, deleteResponse.FailureReason);

    var getAllResponse = Send(BlueprintRequest.CreateGetAllRequest());
    Assert.IsTrue(getAllResponse.Success);

    // 解放後はCreateが通る
    // After unlocking, Create succeeds
    serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();
    var unlockedCreate = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
    Assert.IsTrue(unlockedCreate.Success);

    #region Internal

    BlueprintResponse Send(BlueprintRequest request)
    {
        var payload = MessagePackSerializer.Serialize(request);
        var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
        return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
    }

    #endregion
}
```

（`using Game.UnlockState;`と`using Microsoft.Extensions.DependencyInjection;`を追加。`BlueprintResponse`の失敗理由プロパティ名は`BlueprintPacketDto.cs`の実物に合わせる）

既存2テスト（`CreateGetAllDeleteFlowTest`・`ToJsonObjectはBlueprintGuidを保持するTest`）は冒頭で解放を前置きする:

```csharp
var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
    .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();
```

`HotbarAssignmentDatastoreTest.cs`へ追加（既存テストの`new HotbarAssignmentDatastore(...)`構築は新引数へ追随し、既存ケースは解放済み状態で従来挙動を維持する）:

```csharp
[Test]
public void 未解放時はBP系の新規割当が無視されロード済み割当は保持される()
{
    // DIから実物一式を取得（unlockStateは初期=未解放）
    // Resolve the real instances from DI (unlock state starts locked)
    var (_, serviceProvider) = CreateServer();
    var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
    var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
    var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
    var copyToolId = catalog.CreateEntries(Array.Empty<(Guid, string)>())
        .First(entry => entry.Kind == PlacementTargetKind.BlueprintCopy).Id;

    // 未解放: コピーツールの割当は無視される
    // Locked: assigning the copy tool is ignored
    datastore.SetAssignment(1, 0, copyToolId);
    Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[0]);

    // 旧セーブ相当: BP割当を含むセーブはロックでも保持される（存在チェックのみ）
    // Old-save equivalent: saved blueprint-tool slots survive a locked load (existence check only)
    unlockState.UnlockBlueprint();
    datastore.SetAssignment(1, 0, copyToolId);
    var save = datastore.GetSaveJsonObject();
    var (_, lockedProvider) = CreateServer();
    var lockedDatastore = lockedProvider.GetService<HotbarAssignmentDatastore>();
    lockedDatastore.LoadHotbar(save);
    Assert.AreEqual(copyToolId, lockedDatastore.GetAssignments(1)[0]);
}
```

（`CreateServer`ヘルパーが無ければ`ConnectToolUnlockStateTest.cs`と同型で追加。既存テストが直接`new`している場合はそのスタイルに合わせ、`IGameUnlockStateDataController`はDIから取るか実物`GameUnlockStateDataController`を生成して渡す — 既存テストの構築様式を確認して同じ形に揃えること）

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintProtocolTest|HotbarAssignmentDatastoreTest" --test-mode EditMode`
Expected: 新テストFAIL（NotUnlocked未実装・割当が素通り）

- [x] **Step 3: BlueprintProtocolへ未解放拒否を実装する**

`BlueprintPacketDto.cs`のenum末尾へ`NotUnlocked = 6,`を追加。

`BlueprintProtocol.cs`:
- `using Game.UnlockState;`を追加、フィールド`private readonly IGameUnlockStateDataController _gameUnlockState;`とコンストラクタでの`serviceProvider.GetService<IGameUnlockStateDataController>()`取得を追加
- `HandleCreate`と`HandleDelete`の先頭へ:

```csharp
// 未解放中は状態を変える操作を拒否する（GetAllは読み取り専用のため対象外・ADR 0015）
// Reject mutating operations while locked; the read-only GetAll stays open (ADR 0015)
if (!_gameUnlockState.IsBlueprintUnlocked) return FailResponse(BlueprintFailureReason.NotUnlocked);
```

- [x] **Step 4: HotbarAssignmentDatastoreへ未解放時の割当無視を実装する**

`Game.Hotbar.asmdef`の`references`へ`"Game.UnlockState"`を追加。

`HotbarAssignmentDatastore.cs`:
- `using Game.UnlockState;`を追加
- コンストラクタへ`IGameUnlockStateDataController gameUnlockState`引数を追加し`private readonly IGameUnlockStateData _gameUnlockState;`へ保持（読み取りしか使わない）
- `SetAssignment`の`IsResolvable`チェックの直後へ:

```csharp
// 未解放中のBP系新規割当は不正クライアント同様に無視。ロード済み割当はLoadHotbar側で保持される（ADR 0015）
// Ignore new blueprint-kind assignments while locked; loaded ones are preserved by LoadHotbar (ADR 0015)
if (!IsAssignableUnderUnlock(targetId)) return;
```

- privateメソッドを追加（`IsResolvable`の隣）:

```csharp
private bool IsAssignableUnderUnlock(Guid id)
{
    if (_gameUnlockState.IsBlueprintUnlocked) return true;
    if (_catalog.TryGetMasterEntry(id, out var entry)) return entry.Kind != PlacementTargetKind.BlueprintCopy;
    // マスタ外で解決可能なIDは現行BPのみのため、未解放中は割当不可
    // The only non-master resolvable ids are current blueprints, unassignable while locked
    return false;
}
```

（`LoadHotbar`と`PruneDeletedBlueprint`は変更しない＝既存BP割当・削除連動は従来どおり）

- [x] **Step 5: コンパイルとテストを実行して通す**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintProtocolTest|HotbarAssignmentDatastoreTest|HotbarSaveLoadTest" --test-mode EditMode`
Expected: PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_server
git commit -m "feat: 未解放時のBP作成削除とホットバーBP割当をサーバーで拒否する"
```

---

### Task 7: webui達成トースト（unlockedBlueprint）

**Files:**
- Modify: `moorestech_web/webui/src/features/notification/notificationMessages.ts`
- Modify: `Localization/localization.csv`（リポジトリ直下）
- Generate: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`で再生成）

**Interfaces:**
- Consumes: Task 4がサーバーから送る messageId `achievement.unlockedBlueprint`
- Produces: なし（終端）

- [x] **Step 1: ローカライズキーを追加する**

`Localization/localization.csv`の`ui.notification.unlockedConnectTool`の行を確認し、同じ列構成で直後へ`ui.notification.unlockedBlueprint`の行を追加する（日本語例:「ブループリントが解放されました」/ 英語例: "Blueprint unlocked"。既存行の文体に合わせる）。

- [x] **Step 2: キー定義を再生成する**

Run: `cd moorestech_web/webui && npm run gen:i18n`
Expected: `localizationKeys.ts`に`unlockedBlueprint: "ui.notification.unlockedBlueprint"`が生成される

- [x] **Step 3: 通知テーブルへ追加する**

`notificationMessages.ts`の`["achievement.unlockedConnectTool", ...]`行の直後へ:

```typescript
  ["achievement.unlockedBlueprint", L.ui.notification.unlockedBlueprint],
```

- [x] **Step 4: vitestを実行して通す**

Run: `cd moorestech_web/webui && npm run test`
Expected: PASS（通知テーブル・i18n整合系のテストが緑。既存赤があればbd:moorestech-2lh.1の既知10件かを確認して切り分ける）

- [x] **Step 5: コミットする**

```bash
git add Localization moorestech_web/webui/src
git commit -m "feat: ブループリント解放の達成トーストをwebuiへ追加する"
```

---

### Task 8: 全ブランチレビュー（必須・省略不可）

- [x] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

moores-code-review スキルを起動し、`origin/master`との全差分をレビューする。指摘の機械的修正は適用し、設計判断はAskUserQuestionで裁定を仰ぐ。

---

## 死活表（機能パリティ検査）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| ビルドメニューからのブロック/車両/接続ツール選択・設置 | 生きる | `IsUnlocked`のBlock/TrainCar/ConnectTool分岐は無変更 |
| BPコピーツール選択・範囲コピー・命名 | 解放後のみ（新規ロック=本機能の目的） | ユーザー裁定4件 |
| 保存済みBPの選択・ペースト・右クリック削除 | 解放後のみ（同上） | ユーザー裁定（機能全体1フラグ） |
| ホットバーの非BP割当・スワップ・解除 | 生きる | `IsAssignableUnderUnlock`はBP系のみ弾く |
| 旧セーブの既存BPデータ・ホットバーBP割当 | 保持（未解放中は減光・使用不可、解放で復活） | `LoadHotbar`無変更＋Task 6テスト |
| BP一覧の取得（GetAll・ハンドシェイク後のキャッシュ） | 生きる | GetAllは未解放でも成功（agent前提） |
| 無料設置デバッグ（FreeBlockPlacement） | Block/TrainCarのみ従来どおり。BP系は対象外 | 接続ツール前例に整列（新規パターンではない） |
| プレイテストDSLのホットバー割当・UI設置 | 生きる | 既存シナリオにBP使用なし（grep確認済み）。BP使用シナリオを作る場合は先に解放が必要 |
| webui e2e（mock-host） | 生きる | mock-hostのfixtureは実サーバー解放状態と無関係 |

## 配置と前例（spec-architecture-review）

| 項目 | 配置先 | 前例 |
|---|---|---|
| BlueprintUnlockStateHolder / Info | Game.UnlockState/Holders・States | `ConnectToolUnlockStateHolder.cs`・`ConnectToolUnlockStateInfo.cs`（同ディレクトリ・同形） |
| IsBlueprintUnlocked / OnUnlockBlueprint / UnlockBlueprint | IGameUnlockStateData / IGameUnlockStateDataController | 既存7ドメインと同じインターフェース拡張 |
| unlockBlueprint gameAction | VanillaSchema/ref/gameAction.yml + GameActionExecutorのcase | `unlockConnectTool`（パラメータ形は`playBackgroundSkit`の空properties） |
| blueprintInitialUnlockedシード | buildMenu.ymlルート（マスタ）→ Holderコンストラクタ | connectToolsの`initialUnlocked`→`ConnectToolUnlockStateHolder`ctor |
| イベント/初期データ/ミラー | UnlockedEventPacket / GetGameUnlockStateProtocol / ClientGameUnlockStateData | 同期3点セット標準（`.claude/rules/server-protocol.md`）。新規プロトコルは作らない |
| 解放判定点 | PlacementTargetCatalog.UnlockedEntries（唯一の判定点） | 既存コメント「uGUIとWebの判定ずれによる未解放対象の露出を防ぐ」 |
| プロトコル拒否 | BlueprintProtocol内のガード＋FailureReason末尾追加 | `PlaceBlockProtocol`のNotUnlocked拒否・`BlueprintFailureReason`既存enum |
| ホットバー割当拒否 | HotbarAssignmentDatastore（サーバー検証） | [[2026-08-12-ホットバー割当はサーバー側でカタログ検証する]]・不正ID無視の既存様式 |
| 達成トースト | AchievementNotificationWiring + notificationMessages.ts + localization.csv | `achievement.unlockedConnectTool`一式 |

データフロー地図（既存アンロックパイプラインへの書き手追加のみ。交差点なし）:

```
（研究/チャレンジclearedActions）→（GameActionExecutor）→［GameUnlockStateDataController(_blueprint)］
  →（UnlockedEventPacket / GetGameUnlockStateProtocol / AchievementNotificationWiring）
  →［ClientGameUnlockStateData］→（PlacementTargetCatalog.UnlockedEntries）→（ビルドメニュー/ホットバー/配置解決）
```

## 判断記録（ADR）

設計ADR: `docs/adr/0015-blueprint-feature-unlock-single-flag.md`（ユーザー裁定5件の出所欄つき。`.decisions/2026-08-18-*` 5ファイルが正）

planning中に新たに生じた判断:

- `LoadHotbar`は未解放でもBP割当を保持し、`SetAssignment`のみ未解放拒否する。
  出所: agent前提（ADR 0015 Consequences「旧セーブ由来のホットバーBP割当は減光表示で保持」との整合。Loadで落とすと解放後に復元できず裁定7に反する）
- BP系は`showAllPlaceable`（無料設置デバッグ）の解放対象に含めない。
  出所: agent前提（接続ツールの「無料設置対象外」前例に整列。既存プレイテストシナリオにBP使用が無いことをgrepで確認済み）
- 実マスタ（moorestech_master）のJSON更新はスキーマ`default: false`により不要とし、`unlockBlueprint`の研究ノード配置とあわせて後続タスクbd:moorestech-fy6で行う。
  出所: agent前提（connectTools `initialUnlocked`がスキーマdefaultを持つ前例。worktreeのmasterピン互換を壊さない）
- 研究詳細ペインの`unlockBlueprint`ラベルは本planに含めず、研究UI改修（bd:moorestech-23s）の解放物セクション実装後の後続タスクbd:moorestech-c0yとする。
  出所: agent前提（並行planとの二重編集回避。ADR 0015の当該項目はそのタスクで充足する）
- 未解放時のCreate/Delete失敗にwebui専用のdenied文言は追加しない。
  出所: agent前提（未解放中はUI導線自体が消えており、NotUnlockedはレース/不正クライアント時の防御。既存の未知messageIdフォールバック`ui.notification.unknownMessage`で足りる）
