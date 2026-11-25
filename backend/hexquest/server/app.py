"""
Run:  uvicorn hexquest.server.app:app --port 8765
Unity connects to ws://127.0.0.1:8765/ws
"""
import json
import os

from fastapi import FastAPI, WebSocket, WebSocketDisconnect

from ..rules import RULES_VERSION
from .session import MatchSession


def create_app(agent_delay: float = 0.35) -> FastAPI:
    app = FastAPI(title="HexQuest server")

    @app.get("/health")
    async def health():
        return {"ok": True, "rules_version": RULES_VERSION}

    @app.websocket("/ws")
    async def ws_endpoint(websocket: WebSocket):
        await websocket.accept()
        session = None
        try:
            while True:
                try:
                    msg = json.loads(await websocket.receive_text())
                    kind = msg.get("type")
                except (ValueError, AttributeError):
                    await websocket.send_json({"type": "ERROR", "message": "invalid JSON"})
                    continue

                if kind == "START":
                    session = MatchSession(websocket.send_json, msg, agent_delay)
                    await session.start()
                elif kind == "ACTION" and session is not None:
                    await session.handle_action(msg)
                elif kind == "PING":
                    await websocket.send_json({"type": "PONG"})
                else:
                    await websocket.send_json({"type": "ERROR", "message": f"unexpected message '{kind}'"})
        except WebSocketDisconnect:
            pass

    return app


app = create_app(float(os.environ.get("HEXQUEST_AGENT_DELAY", "0.35")))
