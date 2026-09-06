# TikTok LIVE Climb

Live 9:16 em que um **bot sobe sozinho**. Gifts do chat viram obstáculos, empurrões e boss; um TTS agradece o doador. Zero operação durante a live — só ligar o stream.

**Status:** spec v1 · ainda sem código.  
**Owner:** Bruno Gonzaga Santos  
**Repo:** https://github.com/obrunogonzaga/tiktok-live-climb  
**Tracker:** [Issues](https://github.com/obrunogonzaga/tiktok-live-climb/issues) · [Fase 1](https://github.com/obrunogonzaga/tiktok-live-climb/milestone/1)

## Docs

| Doc | Fonte da verdade de |
| --- | --- |
| [ONEPAGER.md](./ONEPAGER.md) | Loop do produto, stack MVP, fases, riscos |
| [docs/gift-ladder.md](./docs/gift-ladder.md) | Monetização: 7 gifts BR + fallback por coins |
| [docs/events.md](./docs/events.md) | Contrato interno `LiveEvent` v1 + mapeamento TikFinity |
| [docs/overlay-capture.md](./docs/overlay-capture.md) | Canvas 9:16, safe zone, camadas LIVE Studio/OBS |
| [CONTEXT.md](./CONTEXT.md) | Glossário do domínio |

Runtime (quando existir código):

- `config/ladder.v1.toml` — ladder editável sem rebuild
- `schemas/live-event.v1.schema.json` — valida o envelope
- `schemas/overlay-state.v1.schema.json` — estado que o overlay HTML consome

## Stack MVP

Unity + C# (jogo/bot) · TikFinity (bridge) · orchestrator local · ElevenLabs (TTS) · TikTok LIVE Studio (captura 9:16, target Windows).

O jogo **nunca** fala com o TikTok. Só consome eventos internos.

## Próximo passo

Scaffold Unity + HTTP/UDP listener que aceita `LiveEvent` v1. Aceite da Fase 1: live teste 15 min sem tocar no teclado.
