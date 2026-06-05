import { Mermaid } from '../components/comment-feature';

const loopChart = `
graph LR
    subgraph SRC["汚染源 → A_total"]
      NAT["常時の自然増加<br/>a_volume·V + a_surface·S"]
      MAC["製造機の稼働"]
      DOOR["ドア開閉(人)<br/>バースト"]
      HATCH["ハッチ搬入出<br/>単位時間あたり搬送量"]
    end

    SRC -->|毎秒 +A| N["部屋の不純物 N"]
    N -->|濃度 C = N/V| PUR["空気清浄機<br/>n台 × 体積 q を処理"]
    PUR -->|n·q·C を除去| N
    PUR -->|仕事量に比例して消費| FIL["フィルター<br/>除去量 ∝ 消費"]
    PUR -->|常時| PWR["電力"]

    classDef src fill:#FEF2F2,stroke:#DC2626,stroke-width:1.8px,color:#991B1B
    classDef core fill:#F0F9FF,stroke:#0EA5E9,stroke-width:2.2px,color:#0369A1
    classDef sink fill:#ECFDF5,stroke:#059669,stroke-width:2px,color:#065F46
    class NAT,MAC,DOOR,HATCH src
    class N,PUR core
    class FIL,PWR sink
`;

export function PollutionLoop() {
  return (
    <section className="section">
      <div className="eyebrow">05 — 汚染と回復のループ</div>
      <h2>4方向から汚れ、清浄機が消し続ける</h2>
      <p className="lead">
        不純物は常に複数方向から増える。清浄機がそれを上回る能力を持てば濃度は下がり、クラスを維持できる。
        フィルターは<b>除去した不純物量に比例して消費</b>するので、汚い部屋ほどフィルターを食う＝搬送口やドアを減らす動機が直接生まれる。
      </p>

      <Mermaid
        chart={loopChart}
        id="loop"
        label="図05 汚染源(自然増加=体積/表面積比例、製造機稼働、ドア開閉バースト、ハッチ搬入出=単位時間搬送量)が毎秒A個を部屋の不純物Nに足し、空気清浄機がn台×体積qで濃度C=N/Vに比例してn·q·Cを除去し、その仕事量に比例してフィルターを消費し常時電力を使う、入る・出るの循環"
      />

      <h3>汚染源（A_total に合算）</h3>
      <ul className="checks">
        <li><b>常時の自然増加</b> — 部屋ごと固定ではなく体積 V・表面積 S に比例（巨大空室を不利にする）</li>
        <li><b>製造機の稼働</b> — 機械を詰めるほど汚染が増え、清浄機との釣り合いが生まれる</li>
        <li><b>ドアの開閉（人の出入り）</b> — 大きめのバースト。自動化を促す</li>
        <li><b>ハッチのアイテム搬入出</b> — <b>単位時間あたりの搬送量</b>で集計（1個ごとだとベルト速度で不自然に回避できるため）</li>
      </ul>

      <h3>回復側：空気清浄機 ＋ フィルター</h3>
      <ul className="checks">
        <li><b>除去量 = n台 × q × C</b> — 複数台で能力が加算。機械を詰めるほど、体積が大きいほど（ACH要求）増設が必要</li>
        <li><b>フィルターは仕事量ベース消費</b> — 平衡時の消費は総汚染レート A に比例。汚れを減らす動機が直結</li>
        <li><b>常時電力を消費</b>（ElectricMachine 系）。電力・占有面積・ACH要求が清浄機台数の設計圧になる</li>
      </ul>

      <h3>I/O の役割分担（境界種別 Kind）</h3>
      <div className="table-wrap">
        <table className="tbl">
          <thead><tr><th>境界ブロック</th><th>役割</th></tr></thead>
          <tbody>
            <tr><td><b>アイテムハッチ</b></td><td>アイテム搬入出。低スループット（集約の根拠）かつ汚染源</td></tr>
            <tr><td><b>パイプコネクタ</b></td><td>流体搬入出（超純水・IPA 等）</td></tr>
            <tr><td><b>ドア</b></td><td>プレイヤーの出入り。汚染が大きく自動化を促す</td></tr>
          </tbody>
        </table>
      </div>
    </section>
  );
}
