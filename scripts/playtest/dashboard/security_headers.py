"""全応答に付けるセキュリティヘッダ。テスター由来の文字列とバイト列を返すため、スクリプトは自オリジンの静的ファイルだけに絞る。

Security headers sent on every response; tester-supplied text and bytes are served, so scripts are limited to this origin's static files.
"""
from __future__ import annotations

from http.server import BaseHTTPRequestHandler

CSP = "default-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self'; script-src 'self'; frame-ancestors 'none'"


def send_security_headers(handler: BaseHTTPRequestHandler) -> None:
    handler.send_header("Content-Security-Policy", CSP)
    handler.send_header("X-Content-Type-Options", "nosniff")
