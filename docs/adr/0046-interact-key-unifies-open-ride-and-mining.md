# 0046. Fキー「インタラクト」に開く・乗る・採掘を統合し対象側IInteractableで共通化する

日付: 2026-08-30
状態: 採択

## Context

ワールド内の操作は入力も対象検出もバラバラである。機械UIを開くのは左クリック（`GameScreenSubInventoryInteractService`、Blockレイヤ・100m無制限）、採掘/手掘りは左クリック長押し（`MiningController`、MapObjectレイヤ・1.5m）、列車乗車は`KeyCode.E`直書き（`RideVehicleInputService`、OverlapSphere 3m）。ホバー時のハイライトはmapObjectだけ（`MapObjectGameObject.SetFocused`、プレハブ焼き込みのステンシルアウトライン）で、ブロック・列車・露頭には何も出ない。「何が触れるのか」「どう触るのか」がプレイヤーに示されていない。

## Decision

- **開く・乗る・採掘・手掘りをすべて「インタラクト（F）」に統合する。** 左クリックは設置・接続・破壊専用になる。開く/乗る/PickUp種はF単押し、採掘はF長押し（現行FSMの進捗をそのまま）。
  出所: ユーザー裁定 2026-08-30 原文「機械を開く動作はインタラクト + Fとする」→ 選択「開く/乗る＋採掘もFへ」
  棄却案: 採掘は左クリック長押しのまま／採掘は今回触らない
- **インタラクト対象は常に1件を選ぶ。** 照準レイのヒットを優先し、無ければプレイヤー半径2m内で視線との角度が最小のもの。半径は全種別共通2mで、照準ヒット側にも同じ2mを課す（ブロックの100m無制限クリックは廃止）。
  出所: ユーザー裁定 2026-08-30 原文「照準の先に何もない場合、最も近いもの、特定の距離いない（この距離は非常に狭い範囲）で複数候補がある場合、もし視線の先に候補があればそれを候補とする」→ 復唱に「ok」、選択「半径2m・視線との角度が最小の候補」
  棄却案: 照準中の1件のみ／範囲内全部を薄く輪郭し照準中を強調／純粋に最寄り／種別ごとに現行距離を維持
- **ハイライトはその1件に出す。** 見た目はmapObjectと同じステンシルアウトライン（`Outline.mat`＋URP OutlinePass）を、ブロック・列車・露頭には実行時にレンダラー複製で付与する（agent前提: `WrapperPrefabFactory`の焼き込みと同じ仕組みの実行時版）。
- **インタラクトヒントはカーソルツールチップに出す。** 既存の採掘tooltipを共通化し「[F] 石窯を開く」「石 : [F]長押しで採掘」。左下キーヒントHUDはADR-0032どおりE/Fを載せない。
  出所: ユーザー裁定 2026-08-30 原文「インタラクトの方法を示す（どのキーをおす的な感じ」→ 選択「カーソル近傍のツールチップに統合」
  棄却案: 左下HUDに動的行／両方
- **共通化の形は対象側`IInteractable`。** ブロック(openableのみ)・列車車両・mapObject・露頭が「ハイライトON/OFF・ヒント文言・押し方種別・実行」を実装し、単一`InteractController`を`GameScreenState`から`ManualUpdate`で駆動する。基盤は「開く/採掘」を知らない。
  出所: ユーザー裁定 2026-08-30 原文「インタラクトという共通概念を作ってロジックを共通化する」→ 選択「IInteractableを対象側に持たせる」
  棄却案: Controller側で種別switchし既存サービスを呼ぶ
- **列車車両はF＝車両インベントリを開く、E＝乗車の2アクション。** Eは`Playable/Ride`アクションとしてInputSystemへ正式化し、tooltipは「[F] 車両インベントリを開く / [E] 乗車」の2行。乗車距離も共通2mに揃える。
  出所: ユーザー裁定 2026-08-30 選択「F＝車両インベントリを開く、乗車はE維持」
  棄却案: F＝乗車で車両インベントリは左クリック維持／F＝乗車で車両インベントリを開く操作は廃止
- **インタラクトはGameScreenStateでのみ駆動する。** 建築・破壊・デバッグ各モード中は選定もハイライトも行わない（現行のMiningController常時Updateによる建築モード中採掘は消える）。
  出所: ユーザー裁定 2026-08-30 選択「GameScreenだけでよい」
  棄却案: PlaceBlockState/DeleteObjectStateからも駆動
- agent前提: `Interact`アクションを`moorestechInputSettings.inputactions`（Playable）に`<Keyboard>/f`で追加し、`Ride`アクションを`<Keyboard>/e`で追加して`KeyCode.E`直書きは削除（HybridInputのOR読み禁止に従いInputSystem一本）。掘れない露頭・ツール不足も従来どおり選定・ハイライト・理由tooltipの対象（裁定2026-08-14）。miningType Noneの装飾mapObjectはレイ層で除外済み（ADR 0043）のため対象外。

## Consequences

- `ui.tooltip.holdToGet` / `namedMineHold` / `namedMineClick` の文言をF表記へ、`pickUpLeftClick` は `pickUpInteract` へ改名、開く/乗車のヒントキーを新設（localization.csv → 生成物再生成・force-recompile）。`ui.keyHint.key.f` は左下HUDに載せないため追加しない（agent前提）。
- moorestech_master 側の「左クリックで拾う」（challenges.json summary/pinText・localization.csv 3行）を「Fで拾う」へ更新し、別PR＋ピン更新（規約）。
- `GameScreenSubInventoryInteractService` / `RideVehicleInputService` / `MiningController`の入力・対象検出はInteractControllerへ吸収される。プレイテストDSL・既存テストの左クリック採掘手順はF入力へ差し替え。
- チュートリアルの「左クリック」文言をクライアント側でも検索し全て置換する。

## リンク
- .decisions/2026-08-30-インタラクトはFキーに統合し採掘も含める.md
- .decisions/2026-08-30-インタラクト対象は照準優先で無ければ近傍最寄りを1件選ぶ.md
- .decisions/2026-08-30-インタラクト方法の表示はカーソル近傍ツールチップに統合する.md
- .decisions/2026-08-30-インタラクトはIInteractableを対象側に持たせ単一コントローラで駆動する.md
