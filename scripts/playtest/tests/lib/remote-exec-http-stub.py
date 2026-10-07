"""CLIのHTTP契約用スタブ。 / HTTP fixture for the CLI contract."""
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path


class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        ok = (self.path == '/api/remote-exec'
              and self.headers.get('X-Remote-Exec-Token') == 'test-token'
              and body == {'code': 'return 1;\n', 'target': 'client'})
        self.send_response(200 if ok else 403)
        self.end_headers()
        self.wfile.write(json.dumps({'outcome': 'Succeeded' if ok else 'Unauthorized', 'result': '1'}).encode())


server = HTTPServer(('127.0.0.1', 0), Handler)
access = Path(sys.argv[1])
temporary = access.with_name(access.name + '.tmp')
# 読み手がプロセス生存を確かめるため、自分のpidを載せる
# The reader verifies liveness, so the stub publishes its own pid
temporary.write_text(json.dumps({'port': server.server_port, 'token': 'test-token', 'processId': os.getpid()}), encoding='utf-8')
temporary.replace(access)
server.serve_forever()
