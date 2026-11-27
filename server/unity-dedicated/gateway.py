"""WebSocket gateway for the Unity match process. Bind behind a TLS reverse proxy."""
import asyncio
import hashlib
import hmac
import json
import os
import secrets
import time
from dataclasses import dataclass, field
from pathlib import Path
from urllib import request

from websockets.asyncio.server import serve
from websockets.exceptions import ConnectionClosed

from spectators import Spectators

BUILD = "0.6.19"
MAX_ROOMS = 4
MAX_PEERS = 64
RESUME_SECONDS = 90
PROFILE_ROUTES = {
    "profile_auth": "/auth", "profile_refresh": "/profile",
    "profile_update": "/profile", "profile_logout": "/logout",
    "profile_social": "/social", "profile_password": "/password",
    "profile_challenge": "/challenge", "profile_solo_result": "/solo_result",
    "profile_cosmetic": "/cosmetic",
}
AUTHORITY_PORT = int(os.getenv("CF_AUTHORITY_PORT", "49290"))
AUTHORITY_KEY = os.environ["CF_AUTHORITY_KEY"]
PROFILE_PORT = int(os.getenv("CF_PROFILE_PORT", "49173"))
PROFILE_SECRET = os.getenv("CF_PROFILE_SECRET")
VOICE_URL = os.getenv("CF_VOICE_URL")
RESULT_DIR = Path(os.getenv("CF_RESULT_DIR", "pending-results"))
CLASSIC_EMOTES = {"orc", "knight", "undead", "naga", "elf", "goblin"}
EMOTE_COOLDOWN = 1.5
EMOTES = CLASSIC_EMOTES | {
    item["id"]
    for pack in json.loads(Path(__file__).with_name("match_emotes.json").read_text(encoding="utf-8-sig"))
    for item in pack["items"]
}


def post(port, path, headers, data):
    headers["Content-Type"] = "application/json"
    body = json.dumps(data, separators=(",", ":"), allow_nan=False).encode("utf-8")
    req = request.Request(f"http://127.0.0.1:{port}{path}", body, headers)
    with request.urlopen(req, timeout=3) as handle:
        return json.loads(handle.read(8 * 1024 * 1024))


async def authority(op, room=None, **params):
    data = {"op": op, **params}
    if room:
        data["room"] = room.code
    return await asyncio.to_thread(
        post, AUTHORITY_PORT, "/command", {"X-Castle-Authority": AUTHORITY_KEY}, data
    )


async def profiles(path, payload):
    if not PROFILE_SECRET:
        return {"ok": False, "message": "Профили недоступны на тестовом сервере"}
    return await asyncio.to_thread(
        post, PROFILE_PORT, path, {"Authorization": "Bearer " + PROFILE_SECRET}, payload
    )


def text(value, limit):
    return "".join(c for c in value.strip() if c.isprintable())[:limit] if isinstance(value, str) else ""


@dataclass(eq=False)
class Peer:
    socket: object
    browse: bool = False
    room: object = None
    slot: int = -1
    profile: dict = field(default_factory=dict)
    token: str = ""
    activity: str = ""
    challenge: str = ""
    next_profile: float = 0.0
    recent: list = field(default_factory=list)


@dataclass
class Player:
    peer: Peer
    name: str
    race: str
    resume: str = field(default_factory=lambda: secrets.token_urlsafe(32))
    ready: bool = False
    loaded: bool = False
    absent_until: float = 0.0
    next_emote: float = 0.0

    def public(self):
        profile = self.peer.profile
        return dict(name=profile.get("name", self.name), race=self.race, ready=self.ready,
                    id=profile.get("id", ""), wins=profile.get("wins", 0),
                    avatar=profile.get("avatar", ""), equipped=profile.get("equipped", {}),
                    ranked=bool(profile.get("id")))


@dataclass(eq=False)
class Room:
    code: str
    players: list = field(default_factory=lambda: [None, None])
    config: dict = field(default_factory=lambda: dict(gold=250, rounds=1, fog=True, aspects=False, mode="fixed"))
    messages: list = field(default_factory=list)
    phase: str = "waiting"
    wins: list = field(default_factory=lambda: [0, 0])
    choices: list = field(default_factory=lambda: ["", ""])
    round: int = 1
    series_finished: bool = False
    series_id: str = field(default_factory=lambda: secrets.token_hex(16))
    load: dict = field(default_factory=dict)
    deadline: float = 0.0
    emote_serial: int = 0
    finished_snapshot: list = field(default_factory=list)


class Gateway:
    def __init__(self, races):
        self.races = races
        self.rooms = {}
        self.peers = set()
        self.lock = asyncio.Lock()
        self.spectators = Spectators(self.send, profiles)

    async def send(self, peer, packet):
        if peer not in self.peers:
            return
        try:
            await asyncio.wait_for(
                peer.socket.send(json.dumps(packet, ensure_ascii=False, separators=(",", ":"), allow_nan=False)), 1.0
            )
        except (ConnectionClosed, TimeoutError):
            return

    async def room_send(self, room, packet):
        await asyncio.gather(*(self.send(player.peer, packet) for player in room.players if player))

    async def directory(self, peer=None):
        rooms = [dict(id=room.code, host=room.players[0].public()["name"], players=sum(bool(p) for p in room.players),
                      capacity=2, state=room.phase, joinable=room.phase=="waiting" and not room.players[1])
                 for room in self.rooms.values()]
        folder = dict(type="directory", rooms=rooms, can_create=len(rooms)<MAX_ROOMS, build=BUILD,
                      social_supported=int(bool(PROFILE_SECRET)), production_sync=1, races=self.races,
                      spectator_supported=bool(PROFILE_SECRET))
        await asyncio.gather(*(self.send(p,folder) for p in ([peer] if peer else self.peers) if p.browse and p.room is None))

    async def lobby(self, room):
        await self.room_send(room, dict(type="lobby", code=room.code, config=room.config,
                             players=[p.public() if p else {} for p in room.players], messages=room.messages,
                             aspects_supported=True, post_match_supported=True))
        await self.directory()

    async def error(self, peer, message):
        await self.send(peer, {"type": "error", "message": message})

    async def voice(self, room, peer):
        if not (VOICE_URL and PROFILE_SECRET and room.players[0]):
            return
        expiry = int(time.time())+600
        prefix = hashlib.sha256(room.players[0].resume.encode()).hexdigest()[:16]
        voice_room = f"cf_unity_{room.code}_{prefix}"
        body = f"{voice_room}|{peer.slot}|{expiry}"
        ticket = body+"|"+hmac.new(PROFILE_SECRET.encode(),body.encode(),hashlib.sha256).hexdigest()
        await self.send(peer, dict(type="voice_session", room=voice_room, ticket=ticket, url=VOICE_URL, expires=expiry))

    async def profile(self, peer, packet):
        kind = packet["type"]
        action = packet.get("action", "")
        if time.monotonic()<peer.next_profile:
            return dict(ok=False, message="Подождите перед следующим запросом.")
        peer.next_profile=time.monotonic()+.5
        if peer.room and kind in ("profile_auth","profile_logout"):
            return dict(ok=False, message="Сначала выйдите из комнаты.")
        if kind=="profile_auth" and action=="guest" and peer.token:
            if peer.profile.get("guest"):
                return dict(ok=True, profile=peer.profile, token=peer.token)
            return dict(ok=False, message="Сначала выйдите из аккаунта.")
        payload = dict(packet)
        if kind!="profile_auth":
            payload["token"]=peer.token
        if kind=="profile_social" and action=="send" and payload.get("kind")=="invite":
            if not peer.room or peer.room.phase!="waiting" or all(peer.room.players):
                return dict(ok=False, message="Для приглашения нужна открытая комната со свободным местом.")
            payload["text"]=peer.room.code
        if kind=="profile_logout" and peer.token:
            await self.mark_last_seen(peer)
        try:
            result=await profiles(PROFILE_ROUTES[kind],payload)
        except (OSError, ValueError):
            return dict(ok=False,message="Сервис профилей временно недоступен.")
        if result.get("ok"):
            if kind=="profile_logout":
                await self.spectators.disconnect(peer)
                peer.profile={};peer.token=""
                result.update(profile={},token="")
            elif isinstance(result.get("profile"),dict):
                if peer.profile.get("id") != result["profile"].get("id"):
                    await self.spectators.disconnect(peer)
                peer.profile=result["profile"]
                peer.token=result.get("token",peer.token)
                if peer.room:
                    await self.lobby(peer.room)
            if kind=="profile_auth" and peer.token:
                await self.mark_last_seen(peer)
            if kind=="profile_social":
                if action=="remove":
                    await self.spectators.friendship_removed(peer.profile.get("id"),packet.get("peer"))
                await self.spectators.reconcile_friends(peer,result.get("friends"))
                self.attach_presence(peer, result)
        if packet.get("older") is True:
            result["older"]=True
        return result

    async def mark_last_seen(self, peer):
        try:
            await profiles("/social", dict(token=peer.token, action="presence"))
        except (OSError, ValueError):
            pass

    def attach_presence(self, viewer, result):
        online = {}
        priority = {"menu": 0, "spectating": 1, "solo": 1, "challenge": 2, "room": 3, "playing": 4}
        for peer in self.peers:
            identity = peer.profile.get("id")
            if not identity:
                continue
            activity = ("playing" if peer.room.phase != "waiting" else "room") if peer.room else peer.activity or "menu"
            if identity in online and priority[online[identity]["activity"]] >= priority[activity]:
                continue
            public = {key: peer.profile[key] for key in ("id", "name", "avatar", "avatar_ref", "wins") if key in peer.profile}
            online[identity] = dict(public, online=True, activity=activity,
                                    challenge=peer.challenge if activity=="challenge" else "")

        def decorate(player):
            if isinstance(player, dict):
                player.update(online.get(player.get("id"), {"online": False}))

        for field in ("player", "peer"):
            decorate(result.get(field))
        for field in ("friends", "found", "requests", "conversations"):
            for row in result.get(field, []):
                decorate(row.get("player", row))
        if "friends" in result:
            friends = {row["id"] for row in result["friends"]}
            friends.add(viewer.profile.get("id"))
            available={host.profile.get("id") for host in self.peers if self.spectators.available(host)}
            for friend in result["friends"]:
                friend["spectate"]=friend.get("id") in available
            result["playing_now"] = sorted(
                (player for identity, player in online.items() if identity not in friends),
                key=lambda p: p.get("name", "").casefold(),
            )

    async def enter_room(self, peer, packet):
        if peer.room:
            return
        creating=packet["type"]=="create"
        if creating:
            if len(self.rooms)>=MAX_ROOMS:
                return await self.error(peer,"Сейчас все игровые комнаты заняты.")
            code=secrets.token_hex(3).upper()
            while code in self.rooms:
                code=secrets.token_hex(3).upper()
            room=self.rooms[code]=Room(code)
            slot=0
        else:
            room=self.rooms.get(text(packet.get("code"),16).upper())
            if not room or room.phase!="waiting" or room.players[1]:
                return await self.error(peer,"Комната закрыта или уже заполнена.")
            slot=1
            if peer.profile.get("id") and peer.profile["id"]==room.players[0].peer.profile.get("id"):
                return await self.error(peer,"Этот аккаунт уже в комнате.")
        race=packet.get("race")
        if race not in self.races:race=self.races[0]
        player=room.players[slot]=Player(peer,text(packet.get("name"),24) or "Гость",race)
        peer.room,peer.slot=room,slot
        await self.send(peer,dict(type="joined",slot=slot,resume_token=player.resume,production_sync=1))
        await self.lobby(room)
        await self.voice(room,peer)

    async def resume(self, peer, packet):
        key=packet.get("token")
        if isinstance(key,str) and len(key)==43 and not peer.room:
            for room in self.rooms.values():
                for slot,player in enumerate(room.players):
                    if player and time.monotonic()<player.absent_until and secrets.compare_digest(player.resume,key):
                        peer.token,peer.profile=player.peer.token,player.peer.profile
                        player.peer=peer;player.absent_until=0
                        peer.room,peer.slot=room,slot
                        waiting=any(p.absent_until for p in room.players)
                        await self.send(peer,dict(type="resumed",slot=slot,waiting=waiting,
                                                  loading=room.phase=="loading",playing=room.phase in ("playing","finished")))
                        if room.phase=="playing" and not waiting:
                            await authority("resume",room)
                        if not waiting:
                            await self.room_send(room,dict(type="connection_restored"))
                        await self.snapshot(room)
                        await self.voice(room,peer)
                        return
        await self.send(peer,dict(type="resume_rejected",message="Время восстановления истекло."))

    async def close_room(self, room, reason):
        self.rooms.pop(room.code,None)
        if room.phase!="waiting":
            try:await authority("drop",room)
            except OSError:pass
        for player in room.players:
            if not player:continue
            player.peer.room=None;player.peer.slot=-1
            await self.send(player.peer,dict(type="room_closed",message=reason))
        await self.directory()

    async def leave(self, peer, explicit=False):
        room=peer.room
        if room is None:return
        if not explicit and room.phase in ("loading","playing","finished"):
            room.players[peer.slot].absent_until=time.monotonic()+RESUME_SECONDS
            await authority("hold",room)
            await self.room_send(room,dict(type="connection_hold",remaining_ms=RESUME_SECONDS*1000))
        elif room.phase!="waiting" or peer.slot==0:
            await self.close_room(room,"Комната закрыта.")
        else:
            room.players[1]=None
            room.players[0].ready=False
            peer.room=None;peer.slot=-1
            await self.lobby(room)

    async def start(self, room):
        races=[p.race for p in room.players]
        if room.config["mode"]=="mirror":
            races[1]=races[0]
        elif room.config["mode"]=="random":
            races=[secrets.choice(self.races),secrets.choice(self.races)]
        config=dict(room.config)
        result=await authority("create",room,races=races,**config)
        if not result.get("ok"):
            return await self.error(room.players[0].peer,result.get("message","Не удалось начать бой."))
        room.phase="loading"
        room.deadline=time.monotonic()+120
        room.finished_snapshot=[]
        for p in room.players:p.loaded=False
        room.load=dict(type="load",races=races,config=config,round=room.round,wins=room.wins,
                       ultimate_loadouts=result["ultimate_loadouts"])
        await self.room_send(room,room.load)
        await self.directory()

    async def snapshot(self, room):
        if room.finished_snapshot:
            frames=room.finished_snapshot
        else:
            result=await authority("state",room)
            if not result.get("ok"):return
            frames=result["snapshots"]
        if not room.finished_snapshot and frames[0].get("over"):
            winner=frames[0]["winner"]
            if winner in (0,1):
                room.wins[winner]+=1
            room.series_finished=max(room.wins)>room.config["rounds"]//2
            room.phase="finished";room.choices=["",""]
            room.finished_snapshot=frames
            room.deadline=time.monotonic()+240
            ids=[p.peer.profile.get("id","") for p in room.players]
            if winner in (0,1) and any(ids) and PROFILE_SECRET:
                result_id=hashlib.sha256(f"{room.series_id}:{room.round}".encode()).hexdigest()[:32]
                result=dict(match=result_id,players=ids,winner=winner,
                            names=[p.public()["name"] for p in room.players],races=room.load["races"],
                            round=room.round,duration=frames[0]["elapsed"])
                RESULT_DIR.mkdir(parents=True,exist_ok=True,mode=0o700)
                temporary=RESULT_DIR/(result_id+".tmp")
                temporary.write_text(json.dumps(result,ensure_ascii=False),encoding="utf-8")
                temporary.replace(RESULT_DIR/(result_id+".json"))
        for slot,frame in enumerate(frames):
            enriched=dict(frame, wins=room.wins, round=room.round, series_finished=room.series_finished)
            await self.send(room.players[slot].peer,enriched)

    async def post_match(self, room, slot, kind):
        room.choices[slot]="lobby" if kind=="return_lobby" else "rematch"
        await self.room_send(room,dict(type="post_match",choices=room.choices))
        if not all(room.choices):return
        await authority("drop",room)
        if "lobby" in room.choices or room.series_finished:
            room.phase="waiting";room.wins=[0,0];room.round=1;room.choices=["",""];room.series_finished=False
            room.series_id=secrets.token_hex(16)
            for p in room.players:p.ready=False
            await self.room_send(room,dict(type="return_lobby"))
            await self.lobby(room)
        else:
            if room.finished_snapshot and room.finished_snapshot[0]["winner"]!=-1:room.round+=1
            await self.start(room)

    async def handle(self, peer, p):
        kind=p.get("type")
        if not isinstance(kind,str):return
        if kind in ("browse","create","join","resume") and (p.get("protocol")!=1 or p.get("build")!=BUILD or p.get("authority")!=1):
            return await self.error(peer,"Обновите Castle Fight для подключения к Unity-серверу.")
        if kind=="ping":return await self.send(peer,dict(type="pong"))
        if kind in ("spectate_open","spectate_close","spectate_join","spectate_leave"):
            return await self.spectators.command(peer,p,self.peers)
        if kind=="profile_activity":
            activity=p.get("activity")
            if peer.token and activity in ("menu","solo","challenge","spectating"):
                peer.activity=activity
                challenge=text(p.get("challenge"),48)
                peer.challenge=challenge if activity=="challenge" and challenge.replace("_","").isalnum() else ""
            return
        if kind=="browse":
            peer.browse=True
            return await self.directory(peer)
        if kind in PROFILE_ROUTES:
            result=await self.profile(peer,p)
            return await self.send(peer,{**result,"type":"profile","request_type":kind,
                                         "action":p.get("action",""),"social_supported":int(bool(PROFILE_SECRET))})
        if kind in ("create","join","resume") and peer in self.spectators.viewers:
            return await self.error(peer,"Сначала выйдите из просмотра.")
        if kind in ("create","join","resume"):
            await self.spectators.close(peer,"Игрок перешёл в сетевой режим")
        if kind in ("create","join"):return await self.enter_room(peer,p)
        if kind=="resume":return await self.resume(peer,p)
        room,slot=peer.room,peer.slot
        if not room:return
        player=room.players[slot]
        if kind=="leave":return await self.leave(peer,True)
        if kind=="voice_info":return await self.voice(room,peer)
        if kind=="chat":
            body=text(p.get("text"),180)
            if not body:return
            message=dict(name=player.public()["name"],text=body,slot=slot)
            room.messages=(room.messages+[message])[-40:]
            return await self.room_send(room,dict(type="chat",message=message))
        if kind=="emote":
            if room.phase!="playing" or any(p.absent_until for p in room.players if p):
                return await self.send(peer, dict(type="emote_rejected", message="Ожидаем соединения с боем"))
            emote=p.get("id")
            if not isinstance(emote,str) or emote not in EMOTES:
                return await self.send(peer, dict(type="emote_rejected", message="Стикер недоступен"))
            now = time.monotonic()
            if now < player.next_emote:
                return await self.send(peer, dict(type="emote_rejected", retry_after=player.next_emote-now))
            player.next_emote = now + EMOTE_COOLDOWN
            room.emote_serial+=1
            return await self.room_send(room,dict(type="emote",cooldown=EMOTE_COOLDOWN,
                message=dict(id=emote,slot=slot,name=player.public()["name"],serial=room.emote_serial)))
        if room.phase=="waiting":
            if kind=="race" and p.get("race") in self.races:
                player.race=p["race"];player.ready=False
            elif kind=="ready":
                player.ready=p.get("ready") is True
            elif kind=="configure" and slot==0 and isinstance(p.get("config"),dict):
                cfg=p["config"]
                for field, choices in {"gold":(250,500,1000),"rounds":(1,3,5,7),"mode":("fixed","mirror","random"),"fog":(True,False),"aspects":(True,False)}.items():
                    value=cfg.get(field)
                    if type(value) is type(choices[0]) and value in choices:room.config[field]=value
                for other in room.players:
                    if other:other.ready=False
            elif kind=="start" and slot==0 and all(other and other.ready for other in room.players):
                return await self.start(room)
            else:return
            return await self.lobby(room)
        if room.phase=="loading" and kind=="loaded":
            player.loaded=True
            if all(p.loaded and not p.absent_until for p in room.players):
                room.phase="playing"
                await self.snapshot(room)
                await self.room_send(room,dict(type="start"))
                await authority("resume",room)
            return
        if room.phase=="finished" and kind in ("return_lobby","next_round","rematch"):
            return await self.post_match(room,slot,kind)
        if room.phase=="playing" and kind=="command" and isinstance(p.get("command"),dict):
            cmd=p["command"]
            result=await authority("input",room,slot=slot,command=cmd)
            if cmd.get("kind")=="sync_production":
                return await self.send(peer,dict(type="production_sync",count=result.get("count",0)))
            if not result.get("ok"):return await self.error(peer,result.get("message","Команда недоступна."))

    async def receiver(self,socket):
        peer=Peer(socket)
        if len(self.peers)>=MAX_PEERS:return await socket.close(1013,"Server busy")
        self.peers.add(peer)
        try:
            binary_count=0
            binary_window=0
            async for raw in socket:
                now=time.monotonic()
                if isinstance(raw,bytes):
                    if now>=binary_window:
                        binary_window=now+1
                        binary_count=0
                    binary_count+=1
                    if binary_count>110:
                        await socket.close(1008,"Too many spectator chunks");break
                    self.spectators.binary(peer,raw)
                    continue
                peer.recent=[t for t in peer.recent if t>now-1]
                if len(peer.recent)>=28:
                    await socket.close(1008,"Too many messages");break
                peer.recent.append(now)
                try:
                    packet=json.loads(raw)
                    if not isinstance(packet,dict):continue
                    async with self.lock:
                        await self.handle(peer,packet)
                except (OSError, ValueError, TypeError) as error:
                    await self.error(peer,"Не удалось выполнить сетевой запрос.")
                    print("packet handler",type(error).__name__,flush=True)
        except ConnectionClosed:
            pass
        finally:
            async with self.lock:
                self.peers.discard(peer)
                await self.spectators.disconnect(peer)
                if peer.token:
                    await self.mark_last_seen(peer)
                try:await self.leave(peer)
                except OSError:print("authority unavailable during disconnect",flush=True)

    async def frames(self):
        while True:
            await asyncio.sleep(.10)
            async with self.lock:
                now=time.monotonic()
                for room in list(self.rooms.values()):
                    try:
                        expired=any(player and 0<player.absent_until<now for player in room.players)
                        if expired or (room.phase in ("loading","finished") and room.deadline<now):
                            await self.close_room(room,"Время ожидания истекло.")
                        elif room.phase=="playing":
                            await self.snapshot(room)
                    except (OSError, ValueError, KeyError) as error:
                        print("authority offline",type(error).__name__,flush=True)
                        try:await self.close_room(room,"Служба матча временно недоступна.")
                        except OSError:pass


async def flush_results():
    while True:
        if PROFILE_SECRET:
            for path in RESULT_DIR.glob("*.json"):
                try:
                    result=json.loads(path.read_bytes())
                    if (await profiles("/result",result)).get("ok"):path.unlink()
                except (OSError,ValueError) as error:
                    print("result queue waiting",type(error).__name__,flush=True)
                    break
        await asyncio.sleep(5)

async def main():
    health=await authority("health")
    gateway=Gateway(health["races"])
    port=int(os.getenv("CF_GATEWAY_PORT","49291"))
    async with serve(gateway.receiver,"127.0.0.1",port,max_size=32768,max_queue=16):
        print(f"CF_UNITY_GATEWAY_READY port={port} races={len(gateway.races)}",flush=True)
        await asyncio.gather(gateway.frames(),flush_results())

if __name__=="__main__":
    asyncio.run(main())
