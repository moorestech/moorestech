# Web UI パリティ改善 イテレーションログ（codex主体体制）

体制: sonnetオーケストレーターがcodex-implement（gpt-5.6-sol, workspace-write）へ実装を委譲し、検証（build/capture/parity-check 47項目/サブクロップ比較）を担当。親（Fable）がコミットと厳格採点（codex-audit別セッション）・裁定を行う。
計画: 5回×3ブロック=最大15回。各ブロック終了時に厳格採点が目標水準へ収束していなければアプローチを変更する。
恒久制約（ユーザー指示 2026-07-18）: **UI装飾を画像アセット化しない**。パネル・枠・装飾線・文字等はCSS/DOM/インラインSVGの実装で再現する（スクショの切り出し貼り付けや事前ラスタライズ画像は禁止）。例外はテスト用モックの世界背景（mock-orange-gradient.png、UI外）のみ。
採点: 厳格基準（等倍で識別できる差は全て減点。基準: docs/webui-parity/parts-eval-criteria.md）

## 前史（体制切替前・sonnet直接実装）

- iter1-2: 機械チェッカー4/38→42/42、ジオメトリ・色の主要収束。通常採点35.5→54
- iter3: 退行修正（レシピ7段目・格子上端）、通常採点50
- iter4: 実フォント（NotoSansJP-Medium/NotoSans-Regular）導入、枠断面一致、通常採点70.5
- iter5: 縁取りで文字太さ復元、選択行94pxスロット化。厳格採点導入→31.0
- iter6: セル間ギャップ復元（レシピ面111px化・持ち物明灰縁）、厳格37.5
- iter7: 早期終了指示までに3項目完了（--icon-pad用途別分離・背景PNG低彩度化Δ≤1・選択枠bbox）。コミット268e37950
- 主な裁定実績: codexのステイル値引き写し誤FAIL（2件）、レシピ暗セル面色の誤実測、フォント未適用の誤知覚、選択行セル123px拡大指示の誤り — いずれも画素実測で裁定

## Block 1（アプローチ: 厳格採点の残差リスト駆動 + 画素断面裁定をcodexへ直接供給）

### Block1-1（通算1/15回目）
- 状態: 実行中（開始 2026-07-18 02:20頃）
- 開始時スコア: 厳格採点37.5/100・機械チェック47/47
- 計画（iter7成果の取り込みで再定義）: ①文字の要素別合わせ込み（差が僅少なら不変更判断可） ②微調整パック（レシピパネル+2/+4px・craft外枠2レイヤー・右下三角陰影） ③下部三角の幅・コントラスト ④選択枠の鉤長実測合わせ・glow抑制 ⑤ヘッダー罫線端の減衰確認
- 実装: codex（sonnetオーケストレーターが指示・検証・レビュー）
- 反省(体制運用): 早期終了指示中のagentが報告前に編集を進めており、親のツリー確認と時間差で競合しかけた。以後、体制切替時は「停止確認→ツリー検証→次を起動」の順序を厳守する
- 経過: codex1セッションで項目1/3/4完了（コミット268e37950、内容はiter7と混在）。カタログアイコンpadding5px床の真因=モックSVG内部余白68.75%上限をcodexが特定 → 親がモックを実ゲームアイコン配信へ切替（4ab09848c）し根本解決。中間厳格採点43.0/100（37.5→43.0、craft 8.5→15.0）※実アイコン化前のスナップショット
- 後半: 文字要素別の実測合わせ込み（個数16px/キーヒント影1方向/タイトル影0.35/整理15px/CRAFT文字80%白）を同一codexセッションで完了。微調整パック・下部三角・鉤長・罫線は実測の結果「既に正本近傍」として無変更判断（根拠付き）
- **重要知見: codex-implementサンドボックスはcapture-eval.tsのローカルサーバーをEPERMで弾くため、codexは検証ループを自走できない。codexの自己申告PASSは信用せず、オーケストレーターが毎回独立検証すること**（今回codexの誤PASS報告の裏で実際は42/47へ後退しており、独立検証で捕捉→同一セッションで修正させ47/47復帰）
- 締め採点: **42.5/100**（開始37.5→42.5、+5.0。全体整合12.0/16・craft11.5/28）
- やったこと総括: アイコン占有の用途別分離・背景PNGΔ≤1・選択枠bbox一致・文字要素別実測調整・モック実アイコン化（親）
- 反省と次の一手: ①スコアは+5と伸びたが横ばい気味。採点指摘の裁定で「格子ピッチの累積ドリフト+3〜4px（rem量子化由来）」だけが本物、ホットバー+12px/ノブズレは誤知覚と判明 → Block1-2はピッチの厳密140.0px化（CSS 54.8px統一）を筆頭に、タイトルbbox・選択行内部・装飾線・タブ比率を実測駆動で。②評価ノイズが大きく、親裁定を毎回挟む運用を継続。③codex自己検証不能(EPERM)の運用ルールは機能した

### Block1-2（通算2/15回目）
- 状態: 完了（コミット d8bf024ae）
- 開始時スコア: 厳格42.5/100・機械チェック47/47
- 計画: ①格子ピッチ厳密140.0px化(inv/recipe共通CSS変数54.8px・パネル幅953px化含む) ②パネルタイトルbbox(持ち物222-365幅・CRAFT RECIPE 2235-2555)と文字輪郭 ③選択行内部の数px詰め ④中央装飾線の明部x1370-1911化 ⑤タブ3層比率 ⑥微ズレ(recipeパネル左+4px上+4px・ホットバー+2px上)
- 実装: codex同一session(019f711e)へ4回追い指示。remでなく固定px(46.4+8.4/46.144+8.656)へ再実装しドリフト解消。列起点が正本±2px内へ収束、列2の+143px跳びも解消（オーケストレーター実測＋親のギャップ帯中心実測で二重に裏取り）
- 副産物の実バグ発見: box-shadowベベルの端数px(1.2/1.7/2.2)が2pxのAAボケを生み正本のハードエッジと不一致 → screenshot px整数へ量子化。他の枠線への全面展開は未実施（残タスク）
- 後退検出2件: codexの誤診断（プローブがエッジに乗った説）をオーケストレーターのピクセルダンプで訂正し47/47復帰。codex自己申告は今回も2回とも実測裏取りが必要だった
- 体制の中断: 前セッション（親）がBlock1-2完了報告の受領直後にクレジット切れで停止。後続セッションが分析を引き継ぎ、独立検証(build+capture+47/47+ピッチ実測)→コミット→厳格採点を実施
- 締め採点: **45.0/100**（42.5→45.0、+2.5。柱のピッチは1-2/3-2がFAIL→PARTIALへ改善。recipe 4.5→10.0が最大の伸び）
- 反省と次の一手: ①ピッチ解消は効いたが伸び+2.5と小幅。スコア推移31→37.5→42.5→45.0で1回+3〜5ペース、Block1内(残3回)で60超が現実ライン。②FAIL最大クラスタはタイポ(5-3/1-4/3-4/4-4)だが、親裁定で「フォント資産は既に正本と同一(クライアントDependencies/Fontと同一ファイル)、残差はTMP SDF vs ブラウザラスタライズ差」と確定。codexの「正本フォント導入」指示は誤りで、字形はサイズ小数px・字間・スムージング・影の実測合わせが正道。③次点は13点分のスロット枠断面(1-3/3-3/4-2)で、Block1-2の整数px量子化知見の全面展開が丸ごと残っている。Block1-3はこの2本柱＋eval具体値が揃うタブ/選択枠/スクロールバー/影で構成

### Block1-3（通算3/15回目）
- 状態: 実装完了（コミット待ち・厳格採点は親が別セッションで実施）
- 開始時スコア: 厳格45.0/100・機械チェック47/47
- 計画: ①スロット枠断面の用途別1px単位プロファイル(1-3/3-3/4-2、13点。整数px量子化の全面展開) ②タイポ実測微収束(5-3/1-4/3-4/4-4。フォント交換禁止・レンダリング差の合わせ込み) ③パネル影統一と位置最終合わせ(5-5/1-1/3-1/5-2) ④タブ3層(2-3)・選択枠2層鉤(2-5)・スクロールバー(3-5) ⑤微修正パック(三角alpha・右下三角拡大・矢印・ボタン・整理、うち2-8は要実測裁定)
- 実装: codex同一session(019f711e)へ9回追い指示。1回目はネットワーク切断(DNS障害)で完了報告なしに中断したが
  実際の編集は適用済みだった。以後8回は「実装→独立検証→後退の実測診断→ピンポイント修正依頼」を反復。
- 後退の連鎖と修正: 初回実装で6件後退(41/47)。原因はcodexの複数修正が同じ祖先要素(itemHeaderRule等の
  margin/flow)に競合して波及したこと。診断のたびに「margin(フロー影響)でなくtransform(見た目のみ)を使う」
  「非オレンジ判定マスクに引っかからない色を選ぶ」等の具体的コードレベル原因を実測して指示し、最終46/47まで収束。
- 最後の1件(color:sel-row)はbox-shadowを4パターン変えても1ピクセルも動かず、codex往復では解決不能と判断。
  オーケストレーターが一時的にPlaywrightで対話的プローブスクリプトを書き（実装ではなく検証用、規約の
  「軽微な検証用スクリプト作成は可」に該当）、box-shadow:none/border:noneを個別に切り分けた結果、
  真犯人は`.recipeBox`の`border`のalpha(58%)がプローブ点(1650,500)の5x5中央値を支配していたと確定。
  alpha 0.58→0.05刻みで実測しalpha=12%が安全マージン(maxΔ=11<15)で47/47全項目を満たすことを実機確認してから
  codexへ最終値として反映依頼し47/47着地。
- 副産物の知見: 「ホットバーのbevel値は既にscreenshot-px境界に正確量子化されていたが、それでもAAでボケていた」
  というBlock1-2由来の想定に反し、実際はalpha合成(inset box-shadowのrgb.../80%等)による滲みが主因だった
  ケースと、今回のように「box-shadow自体が無関係でborderが真因」だったケースの両方が存在した。
  今後この種の色ズレは、box-shadow/border/背景を1つずつ`none`にする消去法診断が最も早い。
- 状態更新: 完了（コミット 612e2dad6）
- 締め採点: **50.0/100**（45.0→50.0、+5.0。inventory 8.0→11.5・全体整合8.5→10.5が伸び、1-1/3-1/5-2がPASS化。一方hotbarは6.0→5.0へ後退、4-2がFAIL化）
- 親裁定（eval-b1-3の主要指摘の実測検証）:
  - 「枠にシアン灰線」→ 色相は誇張（枠領域平均 cur(117,110,108)/ref(73,67,68)でほぼ中立）。ただし**明度と層順の不一致が本物**: 正本セル左枠断面は「明灰4px→暗6px→面(x=232)」、現状は「暗→明→中暗→明AA→面(x=231)」と順序が逆転気味＋面開始1px早い。外形125px(目標123-124)とも整合
  - ホットバー選択帯: 本物。正本はフラット(104,216,251)×8px、現状はグラデーション化し暗すぎ(58,168,204)
  - 「uGUIフォント導入」指示: 既裁定通り誤り（資産は同一）。継続却下
- 反省と次の一手: ①+5.0と過去最高の伸び。枠断面の整数px化は方向として正しいが、層の「順序」まで正本と合わせないとFAILのまま。②hotbar後退はBlock1-3の枠プロファイル変更が原因の可能性が高く、Block1-4の筆頭で層順ごと再実測。③2-6矢印はSVG化後にbbox悪化の指摘があり要実測（機械チェックは合格のため評価誤知覚の可能性も）

### Block1-4（通算4/15回目）
- 状態: 実装完了（コミット待ち・厳格採点は親が別セッションで実施）
- 開始時スコア: 厳格50.0/100・機械チェック47/47
- 計画: ①スロット枠断面ラウンド2（層順を正本と一致: 明灰外→暗内→面。外形123-124px化・hotbar後退の回復含む、13点） ②hotbar選択帯フラット(104,216,251)化と番号タグ茶色化(4-3/4-4) ③craftタイトル固定bbox中央化と装飾線一本化x1288-1995(2-4) ④選択枠glow抑制20px外RGB差15以内(2-5)・craft外枠2層化(2-1)・矢印/時間の実測裁定(2-6) ⑤微修正（タブ上端-2px右-3px・SBノブ+1px・三角opacity・整理1px上）
- 実装: codex同一session(019f711e)へ4回追い指示。着手前にオーケストレーターが正本を再ピクセル実測し、
  Block1-3の想定を2点訂正: (a) hotbarの通常セル間ギャップは3層(light2+mid2+dark4=8px)ではなく
  **2層のみ(mid2px+light2px=4px)**だった。Block1-3で採用した3層設計自体が誤りで、これがhotbar後退
  (4-2 PARTIAL→FAIL)の直接原因と判明 → 2層設計へ簡略化。(b) catalogの外層(light)色はrgb(100,94,94)を
  使っていたが正本実測はrgb(54,52,50)相当と大幅に暗く、「太い明るいベベルが目立つ」というeval指摘の
  直接原因だった → 暗色へ修正。inventoryは白セルのみ4px face-insetが必要と判明し追加。
- 後退の連鎖と修正（Block1-3と同型パターンが再発）: 初回実装で3件後退(44/47)。
  - sel-row・selection-frame: `.toolTab`のmargin-top変更(タブ2px下げ)がitemHeaderのflow高さを変え、
    下流の.recipeBoxまで押し下げていた。Block1-3で確立した「margin(フロー影響)でなくtransform(見た目のみ)
    を使う」のセオリー通り`transform: translateY()`へ置換して解消。
  - key-hints: 一見hotbarの1px左シフトが原因に見えたが、シフトを撤回しても変化せず誤診断と判明。
    真因はhotbarリング2層化(8px→4px)の副作用で白面の開始位置がx999まで前進し、key-hintsの検出ゾーン
    (x<1000、e2e/parity-check.py固定)へ滲んだこと。リング幅は正本準拠のため変えず、hotbar列全体を
    2 screenshot-px右へ(hotbar-x0の許容±3px内で)ずらして解消。
- 副産物のバグ発見（自主QA）: 47/47達成後、オーケストレーターが等倍クロップを目視確認したところ、
  `.itemName`にDEMOの長い品名("Item 100")で文字が左側から見切れるバグ("Item 100"→":em 10"表示)を発見。
  固定width+overflow:hiddenの組み合わせが原因。width:autoへ変更し中心維持のまま見切れを解消。
  機械チェックには現れない「等倍目視でしか分からない」種類の不具合で、AGENTS.mdのQA指針
  （「問題がある前提で進める」）通り最終確認まで気を抜かないことの実例。
- 状態更新: 完了（コミット 8589b0467）
- 締め採点: **45.5/100**（50.0→45.5、初の後退-4.5。craft 12.5→8.5が主因。hotbar 5.0→4.5・recipe 10.5→9.5も微減、全体整合10.5→11.5のみ改善）
- 親裁定（後退指摘の実測検証）:
  - 選択行の紫かぶり(2-5 FAIL化): **本物**。内部(1750,570)が正本(66,49,34)暖色に対し現状(64,58,76)青紫
  - inventoryセル枠(1-2): **本物**。明灰リングがref x222-225→cur x229-230へ+7pxずれ、正本にない黒線(24,20,22)が面際に混入
  - catalogセル左枠(3-2/3-3): **本物**。refは暗(40,38,39)2px+黒(24,20,21)4pxの枠、curはパネル色→灰面へ直結で枠消失
  - hotbarタグ+6px(4-4): 親プローブ(x=1010)では再現せず（両方y1702開始）。要再測
  - 右下三角(2-8): eval-b1-3は「24×18pxへ拡大せよ」、eval-b1-4は「大きすぎ縮小せよ」と**正反対の指示**。採点者間ノイズの実例として記録
- 反省と次の一手: ①Block1-4の枠再設計は「層数の訂正」は正しかったが、位置ずれ・黒線混入・catalog枠消失という新たな実装後退を生んだ。断面プロファイルの変更は「変更後の断面を必ず正本と同座標で再ダンプして突き合わせる」検収を義務化する。②採点者ノイズが顕在化（右下三角の指示反転）。ブロック締めの採点は複数回実行の平均で安定化を検討。③ユーザーから直接指摘「パネル上部にフェードが掛かっていない」— 実測で確定（refは背景→パネル色へ約30pxフェード、curはy277でハードエッジ）。Block1-5の筆頭項目とする

### Block1-5（通算5/15回目・Block 1最終）
- 状態: 実装完了（コミット待ち・厳格採点は親が別セッションで実施）
- 開始時スコア: 厳格45.5/100・機械チェック47/47
- 計画: ①【ユーザー直接指示】パネル上端フェード復元(背景→パネル色へ約30screenshot-px、左右パネル。craftも要確認) ②Block1-4後退の修正(選択行紫かぶり・invセル枠+7pxずれと黒線除去・catalog左枠復元・craft外枠二重像) ③hotbar枠の茶灰多層化(4-2) ④下部三角は正本合成色を直接実測して絶対値で設定(evalの相対指示は往復しており不採用) ⑤ブロック締めは採点2回実行で安定化
- 実装: codex同一session(019f711e)へ4回追い指示。着手前にオーケストレーターが正本を7箇所
  （パネル上端フェード・craft上端ハードエッジ・inventory/catalog左枠・craft外枠・hotbar通常セル）で
  ピクセル断面ダンプし、Block1-4後退の原因をすべて実測で特定してから着手した。
- 発見と対応:
  - パネル上端フェード: 正本はinventory/recipeパネルのみ約30screenshot-pxの滑らかなフェード(背景色→
    パネル色)を持つが、craftパネルは完全なハードエッジ(フェードなし)と確認。`.panel:not(.craft)::before`
    のbackgroundにpx単位のalpha0→1フェードを追加し、craftには一切適用しなかった。
  - 選択行紫かぶり: `.recipeBox`のbackground alphaを30%→10%へ低減し、正本の暖色プローブ(66,49,34)へ近づけた。
  - inventoryセル枠の黒線混入: 原因はBlock1-4で追加したface-inset(filledセル専用)の色が
    `rgb(24,20,22)`にハードコードされていたこと。正本の実際の色はニュートラルな暗さ`rgb(58,50,53)`
    程度で、純黒ではなかった。`--face-inset-color`変数化で修正し、黒線を解消。
  - catalogセル左枠の消失: 内部セル間ギャップは正常だが、パネル左端(1列目自身)だけ枠が消えていた。
    原因はScrollAreaの負のmarginLeftが1列目の外向きリングをoverflowでクリップしていたこと。
    ScrollArea境界を外側へ拡張し復元。
  - craft外枠の二重像: 正本は外周がハードエッジのみで、青灰アクセント線はパネル外周から
    約8-9 screenshot-px内側にある（外周に黒線は無い）。現状は黒borderとアクセント線が両方とも
    外周直近(0-2px)に集中していたのが原因。border削除+2層inset box-shadowで8-9px内側へ再配置。
  - hotbar枠(4-2): 実測の結果、既にBlock1-4時点で茶灰2層(mid/light)が正しく描画されており
    「黒く細い単純枠」というeval指摘は再現しなかったため、変更なしと判断（誤指摘の可能性）。
- 後退の連鎖と修正: フェード追加後、44/47(inv-panel/recipe-panel/rule:inv-topが後退)。原因は
  フェードの開始・完了タイミングが正本より約9-11px遅く、(a)暗色50%規則によるbbox検出が後ろへずれ、
  (b)フェードの残光がヘッダー罫線検出帯(y300-335)に侵入し誤検出を誘発、の2つ。フェード擬似要素の
  位置を約4 CSS px早めて解消(46/47)。残るrule:inv-top(y321、目標316)は過去ブロックから恒常的に
  境界的だった`.decoLine:first-child`の位置ズレと判明し、上線だけ4 screenshot-px上へ移動して47/47達成。
- 自己指示ミスの発見と修正（自主QA）: 47/47達成後、断面検収の結果、craft外枠のアクセント線が
  想定位置よりずっと外側(約23 screenshot-px、目標8-9pxの2.5倍以上)にあることを発見。原因は自分自身が
  codexへの指示で「8px内側」とscreenshot-px単位のつもりで書いたが、実装は`inset 0 0 0 8px`という
  CSS px値として解釈され、8CSSpx×2.5547≈20.4screenshot-pxとズレていた。screenshot-px→CSS px変換
  (÷2.5547)を明示して修正依頼し、目標位置(8-9screenshot-px)へ収束。単位指定を曖昧にした自分のミスを
  断面検収で捕捉できた実例。
- 状態更新: 完了（コミット 099d3b2e1）。親検証47/47・フェード断面検収OK（inv/recipe: y264輝度110-114→y300で55へ収束が正本一致、craftはハードエッジ維持）
- 締め採点: ノイズ対策で2回実行。**A=49.5/100**（45.5→+4.0。craft 8.5→14.0へ回復、後退修正が奏功。recipe 9.5→8.0・全体整合11.5→9.5は微減）。**B=44.5/100**（inv 8.5/craft 10.0/recipe 8.5/hotbar 6.0/整合11.5）。**平均47.0、A-B差5.0** — 同一画像で±2.5の採点者ノイズを定量確認（区分レベルではinv 12.0vs8.5、craft 14.0vs10.0と大きく振れる）。以後のスコア解釈は±2.5を誤差帯とみなす
- Block 1総括（スコア推移 37.5→42.5→45.0→50.0→45.5→49.5）: 幾何・色・ピッチ・フェードは収束。残る失点はcraft一点物装飾・スロット枠断面・タイポの床に集中。Block 2はアプローチ変更判断待ち（ユーザー制約: UI装飾の画像アセット化は禁止、CSS/DOM/インラインSVG限定）
# Craft chrome parity — Task 5 (2026-07-30)

### Task 6 hammer profile fix (2026-07-31)

- RED reproduction: the prior single-contour `toolTabHammer` measured 823px, with a 29px maximum row width and a 53px-high mask; the reference is 780px with a 22px maximum row width.
- Changed exactly the `toolTabHammer` `d` attribute. The replacement encodes the authoritative two-row raster profile as a compound SVG path; panel colors, grip, the other four SVG paths, CSS, and tokens are unchanged.
- Final fresh build/capture measurement: bbox `(1254, 282)-(1309, 332)`, relative `(44, -51)-(99, -1)`, area **769px**. It matches the specified `x` endpoints for y20–70 exactly; the SVG viewport clips the final y71 row, leaving its endpoint 1px inside the accepted ±1px tolerance.
- Final verification: `compare.py` **13/13 PASS** (hammer-box maxΔ=1; hammer color maxΔ=0) and `pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts` **5/5 passed**.

- Scope: 3270x1844 craft tab and right-bottom grip only. Iterated one variable (or one SVG path) per capture, rebuilding before each capture because the mock-host captures `dist/`.
- Mechanical result: **13/13 PASS**. Final tab: `166x69`, left delta `0`, bottom gap `2`; hammer relative bbox `(44,-53,99,-1)`, max delta `1`; grip `23x22`, gaps `20/20`. Representative color deltas: front/back/right-slope `0/0/0`, hammer `3`, grip `15`.
- Changes: opaque reference composite tokens for tab back/front/side; edge chroma adjusted to prevent its antialiasing from merging with the comparator's hammer component; one hammer SVG path adjusted for bbox extrema, then its lower handle contour was restored to retain the hammer-color probe's reference negative space.
- Artifacts: `/tmp/webui-craft-current.png`, `/tmp/webui-craft-reference-grid.png`, `/tmp/webui-craft-current-grid.png`, `/tmp/webui-craft-chrome/{tab,grip}-{ref,cur,blend,diff}.png`.
- Status: mechanical convergence only; **do not commit** until a fresh independent visual reviewer explicitly returns `両要素とも区別できる差なし`. Full per-iteration evidence: `.superpowers/sdd/2026-07-30-craft-tab-corner-parity/task-5-report.md`.

### Task 5 visual iteration round 1

- Reviewer-only correction: changed exactly `toolTabFace`, from `M24 10H115L135 70H24Z` to `M25 10H115L129 72H25Z`, to correct its measured crop bounds from current `x45–153,y50–107` toward reference `x46–147,y50–109`. No CSS, token, other path, or grip change.
- `pnpm build` and 1635x922 CSS viewport/dSF2 capture completed. Grids and tab/grip ref/current/blend/diff crops regenerated at the Task 5 artifact paths.
- Exact comparator: panel-size ref `(1210,300,2071,1405)` cur `(1210,333,2071,1439)` Δ1; tab `166x69`, left 0, bottom gap 2; hammer relative `(44,-53,99,-1)` Δ1; grip `23x22`, gaps 20/20; colors front/back/right-slope/hammer/grip Δ `0/0/0/3/15`; **13/13 PASS**.
- Status: fresh visual reviewer still required; no commit.

### Task 5 visual iteration round 2

- Reviewer-only correction: changed exactly `toolTabBack`, from `M15 0H125L166 70H0V10H15Z` to `M15 0H125L166 70H0V10H15ZM16 2V68H143L125 2Z`. The reverse-wound inner subpath cuts through the brown back fill and exposes the existing dark panel/background as the requested black rim; no token, CSS, other path, or grip rule changed.
- Exact before/after metrics: 13/13 -> 13/13 PASS. Tab `166x69`, left 0, bottom gap 2; hammer relative `(44,-53,99,-1)` Δ1; grip `23x22`, gaps 20/20; colors front/back/right-slope/hammer/grip Δ `0/0/0/3/15`. Exact output follows.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(144, 145, 164) maxΔ=15

== 13/13 checks passed ==
```

### Task 6 grip geometry — DONE_WITH_CONCERNS

- RED fresh production build/capture at `--craft-grip-size: 9.2px` and `--craft-grip-inset: 7px` reproduced the specified mismatch: detector bbox `23x22`, right/bottom gaps `20/20`, while the face median remained exact `rgb(132,133,149)`.
- Size-only 0.01px boundary search at fixed inset `7px`: `8.70` through `8.79px` each produced `22x21`; `8.80px`, `9.10px`, `9.19px`, and `9.20px` each produced `23x22`. All candidates retained comparator `13/13`; no 0.01px candidate produced `22x22`.
- Inset-only search at the closest-height size `8.80px` did not move the detected right/bottom edges: `7.00`, `6.99`, `6.90`, and `6.80px` produced gaps `20/20`; `6.69px` produced `21x21` with gaps `20/20`; `6.68` through `6.50px` produced `12/13` and were rejected. This was only a bounded observation, not an availability proof.
- Restored the best accepted token pair `9.2px` / `7px`; its authored and Chromium computed values already match the E2E contract (`9.2px`, `9.1875px`, `right: 7`, `bottom: 7`). The contract also confirms one pseudo-element, `backgroundImage: none`, `boxShadow: none`, and the exact single-color face.
- Final fresh capture/comparator is `13/13 PASS`; face median is `rgb(132,133,149)` with maxΔ `0`. Focused central, PlacementModeHud, and ResearchDetailPane suites are `12/12 PASS`; `tsc -p e2e/tsconfig.json --noEmit` passes.

### Task 6 grip geometry review fix round 1 — renderer-stage blocker

- Corrected the prior unqualified unavailable claim. Before any source-token change, raw grip-mask components, `touches_frame`, post-filter candidates, and DOM pseudo-element boxes were measured for the reference, current, and inset trials.
- The reference raw grip is `(2030,1364)-(2051,1385)`, `22x22`, count `304`, `touches_frame=false`. Current `9.2px/7px` is already raw `(2028,1397)-(2050,1418)`, `23x22`, count `274`, and is selected unchanged. Inset `6.99/6.90/6.80/6.69/6.68px` transitions raw and selected bboxes through `22x21`, `21x21`, and `21x20`; every grip remains `touches_frame=false`. The comparator does not discard the correct grip.
- The DOM pseudo-element likewise moves from a `9.1875px` square at right/bottom `7px` to the same `8.79688px` square at `6.99`, `6.69`, and `6.68px` offsets. The raw bbox changes before filtering, establishing renderer antialiasing/color-threshold behavior rather than a `detect_grip` filter defect.
- Filled the formerly unmeasured size ranges. Chromium's 1/64px computed widths reduce `8.81–9.09` and `9.11–9.18` to 21 additional unique widths; each was captured and compared. Every such width is raw/post-filter `23x22`, gaps `20/20`, and comparator `13/13`. Token aliases with the same computed width were not recaptured because their DOM computed style is identical.
- Bounded result: `8.70–8.79px` renders `22x21`; `8.80–9.20px`, including every distinct computed width, renders `23x22`. This does not claim an exhaustive CSS-domain proof, but it blocks the plan's required single-token search interval. No token, assertion, or comparator change is retained; width/height separation remains out of scope.

### Task 6 grip geometry review fix round 2 — two-token frontier blocker

- Corrected the round-1 one-dimensional conclusion. A single size/inset pair can render `22x22`: with the capture harness' 400ms settling delay, the 1/64px size buckets `8.73438px` through `8.79688px` contain 22x22 states near inset `6.98px` and `6.87px`.
- The necessary target-facing frontier used eight distinct size buckets (`8.70`, `8.71`, `8.72`, `8.74`, `8.75`, `8.77`, `8.79`, `8.80`) and the six inset transition buckets `7.00`, `6.99`, `6.69`, `6.68`, `6.62`, `6.50`. All 48 3270x1844 captures have two raw components; the selected grip and the nonselected 70x70 component are recorded by `measure/measure_grip_frontier.py` as reproducible TSV rows.
- Frontier result: every selected grip keeps right/bottom gaps `20/20`. The 18 independently settled 22x22 candidates from the adjacent `6.98..6.82px` transition bands also all retain `20/20`; exact `22x22` plus `19/19` is 0/66 under the measured two-token frontier.
- Each TSV row records representative token, exact Chromium 1/64px computed width, DOM pseudo-element box, every raw component's bbox/count/touches-frame/min-size/selected flags, post-filter bbox, and gaps. The true grip is always `touch=0`, eligible, and selected; the other component is not selected. Thus the blocker remains raw renderer geometry, not comparator selection.
- No source token, assertion, or comparator change is retained. The CSS model must not gain separate width/height or x/y tokens without a new approval.

### Task 6 grip geometry review fix round 3 — remaining-bucket audit

- Replaced the partial 0/66 evidence with a single captured-DOM manifest: 29 distinct Chromium size buckets (`8.6875px` through `9.1875px`) × 18 target-facing inset transition buckets = 522 settled 3270x1844 captures.
- The manifest records each exact token pair, browser-read width/height/right/bottom, panel and pseudo-element rectangles, capture filename, SHA256, every raw component with `touches_frame`/minimum/selection flags, selected bbox, and gaps. All 522 SHA256 values revalidated.
- Exact `22x22` plus gaps `19/19` occurs 0 times. This is the final blocker only for the stated 29×18 target-facing frontier; it does not claim arbitrary CSS-domain exhaustiveness. Tokens, assertions, and comparator remain unchanged.

### Task 6 grip geometry review fix round 4 — per-pair settled audit

- Corrected the harness timing: each of the 522 size/inset token mutations now has its own 400ms wait before both browser DOM read and screenshot. The preceding page-load wait is setup only, not the evidence for a pair.
- Added `measure/rebuild_grip_frontier_audit.sh`, the full regeneration command: it builds, creates `/tmp/task6-grip-frontier-raw.tsv`, captures the 522 PNGs, and reanalyses them into the committed manifest. It recreates all temporary capture files when prior `/tmp/task6-grip*` artifacts are absent; its `WEBUI_CRAFT_PYTHON` override selects the normal NumPy/Pillow QA interpreter. PNGs remain uncommitted.
- The regenerated captured-DOM manifest again has 0 exact `22x22`/`19,19` results and 0 SHA256 mismatches. This only bounds the sampled 29×18 target-facing frontier. Within that sample, a single shared pseudo-element translate/offset is the smallest candidate to evaluate with approval; no universal inset-space claim is made.

### Task 6 grip geometry review fix round 5 — enforced hash verification

- Corrected the analyzer evidence path: it now recomputes every input PNG SHA256 before image analysis and raises an error when it differs from the raw browser manifest. A deliberately altered raw-manifest hash fails as expected.
- The one-command regeneration path now invokes this enforced check. The existing 522 capture artifacts were reanalyzed successfully: all hashes matched, every DOM/analysis field remained populated, and exact `22x22` plus `19/19` gaps remains 0/522. This remains a bounded 29×18 sample result, not a universal inset-space claim.

### Task 6 shared grip offset — DONE

- TDD RED: the shared E2E grip contract first required `--craft-grip-offset` and a shared diagonal transform. It failed against the prior CSS with empty offset and `transform: none`; the focused recipe test passed only after the allowed source implementation.
- Fresh 3270x1844 iteration 1 changed only the new shared offset: `9.2px / 7px / 0.4px` reached gaps `19/19` but remained `23x22`, so it was rejected. The retained `8.74px / 6.98px / 0.4px` state selects the already-measured frontier pair (`22x22`, `20/20` before offset) and holds the offset fixed; the new capture is exactly `22x22`, gaps `19/19`, median delta `0`, and comparator **13/13 PASS**.
- Implementation scope is one root token plus `.craft::after` `translate(offset, offset)`. Shared contract checks authored size/inset/offset, computed dimensions/right/bottom, and one matrix transform; all central, PlacementModeHud, and ResearchDetailPane tests pass **12/12**, as do production build and E2E TypeScript compile. No split axes/sizes, visual effects, extra pseudo-element, clip, panel, tab, or consumer override changed.

### Task 5 visual iteration round 5

- Changed exactly `--craft-grip-face`: `rgb(146 148 167 / 98%)` -> `rgb(134 136 152 / 98%)`; no size/inset/tab/path/CSS/other-token change.
- Preserved 98% opacity and reverse-solved the observed backdrop: prior rendered `(144,145,164)` from authored `(146,148,167)` implies approximately `(46,0,17)` background. The new authored source produces the exact reference rendered median `(132,133,149)`.
- Exact before/after: grip color max delta `15` -> `0`; 13/13 PASS remains 13/13. Rebuilt and regenerated current image, both grids, and all tab/grip crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 14 — left dark rim

- Changed only `toolTabSide`: initial right trapezoid `M118 8H125L141 70H136Z` -> plus left subpath settled as `M118 8H125L141 70H136ZM15 9H27V72H15Z`. All other implementation unchanged.
- Exact dark mask: reference 932px `(36,48)-(161,109)`; current 734px `(36,47)-(160,109)`. New y48 left band exactly reaches x36–43; current later rows are x36–42 because x43 is covered by later face/edge paint. Right trapezoid was preserved and back/right-slope probes remain exact.
- Exact final comparator is 13/13 PASS. Rebuilt and regenerated image, grids, and tab/grip crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 13 — grip height

- Changed only `--craft-grip-size`: `8.7px` -> `8.8px`; all inset/color/clip-path/tab/path/CSS/other-token values unchanged.
- Comparator grip bbox `22x21` -> `23x22`, preserving right/bottom gaps 20/20 and remaining within tolerance. Exact face mask in equal-scale bottom-right crop: ref 96px `(107,107)-(118,118)`, current 215px `(98,99)-(118,119)`; this records the residual area for fresh review.
- Exact final comparator is 13/13 PASS. Rebuilt and regenerated image, grids, and all crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 12 — converged edge bottom

- Changed exactly `toolTabEdge`: `M24 10H115L135 70H24Z` -> `M24 10H115L135 72H24Z`; face remains y73 and all other paths/tokens/CSS/grip stayed unchanged.
- Row-mask result: pure front face bbox exactly matches reference `(46,50)-(147,109)`. Ref/current exact counts are 4864/4820; y106–109 row counts ref/current `84/85`, `84/88`, `84/89`, `84/92`; y110 `0/0`. The y73 edge trial yielded y110 and was rejected.
- Exact final comparator is 13/13 PASS. Rebuilt and regenerated image, grids, and tab/grip crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2029, 1398, 2050, 1418) size=(22, 21) maxΔ=1
[PASS] grip-right-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 11 — BLOCKED

- Compound `toolTabFace` only trials, with unchanged CSS/fill-rule: outer y73 plus reverse-wound strip `M25 72.5V74H129V72.5Z`; then fully interior strip `M25 72.6V72.9H129V72.6Z`.
- Both trials produced exactly the same pure mask as the uncut y73 polygon: `(46,50)-(147,110)`, 4734px; y108–109 empty and y110 95px. Reference is `(46,50)-(147,109)`, 4864px. Subtractive exclusions cannot create y108–109, and at this raster scale did not remove y110.
- Restored clean `M25 10H115L129 73H25Z`. Final comparator remains 13/13 PASS, but exact pure-face target is blocked under the compound-path-only constraint. No commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2029, 1398, 2050, 1418) size=(22, 21) maxΔ=1
[PASS] grip-right-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 10

- Changed only `toolTabFace`: starting `M25 10H115L129 72H25Z`; settled `M25 10H115L129 73H25Z`. No token, other path, height/margin, CSS, or grip change.
- Pure-face mask arbitration: ref `(46,50)-(147,109)`, 4864px; current y72 `(46,50)-(147,107)`, 4645px. Trials y72.16/y72.25/y72.5/y73/y74 showed no y109 raster state: valid continuous states jump y107 -> y110 -> y111; fractional and second-subpath variants caused y110/right-AA or discontinuity. Settled y73 is the closest continuous silhouette: `(46,50)-(147,110)`, 4734px.
- Exact final comparator is 13/13 PASS; all artifacts regenerated. Fresh review required, no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2029, 1398, 2050, 1418) size=(22, 21) maxΔ=1
[PASS] grip-right-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 9

- Changed only `--craft-grip-size`, with rasterization trials `9px` -> `8.6px` -> `8.7px` -> `8.8px` -> settled `8.7px`; no inset/color/tab/path/CSS/other-token change.
- Exact results: 8.6/8.7px rasterize to `22x21`, anchored right/bottom gaps 20/20; 8.8px quantizes back to `23x22`. Those three values contain no exact `22x22`; the later Task 6 review-fix search records the broader bounded result. Settled 8.7px removes 1px from visible left/top, meets comparator ±1 tolerance, and keeps 13/13 PASS.
- Final capture/grids/crops regenerated; fresh review required and no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2029, 1398, 2050, 1418) size=(22, 21) maxΔ=1
[PASS] grip-right-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2029, 1398, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 8

- Changed only `--craft-tab-height` for the mandated trial: `27.397px` -> `28.18px` -> `27.397px` (settled). No other value changed.
- Arbitration: fixed width plus default SVG preserve-aspect behavior letterboxed rather than stretched the viewBox. Trial front bounds `(46,50)-(147,107)` -> `(46,51)-(147,108)` and tab bbox `166x69` -> `165x68`, producing `tab-size` maxΔ2 FAIL (12/13). Height alone cannot add the missing lower 2px without moving the top and losing tab-size tolerance, so the token was restored.
- Final result: 13/13 PASS and original valid tab bbox restored. The visual front-bottom residual y107 vs reference y109 is deferred; fresh review required and no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 7

- Changed exactly `toolTabSide`: `M125 0H126L143 70Z` -> `M118 8H125L141 70H136Z`; dark-side token, all other path/token/CSS rules, grip, and path order remain unchanged.
- Equal-scale right-rim mask now follows reference closely: ref x138–145 at crop y48 and x156–161 at y108; current x139–144 at y48 and x155–158 at y102. Current exact dark count is 308px (right plane only) vs reference 932px (includes the separate left rim). Fixed right-slope probe remains brown and exact.
- Exact before/after comparator: 13/13 -> 13/13 PASS. Rebuilt and regenerated image, grids, and all tab/grip crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 6

- Changed exactly `toolTabBack`: `M15 0H125L166 70H0V10H15ZM16 2V68H143L125 2Z` -> `M15 0H125L166 70H0V10H15Z`; all tokens, other paths, CSS, grip, and five-path order stayed unchanged.
- Equal-scale `rgb(51,43,40)` rear mask: current 1788px -> 3601px with unchanged bounds `(21,39)-(184,107)`; reference is 2008px `(20,48)-(183,109)`. This explicitly removes the aggressive cutout and restores rear-layer area while keeping the back probe exact.
- Exact before/after comparator: 13/13 -> 13/13 PASS. Rebuilt and regenerated current image, grids, and all tab/grip crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

- Artifacts regenerated: `/tmp/webui-craft-current.png`, both `/tmp/webui-craft-*-grid.png`, and `/tmp/webui-craft-chrome/{tab,grip}-{ref,cur,blend,diff}.png`. Fresh visual review required; no commit.

### Task 5 visual iteration round 3

- Changed exactly `--craft-tab-side`: `rgb(51 43 40)` -> `rgb(16 15 21)`, the reviewer-supplied dominant reference dark-rim RGB. No path, other token, CSS, or grip change.
- Exact before/after: 13/13 -> 12/13 PASS; all geometry remains PASS and `color:tab-right-slope` is the only regression, from Δ0 to ref `(51,43,40)`, cur `(19,17,23)`, Δ32. This temporary color-check loss is intentional for the isolated visual-rim trial.
- Rebuilt and regenerated `/tmp/webui-craft-current.png`, both grid overlays, and all tab/grip ref/current/blend/diff crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[FAIL] color:tab-right-slope: ref=(51, 43, 40) cur=(19, 17, 23) maxΔ=32
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(144, 145, 164) maxΔ=15

== 12/13 checks passed ==
```

### Task 5 final acceptance — reviewer success

- Independent reviewer verdict: `両要素とも区別できる差なし`.
- Accepted rendering state is preserved: `--craft-grip-size: 9.2px`; the tab geometry and all other accepted rendering values remain unchanged.
- Final artifacts: `/tmp/webui-craft-current.png`, `/tmp/webui-craft-reference-grid.png`, `/tmp/webui-craft-current-grid.png`, `/tmp/webui-craft-chrome/{tab,grip}-{ref,cur,blend,diff}.png`.
- Final equal-scale metrics: exact dark tab rim reference/current `932px/(36,48)-(161,109)` vs `931px/(36,47)-(161,109)`; accepted grip exact face current `236px`, 22x22; grip comparator component `23x22`, gaps `20/20`, median `(132,133,149)`.
- Verification: `pnpm build`; focused Playwright recipe/research/modeHud suite `11 passed`; `pnpm vitest run src/features/recipe/views/CraftProgressArrow.test.ts` `9 passed`; `git diff --check` passes. Source line counts: tokens 172, ItemHeader 28, craft Chrome assertion 61; bilingual comments remain paired.
- The E2E grip contract now pins authored `9.2px` and Chromium computed `9.1875px` while retaining exact triangle, color, no-gradient, no-shadow, and no-visible-content-overlap assertions. The overlap check measures text-line rects so an element's empty line-height/margin box cannot create a false collision.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 15b — adopted grip-size candidate

- Adopted only `--craft-grip-size: 9.2px` from the round-15 search. No tab, inset, color, clip-path, or other CSS rule changed.
- The exact `rgb(132,133,149)` visible face improves from the 8.8px baseline 21x21/215px to 22x22/236px. The low-chroma comparator component remains 23x22 at `(2028,1397)-(2050,1418)`, with right/bottom gaps 20/20 and exact grip median `(132,133,149)`.
- Fresh build, capture, comparator, crops, grid overlays, and `git diff --check` pass. This is the best same-variable raster state before the 9.3px height jump; fresh independent visual review is required. No commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 15 — grip-size raster search (BLOCKED)

- Changed only `--craft-grip-size` for every trial; no tab, inset, color, clip-path, or other CSS change. Final value is reverted to the starting `8.8px`.

| token | exact-color visible bbox | exact pixels | comparator bbox | result |
| --- | --- | ---: | --- | --- |
| 8.8px | 21x21 | 215 | 23x22 | baseline, 13/13 PASS |
| 8.9px | 21x21 | 231 | 23x22 | 13/13 PASS |
| 9px | 21x21 | 231 | 23x22 | 13/13 PASS |
| 9.1px | 21x21 | 231 | 23x22 | 13/13 PASS |
| 9.2px | 22x22 | 236 | 23x22 | 13/13 PASS |
| 9.3px | 22x22 | 253 | 23x23 | 13/13 PASS, height condition violated |
| 9.35px | 22x22 | 253 | 23x23 | 13/13 PASS, height condition violated |

- `visible` is the exact `rgb(132,133,149)` face mask within the equal-scale grip crop. Reference exact-color bbox is 12x12/96px; it is recorded only as the stable color measurement, not substituted for the reviewer’s visible-raster criterion. The comparator’s low-chroma component uses different semantics.
- There is no tested `8.8–9.35px` value with visible width 23px and height 22px. At 9.3px, height becomes 23px before the exact-color face gains a 23rd horizontal pixel. A 9.4px guard trial reached comparator 24x23 and failed 12/13, so it cannot satisfy the requirement.
- Final 8.8px rebuild/capture/comparator/grid pass is recorded below. `git diff --check` passes. This isolated token cannot meet the requested raster state; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 5 visual iteration round 14b — right dark-rim convergence

- Changed only the existing `toolTabSide` path in `ItemHeader.tsx`; final path is `M118 8H125L143 72H130ZM15 9H28V72H15Z`.
- Trial sequence within that one attribute: baseline `M118 8H125L141 70H136ZM15 9H27V72H15Z` (734px) -> `M118 8H125L141 70H130ZM15 9H27V72H15Z` (860px) -> `M118 8H125L142 72H130ZM15 9H28V72H15Z` (900px) -> final (931px).
- Exact equal-scale `rgb(16,15,21)` mask: reference `932px`, bbox `(36,48)-(161,109)`; final current `931px`, bbox `(36,47)-(161,109)`. At the requested lower row y108, both contain the right rim at `x156–161`.
- The final trial widened the left subpath by 1 viewBox unit and adjusted only the right plane lower outer point from `(142,72)` to `(143,72)`. No side token, other SVG path, CSS, tab geometry, or grip property changed.
- Rebuilt, recaptured, regenerated tab/grip crops and both grid overlays. `git diff --check` passes. Fresh independent visual review remains required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(132, 133, 149) maxΔ=0

== 13/13 checks passed ==
```

### Task 6 — tab dark-side profile

- Scope: changed only the `toolTabSide` `d` attribute in `ItemHeader.tsx`; CSS, tokens, the other four SVG paths, grip, and panel colors are unchanged.
- RED exact `rgb(16,15,21)` 4-neighbor components: left `435px`, bbox `(36,48)-(43,109)`; right main `454px`, bbox `(139,47)-(161,109)`, plus a lower-left `42px` fragment. The right row-endpoint maximum difference was `6px` (at y108–109, current reached x150–151 while the reference starts x156).
- GREEN exact components: left `435px`, bbox `(36,48)-(43,109)`, maximum endpoint difference `1px`; right `416px`, bbox `(138,48)-(161,109)`, maximum endpoint difference `2px`. Right area is within the reference `436px` ±5% range (`414–458px`) and its bbox matches exactly.
- The right path uses contiguous stepped subpaths to reproduce the reference's two-row profile while retaining the fixed brown right-slope sample. `pnpm build`, the 1635×922 capture, and `compare.py` completed with **13/13 PASS**; `recipe.spec.ts` completed with **5/5 PASS**.
- Remaining scoped limitation: the left exact-color area remains `435px` because later `toolTabFace`/`toolTabEdge` paint produces `(23,22,33)` at x43 on y49–109; the right's five 2px rows likewise have later-layer composite samples such as `(70,71,114)` or `(18,16,22)` where the reference is exact side color. Altering those later paths would violate this task's path-only scope, so no such change was made.

### Task 6 review fix round 1 — stepped edge

- RED after the Task 6 commit: left exact side component `435px`, bbox `(36,48)-(43,109)`, versus reference `496px`; right `416px`, bbox `(138,48)-(161,109)`, with five rows at 2px endpoint difference.
- The first Edge-only trial moved the left edge to x25 and lower-right endpoint to x136. It restored left to `496px`, bbox `(36,48)-(43,109)`, and all endpoints exact, proving the left deficit was Edge stroke overpaint; its right `400px` was below the `414px` area floor.
- With the Task 6 Side restored, the accepted Edge-only candidate keeps x25 and uses a stepped right polyline: left remains `496px`, bbox exact, endpoint difference `0px`; right is `424px`, bbox `(138,48)-(161,109)`, within `436px ±5%`, with remaining 2px endpoint differences at y89, y95, y100–101, and y107.
- Edge one-pixel notches at source y50/y56/y62/y68 and a separately tested Face stepped edge did not change those five side rows. The Face trial additionally regressed its exact-color bbox to `(46,50)-(146,109)` and area to `4801px` versus reference `4856px`, so it was reverted. `compare.py` remained **13/13 PASS** in every retained and rejected trial.

### Task 6 review fix round 2 — targeted side compensation

- With the round-1 stepped Edge and original Face fixed, changed only the Side subpaths for y89/y95/y100–101/y107. RED was left `496px`, bbox exact, endpoint max `0px`; right `424px`, bbox exact, endpoint max `2px` at the five specified rows.
- GREEN recovers y100's right endpoint and raises the right component to `428px`, bbox `(138,48)-(161,109)`, within the `414–458px` acceptance area; left remains `496px`, bbox exact, endpoint max `0px`. y89, y95, y101, and y107 retain 2px left-endpoint differences, so strict row acceptance remains unmet.
- Evidence of path-insensitive residual: the four corresponding Side intervals were extended an additional 3 viewBox pixels left (`x123→120`, `x125→122`, `x127→124`, `x129→126`). Capture output was unchanged: left `496px`; right `428px`, same bbox and the same four 2px rows; comparator stayed **13/13 PASS**. The larger candidate was reverted, leaving the minimal y100 improvement.

### Task 6 review fix round 3 — user-approved layer order

- User-approved order is now Back → Face → Edge → Side → Hammer; all five existing paths, classes, tokens, and CSS remain. The order-only RED kept `compare.py` at **13/13 PASS** but Side covered its own previous left rim (`868px`, `(36,48)-(49,109)`) and reduced Face to `4551px`, so it was not retained unchanged.
- GREEN changes only Side `d`: left rectangle `H30→H24` restores left to exact `496px`, `(36,48)-(43,109)`, all row endpoints exact. Four affected right starts are moved one viewBox unit left (`129→128`, `131→130`, `133→132`, `135→134`) after the 69/70 raster scale was measured.
- Final exact Side components: right `451px`, bbox `(138,48)-(161,109)` versus reference `436px` (within 414–458px); all row endpoint deltas are ≤1px. Face exact component is `4851px`, bbox `(46,50)-(147,109)`, versus reference `4856px` and within ±1%. Equal-scale crop inspection retains a visible left Edge rim and right stepped Edge silhouette; Hammer comparator bbox/color remains PASS.
- Final build/capture/comparator completed with **13/13 PASS**; `recipe.spec.ts` completed **5/5 PASS**. A two-unit Side shift was rejected: it produced right `463px` (above the 458px ceiling) and adjacent 2px endpoint errors.

### Task 5 visual iteration round 4

- Changed exactly `toolTabSide`: `M125 0H143L166 70H145Z` -> `M125 0H126L143 70Z`; its `rgb(16 15 21)` token, every other path/token/CSS rule, and grip remained unchanged.
- Equal-scale dark-rim mask: before current exact-dark bounds `(146,39)-(184,107)`, 1264px, exceeded the reference right rim (reference total `(36,48)-(161,109)`, 932px). After shrinking the side triangle, the fixed panel-relative `(+140,-20)` probe is brown `(51,43,40)`, matching reference instead of dark `(19,17,23)`.
- Exact before/after comparator: 12/13 -> 13/13 PASS; the only previous failure `color:tab-right-slope` Δ32 recovered to Δ0. Rebuilt and regenerated current image, grid overlays, and tab/grip crops. Fresh review required; no commit.

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(144, 145, 164) maxΔ=15

== 13/13 checks passed ==
```

```text
[PASS] panel-size: ref=(1210, 300, 2071, 1405) cur=(1210, 333, 2071, 1439) maxΔ=1
[PASS] tab-size: bbox=(1210, 262, 1375, 330) size=(166, 69) maxΔ=1
[PASS] tab-left: bbox=(1210, 262, 1375, 330) got=0 maxΔ=0
[PASS] tab-bottom-gap: bbox=(1210, 262, 1375, 330) got=2 range=(0, 3)
[PASS] hammer-box: bbox=(1254, 280, 1309, 332) relative=(44, -53, 99, -1) maxΔ=1
[PASS] grip-size: bbox=(2028, 1397, 2050, 1418) size=(23, 22) maxΔ=1
[PASS] grip-right-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] grip-bottom-gap: bbox=(2028, 1397, 2050, 1418) got=20 maxΔ=1
[PASS] color:tab-front: ref=(58, 59, 72) cur=(58, 59, 72) maxΔ=0
[PASS] color:tab-back: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:tab-right-slope: ref=(51, 43, 40) cur=(51, 43, 40) maxΔ=0
[PASS] color:hammer: ref=(58, 59, 72) cur=(61, 62, 72) maxΔ=3
[PASS] color:grip: ref=(132, 133, 149) cur=(144, 145, 164) maxΔ=15

== 13/13 checks passed ==
```
