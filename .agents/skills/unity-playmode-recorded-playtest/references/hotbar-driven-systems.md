# ユースケース: ホットバー割当（建築ショートカット）で設置対象を操作する

ホットバーは9枠、各枠が**設置対象ID（Guid）への参照**を保持する（アイテムを手持ちするのではない）。
数字キーは「持ち替え」ではなく**建築モードのトグル**: 割当済み枠をタップすると、その設置対象を持って建築モードへ入る。
建築モード中に同じ枠を再タップすると抜ける。別の割当枠をタップすると、画面遷移せず設置対象だけ持ち替わる。
参照先はブロック・接続ツール（電線/レール/歯車チェーン）・BPコピー・BPの4種で、ビルドメニューと同じ
`PlacementTargetCatalog.UnlockedEntries`から解決される（未解放対象は割当てられない＝ビルドメニューに出ない対象と同じ扱い）。
**実証済み手本**: `gear-chain-pole-via-ui.cs`（延長設置で2本/3本セグメント構築）・`gear-chain-connect-via-ui.cs`（接続ツールのクリック結線）・`train-rail-connect-via-ui.cs`（レール結線）。

## 基本パターン（このまま使う）

```csharp
// 0. フレッシュワールドの開幕スキットを飛ばしGameScreenへ（落とし穴8参照）
await p.SkipOpeningSkit();

// 1. アンロック（ブロックと接続ツールは別枠のアンロック状態を持つ）
p.UnlockBlock("歯車チェーンポール");
p.Hotbar.UnlockConnectTool("歯車チェーン");   // 接続ツール(電線/レール/歯車チェーン)を使うときだけ必要

// 2. 在庫を用意する（ブロックは建設コスト、接続ツールは距離×マスタcountの多素材消費）
await p.GiveConstructionCost("歯車チェーンポール", 10);
await p.GiveItem("鉄のワイヤー", 100);   // 歯車チェーン接続ツールの消費素材(落とし穴7: maxStack以内に収める)

// 3. ホットバーへ割当てる（表示名一致。GiveItem/GiveItemDirectとは無関係の別経路）
await p.Hotbar.AssignHotbar(0, "歯車チェーンポール");   // slot0 = キー1
await p.Hotbar.AssignHotbar(1, "歯車チェーン");         // slot1 = キー2（接続ツール）

// 4. 同キーで建築モードへ入る／抜ける（トグル）
await p.Hotbar.SelectHotbar(0);   // ポール保持 → PlaceExtendモード（設置＋チェーン自動結線）
// ...設置操作...
await p.Hotbar.SelectHotbar(0);   // 同じ枠を再タップして建築モードを抜ける
```

## 歯車チェーンポール: 2つの操作モード

place systemの分岐は`context.Target`の型（`BlockPlacementTarget` / `ConnectToolPlacementTarget`）で決まる。
ホットバー割当・ビルドメニュー選択のどちらもこの`Target`を差し替えるだけで、経路によって挙動は変わらない。

### A. ポール(ブロック)保持＝延長設置（PlaceExtend）

- クリック1回目（起点なし）: その場に**孤立設置**。設置完了後、**そのポールが自動的に延長起点になる**
- クリック2回目以降: 新ポール設置＋起点との**チェーン自動結線**（`ExtendGearChainPole`プロトコル1発）。
  ポールの消費はブロックの`RequiredItems`、チェーンの消費は**歯車チェーン接続ツールのマスタ駆動**（distance-based。下記参照）
- 内部的にも接続ツールのGuidを要求する: `ConnectToolCatalog.TryResolveDefaultConnectToolGuid`が**未解放なら早期returnで設置自体が不成立**になる。
  ポールのブロックだけでなく`Hotbar.UnlockConnectTool("歯車チェーン")`も忘れない
- **起点のリセット＝セグメント分離**: `await p.Hotbar.SelectHotbar(slot)`（同キーで抜ける）でplace systemがDisable→ResetStateされ
  延長起点がnullに戻る。別セグメントを作るときは同キーで再度入り直す（割当は残っているので`Hotbar.AssignHotbar`のやり直し不要）
- 設置1回ごとに待つ（応答消費→起点確定に時間がかかる）:
  ```csharp
  await p.AimAtPlaceOrigin("歯車チェーンポール", origin);
  await p.ClickPlace();
  await p.Until(() => p.GetBlock(origin) != null, 15f, $"設置 {origin}");
  await p.WaitBlockGameObject(origin);   // クライアント出現＝延長起点の確定に必要
  await p.WaitSeconds(0.5f);
  ```

### B. 接続ツール保持＝既存ポール同士のクリック結線（ChainConnect）

```csharp
await p.Hotbar.SelectHotbar(1);              // 歯車チェーン接続ツールを保持（同キーで建築モードへ）
await p.AimAt(poleClickPoint(c1));    // ポールAクリック → 起点選択
await p.ClickPlace();
await p.WaitSeconds(0.3f);
await p.AimAt(poleClickPoint(c2));    // ポールBクリック → ConnectGearChain送信
await p.ClickPlace();
```

ポールのクリック座標は**接続エリアコライダーの中心**を狙う（skillの定石 collider.bounds.center）:

```csharp
System.Func<Vector3Int, Vector3> poleClickPoint = pos =>
{
    var blockObject = Client.Game.InGame.Context.ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
    var area = blockObject.GetComponentInChildren<Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts.GearChainPoleConnectAreaCollider>(true);
    return area.GetComponent<Collider>().bounds.center;
};
```

## 接続ツールの消費量: 距離×マスタcount

電線/レール/歯車チェーンの3接続ツールは**距離に応じた多素材消費**（`ConnectToolCostCalculator`）:
`units = ceil(距離 / lengthPerUnit)`、各`requiredItems`の消費 = `units × count`。
`buildMenu.json`の`connectTools`にマスタがあり、`name`（表示名）と`requiredItems`の材料は**別物**なので
`Hotbar.AssignHotbar`には`name`を、`GiveItem`には`requiredItems`側の実材料名を渡す（例: 歯車チェーン→鉄のワイヤー、
レール→補強棒材+鉄板、電線→銅のワイヤー）。距離を跨いだ余裕を持って多めに付与しておくと安全。

## 検証の定石: 歯車ネットワーク所属

```csharp
var gearNetworkDatastore = p.ServerService<Game.Gear.Common.GearNetworkDatastore>();
System.Func<Vector3Int, Game.Gear.Common.GearNetworkId?> networkOf = pos =>
{
    var block = p.GetBlock(pos);
    if (block == null) return null;
    return gearNetworkDatastore.TryGetGearNetwork(block.BlockInstanceId, out var network) ? network.NetworkId : (Game.Gear.Common.GearNetworkId?)null;
};
p.Assert(networkOf(a1).Equals(networkOf(a2)), "同一セグメント");
p.Assert(!networkOf(a1).Equals(networkOf(b1)), "別セグメント");
```

- 孤立ポールも単独ネットワークに所属する（nullは「ブロック不在」を意味する）
- 結線検証は**結線前に「別ネットワーク」をassert**してから結線する（クリックが効いた証明になる）

## 落とし穴

1. **未割当の枠をタップすると建築モードを抜ける**（旧仕様との最大の違い）。`Hotbar.AssignHotbar`せずに
   `Hotbar.SelectHotbar`だけ呼ぶと、GameScreenでは何も起きず、PlaceBlock中なら即座に抜けてしまう
2. **接続ツールはブロックと別枠のアンロック状態**（`ConnectToolUnlockStateInfos`）を持つ。ブロックだけ
   `UnlockBlock`しても、対応する接続ツールを`Hotbar.UnlockConnectTool`しないと延長設置・クリック結線どちらも動かない
3. **PlaceBlock状態に入らないとplace systemは動かない**（ManualUpdateはPlaceBlockState内のみ）。
   ホットバーに割当てるだけではダメで、`Hotbar.SelectHotbar`で実際に建築モードへ入ってから操作する
4. **保持中の対象がplace systemの分岐を決める**（ブロック=延長設置、接続ツール=クリック結線）。
   選択が何であれ、対応するplace systemがManualUpdateで動く
5. クリック判定は`IsPointerOverGameObject`を厳密に見る（UIに被る画素を狙うと無反応）。
   通常ブロック設置(解放時判定)より厳しいので、照準先が左のキー操作ヘルプ等と重ならない座標にする
6. maxConnectionCount（ポールあたり接続上限）とmaxConnectionDistance（結線距離上限）はblocks.jsonの
   `blockParam`で確認してから配置間隔を決める
7. **`GiveItem`は1回の呼び出しで1スタックを生成**するため、`count`がアイテムのmaxStack（多くは100）を
   超えると`ArgumentOutOfRangeException`がサーバー側で握りつぶされ`Until`が無言タイムアウトする。
   大量に必要なら100以下に収めるか複数回`GiveItem`を呼ぶ
8. **フレッシュワールドの開幕スキット(Story)がホットバー入力より優先**される（`GameScreenState`の
   スキット遷移チェックがホットバー遷移チェックより後にあり、スキット中は`GameScreen`にすら入れない）。
   環境構築直後に`await p.SkipOpeningSkit()`で飛ばし`GameScreen`到達を待ってから
   `Hotbar.AssignHotbar`/`Hotbar.SelectHotbar`する（前例: `electric-wire-mutual-range-via-ui.cs`）
9. **`Hotbar.SelectHotbar`自体はUIState遷移完了を待たない**（キータップ+固定0.5秒インターバルのみ）。
   同キーで抜けてすぐ入り直す・別枠へ持ち替えてすぐ操作するなど連続呼び出しは、各タップの後に
   `p.WaitUiState(UIStateEnum.GameScreen/PlaceBlock, 10f)`を挟まないと、前の遷移未完了のまま次のタップが
   割り込みplace systemの状態リセットと競合しうる
