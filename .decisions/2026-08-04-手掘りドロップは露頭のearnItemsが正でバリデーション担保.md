決定: 手掘りドロップは露頭mapObjectのearnItemsが正（混合ドロップ等の自由度を維持）。整合はC#バリデーションで担保し、「item veinの露頭のearnItemsにveinのitemGuidが含まれること」をロード時に検証する。veinParam.itemGuidは掘削機用の正のまま。
棄却案:
- item veinの露頭ドロップをveinParam.itemGuidから自動導出（earnItemsが死にフィールド化・mapObject→veinの逆引きが必要・混合ドロップ不可）
- バリデーションなしでマスタ作成者責任（鉄鉱脈が石を落とす現行コピペバグのようなミスが再発する）
理由: mapObjectの汎用性を保ちつつ、実在したデータバグを機械検出できる。
リンク: .decisions/2026-08-04-露頭参照はoutcropMapObjectGuidに一本化しminingTypeにNoneを追加.md
