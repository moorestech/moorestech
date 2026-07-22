import { useEffect, useState } from "react";

// バイト数を人間可読な単位に変換
// Convert byte count to a human-readable size string
function formatSize(bytes) {
  const units = ["B", "KB", "MB", "GB"];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  return `${value.toFixed(unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
}

function MediaCard({ item }) {
  const src = `/media/${item.name}`;
  return (
    <div className="card">
      <div className="card-media">
        {item.type === "video" ? (
          <video controls preload="metadata" src={src} />
        ) : (
          <img src={src} alt={item.name} loading="lazy" />
        )}
      </div>
      <div className="card-info">
        <span className="card-name" title={item.name}>
          {item.name}
        </span>
        <span className="card-size">{formatSize(item.size)}</span>
      </div>
    </div>
  );
}

export default function App() {
  const [items, setItems] = useState([]);
  const [dirName, setDirName] = useState("");
  const [error, setError] = useState(null);

  // 起動時に一覧APIを取得しグリッド描画
  // Fetch the media list once on mount and render the grid
  useEffect(() => {
    fetch("/api/list")
      .then((res) => res.json())
      .then((data) => {
        setItems(data.items ?? data);
        setDirName(data.dirName ?? "");
      })
      .catch((err) => setError(String(err)));
  }, []);

  return (
    <div className="app">
      <header className="header">
        <h1>Evidence Viewer</h1>
        {dirName && <span className="dir-name">{dirName}</span>}
        <span className="count">{items.length} files</span>
      </header>
      {error && <p className="error">{error}</p>}
      <main className="grid">
        {items.map((item) => (
          <MediaCard key={item.name} item={item} />
        ))}
      </main>
    </div>
  );
}
