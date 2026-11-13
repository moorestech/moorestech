# 見た目の比べ方（撮る・並べる・判定する・記帳する）

部品レーンが部品を作り終えたとき、画面レーンが画面の L1 を通して L3/L4 に入る前、指揮が波の取り込みを判定するときに開く。画素の一致は目標にしない。比べるのは位置・寸法・色・装飾の有無・文字の段組。

目次: 1 誰が・いつ・何を / 2 Web の参照を用意する / 3 UITK を撮る / 4 並べ比べの載せ方 / 5 合否の目安 / 6 差の直し方と例外の記帳

出所: `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4、`origin/master:docs/uitk-migration/plan.md` §4.6。

## 1. 誰が・いつ・何を

| 何を | 誰が | いつ |
|---|---|---|
| Web の参照キャプチャ | integrator | 段の頭（その段の画面を撮り直す）と、master の取り込みでその段の画面に効く Web の変更が入ったとき |
| UITK の部品の見本 | 部品レーン | 部品の L1 が通った後 |
| UITK の画面 | 画面レーン | 画面の L1 が通り、L3/L4 に入る前 |
| 並べ比べ（自己確認） | 作ったレーン | 完了報告の前 |
| 並べ比べ（判定） | 指揮 | 波の取り込みの前。波ごとに codex-audit（画像付き）にかける |
| ユーザーの目視 | ユーザー | 切替 PR の検証ビルドで 1 回だけ |

## 2. Web の参照を用意する

- 置き場は `moorestech_logs/uitk/reference-captures/web-<撮影元の短い sha>/`。撮影元は台帳 `docs/uitk/TODO.md` の「## 参照キャプチャ」に書かれたもの。画面と状態の一覧は `index.md`、機械向けは `manifest.json`
- まず既にある撮影 id（例 `3-build-menu.png`・`2-tooltip-multiline.png`）を使う。部品だけの参照は、その部品が写っている画面の参照から切り抜く（Web に部品の見本ページは無く、作ると Web の変更になる）
- 撮り直しは integrator が master の worktree で行う（`feature/uitk` の Web には手を入れない）:

```bash
cd <master の worktree>/moorestech_web/webui && pnpm install
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA \
UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
```

- 出力先は空のディレクトリでないと止まる。撮り終えたら台帳の撮影元と置き場を新しい方へ書き換える
- mock-host で撮れない状態（実マスタのアイコン・ドラッグの最中など）は台帳の「## 未撮影」にある。それらは PlayMode で撮って同じ置き場へ足す

## 3. UITK を撮る

- **部品**: 部品ギャラリー〔F1〕（`Client.Ui.Editor` の EditorWindow。本番の tss を当て、部品ごとに全状態を並べる。部品レーンが `IUiGalleryEntry`〔F1〕を 1 つ足す）を開き、`uloop screenshot --project-path ./moorestech_client --capture-mode window --window-name <ギャラリーの題> --output-directory <worktree の絶対パス>/moorestech_client/.uloop/outputs/compare/<レーンID>` で撮る
- **画面**: PlayMode・全体スイッチ〔W0-switch〕UITK（今は所有表で UITK 所有にした画面）で、参照キャプチャと同じ状態を DSL のシナリオで作り、ScreenCapture で 1280×720 と 2560×1080 の 2 種を撮る。フレーム番号の透かしを入れて古いフレームを見ないようにする
- `--capture-mode rendering`（PlayMode の既定）には UITK が写らない。撮れた画像は結果の `ImagePath` を開く（ディレクトリの新しい順で拾わない）
- 画面の ScreenCapture も同じ `moorestech_client/.uloop/outputs/compare/<レーンID>/` に置く
- **色はランタイムの PanelSettings で比べる**: GameUi は Web と同じ sRGB 空間で合成する（パネルを `forceGammaRendering` で RT に描き URP の最後に世界へ重ねる。設計書 [A-180]）ので、トークンの α は Web の値そのもの。色は下の撮影道具（本番の PanelSettings の複製を撮る）か PlayMode の画面の撮影で Web と比べる。部品ギャラリー（EditorWindow）も sRGB 合成で同じ見え方になるが、本番の設定を写さないので色の合否はランタイムで出す（設計書 [A-185]）

### 撮影の道具（使い捨ての手順を書かない）

- 計画を書くときは、比較の撮影の手順にこの節の道具を名指しで書く（実行計画 §6.2「計画に必ず書くこと」）。道具で撮れない状態（例: Web の参照の組に無い掴み絵）だけ、計画に理由を書いて使い捨てで撮り、道具の拡張を integrator へ依頼する

- **部品をランタイムの合成で PNG にする**（EditMode。PlayMode 中は止める）: `Client.Ui.Editor.Capture.UiRuntimePanelCapture`。倍率は本番の ScaleWithScreenSize が幅×高さから決める（2560×1080 なら 1.5）。地の色は参照の画素を 1 色で渡す。画素を数で見たいときは `Render(...)` が読める `Texture2D` を返す（左下原点。捨てるのは呼び手）。辞書は引数 `UiCaptureLocalization` で選ぶ（`InitializeDictionary` は撮る前に読み直す・`KeepCurrentDictionary` はテストで足した辞書を残す）。地と幕は引数 `UiCaptureBackdrop` で選ぶ（部品は `UiCaptureBackdrop.GroundColorOnly`。画面を Web の DEMO と並べるときは `new UiCaptureBackdrop(UiCaptureShell.VignetteAndDim, 地の画像)` で本番の `ScreenBackdropView`〈ヴィネット・幕〉を中身の奥に敷き、地の画像〈Web の `moorestech_web/webui/public/mock-orange-gradient.png` を `Texture2D.LoadImage` で読んだもの・cover〉を地の色の代わりにする。幕は traits の Backdrop 列の画面だけ。画像の破棄は呼び手）。フォント資産を作り直した直後の Editor は太さ違いの字を描かないことがあるので、作り直したら Editor を起動し直してから撮る

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(new Client.Ui.Editor.Gallery.Entries.Slots.ItemSlotGalleryEntry().CreateSample(), 2560, 1080, new UnityEngine.Color32(35, 29, 30, 255), Client.Ui.Editor.Capture.UiCaptureBackdrop.GroundColorOnly, Client.Ui.Editor.Capture.UiCaptureLocalization.InitializeDictionary, "<worktree の絶対パス>/moorestech_client/.uloop/outputs/compare/<レーンID>/itemslot-2560x1080.png");'
```

- **Game View の解像度を切り替える**: uloop では `uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Client.Ui.Editor.Capture.GameViewResolution.Set(2560, 1080);'`（無ければ固定解像度を足して選ぶ。撮り終えたら 1280×720 へ戻す）。録画 DSL では `await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);`（`using Client.Playtest.Operations.Ui;`。`Screen` が追いつくまで待つ）。例は `scenarios/uitk/uitk-gameview-resolution-switch.cs`
- 2560×1080 の Web の参照は `web-0e81bbe3c3-2560x1080`（台帳「## 参照キャプチャ」）。撮り直すときは capture の前に `pnpm build` が要る

## 4. 並べ比べの載せ方

- 並べ比べは PR の成果物の載せ先（環境ローカル規約が決める。例: PR 報告ページ `https://review.moores.tech/pr/<番号>/report/`）に載せる。ページの書式は載せ先の生成器が決めるので、独自の HTML を書かない
- 1 状態につき Web と UITK の画像を同じ `group`（状態名）に入れ、Web を先に置く。caption は `Web <撮影 id>` と `UITK <状態名> 1280×720`（画面は `2560×1080` の組も足す）
- 差の表は作業報告 `moorestech_logs/harness/uitk/<レーンID>/done.md` に状態ごとに書く:

```markdown
### <状態名>（Web: <撮影 id> / UITK: <ファイル名>）

| 観点 | Web | UITK | 判定 | 対応 |
|---|---|---|---|---|
| 位置・寸法 | | | 同じ / 差 | 直した / LX-n |
| 色 | | | | |
| 装飾の有無 | | | | |
| 文字の段組 | | | | |
```

- 画像の原本は worktree の gitignore 下（`moorestech_client/.uloop/outputs/compare/<レーンID>/`）に置き、`moorestech_logs` には入れない（`moorestech_logs` に置くのは報告の md とスクショ 3 枚程度まで）

## 5. 合否の目安

- 1280×720 で位置・寸法の差が 2px 以内
- 色はトークン経由で同じ値
- 装飾（罫線・三角・影・ベベル・グラデ）の有無が同じ
- 文字の段組（折り返し・行数）が同じ
- 字形の差（Chrome と TextCore の描き方）は差に数えない

## 6. 差の直し方と例外の記帳

1. 差はまず Web へ寄せて直す（USS の値を Web の CSS と突き合わせる。つまみの上書き漏れ・トークンの取り違え・半透明の補正漏れが多い）
2. 例外にしてよいのは、同じにするための対応が大きい（部品の作り直し・数百行の描画・性能の劣化）か、ブラウザの癖の再現で、かつ差が一目で分からないときだけ。配置・色の系統・装飾の有無・文字の大きさの差は例外にしない（ただし Web と同じ px で実機の比較で読めない文字は、撮影を添えて例外に記帳する。設計書 [A-39]）。直せないときは報告に「要裁定」と書いて止める
3. 例外は台帳 `docs/uitk/TODO.md` の「## 見た目の例外」〔F1〕に 1 行足す。ID は実行計画 §6.1 の自レーンの範囲から取る。列: ID（`LX-n`）／画面・部品／差／同じにしない理由／推奨案／撮影（報告ページの URL）／出所（agent 既定・裁定）
4. 例外にした USS・C# の行に `/* look-exception: LX-n */` を付ける。印と台帳の照合は `LookExceptionLedgerTest`〔F1〕が行う
