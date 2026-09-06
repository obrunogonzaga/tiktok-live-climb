# Agents

Issues da Fase 1 **são o prompt**. Executar só aquele issue. Não expandir. Não reabrir decisão desta página.

Ler nesta ordem:

1. Este arquivo (locks)
2. [CONTEXT.md](./CONTEXT.md)
3. O body do issue (passos + out of scope + validate)
4. Os docs que o issue apontar

## Locks (não renegociar)

| Item | Valor |
| --- | --- |
| Unity | **6 (6000) URP 3D**, pasta `game/` |
| Resolução | 1080×1920, **Windowed** (nunca exclusive fullscreen) |
| Bot | CharacterController + waypoints pra cima. State machine. Sem LLM |
| Orchestrator | Node 22 em `orchestrator/` |
| Eventos | só HTTP `POST http://127.0.0.1:8765/v1/events` — sem UDP |
| WebSocket | `ws://127.0.0.1:8766/game` e `/overlay` |
| Overlay HTTP | `http://127.0.0.1:8790/` arquivos em `overlay/` |
| HUD | só HTML. Unity não desenha ladder/placar/toast |
| Arte | Blender em `art/`. Greybox até o issue #7 |
| Fixtures | `fixtures/events/*.json` |

## Docs por área

| Se for mexer em | Ler |
| --- | --- |
| Gifts, spawn, TTS, config | [docs/gift-ladder.md](./docs/gift-ladder.md) + `config/ladder.v1.toml` |
| Bridge, fila, Unity listener | [docs/events.md](./docs/events.md) + `schemas/live-event.v1.schema.json` |
| Overlay, Studio, áudio | [docs/overlay-capture.md](./docs/overlay-capture.md) + `schemas/overlay-state.v1.schema.json` |

## Done

Issue fechado só quando **Validate** do body passou e o out of scope ficou intocado.
