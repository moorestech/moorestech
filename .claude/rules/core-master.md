---
paths:
  - "moorestech_server/Assets/Scripts/Core.Master/**/*"
---

Core.Masterはマスタデータの生ロード・保持・ID⇔GUID解決のみ。

- Loader/MasterでのJSON改変・欠損プリフィル禁止（読み取り専用）
- `Default*`定数・`?? Default`フォールバック禁止。欠損はスキーマ必須化＋YAML`default`＋全JSON更新で解決する
- ドメイン固有の解釈ロジック（型名・メソッド名にプレイヤー/インベントリ/研究等が現れるもの）は各ドメインの`Game.Xxx.Interface`へ
