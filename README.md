# TikTok LIVE Climb

Live **9:16** em que um **bot sobe sozinho**. O chat paga pra atrapalhar: gift vira obstáculo, fumaça, empurrão ou boss; um TTS agradece o doador. Zero operação durante a sessão — só ligar o stream.

**Status:** MVP local · Maya 3D, torre modular e subida contínua · gifts ainda provisórios
**Owner:** [Bruno Gonzaga Santos](https://github.com/obrunogonzaga)
**Repo:** [obrunogonzaga/tiktok-live-climb](https://github.com/obrunogonzaga/tiktok-live-climb)
**Tracker:** [Issues](https://github.com/obrunogonzaga/tiktok-live-climb/issues) · [Fase 1](https://github.com/obrunogonzaga/tiktok-live-climb/milestone/1)

Formato: lives tipo caos/climb (chat controla). Requisito duro: automação ponta a ponta.

---

## Look (norte visual)

Concept, não frame do Unity. A torre, o céu noturno e a paleta continuam sendo a referência. Na #15, Bruno escolheu **Maya Prado** para substituir o robô; os retratos da persona orientam o avatar. A experiência visual final será revisada na [#17](https://github.com/obrunogonzaga/tiktok-live-climb/issues/17).

<p align="center">
  <img src="docs/refs/gameplay.png" width="280" alt="Concept da torre: bot subindo, gifts virando caos" />
  <img src="docs/refs/viewer.png" width="280" alt="Como o viewer vê no TikTok LIVE: jogo + overlay + chrome do app" />
</p>

<p align="center">
  <em>Esquerda — o jogo. Direita — a live no celular (ladder, toast, likes, comentários).</em>
</p>

O **Bot** vive no centro-esquerda da **safe zone**. A **Ladder** fica à esquerda. Likes/gifts do TikTok comem a direita; comentários comem o baixo. HUD do overlay é HTML, não Unity. Spec: [docs/overlay-capture.md](./docs/overlay-capture.md).

---

## O que o viewer vê

1. **Round** começa → o **Bot** sobe a torre.
2. Alguém manda **Gift** → obstáculo / empurrão / boss + voz “Valeu, {nome}!”.
3. O bot cai ou chega no topo → **Placar** do top gifter → reset sozinho.
4. Overlay mostra a **Ladder** o tempo todo.

Monetização = espetáculo da rosa barata + FOMO do degrau caro. Glossário: [CONTEXT.md](./CONTEXT.md) (`Ladder` ≠ `Placar`, `Bot` ≠ `Orchestrator`).

---

## Arquitetura

O jogo **nunca** fala com o TikTok. Só consome `LiveEvent` v1. Trocar TikFinity = só o **Bridge**.

```
TikTok LIVE
    │  gifts / likes / follows
    ▼
[Bridge]          TikFinity (MVP) → LiveEvent v1
    ▼
[Orchestrator]    fila, ladder, debounce TTS
    ├──► Unity          spawn / damage / reset do Bot
    ├──► TTS            ElevenLabs → wav → Virtual Cable
    └──► Overlay HTML   ladder / placar / toast
    ▼
Captura           LIVE Studio 1080×1920 → ar
```

Contrato: [docs/events.md](./docs/events.md) + [`schemas/live-event.v1.schema.json`](./schemas/live-event.v1.schema.json).

---

## Ladder v1 (BR)

Sete degraus no overlay. Coins, não reais. Gift fora da lista cai no **fallback por coins**, não no chão. Fonte: [docs/gift-ladder.md](./docs/gift-ladder.md) · runtime [`config/ladder.v1.toml`](./config/ladder.v1.toml).

| Gift (overlay) | coins | No jogo |
| --- | ---: | --- |
| Rosa | 1 | obstáculo pequeno |
| Coração | 5 | small + knockback |
| Perfume | 20 | fumaça / slow |
| Confete | 100 | obstáculo médio |
| Galáxia | 1_000 | knockback forte |
| Carro | 7_000 | multi-spawn |
| Leão | 29_999 | boss + highlight |

TTS: no máximo 1 fala / 2,5 s. O resto vira toast. Like não fala.

---

## Stack MVP

| Camada | Escolha |
| --- | --- |
| Jogo + **Bot** | Unity 6 + C# · URP · 1080×1920 Windowed · Windows na live |
| Eventos | TikFinity → **Bridge** → `LiveEvent` |
| Fila / TTS / overlay state | orchestrator local (HTTP + WebSocket) |
| Voz | ElevenLabs + fallback TTS local |
| Captura | TikTok LIVE Studio (OBS só se o Studio falhar) |
| Arte | Blender · Maya com LOD, torre modular e materiais URP |

**Bot** = state machine `Idle → Climb → Avoid → Recover → Celebrate → Reset`. LLM fora do gameplay.

---

## Repo

```
.
├── ONEPAGER.md              produto, fases, riscos
├── CONTEXT.md               glossário
├── AGENTS.md                o que um agente lê antes de codar
├── config/ladder.v1.toml    ladder editável sem rebuild
├── docs/
│   ├── gift-ladder.md
│   ├── events.md
│   ├── overlay-capture.md
│   └── refs/                concepts 9:16 (este README)
└── schemas/
    ├── live-event.v1.schema.json
    └── overlay-state.v1.schema.json
```

O projeto Unity está em `game/`, o Orchestrator Node 22 em `orchestrator/` e o Overlay em `overlay/`.
A direção visual e a subida contínua foram aprovadas para implementação por Bruno; veja [o comportamento atual e as evidências](docs/continuous-climb.md).

---

## Docs

| Doc | Fonte da verdade de |
| --- | --- |
| [ONEPAGER.md](./ONEPAGER.md) | Loop, stack, fases, aceite, riscos |
| [CONTEXT.md](./CONTEXT.md) | Língua do domínio |
| [docs/gift-ladder.md](./docs/gift-ladder.md) | 7 gifts, fallback, TTS |
| [docs/events.md](./docs/events.md) | Envelope `LiveEvent` v1, mapa TikFinity |
| [docs/overlay-capture.md](./docs/overlay-capture.md) | Canvas, safe zone, Studio/OBS, áudio |
| [AGENTS.md](./AGENTS.md) | Ordem de leitura pra implementar |

---

## Fase 1 — issues

Cada issue AFK é um **prompt** (Before / Build / Out of scope / Validate). Locks globais em [AGENTS.md](./AGENTS.md). Fixtures em `fixtures/events/`.

| # | O quê | Tipo |
| --- | --- | --- |
| [#1](https://github.com/obrunogonzaga/tiktok-live-climb/issues/1) | Style bible da pegada | HITL |
| [#2](https://github.com/obrunogonzaga/tiktok-live-climb/issues/2) | Torre greybox + bot sobe / reseta | AFK |
| [#3](https://github.com/obrunogonzaga/tiktok-live-climb/issues/3) | `LiveEvent` synthetic → 3 spawns | AFK |
| [#4](https://github.com/obrunogonzaga/tiktok-live-climb/issues/4) | Overlay HTML 9:16 | AFK |
| [#5](https://github.com/obrunogonzaga/tiktok-live-climb/issues/5) | TTS 1 voz + fila 2,5 s | AFK |
| [#6](https://github.com/obrunogonzaga/tiktok-live-climb/issues/6) | Captura LIVE Studio 15 min | HITL |
| [#7](https://github.com/obrunogonzaga/tiktok-live-climb/issues/7) | Art pass Blender (bot + kit + 3 gifts) | AFK + review |
| [#8](https://github.com/obrunogonzaga/tiktok-live-climb/issues/8) | TikFinity real → `LiveEvent` | HITL |
| [#9](https://github.com/obrunogonzaga/tiktok-live-climb/issues/9) | Ladder completa + Leão + placar | AFK |

`HITL` = precisa do Bruno (conta, Studio, OK visual). `AFK` = dá pra implementar sem ele no loop. As #2–#4 e a composição #14 já foram entregues; #15 integra a Maya e o kit. #16 cobre os gifts finais e #17 o aceite visual completo.

Mãos livres (quando a Fase 1 fechar): bot joga sozinho, gift spawna sozinho, TTS sozinho, round reseta sozinho, overlay sempre visível, fila TTS aguenta raid, **Bridge** reconecta, log CSV pós-live.

Manual aceitável: 1× Go Live + olhar ToS. Não aceitável: reagir a gift na mão ou pilotar o **Bot**.

---

## Arte

1. Greybox (cápsula, cubos) até o climb funcionar.
2. Style bible ([#1](https://github.com/obrunogonzaga/tiktok-live-climb/issues/1)) a partir destes refs.
3. Blender: **Maya** + 12 módulos de torre. Fontes, FBX, texturas e materiais no Git; `*.blend1` ignorado. Props/VFX finais dos gifts pertencem à #16.
4. Unity: FBX + VFX. Gifts = prop + partícula, não cutscene.

Meshy só se um prop travar. Hero (**Bot**) não.

---

## Rodar

Abra `game/Assets/Scenes/Climb.unity` no Unity **6000.6.0f1**, Game **1080×1920**, e clique Play sem teclado.
O Bot sobe por uma hélice; 32 plataformas e quatro trechos de torre são reciclados.
Queda definitiva reinicia o round e preserva o recorde durante a execução.

Com Node 22, na raiz do repo:

```sh
npm --prefix orchestrator ci
npm --prefix orchestrator start
```

Overlay: `http://127.0.0.1:8790/`; eventos: `POST http://127.0.0.1:8765/v1/events`.
[Instruções e validações](game/README.md) · [Maya e kit](docs/issue15-maya.md) · [Vídeo real](docs/evidence/issue15/final/playthrough.mp4).

![Maya na subida contínua — capturas reais do Unity](docs/evidence/issue15/final/mobile-journey.png)

As três actions sintéticas existentes estão integradas; novas ajudas, arte final dos gifts,
TikFinity real, TTS e transmissão ao vivo continuam fora desta entrega.

Secrets (ElevenLabs, etc.) ficam em `.env` local — nunca no git.

---

## Contribuir

Issue tracker é o GitHub deste repo. Spec primeiro, código depois. Agentes: ler [AGENTS.md](./AGENTS.md) antes de tocar em qualquer coisa.
