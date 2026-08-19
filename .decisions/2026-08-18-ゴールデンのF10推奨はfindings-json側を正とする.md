# ゴールデンのF10推奨はfindings.json側を正とする

決定: PR#1155ゴールデンの F10（矢印トークン名）の推奨案は findings.json の `recommended`（A=現状維持）を正とし、golden md の options 先頭を現状維持にする。

棄却案: digest.html 本文の記述（案B=改名を推奨）へ揃える。

理由: 現行成果物は本文とjsonで推奨が食い違っており、これはMarkdown正本化で消したい不具合そのもの。実装（`pr-adjudicated-apply`）が読むのはjson側で、Step 4の同値確認でも想定外差分が出ない。

リンク: [[2026-08-18-digestの見た目はPR1155をゴールデンに据えて担保する]]
