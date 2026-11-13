# user-simulator を廃止し、推奨から外れた回答だけを記録して学びに戻す

決定: user-simulator（review・preanswer・shadow・improve）を撤去する。grill・plan の Stop hook で、推奨付き質問への回答と拒否を logs repo の `harness/recommendation-deviations/` に機械で記録する。外れが 10 件たまったら別セッションを自動で起動し、理由を推測して、質問の作り方の欠陥を grill・plan のスキル本文（PR）へ、蒸留結果を wiki へ戻す。学びのファイルを grill・plan のコンテキストに毎回読ませることはしない。review が拾っていた計画の欠陥の観点は、moores-writing-plans の反証検査へ移す。
棄却案: user-simulator を縮小して残す（shadow の自動実行停止・review と preanswer を絞る）／shadow を残し、採点から改善まで自動で回す形に作り直す／Hermes cron で週次に蒸留する／蒸留結果を毎回読む薄いファイルにして grill・plan で読ませる／蒸留結果を logs repo の reviews に置く。
理由: 予測の的中は「いつも推奨を選ぶ」とほぼ同じで、外れの改善ループも手動起動のため止まっていた。実際の回答を記録すれば予測は要らない。外れ率を見れば効果も測れる。
明示引用: 「そもそも、user-simもやめて、推奨から外れたやつだけ記録する、みたいな形にして、その上で定期的に学びを足す仕組みを作るとかは」「Hermes反映ってなんで必要なの」「コンテキストにgrillやplanの時に読ませるのって適切なの？」「蒸留したものをwikiにいれて、wiki側からも参照する形がいいんじゃないの」→ 整理した案に「ok」
リンク: logs repo `harness/recommendation-deviations/README.md`・`REFLECT.md`
