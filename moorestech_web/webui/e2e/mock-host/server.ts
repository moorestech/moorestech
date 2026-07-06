import { WebSocketServer } from "ws";
import { createMockHttpServer } from "./httpHandler";
import { attachWsHandlers } from "./wsHandler";

const PORT = Number(process.env.MOCK_PORT ?? 5273);

// HTTP(静的配信+テスト制御) と WS(購読/action) を同一 server に束ねて起動する
// Bundle HTTP (static + test control) and WS (subscribe/action) on one server and start it
const server = createMockHttpServer();
const wss = new WebSocketServer({ server, path: "/ws" });
attachWsHandlers(wss);

server.listen(PORT, () => console.log(`mock-host on ${PORT}`));
