# WebUI 研究ツリー パン・ズーム設計

**作成日:** 2026-07-18

## 目的

Web版研究ツリーを、スクロールバーで移動する操作から、空き背景の左ドラッグによるパンとマウスホイールによるズームへ変更する。

## 確定仕様

- 研究ツリーの空き背景をマウス左ボタンで押したまま動かすと、ツリー座標全体が同じ移動量で追従する。
- 研究ノード上の左クリックは従来どおり研究操作に使い、パンを開始しない。
- ホイール上方向で拡大、下方向で縮小する。
- ズーム中心はマウスカーソル位置とし、カーソル直下のツリー座標がズーム前後で動かないようにする。
- 拡大率は `0.4` 以上 `2.5` 以下に制限する。
- 研究ツリーを開いた直後は、移動量 `(0, 0)`、拡大率 `1` とする。
- ネイティブスクロールバーは表示せず、ツリー領域外へはみ出した内容をクリップする。
- ポインターを放した場合と pointer capture を失った場合のどちらでもドラッグ状態を終了する。
- 1280×720基準stageが画面に合わせて縮小されていても、画面上のカーソル位置とドラッグ移動量を一致させる。

## 採用方式

`ResearchTreePanel` がローカルな viewport 状態 `{ x, y, scale }` を所有し、研究キャンバスへ
`translate(x, y) scale(scale)` を適用する。ズーム座標計算は `researchLogic.ts` の純関数へ分離し、
単体テストでクランプとカーソル固定を検証する。

候補は次の3方式を比較した。

1. **CSS transform + pointer events（採用）**: 現在の絶対座標ノードと接続線を保ったまま、ツリー全体を一括変換できる。
2. **ScrollArea + canvas拡縮**: 拡縮のたびにスクロール量とキャンバス寸法を同期する必要があり、カーソル固定計算が二重になる。
3. **SVG viewBoxへ全面移行**: パン・ズームには適するが、Mantine製ノードカードを含む既存DOMを大幅に作り直すため今回の範囲を超える。

## 入力フロー

### パン

1. パン対象の空き背景で primary pointer の押下を受ける。
2. pointer capture を取得し、直前の `clientX/clientY` を保存する。
3. pointer move ごとの差分を、`offsetWidth / getBoundingClientRect().width` でstageのCSS座標へ変換して viewport の `x/y` に加える。
4. pointer up または lost pointer capture でパンを終了する。

### ズーム

1. wheel の `deltaY` から倍率を `exp(-deltaY * 0.0015)` で求める。
2. 現在倍率へ掛け、`0.4`〜`2.5` にクランプする。
3. viewport 左上基準のカーソル座標を、`offsetWidth / getBoundingClientRect().width` でstageのCSS座標へ変換してから、カーソル直下のツリー座標を逆算する。
4. 新倍率でも同じツリー座標がカーソル直下に残るよう `x/y` を補正する。

## コンポーネントと配置

| 変更対象 | 配置 | 責務 |
|---|---|---|
| `researchLogic.ts` | `features/research` | viewportの純粋なズーム座標計算 |
| `researchLogic.test.ts` | `features/research` | 倍率上限・下限とカーソル固定の単体検証 |
| `ResearchTreePanel.tsx` | `features/research` | pointer/wheel入力とローカルviewport状態 |
| `style.module.css` | `features/research` | クリップ、transform原点、grab/grabbing表示 |
| `e2e/tests/research.spec.ts` | WebUI E2E | 実ブラウザでホイール拡縮と背景ドラッグ移動を検証 |

## 配置と前例

- 研究固有の入力状態は既存の `features/research` に置く。`App.tsx` や共有UIへ研究語彙を持ち込まない。
- 既存の研究座標計算 `computeCanvasBounds` / `lineBetween` と同じく、座標演算は `researchLogic.ts` に置く。
- 操作の権威はWeb表示ローカルに限定し、topic・Action・C#ホストの通信契約は変更しない。
- 新規依存、永続状態、グローバルstoreは追加しない。

## テスト

- Vitestで倍率クランプ、ホイール方向、カーソル直下の座標不変を検証する。
- Playwrightでノードの表示サイズがホイール操作後に増減することを測る。
- Playwrightのviewportを基準stageより小さくし、空き背景をドラッグした後も同じノードの画面座標が画面上のドラッグ量だけ移動することを測る。
- Playwrightでノード上のクリックが引き続き `research.complete` を送る既存テストを維持する。
- `pnpm build`、研究Vitest、研究Playwright specをすべて成功させる。

## 対象外

- タッチジェスチャー、ピンチズーム、キーボード移動
- viewport位置・倍率の保存
- 自動フィット、ミニマップ、リセットボタン
- 研究ノード・接続線の見た目変更
