# 0041. ビルドメニュー段階で素材不足をツールチップに出す

日付: 2026-08-28
状態: 採択

## Context

素材不足の告知は現在「設置プレビュー（ゴースト）を出した後」にしか無い。`ConstructionMaterialShortageReporter` が不足素材を `PlacementFeedback` へ積み、カーソルtooltipに `鉄板 2/5` の行として出す（裁定 `.decisions/2026-08-21-素材不足tooltipは不足素材のみ所持と必要を全セル分で出す.md`）。

そのためビルドメニューでエントリを選ぶ段階では「置けるのか」が分からず、選択してワールドへカーソルを向けて初めて不足が判明する。

ビルドメニューの実体は Web UI（`moorestech_web/webui/src/features/buildMenu/`）で、uGUI版（`BuildMenuView`）は退役済み。現状の配線は以下:

- `BuildMenuTopic` が `build_menu.entries` を配信。再配信トリガーは**入場・BP更新・財布残数変化の3つのみ**で、所持インベントリの変化では再配信されない。
- `BuildMenuEntryDtoFactory` が `IPlacementTarget.CreateRequiredItems()` から `RequiredItems: [{ItemId, Count}]` を、`ConstructionWalletQuery` から `SetPlacement: {PerCost, Remaining}` を載せる。
- `BuildMenuSlot` はエントリスロットで、**ツールチップを持たない**。ホバーは `BuildMenuDetailSidebar` の表示対象を更新するだけ（sticky。裁定 `.decisions/2026-08-05-ビルドメニュー詳細は最後のホバーを保持する.md`）。
- `BuildMenuDetailSidebar` は必要素材を `<ItemSlot itemId count={必要数} />` で並べる。所持数は出さず、赤表現もツールチップも無い。

一方、同じ「所持/必要」の提示は research とクラフトに前例がある。`ResearchDetailPane` / `CraftRecipeEntry` が `useMaterialTooltipText`（`名前 所持/必要`）と `ItemSlot insufficient`（赤枠）を共有している（裁定 `.decisions/2026-08-19-素材ツールチップはクラフト側も共通hookへ寄せる.md`）。

必要素材を持つ設置対象は `BlockPlacementTarget` と `TrainCarPlacementTarget` の2種のみ。`Blueprint` / `BlueprintCopy` / `ConnectTool` の `CreateRequiredItems()` は空配列を返す。

## Decision

- **ビルドメニューのエントリスロットにホバーツールチップを新設し、素材不足時に「素材が足りません」を見出しとして不足素材のみを `名前 所持/必要` 形式で並べる。あわせて詳細サイドバーの必要アイテムスロットにも不足表現を入れる（両方）。**
  出所: ユーザー裁定 2026-08-28 原文「ビルドメニューの段階でアイテムが足りないとき、その段階でホバーしたらツールチップにアイテムが足りないって表示するようにしたい」→ 選択「C（エントリスロットと詳細サイドバーの両方）」＋「A（見出し＋不足素材のみ）」
  棄却案: ①詳細サイドバーだけ ②エントリスロットだけ ③見出し無しで不足行のみ ④必要素材を全件列挙し不足行だけ赤

- **素材が足りているエントリではツールチップを出さない**（現状の無表示を維持し、ツールチップの出現自体を不足のシグナルとする）。
  出所: ユーザー裁定 2026-08-28 選択「ツールチップを出さない（現状維持）」
  棄却案: ①ブロック名を出す ②「素材は足りています」と充足も明示する

- **不足しているエントリスロット自体の見た目は変えない**（赤枠も暗転も付けない）。
  出所: ユーザー裁定 2026-08-28 選択「変えない（ツールチップのみ）」
  棄却案: ①`SlotFrame` の既存 `insufficient` で赤枠 ②グレーアウト

- **詳細サイドバーの必要アイテムスロットは research/craft と完全同型へ揃える。** 必要数バッジを廃し、スロット下に `所持/必要` テキスト（不足時は赤字）を置き、不足時は `ItemSlot insufficient` の赤枠、スロットホバーで素材ツールチップを出す。
  出所: ユーザー裁定 2026-08-28 選択「research/craft と完全同型へ揃える」
  棄却案: ①現状の必要数バッジを保ち赤枠とツールチップだけ足す ②バッジを `所持/必要` へ置き換える

- **不足判定はホスト(C#)で行い、`BuildMenuRequiredItemDto` に `Held` と `Lacking` を追加して配信する。** Web は受け取った結果を書式化するだけで、財布・所持の算術を持たない。
  出所: ユーザー裁定 2026-08-28 選択「ホスト(C#)で判定しDTOに不足を載せる」
  棄却案: Web側で `Topics.inventory` を購読し `buildOwnedCounts` で突き合わせる（recipe/research と同型・ホスト無変更）
  根拠: 財布の判断を内側へ閉じる裁定（`.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md` / `.decisions/2026-08-23-クライアントの財布窓口は問い合わせ専用の共有クラスへ集約する.md`）。書式はWeb側が持つ（`.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md`）ため、判定=ホスト・書式=Web で分割する。

- **所持数もホストが同じDTOで配信する**（`Held`）。表示数値と赤判定を同一スナップショットから出す。
  出所: ユーザー裁定 2026-08-28 選択「build_menu の必要アイテムに Held と Lacking を載せる」
  棄却案: `Lacking` だけ載せ、表示数値は Web が `Topics.inventory` から取る（線に二重に流さない）

- **財布制ブロックは残り設置数 ≥ 1 なら不足なしとする。** 残り0のときだけ1セット分の必要素材と所持を突き合わせる。
  出所: ユーザー裁定 2026-08-28 選択「残りがあれば不足なし、残り0のときだけ1セット分を突き合わせる」
  棄却案: 残りがあっても次の1セットを買えないなら予告として不足を出す

- **詳細サイドバーの赤表現も同じ `Lacking` を使う。** 残りがある間は `0/5` でも黒字・白枠のままにする。
  出所: ユーザー裁定 2026-08-28 選択「赤くしない（判定を一本化）」
  棄却案: サイドバーは所持<必要をそのまま赤で示す（素材の事実に正直）

- **`FreeBlockPlacement` デバッグON時は `Lacking` を常に false にする。**
  出所: ユーザー裁定 2026-08-28 選択「不足を出さない（設置時tooltipと揃える前例一致）」
  棄却案: デバッグ中でも実所持で判定して不足を出す

- **ホットバーは対象外。** ビルドメニューだけを変更する。
  出所: ユーザー裁定 2026-08-28 選択「対象外（ビルドメニューだけ）」
  棄却案: ホットバーにも同じ不足ツールチップを出す

- 対象エントリは必要素材を持つブロックと車両。`Blueprint` / `BlueprintCopy` / `ConnectTool` は `CreateRequiredItems()` が空配列を返すため、不足も出ない（agent前提: 既存の供給点をそのまま使った帰結であり、新たな分岐は設けない）。
- `BuildMenuTopic` に `ILocalPlayerInventory.OnItemChange` の購読を追加し、所持数変化で再配信する（agent前提: 既存の3トリガーと同じ `SchedulePublish` へ流す。UniRxの変化通知を購読する設計原則どおりで、`Update()` でのポーリングはしない）。
- ツールチップ見出しは新規キー `ui.buildMenu.materialShortageTitle`、サイドバーの素材ツールチップは新規キー `ui.buildMenu.materialTooltip` を起こし、後者を web 側 `MaterialTooltipKey` union へ追加する（agent前提: research が `ui.research.consumeItemTooltip` の専用キーを持つ前例に一致させる）。
- 不足行の算出はホスト側で `ConstructionCostShortageCalculator` の既存ロジックを流用する（agent前提: 設置時カーソルtooltipと同じ計算を二重実装しない）。

## Consequences

- `build_menu.entries` の配信頻度が上がる。所持数はアイテムを拾うたびに変わるため、`ILocalPlayerInventory.OnItemChange` は高頻度で発火する。`BuildMenuTopic` の既存デバウンス（`PostLateUpdate` で1フレーム1回）に載るが、ビルドメニューを開いていない間も再配信され得る点は実装時に確認する。
- ワイヤ契約が変わる。`BuildMenuRequiredItemDto` への `Held` / `Lacking` 追加は `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts` と fixture の更新を伴い、`wireContract.test.ts` / `BuildMenuEntryDtoFactoryTest` が追従対象になる。
- 詳細サイドバーの見た目が変わる（必要数バッジ廃止 → スロット下の `所持/必要`）。`build-menu-detail` を見る既存テストとレイアウトCSSが影響を受ける。
- 財布制ブロックでは「残り2・鉄板0」で赤も警告も出ない。素材が尽きていることは残り設置数が0になるまで表面化しない。これはユーザーがプレビュー付きで選んだ帰結として受け入れる。
- ホットバーからの設置では引き続き不足がゴースト段階まで分からない。ビルドメニュー経由と体験差が残る。
