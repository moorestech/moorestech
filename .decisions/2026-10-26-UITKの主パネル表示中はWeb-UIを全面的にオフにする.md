# UITK の主パネル表示中は Web UI を全面的にオフにする

決定: 共存中、UITK が主パネル（幕を張る画面。ポーズ・設定・バグ報告・インベントリ・ビルドメニューなど）を出しているあいだは、Web UI を全面的にオフにする。CEF の RawImage を隠し、CEF への入力の転送を止め、Unity 側は Web の pointerOverUi と textInputFocused を読まない。主パネルを閉じたら、Web 側の古い値を捨ててから描画と転送を戻す。その間は Web の HUD（ホットバーなど）も消える。移行計画の R1・§3.8 の「両系の値の論理和」は、主パネルが Web の状態だけに狭める。
棄却案: 委譲中は Unity 側だけが Web の pointerOverUi を無視し、Web の HUD は描き続ける（agent の推奨だった案）／Web に `pointer-events: none` を当てて Web 側から false を送らせる（Web の描画の時機に左右される）。
理由: Web は廃止する前提なので、UITK の画面を出しているあいだ Web を残す理由が無い。実測 S15 では、`pointer-events: none` で Web の操作は止まったが、CEF 経由の pointerOverUi が true のまま固まった（PR #1558）。
出所: ユーザー 2026-10-26「4 良く分からないんだけど。そもそもwebを廃止するって話だから、UITKを表示中は全面的にweb uiをオフにしたら良いんじゃないの？」（UITK 段階0 実測セッションでの回答）。「UITK を表示中」を「UITK が主パネルを出しているあいだ」と読むのは agent の解釈。段階 2 で常駐表示が UITK になった後も Web の画面（インベントリなど）とホットバーが残るため、UITK の表示があれば常に Web を消す読み方は取らない。HUD・通知・ツールチップ・トーストのような幕を張らない UITK 表示では Web を消さず、UITK のモーダルは主パネルと同じく消す（agent 既定。計画 §7.2）。
リンク: [[2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する]]、`docs/uitk-migration/plan.md` §3.3 の R1・§3.8・§4.8 の S15・§7.2、PR #1558（実測の報告）
