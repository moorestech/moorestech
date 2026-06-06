type Ref = { title: string; path: string; desc: string };

const REFS: Ref[] = [
  {
    title: '設計仕様',
    path: 'docs/superpowers/specs/2026-06-05-upgrade-system-design.md',
    desc: 'アップグレード（モジュール）システムの設計仕様。2レイヤー構造・独立ID・不変条件・抽選順序まで。',
  },
  {
    title: '実装計画（フェーズA）',
    path: 'docs/superpowers/plans/2026-06-05-upgrade-system-phase-a.md',
    desc: 'A1マスタ基盤 → A2スロットコンポーネント → A3効果集計+プロセッサ統合のTDDタスク。',
  },
  {
    title: '機械プロセッサ',
    path: 'moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs',
    desc: '効果スナップショット・処理時間・追加産出を適用する対象。Idle()で開始、Processing()で完了。',
  },
  {
    title: 'アイテムスタック（メタデータの土台）',
    path: 'moorestech_server/Assets/Scripts/Core.Item/Implementation/ItemStack.cs',
    desc: 'IsAllowedToAdd は Id のみ・Equals だけ meta 比較。独立ID採用の根拠になったファイル。',
  },
  {
    title: '出力インベントリ',
    path: 'moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs',
    desc: 'IsAllowedToOutputItem の独立OR判定 / InsertOutputSlot の全出力格納。容量予約の改修対象。',
  },
  {
    title: 'マスタ保持',
    path: 'moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs',
    desc: 'ModuleMaster を追加する場所。modules.json 不在 mod を壊さない許容ロードが必要。',
  },
  {
    title: '元構想メモ（別リポジトリ）',
    path: '../moorestech_master/server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json',
    desc: '半導体エリアの品質モジュール・チップレベル概念のメモ。このシステムの出発点。',
  },
];

export function References() {
  return (
    <section className="section refs" id="refs">
      <div className="eyebrow">08 — 参照</div>
      <h2>ソース</h2>
      <p className="lead">設計ドキュメントと、判断の根拠になった実コード（リポジトリ内パス）。</p>
      <ul>
        {REFS.map((r) => (
          <li key={r.path}>
            <span className="rk">{r.title}</span>
            <br />
            <code>{r.path}</code>
            <span className="desc">{r.desc}</span>
          </li>
        ))}
      </ul>
      <div className="footer-note">
        この図解は moorestech リポジトリの設計仕様・実装計画と、Codex 外部監査の指摘をもとに構成しています。
        図・本文へのコメントは左下「コメント」から Markdown で一括コピーできます。
      </div>
    </section>
  );
}
