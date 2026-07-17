# webUI ⇔ uGUI 視覚一致 基準ドキュメント（実測ベース）

作成: 2026-07-17。測定時点の webui は commit `c801e6060`（tree3）、レンダーは `current/current-20260717.png`。
目的: CEF webUI のインベントリ/クラフト画面を、uGUI 時代の見た目（`reference/inventory.png` = 正本）に一致させる。
このドキュメントは**別セッションが前提知識ゼロで実装・採点を再開できる**ことを目的とした自己完結の基準書。
全ての目標値は画素実測（probe/ に出力保存済み）からの転記であり、[実測]=画素実測値、[推定]=目視・要再検証。

---

## 1. ユーザー指摘の原文（2026-07-17、これが発端のタスク）

> 1-1 原本は端はフェードアウト+線が2行 / 1-2 現状は線が1行で境界線＋きっぱりわかれている
> 2-1 中心部分も、原本は大きな1枠+その周りを内側が1ライン囲っている / 2-2 web uiでは単にアウトラインがあるだけ、エッジも丸い
> 3-1 クラフトレシピも元は2行＋境界フェード / 3-2 webuiはそうなってない
> 4-1 ホットバーの数字表記と、その囲いも違う / 4-2 webuiは中に番号表記がある（注: 現HEADでは外出し済み、様式差が残る）
> 5-1 クラフトフィールドに下向きの三角形の装飾（⊿）がある / 5-2 三角形の装飾がない
> 6-1 境界線の真ん中にダガーとダイヤの装飾がある / 6-2 そのような装飾がない
> 7-1 クラフト選択中の枠線に、角ふと+グローの装飾がある / 7-2 そのような装飾がない
> 8-2 web ui側は元画像でキャラクターが写っているところをUIと誤認しているため削除（注: 現HEADのDEMOレンダーでは既に非表示。コード上の craftPreview の扱いは要確認）

指摘の元画像は `reference/user-crops/`（X-1=uGUI原本 / X-2=2026-07-16時点のwebUI）。
**注意**: user-crops のwebUI側は古い。現状は必ず新規キャプチャ（§3）で確認すること。

## 2. 採点条件（状態パリティ・環境乖離）

- 採点スクショは**正本と同じ論理状態**で撮る: インベントリ画面・レシピ1件選択（選択枠表示）・素材/成果スロット表示・ホットバー1-5充填&slot1選択・明背景注入・**2568x1450**
- **環境乖離（採点から除外）**: アイテムアイコンの絵柄（モック色板 vs 実写真アセット）・実データ差（"Item 100" vs "石の斧"、個数 "396/2" 表記）・背景が実3Dワールドでなく合成グラデ・正本キーヒントのタイポ "ECS"（webUIの "ESC" を正とする）
- **UI様式（スロット地色・枠・タグ・ボタン形状・質感）は環境乖離に含めない**（除外送り禁止）

## 3. キャプチャ手順（現状の取得）

```bash
cd <repo>/moorestech_web/webui
CAPTURE_OUT=/path/to/out.png npx tsx e2e/capture-eval.ts   # 2568x1450・DEMOモック・レシピ選択済み・明背景
# 素の見た目確認（背景注入なし）: CAPTURE_BG=0 を付ける
```

## 4. 計測ツール

`~/.claude/skills/run-eval-loop/scripts/visual-probe.py`（要 python3+PIL/numpy）。座標は**全て画像寸法に対する % (0-100)**。
- 線の本数/層構造: `profile --img X --box X,Y,W,H --axis row|col`（bandsのRLEとpeaks本数を読む）
- 外寸/余白: `fit`、色: `sample`、角丸: `corner`、線端フェード: `fade`、透過: `alpha`（2背景法）
- 再計測の box 座標例は `scripts/batch1.sh` / `batch2.sh` を参照（probe/ の出力ファイル名と対応）

## 5. 基準（8軸・配点・実測目標値）

総合 = Σ(軸スコア0-100 × 配点)/100。閾値の目安 70。
**正本の値が目標値**。現状値は2026-07-17時点の差分の大きさを示す参考。

### 軸1: 見出し・ディバイダー様式（15点）— 指摘#1,#3,#6
| 項目 | 正本（目標）[実測] | 現状 2026-07-17 [実測] | 根拠 |
|---|---|---|---|
| 持ち物/CRAFT RECIPE見出し線 | **タイトルの上と下に1本ずつ**（持ち物: y≈17.0%/20.5%） | 下に2重線のみ(y21.2/21.7) | probe/profile-header-inv-*.txt, profile-header-recipe-*.txt |
| 線の端 | **フェードで消える**（右端減衰57%/56%） | 均一・フェード無し | probe/fade-header-recipe-line-*-ref.txt |
| 線色 | slate系 (95,100,112)・半透明（背景で減光） | (113,120,131) 不透明 | probe/sample-header-inv-line*-*.txt |
| 中央見出しの構成順 | タイトル→**オーナメント**→ボタン | タイトル→ボタン→オーナメント（逆） | probe/profile-header-center-*.txt |
| オーナメント | **1本線**(y21.74)+**左右ダガー**(97,100,118)+**輪郭型ダイヤ**・全幅13.5% | 2重線・ダガー無・塗りダイヤ(159,170,170)・幅25.0% | probe/profile-ornament-*.txt, sample-ornament-*.txt, crops/ornament-*.png |

### 軸2: パネル枠・輪郭（15点）— 指摘#2,#5
| 項目 | 正本（目標）[実測] | 現状 [実測] | 根拠 |
|---|---|---|---|
| サイドパネル縁 | **枠線なし・世界へ溶ける**（明線ピーク0本: bands `Dx64 Mx2 Dx6 Lx26 Mx17` / `Mx39 Dx46 Lx2`） | 7px明線ベベル(peak207.6) | probe/profile-panel-inv-edge-*.txt, profile-panel-recipe-edge-*.txt |
| 中央パネル縁 | **明線1本2px**(peak97.2)+外側暗帯、内側に細ライン[推定]（reference/user-crops/2-1.png） | 7pxベベル単層 | probe/profile-panel-center-edge-ref2.txt, -cur.txt |
| 中央パネル角 | radius≈3px（ほぼ直角） | 15px角丸 | probe/corner-panel-center-*.txt |
| 上部タブ位置 | パネル**左端寄り**(col36.8-43.0、パネル左端36.0) | 中央(col48.4-53.5) | probe/fit-tab-top-*.txt |
| 三角グリップ⊿ | **有**: 白灰(92,90,97)・中央パネル右下内側(col60.3-63.5×row72.3-75.7) | 無（欠落） | probe/fit-tri-grip-ref-*.txt, crops/tri-grip-*.png |
| アーガイル柄 | ほぼ不可視[推定] | 明瞭に見える（強すぎ） | crops/panel-inv-*.png |

### 軸3: 選択枠・アクセント（15点）— 指摘#7
| 項目 | 正本（目標）[実測] | 現状 [実測] | 根拠 |
|---|---|---|---|
| 選択レシピ枠 | **細線**(43,87,128)+**角に太いブラケット**(明るめ50,101,133+グロー)・**直角**・外寸23.4×10.0% | 角丸12px均一線(58,115,171)・26.4×12.1% | probe/sample-sel-frame-*.txt, fit-sel-frame-*, corner-sel-frame-*, crops/sel-frame-*.png |
| 進行矢印 | **白い矢印形**(223,232,232)+ティール系グロー | 暗いピル(47,48,48) | probe/sample-arrow-body-*.txt, crops/arrow-*.png |
| ホットバー選択 | スロット**全面cyan塗り**(93,178,205) | 外側1px cyanリングのみ | probe/sample-hotbar-sel-fill-ref.txt, profile-hotbar-sel-cur2.txt |
| CRAFTボタン | cyan寄り(72,160,232)→**光沢帯**(177,190,223)→(80,175,245)・**直角** | blue(63,135,241)・角丸4px | probe/sample-btn-craft-*.txt, corner-btn-craft-*.txt |
| レシピツリーボタン角 | 直角(radius≈0) | 角丸17px | probe/corner-btn-tree-*.txt |

### 軸4: スロット・タグ様式（10点）— 指摘#4
| 項目 | 正本（目標）[実測] | 現状 [実測] | 根拠 |
|---|---|---|---|
| ホットバー番号タグ | **小**(≦1.8×2.4%)・角丸7px・細枠(112,110,102)・**半透明地**・スロット上端に食い込み気味 | 大(2.4×2.8%)・不透明・スロットから浮く | probe/fit-hotbar-tag-*.txt, corner-hotbar-tag-ref.txt, crops/hotbar-tag-*.png, probe/crosscheck-hotbar-tag.txt |
| 個数数字 | **黒字**（白プレート上） | 緑字 | probe/sample-slot-count-*.txt |
| 空スロット枠 | **4px幅の半透明白**(bands `Dx28 Lx4 Dx29`, (94,96,97))・中身は背景が透ける | 1px crisp+ベベル・不透明 | probe/profile-slot-empty-edge-*.txt, sample-slot-empty-*.txt |
| ホットバースロット枠 | 1本(peak71.8・半透明) | 2本(外ベベル+内枠) | probe/profile-hotbar-slot-edge-*.txt |
| 充填スロット | 白プレートが枠いっぱい（絵柄は環境乖離） | 枠+暗パディング+アイコン | probe/profile-slot-filled-edge-*.txt |

### 軸5: 幾何・密度（10点）
| 項目 | 正本（目標）[実測] | 現状 [実測] | 根拠 |
|---|---|---|---|
| CRAFTボタン外寸 | **8.3×2.9%**(col45.95-54.24×row70.07-72.90) | 28.3×6.1%（3.4倍幅） | probe/fit-btn-craft-*.txt |
| 持ち物パネル下部 | スロット終端でパネルも終わる（余白≈0.2〜2.4%） | スロット終端69.45%に対しパネル79.45%（**約10%の空洞**） | probe/fit-panel-inv-*-row.txt |
| 所要時間テキスト | 選択枠の**下端線に重なる**(row32.6-35.1、枠下端35.1) | 枠から離れて下(40.8-43.3) | probe/fit-timetext-*.txt |
| ホットバー | スロットrow90.2-96.1・幅4.63% | row86.6-94.6・幅5.18%（大きく高い） | probe/fit-hotbar-slot-*.txt |
| レシピツリーボタン | 8.3×2.9% | 11.3×3.2% | probe/fit-btn-tree-*.txt |

### 軸6: 質感・透過（10点）
| 項目 | 正本（目標） | 現状 | 根拠 |
|---|---|---|---|
| 中央パネルα | [実測] **α=0.51**（塗り≈黒） | α=0.29（薄すぎ） | probe/alpha-panel-center-*.txt |
| サイドパネル | [推定] 高透過（上部平均(43,55,33)が草色寄り） | 弱透過(58,67,77)+柄強い | probe/sample-panel-inv-top-*.txt |
| ボタン光沢 | CRAFTボタンに光沢帯（bands `Dx7 Lx4 Mx10 Lx16 Mx6 Lx4 Mx2 Dx14`） | 滑らかグラデのみ | probe/profile-btn-craft-*.txt |
| ベベル立体縁 | **無し**（縁は1本線 or 無し） | 全要素にベベル（発明） | probe/profile-panel-*-edge-*.txt |

### 軸7: ガード・存在パリティ（10点）
- 欠落（追加すべき）: **三角グリップ⊿**・**スクロールバー**（正本は白バー x≈94.1%・常時表示・bands `Dx8 Mx2 Lx6 Mx2 Dx43`）・**ダガー装飾**・**白矢印**
- 発明（削除/抑制すべき）: レシピグリッド選択のcyanリング（正本に選択表示なし）・過剰ベベル
- 維持: 3パネル+ホットバー+タブ+整理ボタン+キーヒントの構成。中央プレビュー領域は**UI無し・素通し**（正本=世界が透ける。craftPreviewの箱を復活させたら減点 — 指摘#8）
- 正本に有る要素の欠落・無い要素の発明は他軸の得点に関わらず減点

### 軸8: ゲシュタルト並置（15点）
全景ペア＋規定クロップペア（crops/ の header-inv / panel-center / ornament / sel-frame(+tr) / hotbar-tag / btn-craft / tri-grip / arrow）を並置し**全体印象のみ**で採点。数値をこの軸に持ち込まない。「区別困難」は全景とクロップの両方で成立して初めて最高帯。

## 6. 自己検品（基準の判別力）

- 正本vs正本 → 全軸実測値一致で満点となる構成
- 現状(2026-07-17) → 概算 30〜40点（軸1≈20, 軸2≈25, 軸3≈25, 軸4≈40, 軸5≈35, 軸6≈40, 軸7≈50, 軸8≈30）で threshold 70 を大きく下回る=判別力あり
- 感度: 例) 見出し線の構造修正だけで軸1が20→80、総合+9

## 7. ファイルマップ

```
README.md                        このファイル（基準書）
reference/inventory.png          正本（uGUI全景 2568x1450）
reference/user-crops/            ユーザー指摘の元画像（X-1=uGUI / X-2=旧webUI 2026-07-16）
current/current-20260717.png     現状レンダー（測定時点）
crops/<要素>-{ref,cur}.png       要素別クロップペア（並置確認用・51枚）
crops/tiles/                     タイル走査画像（12分割×2）
tilemap.md                       タイル走査表（全要素の所在）
probe/*.txt                      画素実測の生出力（fit/profile/sample/corner/fade/alpha 約200件）
probe/crosscheck-*.txt           独立視認記述（sonnet subagent による構造記述・18要素）
scripts/batch{1,2}.sh            計測の再現スクリプト（box座標の正本。パスは要読み替え）
```

## 8. 実装の当たり（CSSの所在）

- パネル共通: `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
- スロット: `src/shared/ui/ItemSlot/style.module.css`, `src/shared/ui/SlotGrid/style.module.css`
- 見出し/レシピ画面: `src/features/recipe/RecipeViewer.module.css`（オーナメント・選択枠・craftPreview・CRAFTボタン）
- ホットバー: `src/features/inventory/HotbarPanel/style.module.css`
- インベントリパネル: `src/features/inventory/InventoryPanel/`, 全体: `src/app/App.module.css`, `src/app/index.css`
- 検証は §3 のキャプチャ→正本と並置（差分が視認できる限り繰り返す）。e2e の既存specに `craft-recipe-box` testid 依存が残っている場合はレガシー（HEADで撤去済みのtestid）
