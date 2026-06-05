import { Mermaid } from '../components/comment-feature';

const outcomeChart = `
flowchart TD
    START["製造1回"] --> EUV{"EUV失敗判定<br/>(例 20%)"}
    EUV -->|失敗| SCRAP["不良 → 捨て"]
    EUV -->|成功| CAP["クラスが決める<br/>最大グレードで生成"]
    CAP --> DOWN["汚れ(クラス)に応じて<br/>下位グレードへ格下げ"]
    DOWN --> MOD["品質モジュールで<br/>天井内の上位へ寄せる"]
    MOD --> OUT["出力<br/>レベル1 / 2 / 3"]

    classDef start fill:#EFF6FF,stroke:#2563EB,stroke-width:2px,color:#0F172A
    classDef scrap fill:#FEF2F2,stroke:#DC2626,stroke-width:2px,color:#991B1B
    classDef room fill:#ECFDF5,stroke:#059669,stroke-width:2px,color:#065F46
    classDef mod fill:#F5F3FF,stroke:#8B5CF6,stroke-width:2px,color:#5B21B6
    class START start
    class EUV,SCRAP scrap
    class CAP,DOWN room
    class MOD,OUT mod
`;

export function ClassBinning() {
  return (
    <section className="section">
      <div className="eyebrow">04 — クラスと効果（binning型）</div>
      <h2>純度は「成功率」ではなく「格付け」を動かす</h2>
      <p className="lead">
        濃度 C の閾値で<b>クリーンルームクラス</b>が決まる。クラスは作れる<b>最大グレードの天井</b>と、
        汚れによる<b>下位グレードへの格下げ率</b>を規定する。歩留まりレバーを「失敗・格付け・分布」に直交させ、
        プレイヤーが出力を推論できるようにするのが狙い。
      </p>

      <Mermaid
        chart={outcomeChart}
        id="outcome"
        label="図04 製造1回の結果が決まる順序。まずEUV失敗判定で一定割合が不良として捨てられ、成功分はクラスが決める最大グレードで生成され、汚れ(クラス)に応じて下位グレードへ格下げされ、最後に品質モジュールが天井内の分布を上位へ寄せてレベル1/2/3が出力される流れ"
      />

      <h3>3つのレバーの役割分担</h3>
      <div className="table-wrap">
        <table className="tbl">
          <thead><tr><th>レバー</th><th>役割</th></tr></thead>
          <tbody>
            <tr><td><b>クリーンルームクラス</b><br />（純度）</td><td>製造できる<b>最大グレードの天井</b> ＋ 汚れによる<b>下位グレードへの格下げ率</b></td></tr>
            <tr><td><b>品質モジュール</b></td><td>天井の範囲内で<b>上位グレードへ寄せる分布</b></td></tr>
            <tr><td><b>EUV 20%失敗 /<br />歩留まり改善モジュール</b></td><td><b>出力なしの catastrophic 失敗率</b>（不良＝捨て）</td></tr>
          </tbody>
        </table>
      </div>
      <p>
        この分離前は「クラス＝基礎歩留まり率の床」という案だったが、それだと<b>クラスの床も EUV失敗率も“良品率”を触って重複</b>し、
        最終出力が読めなくなる（Codex 指摘）。binning に寄せることで各レバーが別々の軸を担い、半導体らしい選別モデルにもなった。
      </p>

      <div className="callout">
        <b>濃度の実体：</b> 現実の ISO クラスと同じく「単位体積あたりの不純物数」。値の閾値でレベルが段階化し、レベルが基礎歩留まりと最大チップレベルを決める——という設計者の指定をそのまま数理化している。
      </div>
    </section>
  );
}
