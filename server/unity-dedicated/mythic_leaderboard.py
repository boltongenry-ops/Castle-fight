"""Castle Fight mythic challenge leaderboard. Run behind the game's HTTPS proxy."""
import json
import os
import re
import sqlite3
import unicodedata
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib import error, parse, request

CHALLENGE = "mythic_eternal_winter"
CHALLENGES = (CHALLENGE, "mythic_funeral_choir", "mythic_cursed_horde", "mythic_last_protocol", "mythic_abyssal_tide", "mythic_quackalypse")
ACHIEVEMENTS = frozenset(CHALLENGES) | frozenset("""
    five_pillars no_legends mixed_army jade_horde web_and_ice tide_and_fire iron_and_bone
    wild_defense elemental_siege elf_precision north_storm bone_fortress corrupt_triangle
    orc_no_rescue chaos_rush swarm_survivor jade_without_safety nature_small_grove
    centaur_open_steppe human_three_families naga_veterans nature_without_magic chaos_thin_front
    north_ice_guard mech_field_workshop undead_silent_crypt elemental_four_winds elves_unbroken
    nerubian_deep_siege ultimate_adaptation siege_iron_marathon siege_deep_bastion siege_no_rest
    siege_broken_gate siege_steel_watch siege_living_wall siege_steppe_reserve siege_before_dawn
    siege_prism siege_jade_stock siege_lanterns siege_ultimate_keep
""".split())
NORTHERN_RACE = 5
WINTER_RULES_REVISION = 2
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
CREATE INDEX IF NOT EXISTS player_times ON times(player, challenge);
CREATE TABLE IF NOT EXISTS achievements (
    player TEXT NOT NULL, challenge TEXT NOT NULL, PRIMARY KEY (player, challenge)
);
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


def initialize():
    DB.parent.mkdir(parents=True, exist_ok=True)
    with closing(connect()) as connection, connection:
        connection.execute("PRAGMA journal_mode=WAL")
        connection.executescript(SCHEMA)
        connection.execute("BEGIN IMMEDIATE")
        fields = {column["name"] for column in connection.execute("PRAGMA table_info(times)")}
        if "rules_revision" not in fields:
            connection.execute("ALTER TABLE times ADD COLUMN rules_revision INTEGER NOT NULL DEFAULT 1")
        if "comment" not in fields:
            connection.execute("ALTER TABLE times ADD COLUMN comment TEXT NOT NULL DEFAULT ''")
        return connection.execute(
            "DELETE FROM times WHERE challenge=? AND race=? AND rules_revision<?",
            (CHALLENGE, NORTHERN_RACE, WINTER_RULES_REVISION),
        ).rowcount


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


def score_comment(value):
    if not isinstance(value, str) or len(value) > 120 or any(
        unicodedata.category(c) in {"Cc", "Cf", "Cs", "Zl", "Zp"} or c in "<>" for c in value
    ):
        raise RequestError(400, "invalid_comment")
    return " ".join(value.split())


RANKED_PLAYERS = """WITH attempts AS (
    SELECT player, nickname, race, time_ms, achieved_at, comment,
           ROW_NUMBER() OVER (
               PARTITION BY player ORDER BY time_ms, achieved_at, race
           ) AS attempt
    FROM times WHERE challenge=?
), ranked AS (
    SELECT player, nickname, race, time_ms, comment,
           ROW_NUMBER() OVER (ORDER BY time_ms, achieved_at, player) AS place
    FROM attempts WHERE attempt=1
)"""


def ranking(connection, challenge=CHALLENGE):
    rows = connection.execute(
        RANKED_PLAYERS + " SELECT nickname, race, time_ms, comment, place FROM ranked ORDER BY place LIMIT 30",
        (challenge,),
    )
    return [dict(row) for row in rows]


def store(payload):
    race = payload.get("race")
    elapsed = payload.get("time_ms")
    challenge = payload.get("challenge")
    comment = score_comment(payload.get("comment", ""))
    if challenge not in CHALLENGES:
        raise RequestError(400, "unknown_challenge")
    if type(race) is not int or not 0 <= race < 18 or type(elapsed) is not int or not 1000 <= elapsed <= 86400000:
        raise RequestError(400, "invalid_result")

    revision = 1
    if challenge == CHALLENGE and race == NORTHERN_RACE:
        revision = payload.get("rules_revision")
        if type(revision) is not int or revision != WINTER_RULES_REVISION:
            raise RequestError(409, "outdated_winter_rules")

    token = payload.get("token") or ""
    if not isinstance(token, str) or len(token) > 256:
        raise RequestError(400, "invalid_token")
    if token:
        profile = public_profile(token)
        player = "account:" + str(profile["id"])
        name = nickname(payload.get("nickname", profile["name"]) if profile.get("guest") else profile["name"])
    else:
        guest = payload.get("guest")
        if not isinstance(guest, str) or not re.fullmatch("[0-9a-f]{32}", guest):
            raise RequestError(400, "invalid_guest")
        player = "guest:" + guest
        name = nickname(payload.get("nickname"))

    with closing(connect()) as connection, connection:
        connection.execute(
            """INSERT INTO times(challenge,player,race,nickname,time_ms,achieved_at,rules_revision,comment)
               VALUES(?,?,?,?,?,strftime('%Y-%m-%dT%H:%M:%fZ','now'),?,?)
               ON CONFLICT(challenge,player,race) DO UPDATE SET nickname=excluded.nickname,
                 comment=CASE WHEN excluded.time_ms<times.time_ms THEN excluded.comment ELSE times.comment END,
                 achieved_at=CASE WHEN excluded.time_ms<times.time_ms THEN excluded.achieved_at ELSE times.achieved_at END,
                 rules_revision=CASE WHEN excluded.time_ms<times.time_ms THEN excluded.rules_revision ELSE times.rules_revision END,
                 time_ms=MIN(times.time_ms,excluded.time_ms)""", (challenge, player, race, name, elapsed, revision, comment)
        )
        position = connection.execute(
            RANKED_PLAYERS + " SELECT place FROM ranked WHERE player=?", (challenge, player)
        ).fetchone()[0]
        return {"ok": True, "challenge": challenge, "entries": ranking(connection, challenge), "place": position}


def player_achievements(connection, player):
    identity = "account:" + player
    rows = connection.execute(
        """SELECT challenge FROM achievements WHERE player=?
           UNION SELECT challenge FROM times WHERE player=? ORDER BY challenge""",
        (identity, identity),
    )
    return {"ok": True, "player": player, "achievements": [row[0] for row in rows if row[0] in ACHIEVEMENTS]}


def sync_achievements(payload, bearer):
    choices = payload.get("achievements")
    if not isinstance(choices, list) or len(choices) > len(ACHIEVEMENTS) or any(
        not isinstance(value, str) or value not in ACHIEVEMENTS for value in choices
    ):
        raise RequestError(400, "invalid_achievements")
    if not bearer.startswith("Bearer ") or not 0 < len(bearer[7:]) <= 256:
        raise RequestError(401, "missing_session")
    profile = public_profile(bearer[7:])
    player = str(profile["id"])
    completed = set(choices)
    completed.update(value for value in profile.get("completed", []) if isinstance(value, str) and value in ACHIEVEMENTS)
    with closing(connect()) as connection, connection:
        connection.executemany(
            "INSERT OR IGNORE INTO achievements(player, challenge) VALUES (?,?)",
            [("account:" + player, challenge) for challenge in completed],
        )
        return player_achievements(connection, player)


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
        if "profile" in query:
            player = query["profile"][0]
            if not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", player):
                return self.reply(400, {"ok": False, "error": "invalid_player"})
            try:
                with closing(connect()) as connection:
                    return self.reply(200, player_achievements(connection, player))
            except sqlite3.Error:
                return self.reply(503, {"ok": False, "error": "storage_unavailable"})
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
            if payload.get("action") == "sync_achievements":
                self.reply(200, sync_achievements(payload, self.headers.get("Authorization", "")))
            else:
                self.reply(200, store(payload))
        except RequestError as exc:
            self.reply(exc.status, {"ok": False, "error": exc.code})
        except (ValueError, OSError):
            self.reply(400, {"ok": False, "error": "invalid_request"})
        except sqlite3.Error:
            self.reply(503, {"ok": False, "error": "storage_unavailable"})


if __name__ == "__main__":
    removed = initialize()
    print(f"Removed {removed} legacy Northern winter results", flush=True)
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
