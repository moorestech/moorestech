# 設置ターゲット抽象化（IPlacementTarget）設計

日付: 2026-07-10
対象: moorestech_client 設置システム（PlaceSystem / BuildMenu / UIState）
出典: PR #987 レビューコメント 2件（r3537879954, r3537902052）

## 背景と問題

PR #987 で導入された設置システムには、レビューで指摘された2つの構造的問題がある。

### 問題1: タグ付き共用体もどきの飛散

「Type enum + 全バリアント分のフィールド」という共用体もどきが3箇所にコピーされている。

| 場所 | 形 |
|---|---|
| `BuildMenuEntry` | EntryType + BlockId / TrainCarGuid / ConnectPlaceMode / BlueprintName |
| `PlacementSelection` | SelectionType + Selected○○ ×5 + setter ×5 |
| `PlaceSystemUpdateContext` | 同フィールド群の再コピー |

さらにこれを支える手書きコードが飛散している。

- `PlaceSystemStateController` — `_last○○` ×6、変化検知の `||` 6連、リセット6連
- `BuildMenuView.IsSameEntry` — 全フィールド手書き等値比較
- `BuildMenuState.GetNextUpdate` — entry→selection 詰め替え switch（5分岐）
- `PlaceSystemSelector` — type→system switch

新しい設置タイプを1つ追加すると約7ファイル・9箇所の修正が必要で、変化検知や等値比較を書き忘れてもコンパイルは通る（サイレントバグ）。

### 問題2: 共有インスタンス経由の不可視なデータフロー

`PlacementSelection`（DIシングルトン・可変）を介してデータが運ばれる。

- 書き手: `BuildMenuState`（メニュー選択）、`BlockPickService`（スポイト、GameScreen/PlaceBlock両方から）
- 読み手: `PlaceSystemStateController`（毎フレームpoll）、`DisplayEnergizedRange`（直読み）

特に `GameScreenState` はスポイト成功時に**空の** `UITransitContext` で PlaceBlock へ遷移し、実データは裏の共有インスタンス経由で届く。遷移コードを読んでも何が渡ったのか見えない。

調査事実: `ClearSelection()` の呼び出し元はゼロ。共有シングルトンの「PlaceBlock外でも選択が生き続ける」性質は誰にも使われておらず、PlaceBlockへの全入口（メニュー選択・スポイト）が直前に必ず選択を書き直している。選択の実質的な寿命は PlaceBlock 滞在中のみ。

## 設計

対応は2本立て。(1) 共用体もどきを `IPlacementTarget` 多相化で潰す、(2) 共有インスタンスを削除し既存の遷移ペイロード機構に乗せ替える。

### Part 1: IPlacementTarget 多相化

```csharp
// PlaceSystem/Targets/ に配置（interface 1 + 具象5 = 6ファイル）
public interface IPlacementTarget : IEquatable<IPlacementTarget> { }

public sealed class BlockPlacementTarget : IPlacementTarget
{
    public readonly BlockId BlockId;
    public readonly BlockDirection? PickedDirection; // スポイト由来の向き（メニュー選択時はnull）
    // Equals/GetHashCode: BlockId + PickedDirection
}
public sealed class TrainCarPlacementTarget : IPlacementTarget      { /* Guid TrainCarGuid */ }
public sealed class ConnectToolPlacementTarget : IPlacementTarget   { /* string PlaceMode */ }
public sealed class BlueprintPlacementTarget : IPlacementTarget     { /* string BlueprintName */ }
public sealed class BlueprintCopyToolPlacementTarget : IPlacementTarget { /* 常に等値 */ }
```

- record はコードベースに前例が無いため通常クラス + `IEquatable` 手実装
- 等値は変化検知・エントリ同一性判定の基盤。`BlockPlacementTarget` は `PickedDirection` を等値に含める（スポイト→メニュー再選択を変化として検知するため）
- 「未選択」は null で表現（Null Object は導入しない。selector の default が受ける）

各所は次のように潰れる。

```csharp
// BuildMenuEntry: 共用体 → ターゲット + 表示情報
public readonly struct BuildMenuEntry
{
    public readonly IPlacementTarget Target;
    public readonly ItemViewData IconView; // BPはnull（テキスト表示）
    public readonly string ToolTipText;
}

// BuildMenuView.IsSameEntry → a.Target.Equals(b.Target)
// BuildMenuView のBP右クリック削除分岐 → entry.Target is BlueprintPlacementTarget bp

// PlaceSystemUpdateContext:
public readonly struct PlaceSystemUpdateContext
{
    public readonly IPlacementTarget Target;
    public readonly bool IsSelectionChanged;
}

// PlaceSystemStateController の変化検知: _last×6 + ||6連 →
var isSelectionChanged = !Equals(_lastTarget, CurrentTarget);

// PlaceSystemSelector: 具体型を知る唯一の分岐点
return context.Target switch
{
    BlockPlacementTarget b => ResolveBlockPlaceSystem(b.BlockId), // ベルト/レール/歯車ポール/通常のサブルーティング
    TrainCarPlacementTarget => _trainCarPlaceSystem,
    ConnectToolPlacementTarget c => ResolveConnectSystem(c.PlaceMode),
    BlueprintPlacementTarget => _blueprintPasteSystem,
    BlueprintCopyToolPlacementTarget => _blueprintCopySystem,
    _ => EmptyPlaceSystem, // null（未選択）含む
};
```

各 PlaceSystem の受け口は `PlaceSystemBase<TTarget>` を1枚挟み、基底で1回だけキャストする。

```csharp
public abstract class PlaceSystemBase<TTarget> : IPlaceSystem where TTarget : class, IPlacementTarget
{
    public void ManualUpdate(PlaceSystemUpdateContext context)
        => ManualUpdate((TTarget)context.Target, context.IsSelectionChanged);
    protected abstract void ManualUpdate(TTarget target, bool isSelectionChanged);
    public abstract void Enable();
    public abstract void Disable();
}
```

- selector が振り分けを保証するという不変条件が型に現れ、`BeltConveyorPlaceSystem` の `if (!context.SelectedBlockId.HasValue) return;` のような「自分宛てじゃないかもしれない」防御コードが消える
- 不変条件が壊れた場合は `InvalidCastException` で即死（サイレント誤動作より正しい）
- 例外2つ: `EmptyPlaceSystem` は `IPlaceSystem` 直実装（ターゲット不要）。`GearChainPoleConnectSystem` は「ポールブロック設置」と「接続ツール」の2モード両対応のため `IPlaceSystem` 直実装でパターンマッチ（`context.Target is BlockPlacementTarget` / `is ConnectToolPlacementTarget`）。これは実際に2種を受ける正直な分岐

具体型を知ってよい場所は次の3種に限定する。

1. `PlaceSystemSelector` の type switch（唯一の振り分け点）
2. 各 PlaceSystem 自身（自分のターゲット型）
3. その具体に本当に用がある機能（`BuildMenuView` のBP削除、`DisplayEnergizedRange` のブロック判定）

中間（BuildMenuEntry / BuildMenuState / PlaceBlockState / StateController / UpdateContext）は `IPlacementTarget` を不透明に運ぶだけになる。

### Part 2: 共有インスタンス削除とデータフロー明示化

`PlacementSelection` クラスと `PlacementSelectionType` enum を**削除**し、既存の `UITransitContextContainer`（遷移ペイロード）に乗せ替える。先行例: `GameScreenSubInventoryInteractService` → `SubInventoryState` の `ISubInventorySource` 受け渡しと同型。

```csharp
// GameScreenState（スポイトで拾って遷移）
if (_blockPickService.TryPickBlockUnderCursor(out var target))
    return new UITransitContext(UIStateEnum.PlaceBlock,
        UITransitContextContainer.Create<IPlacementTarget>(target));

// BuildMenuState（メニュー選択で遷移。Leave() に container 引数を追加）
if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
    return Leave(UIStateEnum.PlaceBlock,
        UITransitContextContainer.Create<IPlacementTarget>(entry.Target));

// PlaceBlockState
public void OnEnter(UITransitContext context)
{
    if (context.TryGetContext<IPlacementTarget>(out var target))
        _placeSystemStateController.SetTarget(target);
    // 以降は既存処理
}

// PlaceBlock滞在中のスポイト持ち替え（呼び出し箇所で受け渡しが見える）
if (_blockPickService.TryPickBlockUnderCursor(out var picked))
    _placeSystemStateController.SetTarget(picked);
```

役割の再分担:

- **遷移間のデータ運搬** → `UITransitContextContainer`（payload）
- **滞在中の現在値の保持** → `PlaceSystemStateController` が唯一の所有者。`CurrentTarget` を読み取り専用公開、書き込みは `SetTarget(IPlacementTarget)` のみ
- `BlockPickService` は共有状態への書き込み係 → `bool TryPickBlockUnderCursor(out IPlacementTarget)` の純粋リゾルバに変更（`PlacementSelection` 依存を除去）
- `DisplayEnergizedRange` は `PlacementSelection` 注入 → `PlaceSystemStateController` 注入に変更し `CurrentTarget is BlockPlacementTarget` で判定
- `PlaceBlockState.OnExit` の `controller.Disable()` でターゲットも破棄。「選択寿命 = PlaceBlock 滞在中」が仕様として明文化される
- DI: `MainGameStarter` の `builder.Register<PlacementSelection>` を削除（`PlaceSystemStateController` は登録済み）

#### 実装上の注意

- `UITransitContextContainer.Create` は `typeof(T)` キーの Dictionary。型推論に任せると具象型キーで格納され、受け側 `GetContext<IPlacementTarget>()` が**黙って null を返す**。必ず `Create<IPlacementTarget>(...)` と型引数を明示する（先行例 `Create<ISubInventorySource>` と同じ規約）
- payload 無しで PlaceBlock に入った場合は target null のまま → `EmptyPlaceSystem` に落ちる（現状の `SelectionType.None` と同じ無害挙動）。現存する遷移元2本は必ず payload を積む

## 変更ファイル一覧

新規（PlaceSystem/Targets/ 6ファイル + 基底1）:
- `IPlacementTarget.cs`, `BlockPlacementTarget.cs`, `TrainCarPlacementTarget.cs`, `ConnectToolPlacementTarget.cs`, `BlueprintPlacementTarget.cs`, `BlueprintCopyToolPlacementTarget.cs`
- `PlaceSystemBase.cs`

削除:
- `PlacementSelection.cs`（`PlacementSelectionType` enum ごと）

修正:
- `BuildMenuEntry.cs` / `BuildMenuEntryCatalog.cs` / `BuildMenuView.cs`
- `BuildMenuState.cs` / `GameScreenState.cs` / `PlaceBlockState.cs`
- `BlockPickService.cs`
- `PlaceSystemStateController.cs` / `PlaceSystemSelector.cs` / `IPlaceSystem.cs`（UpdateContext）
- 各 PlaceSystem（Common / BeltConveyor / TrainRail / TrainCar / TrainRailConnect / ElectricWireConnect / GearChainPoleConnect / BlueprintPaste / BlueprintCopy / Empty）
- `DisplayEnergizedRange.cs` / `MainGameStarter.cs`

移行は enum・クラス削除によるコンパイルエラー駆動で漏れを機械検出する。

注: `TrainCarPlacementSelectionResolver` は列車の経路選択（rail route selection）であり本件の PlacementSelection とは無関係（名前が近いだけ）。触らない。

## 却下した代替案

- **enum 維持でフィールドだけ整理** — 詰め替え・switch・手書き等値が全部残り、問題の核に触れない
- **完全レジストリ型（`Dictionary<Type, IPlaceSystem>` / ターゲット自身がシステムを返す）** — Block はマスタデータで4システム、ConnectTool は mode 文字列で3システムに分岐し、型だけではシステムが決まらないため結局ルーターが要る。「ターゲットがシステムを知る」はUI層で生成されるデータオブジェクトがDI済みシステムを参照する依存逆転を起こす。1ファイル1switchが正直な最小
- **選択状態の ReactiveProperty 化** — `PlaceSystemStateController` はフレーム駆動（ManualUpdate）であり、イベント駆動を混ぜると駆動方式の交差点が増える。可読性問題の実体は「6フィールドがバラバラに書かれる」ことで、1参照化+所有者明確化で解消する

## エッジケース検証（設計時に叩いた点）

- **スポイト（方向つき）→メニュー（方向なし）で同じブロックを再選択**: `BlockPlacementTarget` の等値が `PickedDirection` を含むため変化として検知される（現行と同挙動）
- **PlaceBlock→Tab→ビルドメニュー→Bで閉じる**: 現行は選択が共有インスタンスに残るが読む者がいない。新設計では OnExit で破棄。`DisplayEnergizedRange` も PlaceBlock 中しか表示しないため観測可能な挙動差なし
- **DeleteBar への遷移**: DeleteBar から PlaceBlock へ戻る遷移は存在しない（grep確認済み）ため、中間状態が payload を転送し続ける問題は起きない
- **`_clickedEntry` のリビルド跨ぎ保持**: 現行 `IsSameEntry` の意味論（ID系のみ比較・アイコン参照除外）は `Target.Equals` と一致

## テスト・検証方針

挙動を変えないリファクタリング。検証は次の順で行う。

1. `uloop compile --project-path ./moorestech_client`（worktree側。初回はLibrary構築で時間がかかる）
2. 既存テスト: `uloop run-tests --filter-type regex` で PlaceSystem / BuildMenu / BuildView 関連を実行
3. プレイテストDSL（unity-playmode-recorded-playtest スキル）で「ビルドメニューからブロック設置」「スポイト持ち替え」「接続ツール」「BP設置」の実プレイ経路を通す
4. 追加テスト: ターゲット等値（`BlockPlacementTarget` の方向込み等値）と `PlaceSystemSelector` の振り分けは純粋ロジックなので EditMode テストを新規追加する

## 実装環境

- worktree: `/Users/katsumi/moorestech-worktrees/place-target`
- ブランチ: `refactor/placement-target-abstraction`（base: `master-fable-tmp` @ d6c907fd8）
- 本体working tree（歯車Tick作業中・コンフリクトあり）には触れない
