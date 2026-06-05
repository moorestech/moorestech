export function Hero() {
  return (
    <header className="hero">
      <span className="kicker">moorestech V8 mod ・ 設計ドキュメント</span>
      <h1>クリーンルーム<br />（空気純度）システム</h1>
      <p className="hero-lead">
        半導体（EUV露光〜ICチップ）生産の終盤コンテンツ。壁で密閉した部屋の
        <b>不純物濃度</b>を空気清浄機とフィルターで維持し、その清浄度（クラス）が
        <b>作れるチップの最大グレードと格下げ率</b>を決める。3D密閉部屋検出・濃度の数理モデル・
        binning型の歩留まりまでを一枚で俯瞰する。
      </p>
      <div className="hero-meta">
        <span>対象 <b>moorestech_server（C# / Unity）</b></span>
        <span>状態 <b>設計確定・フェーズ1プラン化済み</b></span>
        <span>読者 <b>開発者・設計者</b></span>
        <span>外部監査 <b>Codex 2周反映済み</b></span>
      </div>
    </header>
  );
}
