# ユースケース: UI経路（ビルドメニュー→プレビュー→クリック/ドラッグ）でブロックを設置する

実プレイヤーと同じキーマウ経路で構築し、UI・操作系・設置プレビュー系のバグをE2Eで捕まえる。
**実証済み手本**: 本スキル同梱の `scenarios/belt-line-via-ui.cs`（direct版`belt-line.cs`と同一assertで全通過）。

## 最小コード（このまま使う）

```csharp
await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());   // 足場+ワープ+無料設置を1行で
p.WarpPlayer(new Vector3(3.5f, 33.5f, -1f));       // 設置エリアの南側へ（設置カメラは北向き・浅ピッチ）

// 遠いセルから設置する（手前の既設ブロックが奥セルへのレイを遮るため。単クリックは隣接が埋まる前に）
// 単クリック設置: 向きはNorth固定
await p.PlaceBlockViaUi("木のコンベアチェスト", new Vector3Int(4, 32, 8), BlockDirection.North);
// ドラッグ設置: 向きは経路から自動解決（(2,2)→(2,6)なら北向き。ベルトのライン構築はこれ）
await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), new Vector3Int(2, 32, 6));
await p.ExitToGameScreen();
```

## 前提（欠けると必ず失敗する）

1. **`SetupDebugEnvironment(new PlaytestEnvironmentConfig())` 必須（推奨）**: UI設置のレイキャストは `GroundGameObject` コンポーネント付き、
   かつ**上面がy=32ちょうど**の足場が前提（`Floor(hit.y)` がブロックグリッドと一致する条件）。
   素のCubeを自作すると「プレビューが出ない/1段沈む」。旧`SetupFlatGround()`は足場+ワープのみ
2. **アンロックとコスト**: デフォルトの`FreeBlockPlacement=true`なら**メニュー全表示+コスト無料**になり
   `PrepareBlockForUiPlacement`は不要。在庫消費まで検証するときのみ`FreeBlockPlacement=false`にして
   `PrepareBlockForUiPlacement`（アンロック+建設コスト付与）を使う
3. **プレイヤーは設置エリアの南側に立たせる**: 設置カメラは**北向き・浅いピッチ**（旧トップダウンから変更）。
   足元・背後のセルは画面外になり `AimAtWorldPosition` が「off-screen」例外で失敗する（黙った空振りは廃止済み）
4. **カメラから遠いセル→近いセルの順に設置**: 浅いカメラでは手前の既設ブロック天面/側面が
   奥セルへの照準レイを奪い誤設置する。単クリック設置は隣接セルが埋まる前に行う

## 内部動作（デバッグ時に知るべきこと）

- UI状態遷移: GameScreen --B--> BuildMenu --スロットクリック--> PlaceBlock。
  **PlaceBlock中のBはGameScreenへ抜ける。メニュー再オープンはTab**（`OpenBuildMenuAndSelectBlock`が自動で使い分け）
- ビルドメニューのスロット選択は**CEF(Web UI)モードではDOMクリック経路**
  （DOM矩形→座標逆変換→注入マウス→`CefInputForwarder`がCEFへ転送。write-scenario.mdのWeb UI操作参照）、
  **uGUIモードではEventSystem直叩き**（`ExecuteEvents.pointerDown/UpHandler`）へ自動分岐
- 照準は `PlaytestUiOps.PlaceAimPoint` = CalcPlacePointの逆算。**接地面上のフットプリント中心**
  （`origin + rotatedSize/2` のx,z、y=origin.y）を狙えば指定originに置かれる
- クリックは押下→2フレーム→解放。**設置はScreenLeftClickのGetKeyUp（解放）で確定**する
- 設置システムの選択優先度: HoldingItemId（ホットバー手持ち）のplaceSystemマスタ >
  歯車チェーンポールのブロックアイテム > ビルドメニュー選択がベルトファミリー > 通常ブロック。
  **ホットバーにplace mode系アイテムを持ったままだとメニュー選択が無視される**ので注意

## 制約（現状の仕様）

| 制約 | 回避策 |
|---|---|
| 単クリック設置の向きはNorth固定（place system内部の`_currentBlockDirection`は外部から読めず回転キー注入は未対応） | 向きが要るラインはドラッグで組む／Northで成立する配置を選ぶ |
| ベルトのドラッグは長尺バリアントに自動分解されることがある | セル単位のGetBlockは「そのセルを覆うブロック」を返すのでassertは通る |
| 注入が効くのはInputSystem/`Client.Input.HybridInput`経由のコードのみ | 駆動しない入力を見つけたらHybridInput化する（input-injection.md） |
| 高さのあるブロック（石窯等・高さ2以上）の**隣接セルへ後から**設置すると、照準レイが既設ブロックの天面にヒットして上（y+段数）に誤設置される | **背の高いブロックを最後に設置**する（ベルト等の低いブロックを先に敷く） |
| 電気系ブロック（電線コネクタ持ち）は建設コストだけではプレビューが赤のまま設置不能（`ElectricWireAutoConnectPreview.TrySelectWire`の在庫ガード＝既知の実バグ。非電気ブロックは影響なし） | 修正されるまで `GiveItem("<ブロック名>", 1)` でブロックアイテム自体を在庫へ足す（設置クリックはUI経路のまま保てる） |

## 検証の定石

- 設置反映は `PlaceBlockViaUi`/`DragPlaceViaUi` にUntil内蔵。追加の目視用に `WaitBlockGameObject` → `Screenshot`
- 搬送ラインは投入前に末端の `BlockConnectorComponent<IBlockInventory>.ConnectedTargets.Count` をassert
  （受け側の `inputConnects` が空のブロックは何をしても繋がらない。コンベアチェスト等を使う）
