"""One match = one WebSocket connection: a human in seat 0 plus server-side agents."""
import asyncio
import uuid
from typing import Awaitable, Callable, List

from ..agents import make_agent
from ..engine import apply_action, drop_player, legal_actions, new_game
from ..rules import PASS, RulesConfig
from ..runner import PASS_ACTION, request_action
from .protocol import (parse_action, result_for_action, result_message, state_message, turn_id)

HUMAN_SEAT = 0
DEFAULT_AGENTS = ["planner", "greedy", "random", "greedy", "random"]
AGENT_TIME_BUDGET = 5.0
MAX_AGENT_FAILURES = 5

Send = Callable[[dict], Awaitable[None]]


class MatchSession:
    def __init__(self, send: Send, start: dict, agent_delay: float = 0.35):
        self.send = send
        self.agent_delay = agent_delay

        n = max(2, min(6, int(start.get("player_count", 3))))
        seed = int(start.get("map_seed", 12345))
        cfg = RulesConfig(board_radius=int(start.get("board_radius", 4)),
                          rounds=int(start.get("rounds", 40)))
        self.state = new_game(n, seed, cfg)
        self.match_id = f"m{seed}-{uuid.uuid4().hex[:6]}"

        specs = [a for a in (start.get("agents") or []) if a]
        specs = (specs + DEFAULT_AGENTS)[: n - 1] if len(specs) < n - 1 else specs[: n - 1]
        self.agents = [None] + [make_agent(spec, seed) for spec in specs]
        self.names = ["You"] + [f"{spec.partition(':')[0].title()} {i}" for i, spec in enumerate(specs, 1)]
        self.controllers = ["local-user"] + [a.label for a in self.agents[1:]]
        self.failures = [0] * n
        for seat, agent in enumerate(self.agents):
            if agent:
                agent.reset({"seat": seat, "n_players": n, "seed": seed})

    # ------------------------------------------------------------------ outgoing

    async def push_state(self):
        await self.send(state_message(self.state, self.match_id, self.names, self.controllers, HUMAN_SEAT))

    async def start(self):
        await self.push_state()
        await self.run_agents()

    # ------------------------------------------------------------------ human action

    async def handle_action(self, msg: dict):
        s = self.state
        reject = None
        if s.done:
            reject = "match_over"
        elif (msg.get("turn_id") != turn_id(s)
              or msg.get("expected_state_revision") != turn_id(s)):
            reject = "stale_request"
        elif s.current != HUMAN_SEAT or msg.get("player_id") != HUMAN_SEAT:
            reject = "not_your_turn"
        else:
            action = parse_action(msg, s)
            if action is None or action not in legal_actions(s, HUMAN_SEAT):
                reject = "illegal_action"

        if reject:
            await self.send(result_message(HUMAN_SEAT, int(msg.get("action_type", PASS) or 0), False,
                                           "Rejected: " + reject, reject))
            await self.push_state()          # resync the client with the authoritative state
            return

        await self._apply_and_publish(HUMAN_SEAT, action)
        await self.run_agents()

    # ------------------------------------------------------------------ agents

    async def run_agents(self):
        loop = asyncio.get_running_loop()
        while not self.state.done and self.state.current != HUMAN_SEAT:
            if self.agent_delay:
                await asyncio.sleep(self.agent_delay)
            seat = self.state.current
            # Agents may be CPU-heavy (Planner): keep the event loop responsive.
            action, failure, _, _ = await loop.run_in_executor(
                None, request_action, self.agents[seat], self.state, seat, AGENT_TIME_BUDGET)
            await self._apply_and_publish(seat, action)
            if failure:
                self.failures[seat] += 1
                if self.failures[seat] >= MAX_AGENT_FAILURES and self.state.active[seat]:
                    drop_player(self.state, seat)
                    await self.send(result_message(seat, PASS, True, f"{self.names[seat]} was removed (too many failures)"))
                    await self.push_state()

    async def _apply_and_publish(self, seat: int, action: tuple):
        before = self.state.clone()
        info = apply_action(self.state, action)
        await self.send(result_for_action(before, self.names[seat], action, info, self.names))
        await self.push_state()
