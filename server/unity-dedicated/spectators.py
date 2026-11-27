"""Live, friends-only viewing of client-simulated challenges."""

import asyncio
from dataclasses import dataclass, field
import re
import time

from websockets.exceptions import ConnectionClosed


CHUNK_BYTES = 30 * 1024
FRAME_BYTES = 512 * 1024
MAX_VIEWERS = 4


@dataclass(eq=False)
class Stream:
    host: object
    notice: dict
    viewers: dict = field(default_factory=dict)
    partial: bytearray | None = None
    next_frame: float = 0


@dataclass(eq=False)
class Viewer:
    peer: object
    stream: Stream
    queue: asyncio.Queue = field(default_factory=lambda: asyncio.Queue(maxsize=1))
    sender: asyncio.Task | None = None


class Spectators:
    def __init__(self, send, profiles):
        self.send = send
        self.profiles = profiles
        self.streams = {}
        self.viewers = {}
        self.join_after = {}

    def available(self, peer):
        return peer in self.streams and len(self.streams[peer].viewers) < MAX_VIEWERS

    async def command(self, peer, packet, peers):
        kind = packet["type"]
        if kind == "spectate_close":
            winner = packet.get("result")
            reason = "Друг победил" if winner == 0 else "Друг проиграл" if winner == 1 else "Просмотр завершён"
            await self.close(peer, reason)
        elif kind == "spectate_leave":
            if peer in self.viewers:
                self.join_after.pop(peer, None)
            await self.leave(peer)
        elif kind == "spectate_open":
            await self.open(peer, packet)
        elif kind == "spectate_join":
            await self.join(peer, packet, peers)

    async def open(self, peer, packet):
        if not peer.token or peer.room or peer in self.viewers:
            return
        match = packet.get("match")
        version = packet.get("version")
        challenge = packet.get("challenge")
        races = packet.get("races")
        draws = packet.get("ultimate")
        if not (isinstance(match, str) and re.fullmatch("[a-f0-9]{32}", match)
                and isinstance(version, str) and re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version)
                and isinstance(challenge, str) and re.fullmatch("[A-Za-z0-9_-]{1,48}", challenge)
                and isinstance(races, list) and len(races) == 2
                and all(type(race) is int and 0 <= race < 18 for race in races)
                and isinstance(draws, list) and len(draws) == 2
                and all(isinstance(draw, list) and len(draw) <= 32
                        and all(isinstance(code, str) and re.fullmatch("[A-Za-z0-9]{4}", code)
                                for code in draw) for draw in draws)):
            return
        previous = self.streams.get(peer)
        if previous and previous.notice["match"] == match:
            return
        await self.close(peer, "Игрок начал новое испытание")
        notice = dict(type="spectate_start", match=match, host=peer.profile.get("name", ""),
                      version=version, challenge=challenge, races=races, ultimate=draws)
        self.streams[peer] = Stream(peer, notice)

    async def join(self, peer, packet, peers):
        now = time.monotonic()
        if now < self.join_after.get(peer, 0):
            return await self.send(peer, dict(type="spectate_error", message="Повторите попытку чуть позже."))
        self.join_after[peer] = now + 1.5
        target = packet.get("peer")
        if not peer.token or peer.room or peer in self.streams or peer in self.viewers or not isinstance(target, str):
            return await self.send(peer, dict(type="spectate_error", message="Просмотр доступен из меню."))
        host = next((host for host in self.streams if host in peers
                     and host.profile.get("id") == target and self.available(host)), None)
        if host is None or host == peer:
            return await self.send(peer, dict(type="spectate_error", message="Сейчас просмотр недоступен."))
        try:
            relationship = await self.profiles("/social", dict(token=peer.token, action="player", peer=target))
        except (ValueError, OSError):
            relationship = {}
        if relationship.get("ok") is not True or relationship.get("friend") is not True:
            return await self.send(peer, dict(type="spectate_error", message="Просмотр доступен только друзьям."))
        stream = self.streams[host]
        if packet.get("version") != stream.notice["version"]:
            return await self.send(peer, dict(type="spectate_error", message="Для просмотра нужна та же версия игры."))
        viewer = Viewer(peer, stream)
        self.viewers[peer] = viewer
        stream.viewers[peer] = viewer
        await self.send(peer, stream.notice)
        viewer.sender = asyncio.create_task(self.forward(viewer))
        await self.count(stream)

    async def count(self, stream):
        await self.send(stream.host, dict(type="spectate_count", match=stream.notice["match"],
                                         viewers=len(stream.viewers)))

    async def forward(self, viewer):
        try:
            while True:
                frame = await viewer.queue.get()
                await asyncio.wait_for(viewer.peer.socket.send(frame), timeout=2)
        except (ConnectionClosed, TimeoutError):
            await viewer.peer.socket.close(1013, "Spectator connection too slow")

    def binary(self, peer, data):
        stream = self.streams.get(peer)
        if not stream or not stream.viewers or not 1 < len(data) <= CHUNK_BYTES:
            return
        flags = data[0]
        if flags > 3:
            stream.partial = None
            return
        if flags & 1:
            now = time.monotonic()
            stream.partial = bytearray() if now >= stream.next_frame else None
            if stream.partial is not None:
                stream.next_frame = now + .12
        if stream.partial is None:
            return
        if len(stream.partial) + len(data) - 1 > FRAME_BYTES:
            stream.partial = None
            return
        stream.partial.extend(memoryview(data)[1:])
        if flags & 2:
            complete = bytes(stream.partial)
            stream.partial = None
            if not complete.startswith(b"\x1f\x8b"):
                return
            for viewer in stream.viewers.values():
                if viewer.queue.full():
                    viewer.queue.get_nowait()
                viewer.queue.put_nowait(complete)

    async def leave(self, peer, reason=None, update_host=True):
        viewer = self.viewers.pop(peer, None)
        if viewer is None:
            return
        stream = viewer.stream
        stream.viewers.pop(peer, None)
        if viewer.sender:
            viewer.sender.cancel()
        if reason:
            await self.send(peer, dict(type="spectate_end", match=stream.notice["match"], message=reason))
        if update_host:
            await self.count(stream)

    async def close(self, host, reason):
        stream = self.streams.pop(host, None)
        if stream:
            await asyncio.gather(*(self.leave(peer, reason, update_host=False)
                                   for peer in tuple(stream.viewers)))
            await self.count(stream)

    async def disconnect(self, peer):
        self.join_after.pop(peer, None)
        await self.leave(peer)
        await self.close(peer, "Игрок отключился")

    async def reconcile_friends(self, peer, friends):
        if not isinstance(friends, list):
            return
        current = {row.get("id") for row in friends if isinstance(row, dict)}
        viewer = self.viewers.get(peer)
        if viewer and viewer.stream.host.profile.get("id") not in current:
            await self.leave(peer, "Доступ к просмотру закрыт.")
        stream = self.streams.get(peer)
        if stream:
            for viewer in tuple(stream.viewers.values()):
                if viewer.peer.profile.get("id") not in current:
                    await self.leave(viewer.peer, "Доступ к просмотру закрыт.")

    async def friendship_removed(self, first, second):
        for viewer in tuple(self.viewers.values()):
            identities = {viewer.peer.profile.get("id"), viewer.stream.host.profile.get("id")}
            if identities == {first, second}:
                await self.leave(viewer.peer, "Доступ к просмотру закрыт.")
