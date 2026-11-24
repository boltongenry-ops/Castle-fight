"""Castle Fight mythic challenge leaderboard. Run behind the game's HTTPS proxy."""
import json
import os
import re
import sqlite3
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib import error, parse, request

CHALLENGE = "mythic_eternal_winter"
ROUTE = "/mythic-leaderboard"
PORT = int(os.getenv("CF_MYTHIC_PORT", "49292"))
PROFILE_PORT = int(os.getenv("CF_PROFILE_PORT", "49173"))
DB = Path(os.getenv("CF_MYTHIC_DB", "/var/lib/castle-fight-mythic/leaderboard.sqlite3"))

SCHEMA = """
CREATE TABLE IF NOT EXISTS times (
    challenge TEXT NOT NULL, player TEXT NOT NULL, race INTEGER NOT NULL,
    nickname TEXT NOT NULL, time_ms INTEGER NOT NULL, achieved_at TEXT NOT NULL,
    PRIMARY KEY (challenge, player, race)
);
CREATE INDEX IF NOT EXISTS ranked_times ON times(challenge, time_ms, achieved_at);
"""


class RequestError(Exception):
    def __init__(self, status, code):
        super().__init__(code)
        self.status = status
        self.code = code


def connect():
    connection = sqlite3.connect(DB, timeout=5)
    connection.row_factory = sqlite3.Row
    return connection


def public_profile(token):
    secret = os.getenv("CF_PROFILE_SECRET")
    if not secret:
        raise RequestError(503, "profiles_unavailable")
    req = request.Request(
        f"http://127.0.0.1:{PROFILE_PORT}/auth",
        json.dumps({"action": "token", "token": token}).encode(),
        {"Content-Type": "application/json", "Authorization": "Bearer " + secret},
    )
    try:
        with request.urlopen(req, timeout=4) as response:
            profile = json.load(response)
    except error.HTTPError as exc:
        if exc.code == 400:
            raise RequestError(401, "session_expired") from exc
        raise RequestError(503, "profiles_unavailable") from exc
    except (OSError, ValueError) as exc:
        raise RequestError(503, "profiles_unavailable") from exc
    if not profile.get("ok") or not isinstance(profile.get("profile"), dict):
        raise RequestError(401, "session_expired")
    return profile["profile"]


def nickname(value):
    if not isinstance(value, str):
        raise RequestError(400, "invalid_nickname")
    value = " ".join(value.strip().split())
    if not (2 <= len(value) <= 24) or any(not c.isprintable() or c in "<>" for c in value):
        raise RequestError(400, "invalid_nickname")
    return value


def ranking(connection, challenge=CHALLENGE):
    rows = connection.execute(
        """SELECT nickname, race, time_ms FROM times WHERE challenge=?
           ORDER BY time_ms, achieved_at, player, race LIMIT 30""", (challenge,)
    )
    return [{**dict(row), "place": i} for i, row in enumerate(rows, 1)]


def store(payload):
    race = payload.get("race")
    elapsed = payload.get("time_ms")
    if payload.get("challenge") != CHALLENGE:
        raise RequestError(400, "unknown_challenge")
    if type(race) is not int or not 0 <= race < 18 or type(elapsed) is not int or not 1000 <= elapsed <= 86400000:
        raise RequestError(400, "invalid_result")

    token = payload.get("token") or ""
    if not isinstance(token, str) or len(token) > 256:
        raise RequestError(400, "invalid_token")
    if token:
        profile = public_profile(token)
        player = "account:" + str(profile["id"])
        name = nickname(profile["name"])
    else:
        guest = payload.get("guest")
        if not isinstance(guest, str) or not re.fullmatch("[0-9a-f]{32}", guest):
            raise RequestError(400, "invalid_guest")
        player = "guest:" + guest
        name = nickname(payload.get("nickname"))

    with closing(connect()) as connection, connection:
        connection.execute(
            """INSERT INTO times(challenge,player,race,nickname,time_ms,achieved_at)
               VALUES(?,?,?,?,?,strftime('%Y-%m-%dT%H:%M:%fZ','now'))
               ON CONFLICT(challenge,player,race) DO UPDATE SET nickname=excluded.nickname,
                 achieved_at=CASE WHEN excluded.time_ms<times.time_ms THEN excluded.achieved_at ELSE times.achieved_at END,
                 time_ms=MIN(times.time_ms,excluded.time_ms)""", (CHALLENGE, player, race, name, elapsed)
        )
        position = connection.execute(
            """WITH ranked AS (
                   SELECT player,race,ROW_NUMBER() OVER (ORDER BY time_ms,achieved_at,player,race) AS place
                   FROM times WHERE challenge=?
               ) SELECT place FROM ranked WHERE player=? AND race=?""", (CHALLENGE, player, race)
        ).fetchone()[0]
        return {"ok": True, "entries": ranking(connection), "place": position}


class Handler(BaseHTTPRequestHandler):
    def reply(self, status, data):
        content = json.dumps(data, ensure_ascii=False, allow_nan=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(content)

    def log_message(self, fmt, *args):
        pass

    def do_GET(self):
        if parse.urlsplit(self.path).path != ROUTE:
            self.reply(404, {"ok": False, "error": "not_found"})
            return
        query = parse.parse_qs(parse.urlsplit(self.path).query)
        challenge = query.get("challenge", [CHALLENGE])[0]
        with closing(connect()) as connection:
            self.reply(200, {"ok": True, "challenge": challenge, "entries": ranking(connection, challenge)})

    def do_POST(self):
        if parse.urlsplit(self.path).path != ROUTE:
            self.reply(404, {"ok": False, "error": "not_found"})
            return
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if not 0 < size <= 4096:
                raise RequestError(400, "invalid_request")
            self.connection.settimeout(10)
            payload = json.loads(self.rfile.read(size))
            if not isinstance(payload, dict):
                raise RequestError(400, "invalid_request")
            self.reply(200, store(payload))
        except RequestError as exc:
            self.reply(exc.status, {"ok": False, "error": exc.code})
        except (ValueError, OSError):
            self.reply(400, {"ok": False, "error": "invalid_request"})
        except sqlite3.Error:
            self.reply(503, {"ok": False, "error": "storage_unavailable"})


if __name__ == "__main__":
    DB.parent.mkdir(parents=True, exist_ok=True)
    with closing(connect()) as connection, connection:
        connection.execute("PRAGMA journal_mode=WAL")
        connection.executescript(SCHEMA)
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
