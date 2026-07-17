# ユースケース: ホットバー手持ち駆動システム（歯車チェーンポール・レール・電線等）を操作する

一部のブロックはビルドメニュー選択ではなく**ホットバーの手持ちアイテム（HoldingItemId）**で
専用place systemに切り替わる。placeSystem.jsonマスタの`usePlaceItems`（例: 歯車チェーン・レール・蒸気機関車）と、
歯車チェーンポールのブロックアイテムがこれに該当する。
**実証済み手本**: `gear-chain-pole-via-ui.cs`（延長設置で2本/3本セグメント構築）・`gear-chain-connect-via-ui.cs`（チェーン手持ちクリック結線）。

## 基本パターン（このまま使う）

```csharp
// 1. アイテムをホットバーの特定スロットへ（GiveItem/GiveItemDirectは先頭空きスロットに入るため不可）
p.UnlockBlock("歯車チェーンポール");                       // ビルドメニュー表示に必要
await p.GiveItemToHotbar(0, "歯車チェーンポール", 10);     // slot0 = キー1
await p.GiveItemToHotbar(1, "歯車チェーン", 20);           // slot1 = キー2

// 2. PlaceBlock UI状態へ入る（唯一の入口はビルドメニューのスロットクリック）
await p.OpenBuildMenuAndSelectBlock("歯車チェーンポール");

// 3. ホットバーで手持ちを切り替える → HoldingItemIdでplace systemが切り替わる
await p.SelectHotbar(0);   // ポール手持ち → PlaceExtendモード（設置＋チェーン自動結線）
```

## 歯車チェーンポール: 2つの操作モード

### A. ポール手持ち＝延長設置（PlaceExtend）

- クリック1回目（起点なし）: その場に**孤立設置**。設置完了後、**そのポールが自動的に延長起点になる**
- クリック2回目以降: 新ポール設置＋起点との**チェーン自動結線**（`ExtendGearChainPole`プロトコル1発）。
  ポールはホットバーの手持ちスタックから、チェーンは**インベントリ内の所持から自動選択**して消費される
- **起点のリセット＝セグメント分離**: `await p.ExitToGameScreen()` でplace systemがDisable→ResetStateされ
  延長起点がnullに戻る。別セグメントを作るときは抜けてから入り直す
- 設置1回ごとに待つ（応答消費→起点確定に時間がかかる）:
  ```csharp
  await p.AimAtPlaceOrigin("歯車チェーンポール", origin);
  await p.ClickPlace();
  await p.Until(() => p.GetBlock(origin) != null, 15f, $"設置 {origin}");
  await p.WaitBlockGameObject(origin);   // クライアント出現＝延長起点の確定に必要
  await p.WaitSeconds(0.5f);
  ```

### B. チェーン手持ち＝既存ポール同士のクリック結線（ChainConnect）

```csharp
await p.SelectHotbar(1);              // 歯車チェーンを手持ちに（同じPlaceBlock状態のまま切替可）
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

## 検証の定石: 歯車ネットワーク所属

```csharp
var nets = p.ServerService<Game.Gear.Common.GearNetworkDatastore>().GearNetworks;
System.Func<Vector3Int, Game.Gear.Common.GearNetworkId?> networkOf = pos =>
{
    var block = p.GetBlock(pos);
    if (block == null) return null;
    foreach (var pair in nets)
    foreach (var t in pair.Value.GearTransformers)
        if (t.BlockInstanceId == block.BlockInstanceId) return pair.Key;
    return null;
};
p.Assert(networkOf(a1).Equals(networkOf(a2)), "同一セグメント");
p.Assert(!networkOf(a1).Equals(networkOf(b1)), "別セグメント");
```

- 孤立ポールも単独ネットワークに所属する（nullは「ブロック不在」を意味する）
- 結線検証は**結線前に「別ネットワーク」をassert**してから結線する（クリックが効いた証明になる）
- 消費量も検証材料: ポール=設置数、チェーン=結線数だけホットバー/在庫から減る

## 落とし穴

1. **PlaceBlock状態に入らないとplace systemは動かない**（ManualUpdateはPlaceBlockState内のみ）。
   ホットバーに持つだけではダメで、ビルドメニュー経由でPlaceBlockへ入ってから`SelectHotbar`する
2. **ポール手持ち中はメニュー選択より優先**される（選択が何であれGearChainPole systemが動く）
3. クリック判定は`IsPointerOverGameObject`を厳密に見る（UIに被る画素を狙うと無反応）。
   通常ブロック設置(解放時判定)より厳しいので、照準先が左のキー操作ヘルプ等と重ならない座標にする
4. maxConnectionCount（ポールあたり接続上限）とmaxConnectionDistance（結線距離上限）はblocks.jsonの
   `blockParam`で確認してから配置間隔を決める
