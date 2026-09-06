# Orchestrator

Node 22 worker for synthetic `LiveEvent` v1 input. It loads `../config/ladder.v1.toml` at startup, exposes the HTTP bridge endpoint, and serves separate Unity and Overlay WebSocket routes.

```bash
cd orchestrator
npm install
npm start
```

- `POST http://127.0.0.1:8765/v1/events`
- `ws://127.0.0.1:8766/game`
- `ws://127.0.0.1:8766/overlay`

`/game` receives only `SpawnObstacle.Small`, `SpawnObstacle.Medium`, or `SpawnSmoke`. Unsupported ladder actions remain counted but do not spawn. `/overlay` receives a schema-valid snapshot immediately and thereafter at most 10 times per second.

```bash
curl -sS -o /dev/null -w '%{http_code}\n' \
  -H 'Content-Type: application/json' \
  --data-binary @../fixtures/events/gift-rose.json \
  http://127.0.0.1:8765/v1/events
```

Run the isolated suite with `npm test`. Tests bind ephemeral ports and do not start the default listener.
