import json
import signal
import socket
import sys
import threading
import time
from typing import Dict, Tuple

# ----- 配置常量（与客户端 NetworkConstants 保持一致） -----
HOST = "127.0.0.1"
PORT = 8765
TICK_RATE = 20.0


class ServerState:
    def __init__(self) -> None:
        self.lock = threading.Lock()
        self.next_player_id = 1
        self.players: Dict[str, Tuple[float, float]] = {}
        self.clients: Dict[socket.socket, str] = {}

    def add_client(self, conn: socket.socket) -> str:
        with self.lock:
            player_id = str(self.next_player_id)
            self.next_player_id += 1
            self.players[player_id] = (0.0, 0.0)
            self.clients[conn] = player_id
            return player_id

    def remove_client(self, conn: socket.socket) -> None:
        with self.lock:
            player_id = self.clients.pop(conn, None)
            if player_id is not None:
                self.players.pop(player_id, None)

    def apply_move(self, player_id: str, x: float, y: float) -> None:
        # Minimal authoritative rule: server owns final world state.
        # You can add speed checks/physics checks here later.
        with self.lock:
            if player_id in self.players:
                self.players[player_id] = (float(x), float(y))

    def snapshot(self) -> Dict[str, object]:
        with self.lock:
            players = [
                {"id": pid, "x": pos[0], "y": pos[1]}
                for pid, pos in self.players.items()
            ]
        return {"type": "snapshot", "players": players}

    def sockets(self):
        with self.lock:
            return list(self.clients.keys())

    def player_id_of(self, conn: socket.socket):
        with self.lock:
            return self.clients.get(conn)


STATE = ServerState()


# ---- ServerCode (mirrors Constants.ServerCode in C#) ----
class ServerCode:
    SUCCESS = 1
    FAIL = 1 << 1
    ERROR = 1 << 2


def make_envelope(code: int, data: dict = None, message: str = "") -> dict:
    """Build a ServerEnvelope dict matching the C# ServerEnvelope wire format.
    Note: `data` is serialized to a JSON *string*, not a nested object."""
    return {
        "code": code,
        "data": json.dumps(data, separators=(",", ":")) if data else "{}",
        "message": message,
    }


# ---- Short-request business handlers (keyed by ClientNetworkMessage.type) ----

def _handle_sample_success(msg: dict) -> dict:
    content = msg.get("content", "")
    return make_envelope(ServerCode.SUCCESS, {"content": f"echo: {content}"}, "ok")


def _handle_sample_fail(msg: dict) -> dict:
    content = msg.get("content", "")
    return make_envelope(ServerCode.FAIL, {"content": f"echo: {content}"}, "service failed")


SHORT_REQUEST_HANDLERS: Dict[int, callable] = {
    0: _handle_sample_success,   # SampleState2MessageType.SUCCESS
    1: _handle_sample_fail,      # SampleState2MessageType.FAIL
}


def send_json_line(conn: socket.socket, data: Dict[str, object]) -> None:
    payload = json.dumps(data, separators=(",", ":")) + "\n"
    conn.sendall(payload.encode("utf-8"))


def handle_short_connection(conn: socket.socket, addr) -> None:
    """One-shot short-lived request: read one message, respond with ServerEnvelope, close."""
    try:
        file_obj = conn.makefile("r", encoding="utf-8", newline="\n")
        line = file_obj.readline()
        if not line or not line.strip():
            return

        try:
            msg = json.loads(line.strip())
        except json.JSONDecodeError:
            send_json_line(conn, make_envelope(ServerCode.ERROR, message="Invalid JSON"))
            return

        msg_type = msg.get("type")
        handler = SHORT_REQUEST_HANDLERS.get(msg_type)
        if handler:
            envelope = handler(msg)
        else:
            envelope = make_envelope(ServerCode.ERROR, message=f"Unknown type: {msg_type}")

        send_json_line(conn, envelope)
    except (ConnectionError, OSError):
        pass
    finally:
        try:
            conn.close()
        except OSError:
            pass
        print(f"[~] Short request from {addr} completed")


def handle_game_connection(conn: socket.socket, addr) -> None:
    """Long-lived game connection: server sends welcome, then processes move commands."""
    player_id = STATE.add_client(conn)
    try:
        send_json_line(conn, {"type": "welcome", "id": player_id})
        file_obj = conn.makefile("r", encoding="utf-8", newline="\n")
        while True:
            line = file_obj.readline()
            if not line:
                break
            line = line.strip()
            if not line:
                continue

            try:
                msg = json.loads(line)
            except json.JSONDecodeError:
                continue

            msg_type = msg.get("type")
            if msg_type == "move":
                x = msg.get("x", 0.0)
                y = msg.get("y", 0.0)
                STATE.apply_move(player_id, x, y)
    except (ConnectionError, OSError):
        pass
    finally:
        STATE.remove_client(conn)
        try:
            conn.close()
        except OSError:
            pass
        print(f"[-] Disconnected: {addr} (player {player_id})")


def handle_client(conn: socket.socket, addr):
    """Detect connection type by peeking for early data, then dispatch.

    Short connections (ClientNetworkMessage): client speaks first.
    Game connections: client waits for the server's welcome message.
    """
    conn.settimeout(0.5)
    try:
        peek = conn.recv(1, socket.MSG_PEEK)
    except socket.timeout:
        peek = None
    except (ConnectionError, OSError):
        try:
            conn.close()
        except OSError:
            pass
        return

    if peek:
        conn.settimeout(5.0)
        handle_short_connection(conn, addr)
    else:
        conn.settimeout(None)
        handle_game_connection(conn, addr)


def broadcast_loop():
    interval = 1.0 / TICK_RATE
    while True:
        start = time.time()
        snap = STATE.snapshot()
        dead = []
        for conn in STATE.sockets():
            try:
                send_json_line(conn, snap)
            except (ConnectionError, OSError):
                dead.append(conn)
        for conn in dead:
            STATE.remove_client(conn)
            try:
                conn.close()
            except OSError:
                pass
        elapsed = time.time() - start
        sleep_time = interval - elapsed
        if sleep_time > 0:
            time.sleep(sleep_time)


def main():
    shutdown = threading.Event()

    def on_sigint(*_):
        shutdown.set()

    try:
        signal.signal(signal.SIGINT, on_sigint)
    except (ValueError, OSError):
        # 非主线程或某些平台可能不可用
        pass

    print(f"Authoritative server start on {HOST}:{PORT} (Ctrl+C to stop)")
    threading.Thread(target=broadcast_loop, daemon=True).start()

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((HOST, PORT))
        server.listen()
        server.settimeout(1.0)
        while not shutdown.is_set():
            try:
                conn, addr = server.accept()
            except socket.timeout:
                continue
            except KeyboardInterrupt:
                shutdown.set()
                break
            except OSError:
                if shutdown.is_set():
                    break
                raise
            thread = threading.Thread(target=handle_client, args=(conn, addr), daemon=True)
            thread.start()
    print("Server stopped.")


if __name__ == "__main__":
    main()
    sys.exit(0)
