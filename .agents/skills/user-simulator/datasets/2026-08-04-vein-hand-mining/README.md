# 2026-08-04 vein-hand-mining 設計セッション シャドー採点

## 経緯
「Veinを手で採掘出来るようにしたい」のgrill設計セッション（moores-grill-with-docs）。
AskUserQuestion 14問（うち1問は途中でユーザーが方針転換: 露頭mapObject化→vein第一級対象化の全面棄却あり）。
成果物: docs/adr/0007-vein-as-hand-minable-target.md・.decisions/2026-08-04-*.md 10件・
docs/superpowers/plans/2026-08-04-vein-hand-mining.md

## 予測と実測サマリ（r1・2026-08-04）
目視確定で的中10/13=77%（機械exact=3はgoldの「 (Recommended)」接尾辞による正規化割れ）。
ベースライン「常に推奨」も目視77%で同等 — 価値源泉の逸脱問（02ライダー・08=1振り1ドロップ・11=AABBごと）は0/3。
装備スロットr1で見えた「推奨追従の再現しかできない」弱点がout-of-sampleで再現した。
なおセッション中盤の最大逸脱（露頭mapObject化の全面棄却＝自由テキストでの方針転換）はAskUserQuestion外で
発生したためデータセット化されておらず、盲検予測の対象外。逐次文脈上、棄却後のtask-08以降は
「棄却済み」という前提を持たない予測体には構造的に不利だった点は解釈時に注意。

## 学び
- 視覚粒度・演出系（task-11露頭密度）の逸脱は既知弱点の再発。deviation-casesへの追記候補
- 「はい、ただ〜」の条件付き承認ライダー（task-02）は複数予測体が前科として言及済み。
  「大型HP代用の解消」のような既存ハックの正式概念化要求は予測可能性がある
- 質問外の自由テキスト方針転換（重複を理由にした設計案の全面棄却）はシャドー機構の盲点。
  「定義の重複が2箇所を超えたら統合ではなく所有権の再設計を疑う」パターンとして知識化する価値あり

## 再チェック手順
tasks/task-XX.md を盲検入力としてopusで再予測 → runs/<日付-ラベル>/ に pred-XX.json を置き
python3 ../../modes/shadow/scripts/score.py runs/<日付-ラベル>/ で採点、gold.json と比較。
