import styles from "./style.module.css";

type Props = {
  // アイコンURLを持たない対象は名前で描く。kindの列挙は持たない
  // Targets without an icon URL render as their name; no kind enumeration lives here
  iconUrl: string | null | undefined;
  displayName: string;
};

// 設置対象の面。ビルドメニューとホットバーが同じ描画を共有する
// The placement target's face, shared by the build menu and the hotbar so both render identically
export default function PlacementTargetFace({ iconUrl, displayName }: Props) {
  if (!iconUrl) return <span className={styles.label}>{displayName}</span>;
  return <img className={styles.icon} src={iconUrl} alt={displayName} draggable={false} />;
}
