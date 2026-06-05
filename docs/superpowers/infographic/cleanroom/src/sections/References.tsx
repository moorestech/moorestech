type Ref = { href: string; label: string; desc: string };

const refs: Ref[] = [
  {
    href: 'file:///Users/katsumi/moorestech/docs/superpowers/specs/2026-06-05-cleanroom-design.md',
    label: '設計書: クリーンルーム（空気純度）システム',
    desc: 'docs/superpowers/specs/2026-06-05-cleanroom-design.md — 濃度モデル・binning・汚染源・I/O・無効化挙動の確定設計（Codex監査2周反映）',
  },
  {
    href: 'file:///Users/katsumi/moorestech/docs/superpowers/plans/2026-06-05-cleanroom-phase1-detection.md',
    label: '実装プラン: フェーズ1（3D密閉部屋検出）',
    desc: 'docs/superpowers/plans/2026-06-05-cleanroom-phase1-detection.md — 境界ブロック・検出器・世界システムのTDDタスク分解',
  },
  {
    href: 'file:///Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json',
    label: '構想メモ: nodeGraph（半導体エリア）',
    desc: '../moorestech_master/server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json — クリーンルーム/EUV/チップレベルの元メモ',
  },
  {
    href: 'file:///Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json',
    label: 'マスタ: クリーンルーム系アイテム定義',
    desc: 'items.json — クリーンルームブロック/ハッチ/コネクタ/ドア/空気清浄機（枠は定義済み、挙動は未実装）',
  },
  {
    href: 'file:///Users/katsumi/moorestech/moorestech_server/Assets/Scripts/Game.World/DataStore/WorldBlockDatastore.cs',
    label: 'コード: WorldBlockDatastore',
    desc: 'flood-fill が参照する BlockMasterDictionary / GetBlock(Vector3Int) の実装',
  },
  {
    href: 'file:///Users/katsumi/moorestech/moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs',
    label: 'コード: VanillaIBlockTemplates',
    desc: '新しい境界 blockType を登録するレジストリ',
  },
];

export function References() {
  return (
    <section className="section">
      <div className="eyebrow">07 — ソース</div>
      <h2>References</h2>
      <p className="lead">この図解の根拠ドキュメントとコード。クリックで対象を開く。</p>
      <ul className="refs">
        {refs.map((r) => (
          <li key={r.href}>
            <a href={r.href}>{r.label}</a>
            <span className="desc">{r.desc}</span>
          </li>
        ))}
      </ul>
      <div className="footer">
        moorestech クリーンルーム（空気純度）システム設計 ・ 2026-06-05 ・ 設計確定＋フェーズ1プラン化済み。
        数値（クラス閾値・q・各汚染係数・歩留まり率）は実装プランで確定する。
      </div>
    </section>
  );
}
