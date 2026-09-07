"""Loopback-only finite and unknown-length Vorbis controls for the in-game probe."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import sys
import time

root = Path(sys.argv[1]).resolve()

class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self):
        live = self.path == "/live.ogg"
        if self.path not in ("/finite.ogg", "/live.ogg"):
            self.send_error(404)
            return
        data = (root / ("long.ogg" if live else "finite.ogg")).read_bytes()
        self.send_response(200)
        self.send_header("Content-Type", "audio/ogg")
        self.send_header("Connection", "close")
        if not live:
            self.send_header("Content-Length", str(len(data)))
        else:
            self.send_header("Transfer-Encoding", "chunked")
        self.end_headers()
        try:
            if not live:
                self.wfile.write(data)
            else:
                # No end-of-response during the startup observation; audio pages
                # are delivered incrementally, with no Content-Length available.
                for i in range(0, len(data), 4096):
                    block = data[i:i + 4096]
                    self.wfile.write(f"{len(block):x}\r\n".encode() + block + b"\r\n")
                    self.wfile.flush()
                    time.sleep(0.5)
                self.wfile.write(b"0\r\n\r\n")
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass
        self.close_connection = True

server = ThreadingHTTPServer(("127.0.0.1", 18764), Handler)
print("Native controls ready at http://127.0.0.1:18764", flush=True)
server.serve_forever()
