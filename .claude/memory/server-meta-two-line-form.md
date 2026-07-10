---
name: server-meta-two-line-form
description: moorestech_server配下の.cs.metaは2行形式（fileFormatVersion+guidのみ）がUnity正規出力 — 手動作成と誤判定しない
metadata: 
  node_type: memory
  type: project
  originSessionId: f4d8cf8e-bb07-4127-87fa-2305dda71b2b
---

moorestech_serverのコードはクライアントプロジェクトから `tech.moores.server: file:../../moorestech_server/Assets/Scripts` のローカルパッケージ参照でインポートされる。この経路でUnityが生成する.cs.metaは**fileFormatVersion+guidの2行形式**であり、MonoImporterブロックを含むフル形式はserverプロジェクトを直接Unityで開いた場合のみ生成される。

**Why:** レビューで「MonoImporterブロック無し=手動作成の.meta」と誤判定しがち（2026-07-05に実際に誤指摘→削除・再生成実験で同一GUID・同一内容が復元されfalse positiveと実証。ベースコミット時点で既存528件が同形式）。

**How to apply:** server配下の新規.cs.metaが2行形式でも正常。手動作成疑いの検証は「削除→AssetDatabase.Refresh→diffゼロなら正規」で行う。関連: [[server-tests-immutable-package]]
