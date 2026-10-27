# ユースケース: 新しいシナリオを書く

シナリオは execute-dynamic-code のC#スニペット形式（using可・文の羅列・returnで値を返す）。
置き場所は本スキル同梱の `scenarios/<カテゴリ>/<名前>.cs`（`.claude/skills/unity-playmode-recorded-playtest/scenarios/` 配下のカテゴリ別サブディレクトリ: `building/` `connect/` `train/` `misc/`）。**実証済みの手本**: `building/belt-segment-flow.cs`(direct構築・ベルト搬送) /
`connect/gear-chain-pole-via-ui.cs`(ホットバー駆動) / `connect/gear-chain-connect-via-ui.cs`(クリック結線)。

## テンプレート（このまま使う）

```csharp
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

// 調査は録画を省き、提出用の最終実行だけ録画する
// Skip recording during investigation; record only the final submission run
var options = new PlaytestRunOptions { Record = false };
// キー・マウス使用時はready後、この位置でinput-injection.mdのde-risk probeを完了する
// For keyboard/mouse input, complete the input-injection.md de-risk probe here after ready
// UI probeはready → Start() → 期限付きIsAvailable()待機 → probeの順で実行する
// For a UI probe, wait for ready, call Start(), wait for IsAvailable() with a deadline, then probe
// 成功・失敗を問わずfinallyでPlaytestUiLocatorSession.Stop()を呼び、失敗理由を記録する
// Always call PlaytestUiLocatorSession.Stop() in finally and record any failure reason
return PlaytestRunner.Run("my-scenario", options, async p =>
{
    // デバッグ環境を1行構築（無限落下防止の足場+ワープ+無料設置）。設定値は冒頭ログとTimelineに流れる
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 開幕スキットは全UI入力を塞ぐため必ず先に飛ばす（プレイテストのフレッシュワールドでは常に再生される）
    await p.SkipOpeningSkit();
    p.Note("チェスト設置フェーズ開始");                       // AIナレーション（動画とTimelineに残る）
    // ...操作...
    p.Assert(条件, "ラベル");                                // 失敗しても続行し記録される
    await p.Until(() => 条件, 30f, "ラベル");                // タイムアウトで例外中断+記録
    await p.Screenshot("01-milestone");
});
```

調査用は`Record = false`で`result.json`の`Success`と`Asserts`を確認する。提出用の最終実行だけ`Record = true`にして、動画・期待スクショ・実プレイ視点も確認する。
キー・マウスを使う場合は[input-injection.md](input-injection.md)のde-risk probeを`PlaytestRunner.Run`前のトップレベルで完了する。UIをprobeする場合はreadyを確認して`PlaytestUiLocatorSession.Start()`を呼び、期限付きで`PlaytestUiLocatorSession.Locator.IsAvailable()`を待ってからprobeする。成功・失敗を問わずfinallyで`PlaytestUiLocatorSession.Stop()`を呼び、失敗理由を記録する。必要なEditor／GameViewフォーカス回復、有効状態の再確認、単体入力成立確認は、`Record=true`へ切り替えた最終実行でもコールバック内に置かない。

`PlaytestEnvironmentConfig` のフィールド（すべてpublicフィールド・デフォルト値あり）:
`FreeBlockPlacement=true`（建築全解放+クライアント/サーバー両方のコスト無料。falseでも毎回明示保存され前回実行の残留なし）/
`CreateFlatGround=true`（上面y=32の足場）/ `SpawnPosition=(0, 33.5, 0)`。
在庫消費まで検証したい場合のみ `FreeBlockPlacement = false` にして従来の `PrepareBlockForUiPlacement` を使う。

## Driver API 全リファレンス（PlaytestDriver）

### セットアップ・状態構築（サーバー直＝検証対象外の準備用）
| API | 用途 |
|---|---|
| `SetupDebugEnvironment(config)` | **推奨の最初の1行**。足場生成+ワープ+無料設置トグルを`PlaytestEnvironmentConfig`で一括設定し、全設定値をログ/Timelineへ出力 |
| `SkipOpeningSkit()` | 開幕スキットをSkipインテントで飛ばし`GameScreen`到達まで待つ。スキット中は全UI入力が塞がるため**`SetupDebugEnvironment`直後の定型2行目** |
| `SetupFlatGround()` | 足場(50x4x50 @ y30、**上面y=32ちょうど**、`GroundGameObject`付与)生成+ワープのみ（旧API・互換） |
| `WarpPlayer(pos)` / `PlayerPosition` | テレポート/現在地。UI設置前に対象範囲の中央へワープ推奨 |
| `GiveItemDirect(name, count)` | サーバーインベントリ直挿入（即時） |
| `GiveItem(name, count)` | 本番giveコマンド経路＋サーバー在庫反映待ち |
| `UnlockBlock(name)` | サーバー側アンロック→イベントでクライアント（ビルドメニュー）同期 |
| `GiveConstructionCost(name, blockCount)` | マスタ`RequiredItems`×個数をgive経路で付与＋クライアント同期待ち |
| `PrepareBlockForUiPlacement(name, blockCount)` | ↑2つの複合。**UI設置の前提はこれ1行** |
| `PlaceBlockDirect(name, pos, dir)` | サーバー直設置（インベントリ非消費・アンロック不問） |
| `RemoveBlock(pos)` / `GetBlock(pos)` | サーバーデータストアの削除/取得（GetBlockはassertにも使う） |

### UI経路操作（実プレイヤーと同じキーマウ経路＝検証対象）
| API | 用途 |
|---|---|
| `OpenBuildMenuAndSelectBlock(name)` | B/Tab注入→ビルドメニュー→スロット選択→PlaceBlock遷移＋選択後0.6s待機。**スロットはUIロケータ経由でクリックする。描画系が15秒以内に使えなければ例外** |
| `PlaceBlockViaUi(name, origin, dir)` | 単クリック設置の統合操作（**向きはNorth固定**）。設置反映Until込み |
| `DragPlaceViaUi(name, from, to)` | ドラッグ設置（ベルト等）。**向きは経路から自動解決** |
| `ExitToGameScreen()` | B注入でGameScreenへ（**place systemの内部状態をリセットする副作用**が重要。歯車ポールの延長起点等） |
| `Hotbar.SelectHotbar(slot)` | 数字キー注入（slot 0→キー1）。建築モードのトグル: 割当済み対象を持って入り、同じ枠で抜ける |
| `Hotbar.AssignHotbar(slot, targetName)` | ビルドメニューと同一供給源(`PlacementTargetCatalog.UnlockedEntries`)から表示名でホットバーへ割当てる |
| `Hotbar.UnlockConnectTool(name)` | 接続ツール(電線/レール/歯車チェーン)のアンロック。ブロックとは別枠の状態で、`Hotbar.AssignHotbar`前に必要 |
| `PressKey(key)` | 任意キーのタップ（UnityEngine.InputSystem.Key） |
| `AimAt(worldPos)` / `AimAtPlaceOrigin(name, origin)` | マウス絶対座標照準（後者は設置原点→フットプリント中心の逆算込み） |
| `ClickPlace()` | 左クリック（押下→2フレーム→解放。設置はGetKeyUpで確定するため解放必須） |
| `PressInteract()` | Fを単押し（機械を開く・小石を拾う） |
| `HoldInteract(seconds)` | Fを指定秒押し続ける（採掘・手掘り） |
| `PressRide()` | Eを単押し（列車に乗る） |
| `MiddleClick()` | ミドルクリック（スポイト。押下→2フレーム→解放） |
| `PickWithAltHold(worldPos)` | 通常モードのスポイト1組（左Alt押下→照準→ミドルクリック→解放）。**GameScreenでのスポイトは必ずこれを使う** |
| `CurrentUiState` / `WaitUiState(state, timeout)` | UIState確認/遷移待ち |

通常モード（GameScreen）の三人称照準は左Alt非押下中は画面中央に固定されるため、`AimAt`＋`MiddleClick`だけでは
画面中央外の対象をスポイトできない（[docs/adr/0008](../../../../docs/adr/0008-aim-source-pushed-by-ui-state.md)）。
シナリオ側でAltホールドを書き写さず`PickWithAltHold`を呼ぶこと（実体は`Client.Playtest.Operations.PlaytestPickOps`）。
PlaceBlock中のスポイトは照準がカーソル追従のためAltは不要で、`AimAt`＋`MiddleClick`でよい。

### UI 操作（testid で要素を解決し通常マウス経路で操作。描画系に依存しない）
| API | 用途 |
|---|---|
| `ClickUi(testid)` | `testid` の要素中心へ注入マウスを滑走→クリック。2 回照会の矩形一致（レイアウト安定）＋被覆チェック込み。要素出現まで内部リトライ（15s） |
| `HoverUi(testid)` | 同解決でポインタ移動のみ（ツールチップ検証等） |
| `UntilUiElement(testid, timeout)` | 要素が可視かつクリック可能（遮蔽なし）になるまで待つ |
| `SetText(testid, text)` | テキスト欄の値を置き換えて変更イベントを流す（IME は通さない。Web は input/textarea、段階1以降の UITK は TextField）。欄の出現・有効化まで内部リトライ（15s） |
| `ClickBuildMenuBlock(blockName)` | ビルドメニューのブロックスロットを1行でクリック（testid `build-menu-entry-block-{BlockGuid}` を自動解決） |
| `CloseBuildMenuPanel()` | ビルドメニューの閉じるボタン（`build-menu-close`）をクリック |
| `PlaytestUiElementOps.FindUi(testid)` | 1 回照会して `UiElementSnapshot` を返す（`IsFound`・`Visible`・`Clickable`・`ScreenRect`〈Unity スクリーン座標・左下原点〉・`Text`・`HasStateFlag(name)`）。待たない |
| `PlaytestUiElementOps.WaitUiTextContains(testid, text, timeout)` | 要素の表示文字列が `text` を含むまで待つ |
| `PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(testid, scrollAreaTestid, timeout)` | スクロール領域上でホイールを回して要素を表示領域へ送る |

単発の照会をRun前に行う例。事前probeで開いたロケータは失敗時も閉じる。

```csharp
PlaytestUiLocatorSession.Start();
try
{
    var inventorySort = await PlaytestUiElementOps.FindUi("inventory-sort");
    if (!inventorySort.IsFound) throw new System.InvalidOperationException("inventory-sortが見つからない");
}
finally
{
    PlaytestUiLocatorSession.Stop();
}
```

Run内の説明文入力は、欄をクリックした直後に `await p.SetText("bug-report-description", description);` を使う。設置理由の表示観測のように長いシナリオ固有処理は `PlacementReasonTooltipScenarioProbe` の使用例を参照できる。これはEditor専用の観測補助で、Driverの汎用UI入力は上のAPIを使う。

状態フラグ: Web は `data-testid` 以外の `data-*` 属性のうち値が空か `true` のもの（`data-selected="true"` → `HasStateFlag("selected")`）。
シナリオは描画系の内部型を直接参照せず、照会と入力に上の API を使う。
同じtestidが複数あるとFind/SetTextはAmbiguousで失敗する。SetTextはDOMを書き換える前に拒否する。ロケータを直接使う場合はFind(testid, remainingSeconds)、SetText(testid, text, remainingSeconds)へ残予算を必ず渡す。Web通信上限は既存3秒との小さい方。Stop後の旧SetText再試行は終了する。

testidは `moorestech_web/webui/src/features/**` をgrepして実在確認する。ビルドメニューのエントリは
`build-menu-entry-{kind}-{id}`（kind: block/trainCar/connectTool/blueprintCopy/blueprint、
idは種別を問わず設置対象のGuid文字列。blockのtestidは`PlaytestBuildMenuOps.BuildMenuBlockTestId(名前)`で組み立てる）。
表示名はスロットではなく詳細サイドバー(`build-menu-detail`)に出るため、名前を絵に残すには`HoverUi`してからScreenshotする。
カテゴリ切替は`build-menu-category-{categoryGuid}`（カテゴリ名ではなくGuid）。PlaceBlock遷移で視点は変わらないため、
シナリオ側での追加のカメラ待ちは不要。
仕組み: DSL は `IUiElementLocator`（`PlaytestUiLocatorSession.Locator`）だけを見る。Web 版は DOM 矩形を WS 往復（`playtest.dom_query`）で取得し、Unity スクリーン座標へ逆変換して `SemanticInput` で注入する。注入マウスはプレイテスト専用の入力転送経路を通る（パッケージの legacy Input 転送は注入不感のため）。文字入力は `playtest.dom_set_text` の往復でネイティブ setter＋`input` イベント。どの描画系で解決したかは `[PlaytestUi] <testid> resolved by Web` のログに出る。

### 検証・待機・記録
| API | 用途 |
|---|---|
| `Assert(cond, label)` | 記録して続行 |
| `Until(cond, timeout, label)` | 条件成立待ち（固定sleep禁止の代替）。タイムアウトは例外中断 |
| `WaitBlockGameObject(pos)` | クライアント側View出現待ち（スクショ前・ポール起点確定前に必須） |
| `WaitSeconds(s)` / `Screenshot(name)` | 短い演出待ち/GameViewスクショ（UIオーバーレイ込み） |
| `CountItem(name)` | サーバー在庫数 |
| `SendCommand(cmd)` / `ServerService<T>()` | 低レベル脱出口（VanillaApi / ServerContext DI） |
| `Note(message)` | AIナレーション。動画左上のログとresult.jsonのTimelineに記録 |

## 書き方の規則

1. **ブロック/アイテムはマスタの日本語Name**で指定（「木のチェスト」「鉄インゴット」）。実在名は
   `<master>/mods/*/master/blocks.json` / `items.json` で確認。存在しない名前は即例外
2. **シナリオ内では全アセンブリが使える**（EDCは全ロード済みアセンブリ参照）。assertで
   `Game.Gear.Common.GearNetworkDatastore` 等サーバー内部も直接読める。Driver本体(asmdef)は制限あり
3. **状態構築はDirect系、検証対象の操作はUI経路/本番プロトコル**と使い分ける。UI設置は
   インベントリの`RequiredItems`を消費し未解放ブロックは置けない（Directは両方無視）
4. **搬送ライン系は投入前に接続数をassert**:
   `p.GetBlock(pos).GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count`
   を先に確認。接続0の原因は座標ミスのほか**受け側マスタの`inventoryConnectors.inputConnects`が空**
   （＝そのブロックは搬入を受けない仕様。例: 素の木のチェスト→コンベアチェストを使う）
5. **歯車ネットワークのassert**（連結検証の定石）:
   ```csharp
   var gearNetworkDatastore = p.ServerService<Game.Gear.Common.GearNetworkDatastore>();
   // posのブロックの所属NW: TryGetGearNetwork(blockInstanceId, out network)でnetwork.NetworkIdを取る
   ```
6. 複数ブロック連結はYAMLの`inputConnects[].offset`/`outputConnects[].offset`を
   「OriginalPos + offset = 絶対座標」で先に表にする（手書き座標は「設置は成功するが繋がらない」を生む）
7. スニペットで使うAPI/フィールド名は**書く前に実ファイルをRead/Grepで実在確認**（存在しない名前は
   pollingを無限ループ化させる最頻出ミス）
8. **フェーズごとに`p.Note()`で実況を書く**。非同期アクションAPIは0.5秒インターバル自動挿入。
   同期API連打時は`WaitSeconds(0.5f)`を挟む（動画の認知性ルール、SKILL.md参照）

## 複雑シナリオはStep 0（サブエージェント探索）を先に

複数ブロック連結・UI操作・列車などは、Plan/general-purposeサブエージェントに
「前提手順の連鎖 / 各操作の呼び出し経路（legacy Input直読みの有無をgrep）/ 絶対座標とconnector offsetの表 /
API実在確認 / Step単位の実行計画」を調査・出力させてから書く。

CEF向け要求は実際のCEF起動へ対応付けた接続だけに配信する。外部ブラウザが表示されていてもCEFのIsAvailableにはならない。不正応答は契約例外となりresult.Errorへ理由が残る。
