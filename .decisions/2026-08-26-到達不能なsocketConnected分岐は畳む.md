# 到達不能な socket.Connected 分岐は畳む

日付: 2026-08-26

決定: `ConnectServer.Connect()` の `if (socket.Connected)` を外して直線コードにする。`Connect` が失敗時に必ず例外を投げる事実を2行セットコメントで残す。

棄却案:
- `else` で `ConnectFailed` を表示し、接続結果の集合を表示レベルで閉じる
- 現状維持（分岐をそのまま残す）

理由: 到達不能なフォールバックは「守られている」という誤った安心を生む。将来 `ConnectAsync` 等の非throw系APIへ差し替えるときは結果判定を明示的に書き直すのが筋であり、今の分岐はその書き直しを促さない。

リンク: [[docs/adr/0034-localization-gap-fixes-and-german-locale.md]]
