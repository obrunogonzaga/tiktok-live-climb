# Agents

Spec-only repo. Não há código ainda. Ler nesta ordem antes de implementar:

1. [CONTEXT.md](./CONTEXT.md) — língua do domínio
2. [ONEPAGER.md](./ONEPAGER.md) — produto, stack, fases, aceite
3. A fonte da verdade da área que você vai tocar:

| Se for mexer em | Ler |
| --- | --- |
| Gifts, spawn, TTS por gift, config | [docs/gift-ladder.md](./docs/gift-ladder.md) + `config/ladder.v1.toml` |
| Bridge, webhook, fila, Unity listener | [docs/events.md](./docs/events.md) + `schemas/live-event.v1.schema.json` |
| HTML overlay, LIVE Studio, OBS, áudio | [docs/overlay-capture.md](./docs/overlay-capture.md) + `schemas/overlay-state.v1.schema.json` |

Regras que já estão decididas:

- O jogo nunca fala com o TikTok. Só consome `LiveEvent` v1.
- O **Bot** é state machine. LLM fora do loop de gameplay.
- Ladder e overlay state são JSON/TOML editáveis sem rebuild.
- Aceite da Fase 1: 15 min de live teste sem teclado.
