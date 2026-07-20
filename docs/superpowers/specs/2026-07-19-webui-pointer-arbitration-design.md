# Web UI ポインタ入力調停の消費側判定化（グローバル抑止の廃止）

日付: 2026-07-19
状態: 設計確定（ユーザー承認済み）／実装待ち

## 背景と問題

現行の `Client.Input.WebUiInputExclusivity` は、Web UI(CEF)から通知される `pointerOverUi` が
true の間、Pointer スコープの全入力（カメラ Look・ワールドクリック・ホイール等）を
`InputKey.ReadValue` / `HybridInput` の読み取り境界でゼロ値に置換する「グローバル抑止ゲート」である。

この設計には2つの本質的問題がある。

1. **アプリ層の方針を汎用基盤が強制している。** 「UIホバー中にカメラを回してよいか」は
   UIState（アプリケーションレイヤー）が文脈ごとに決めるべき方針であり、実際カメラ回転可否は
   既に `UIState → IPlayerCameraInteractionApplier.SetCameraRotatable` が所有している。
   入力層でのホバー抑止は二重統治であり、「UIをドラッグしながらカメラも動かす」といった
   要件をアプリ層で表現できない。AGENTS の「汎用基盤にドメイン語彙を持ち込まない。
   判断は具体側で行う」に反する。
2. **ラッチ不具合の温床。** `pointerOverUi` の解除は「次の pointermove が透明DOMに当たること」
   に依存する。カーソルロック（GameScreen復帰）後は CEF に mousemove が届かず、true のまま
   残留してカメラ等が永久抑止される（実例: インベントリでドラッグ→Tabで閉じる→カメラ固着。
   コミット 3381f5ecc でロック中無効化の対症療法を適用済みだが、本設計で構造ごと解消する）。

一方、uGUI 側には正しい前例が既にある: ワールドクリック系の各コンシューマが
`EventSystem.current.IsPointerOverGameObject()` を**呼び出し側で判定**しており、
入力層は何も握り潰さない。本設計はWeb UIのヒットテストをこの前例と同じ形に統一する。

## 設計方針

**「抑止ゲート」を「問い合わせ可能な状態」に格下げする。**

- Pointer スコープのグローバル抑止（`InputKey` / `HybridInput` でのゼロ置換）を**廃止**する
- Web からの `pointerOverUi` 通知（WS `input_state` op）は**そのまま残し**、読み取り可能な
  状態としてのみ保持する
- ワールドに作用するクリック・ホイールのコンシューマは、uGUI と Web を統合した
  **統一クエリ**を呼び出し側で判定する（uGUI の既存パターンに合流）
- キーボード側（`textInputFocused` 中のキーバインド抑止）は**現状維持**。テキスト入力中に
  キーバインドが発火してよい文脈は存在せず、全消費者で答えが同じため中央抑止が正しい

## 変更内容

### 1. 統一クエリの新設（Client.Game）

```csharp
// Client.Game 配下（例: Client.Game.InGame.Control）
public static class UiPointerHitTest
{
    /// <summary>uGUI または Web UI のいずれかにポインタが乗っているか</summary>
    public static bool IsPointerOverAnyUi()
    {
        // カーソルロック中はOSカーソルが存在せず、いかなるUIにも乗り得ない
        // （pointerOverUiのラッチ残留もここで無害化される）
        if (Cursor.lockState == CursorLockMode.Locked) return false;
        return EventSystem.current.IsPointerOverGameObject()
            || WebUiInputExclusivity.IsPointerOverWebUi;
    }
}
```

- 配置は Client.Game 側とする（`UnityEngine.EventSystems` 参照の都合。Client.Input asmdef は
  Unity.InputSystem のみ参照であり、そこには置かない）
- クラス名・名前空間は実装時に周辺規約へ合わせて調整可

### 2. WebUiInputExclusivity の縮退（Client.Input）

- `_pointerOverUi` はフィールドとして残し、`public static bool IsPointerOverWebUi` で公開する
- `IsSuppressed` / `ProbeSuppressed` から Pointer 分岐を削除し、keyboard
  （`textInputFocused`）専用にする
- `InputSuppressionScope` enum から `Pointer` を**削除**し、コンパイルエラー駆動で
  全参照箇所（`InputManager` の InputKey 生成、`HybridInput` のマウス系、
  `BlueprintCopySystem` の直接参照）を洗い出して修正する
- コミット 3381f5ecc で `IsSuppressed` に入れたカーソルロック判定は削除する
  （統一クエリ側に同じ物理条件として吸収済み）
- `SetState(pointerOverUi, textInputFocused)` と WS `input_state` 経路、
  `WebSocketHub` の切断時リセットは変更しない

### 3. InputKey / HybridInput から Pointer 抑止を除去（Client.Input）

- Pointer スコープだった InputKey（`Player.Look`, `Playable.ScreenLeftClick` /
  `ScreenRightClick` / `ClickPosition`, `UI.InventoryItemOnePut` / `InventoryItemHalve` /
  `SwitchHotBar`）は抑止なしの素の読み取りにする
- `HybridInput.GetMouseButton*` の Pointer 抑止を除去する（Keyboard 系は現状維持）
- カメラ `Look` はこれで UIState の `SetCameraRotatable` だけが回転可否を決める状態になる
  （追加変更不要）

### 4. コンシューマの統一クエリ化（Client.Game）

**(a) 既存の `EventSystem.current.IsPointerOverGameObject()` 直呼び 9 箇所を
`UiPointerHitTest.IsPointerOverAnyUi()` に置換する:**

- `UIState/State/DragDelete/DeleteObjectService.cs:56`
- `UIState/State/SubInventory/GameScreenSubInventoryInteractService.cs:29`
- `UI/Tooltip/GameObjectToolTipTargetController.cs:36`
- `Mining/MapObjectMiningController.cs:60`
- `BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:101`
- `BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameInputCollector.cs:129`
- `BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:133`
- `BlockSystem/PlaceSystem/Blueprint/BlueprintCopySystem.cs:92`
- `BlockSystem/PlaceSystem/Blueprint/BlueprintPasteSystem.cs:81`

**(b) Web UI 上の操作でワールド作用が誤発火し得るのに判定が無いコンシューマへ、
統一クエリ判定を追加する。** 対象候補（実装時に各ファイルの発火条件を確認して要否判定）:

- `UI/Inventory/HotBarView.cs` — `SwitchHotBar`（ホイール）。Webパネルのスクロールで
  ホットバーが切り替わってはならない
- `BlockSystem/PlaceSystem/Blueprint/BlueprintCopySystem.cs:140` 付近 —
  現在 `WebUiInputExclusivity.IsSuppressed(Pointer)` を直接見ているホイール拡縮。
  統一クエリ判定へ置換
- `BlockSystem/PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs`
- `BlockSystem/PlaceSystem/ElectricWireConnect/Modes/ElectricWireExtendMode.cs` / `ElectricWireEditMode.cs`
- `BlockSystem/PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`
- `BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceSystem.cs`
- `Mining/MapObjectMiningFocusState.cs` / `MapObjectMiningMiningState.cs`
  （`MapObjectMiningController` 側で既に判定済みなら不要）
- `UIState/State/DebugBlockInfoState.cs`
- `UIState/State/PlacementPick/PlacementTargetPickService.cs`（ミドルクリックスポイト）

判定基準: **そのクリック/ホイールがワールド状態を変えるか、UI操作と衝突し得るか**。
純粋なUI内操作（`InventoryItemOnePut` 等、uGUIビュー自身がホバー対象のもの）は
ビューの活性状態で既に排他されているため追加不要。

### 5. Web UI 側（moorestech_web）

変更なし。`useWebInputExclusivity` / `input_state` 送信はそのまま。

### 6. ドキュメント

`docs/webui/design/input-focus-exclusivity.md` を本設計に合わせて改訂する
（ポインタは「抑止」ではなく「問い合わせ」モデルである旨、キーボードは中央抑止継続の旨、
2026-07-19 のロック判定の記述は統一クエリへの言及に差し替え）。

## 受け入れ条件

1. コンパイル成功（`uloop compile`）、既存テストがグリーン
2. Webインベントリでドラッグ→Tabで閉じる→カメラが即座に動く（元不具合の解消維持）
3. Webのボタン/スロットをクリックしても背後の採掘・設置・インタラクトが発火しない
4. Webパネル上のホイールスクロールでホットバーが切り替わらない
5. Webのテキスト入力中にキーバインド（B/R/T/WASD等）が発火しない（現状維持）
6. uGUIモード（Ctrl+I切替）でも 3〜4 と同等の排他が従来どおり機能する

## リスクと割り切り

- **新規コンシューマの判定漏れ**: 今後ワールドクリック実装を追加する際に統一クエリの
  呼び忘れが起こり得る。これは uGUI の `IsPointerOverGameObject` パターンが既に受け入れて
  いるリスクであり、Web だけ中央集権にする理由にはならない（レビュー観点として扱う）
- **`pointerOverUi` の残留**: ラッチ自体は残るが、値を参照するのはカーソル可視状態
  （= pointermove が流れ続け即座に更新される状態）のクリック判定のみになり、実害が消える。
  統一クエリのロック判定が二重の安全弁になる
