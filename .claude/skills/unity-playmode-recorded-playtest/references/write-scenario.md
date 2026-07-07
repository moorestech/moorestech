# ユースケース: 新しいシナリオを書く

シナリオは execute-dynamic-code のC#スニペット形式（using可・文の羅列・returnで値を返す）。
置き場所は本スキル同梱の `scenarios/<名前>.cs`（`.claude/skills/unity-playmode-recorded-playtest/scenarios/`）。**実証済みの手本**: `belt-line.cs`(direct構築) /
`belt-line-via-ui.cs`(UI経路) / `gear-chain-pole-via-ui.cs`(ホットバー駆動) / `gear-chain-connect-via-ui.cs`(クリック結線)。

## テンプレート（このまま使う）

```csharp
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };   // Record=true でmp4録画内蔵
return PlaytestRunner.Run("my-scenario", options, async p =>
{
    await p.SetupFlatGround();                              // 足場生成+ワープ（必ず最初）
    // ...操作...
    p.Assert(条件, "ラベル");                                // 失敗しても続行し記録される
    await p.Until(() => 条件, 30f, "ラベル");                // タイムアウトで例外中断+記録
    await p.Screenshot("01-milestone");
});
```

## Driver API 全リファレンス（PlaytestDriver）

### セットアップ・状態構築（サーバー直＝検証対象外の準備用）
| API | 用途 |
|---|---|
| `SetupFlatGround()` | 足場(50x4x50 @ y30、**上面y=32ちょうど**、`GroundGameObject`付与)生成+ワープ。UI設置の前提 |
| `WarpPlayer(pos)` / `PlayerPosition` | テレポート/現在地。UI設置前に対象範囲の中央へワープ推奨 |
| `GiveItemDirect(name, count)` | サーバーインベントリ直挿入（即時） |
| `GiveItem(name, count)` | 本番giveコマンド経路＋サーバー在庫反映待ち |
| `GiveItemToHotbar(slot, name, count)` | **ホットバー特定スロット**(0始まり)へ直接セット＋クライアント同期待ち。HoldingItemId駆動システム用 |
| `UnlockBlock(name)` | サーバー側アンロック→イベントでクライアント（ビルドメニュー）同期 |
| `GiveConstructionCost(name, blockCount)` | マスタ`RequiredItems`×個数をgive経路で付与＋クライアント同期待ち |
| `PrepareBlockForUiPlacement(name, blockCount)` | ↑2つの複合。**UI設置の前提はこれ1行** |
| `PlaceBlockDirect(name, pos, dir)` | サーバー直設置（インベントリ非消費・アンロック不問） |
| `RemoveBlock(pos)` / `GetBlock(pos)` | サーバーデータストアの削除/取得（GetBlockはassertにも使う） |

### UI経路操作（実プレイヤーと同じキーマウ経路＝検証対象）
| API | 用途 |
|---|---|
| `OpenBuildMenuAndSelectBlock(name)` | B/Tab注入→ビルドメニュー→対象スロットをEventSystem直叩きでクリック→PlaceBlock遷移＋カメラtween待ち0.6s |
| `PlaceBlockViaUi(name, origin, dir)` | 単クリック設置の統合操作（**向きはNorth固定**）。設置反映Until込み |
| `DragPlaceViaUi(name, from, to)` | ドラッグ設置（ベルト等）。**向きは経路から自動解決** |
| `ExitToGameScreen()` | B注入でGameScreenへ（**place systemの内部状態をリセットする副作用**が重要。歯車ポールの延長起点等） |
| `SelectHotbar(slot)` | 数字キー注入（slot 0→キー1）。HoldingItemIdが変わりplace systemが切り替わる |
| `PressKey(key)` | 任意キーのタップ（UnityEngine.InputSystem.Key） |
| `AimAt(worldPos)` / `AimAtPlaceOrigin(name, origin)` | マウス絶対座標照準（後者は設置原点→フットプリント中心の逆算込み） |
| `ClickPlace()` | 左クリック（押下→2フレーム→解放。設置はGetKeyUpで確定するため解放必須） |
| `CurrentUiState` / `WaitUiState(state, timeout)` | UIState確認/遷移待ち |

### 検証・待機・記録
| API | 用途 |
|---|---|
| `Assert(cond, label)` | 記録して続行 |
| `Until(cond, timeout, label)` | 条件成立待ち（固定sleep禁止の代替）。タイムアウトは例外中断 |
| `WaitBlockGameObject(pos)` | クライアント側View出現待ち（スクショ前・ポール起点確定前に必須） |
| `WaitSeconds(s)` / `Screenshot(name)` | 短い演出待ち/GameViewスクショ（UIオーバーレイ込み） |
| `CountItem(name)` | サーバー在庫数 |
| `SendCommand(cmd)` / `ServerService<T>()` | 低レベル脱出口（VanillaApi / ServerContext DI） |

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
   var nets = p.ServerService<Game.Gear.Common.GearNetworkDatastore>().GearNetworks;
   // posのブロックの所属NW: 各netのGearTransformersからBlockInstanceId一致を探す
   ```
6. 複数ブロック連結はYAMLの`inputConnects[].offset`/`outputConnects[].offset`を
   「OriginalPos + offset = 絶対座標」で先に表にする（手書き座標は「設置は成功するが繋がらない」を生む）
7. スニペットで使うAPI/フィールド名は**書く前に実ファイルをRead/Grepで実在確認**（存在しない名前は
   pollingを無限ループ化させる最頻出ミス）

## 複雑シナリオはStep 0（サブエージェント探索）を先に

複数ブロック連結・UI操作・列車などは、Plan/general-purposeサブエージェントに
「前提手順の連鎖 / 各操作の呼び出し経路（legacy Input直読みの有無をgrep）/ 絶対座標とconnector offsetの表 /
API実在確認 / Step単位の実行計画」を調査・出力させてから書く。
