# 0033. ツリーのノード押下もパンとして受け、閾値未満の解放だけを選択にする

日付: 2026-08-27
状態: 採択

## Context

`TreeView`（研究ツリー・チャレンジツリーの共通部品）は、押下点がノードカードの内側だった場合にパンを開始しない実装だった。

```
if (!event.isPrimary || event.button !== 0 || (target instanceof Element && target.closest(nodeTargetSelector))) return;
```

除外の目的はクリック選択の保護にある。ノード上の押下でパンを始めると、`setPointerCapture` でイベントがviewportへリターゲットされ、カード側の `onClick` が素直には成立しないためである。

しかし研究ツリーはノードが密で、掴める空背景がほとんど残らない。実プレイでは「ツリーを動かせない」状態になる（ユーザー報告 2026-08-26）。さらにチャレンジツリー側の `ChallengeNodeCard` はそもそも `onClick` を持たず、**守る対象が無いのにパンだけを塞がれていた**。

同じ「掴んで動かす」と「その場のクリックで選ぶ」の両立は、レシピ一覧で既に解かれている（`features/recipe/panels/useDragScroll.ts` + `shared/pointerGesture/dragThreshold.ts`）。押下点からの移動が5px未満なら選択、超えたらスクロールに徹する形である。

## Decision

- **ノード上の押下もパンとして受ける。** 押下点からの移動が5px未満のまま離した時だけ、その押下点のノードをタップとして選択する。閾値と判定は `shared/pointerGesture/dragThreshold`（`DRAG_THRESHOLD_PX = 5` / `exceededThreshold`）を再利用し、レシピ一覧の前例と同じ振る舞いに揃える。
  出所: ユーザー裁定 2026-08-26 原文「研究ノードをドラッグすると研究UIが動かない問題がある」→ 選択「閾値判定で両立」
  棄却: ノード上はパン不可のまま別のパン手段（ホイール・専用ハンドル等）を足す／ノードのドラッグを別機能（ノード移動等）へ割り当てる
- **選択の入口を `TreeView` へ一本化する。** `TreeView` がタップ時に押下点からノードを解決し `onNodeTap(node: T)` で呼び出し側へ渡す。`ResearchNodeCard` の `onClick` は撤去する。`TreeView` はジェネリック `T` のままで、研究・チャレンジのドメイン語彙を持たない。
  出所: ユーザー裁定 2026-08-26「TreeViewがノードを解決して通知」
  棄却: 押下点のDOM要素をそのまま `onTap(target)` で渡し呼び出し側でID再抽出（`useDragScroll` と逐語同型だがDOM文字列からのID再抽出が要る）／カードの `onClick` を残しドラッグ時だけclickを潰す（選択の入口が2系統残る）
- **閾値は実画面px（`clientX/Y` の生の差分）で測る。** UI拡縮率（`--ui-scale`）やツリーのズーム倍率で変えない。
  出所: ユーザー裁定 2026-08-26「実画面pxのまま」
  棄却: `toCssScale` を掛けてステージ内座標へ直してから5pxを見る
- **ノード解決の印は `TreeView` 自前へ一本化する。** ノード包みdivへ `data-tree-node-id` を付け `closest` で引く。`nodeTargetSelector` propは撤去し、呼び出し側の `data-research-node` / `data-challenge-node` も削除する。チャレンジツリーもノードを掴んでパンできるようになる（クリック先が無いため選択への影響は無い）。
  出所: ユーザー裁定 2026-08-26「TreeView自前の印へ一本化」
  棄却: `nodeTargetSelector` を「タップ対象セレクタ」へ流用（呼び出し側のDOM印とTreeView内部の包みdivが二重管理になる）／チャレンジツリーだけ現状維持（振る舞い分岐のフラグが増える）
- 閾値を超えた最初の move では、押下点からの全量ぶんパンする（agent前提: 5pxのデッドゾーンを恒久的なズレとして残さず、内容をポインタへぴったり追従させる。既存の空背景ドラッグの移動量も変わらない）。
- 慣性の滑走はノード起点でも空背景起点と同一に扱い、タップ確定時は滑走しない（agent前提: 起点で区別する理由が無い）。
- パン中はノード上のカーソルも `grabbing` に統一する（agent前提: `.node { cursor: pointer }` が残るとパン中だけ意味が食い違う）。
- ホイールズーム・ツールチップ・詳細ペインの挙動は変更しない（agent前提: 今回の裁定の対象外）。

## Consequences

- 研究ツリーはノードが密でもどこを掴んでも動かせる。チャレンジツリーも同様にパンできるようになる。
- 選択のIN（`onNodeTap`）が1本になり、カード側はクリックを持たない表示専用コンポーネントになる。
- 選択はブラウザの `click` ではなく `pointerup` で確定する。押下と解放が別ノードにまたがった場合は**押下点**のノードが選ばれる（`useDragScroll` と同じ規則）。
- `nodeTargetSelector` が消えるため、`TreeView` を使う新しいツリーは印の付与を意識しなくてよくなる。
- 掴み操作は `useTreePanGesture` へ、DOM実測（`toCssScale` / `toContentBox`）は `viewport/viewportElement.ts` へ切り出した（agent前提: 200行規約に収めるための分割。`TreeView` はビューポート状態と描画に徹する）。
