# One-pager — TikTok LIVE Climb (automatizado)

**Status:** spec v1 · 2026-09-05  
**Owner:** Bruno Gonzaga Santos  
**Objetivo:** live 9:16 em que um bot sobe sozinho; gifts do chat spawnam obstáculos/efeitos; áudio agradece o doador. Zero operação manual durante a live (só ligar stream).

Contratos (não duplicar aqui): [ladder](./docs/gift-ladder.md) · [eventos](./docs/events.md) · [overlay/captura](./docs/overlay-capture.md) · [glossário](./CONTEXT.md)

**Referência de formato:** lives tipo @darkbosstv (caos / climb / chat controla).  
**Requisito duro:** automação ponta a ponta.

---

## 1. Loop do produto (60s mental)

1. Round começa → bot sobe a torre/pista.
2. Chat manda gift → obstáculo / empurrão / buff na cena + TTS “obrigado, {nome}”.
3. Bot cai ou chega no topo → placar / highlight do top gifter → reset automático do round.
4. Overlay fixo mostra ladder de gifts o tempo todo.

Monetização = espetáculo imediato do gift barato + FOMO do gift caro.

---

## 2. Arquitetura (desacoplada)

```
TikTok LIVE
    │  gifts / likes / follows / chat
    ▼
[Event bridge]  ← TikFinity (MVP) | depois adapter próprio
    │  JSON normalizado
    ▼
[Orchestrator]  ← Node ou C# worker
    ├──► Game (Unity)     spawn / damage / reset
    ├──► TTS worker       ElevenLabs → wav → play
    └──► Overlay state    ladder / last donors / goals
    ▼
Captura (TikTok LIVE Studio ou OBS) → ar
```

**Princípio:** o jogo **nunca** fala com o TikTok direto. Só consome `LiveEvent` v1 (`gift`, `like`, `follow`, …). Trocar TikFinity por outro bridge = só o adapter.

---

## 3. Stack proposto (MVP)

| Camada | Escolha | Por quê |
|--------|---------|---------|
| Jogo + bot | **Unity + C#** | física 2D/3D rápida, build Windows (bridges TikTok são melhores aí), navmesh/state machine trivial |
| Eventos LIVE | **TikFinity** (começar) | gift→webhook/ação sem reinventar ToS/API |
| Orquestração | TikFinity actions **ou** mini-serviço local (HTTP/WebSocket) | fila, debounce, rate limit TTS |
| Voz | **ElevenLabs** + fallback TTS local (macOS/Windows) | qualidade; fallback se cota/latência |
| Stream | TikTok LIVE Studio (ou OBS → stream key) | canvas 9:16, Game Capture + Browser overlay |
| Host | **Windows** pra live (Mac ok pra dev do Unity) | compat bridge |

---

## 4. Contrato de eventos (interno)

Envelope `LiveEvent` v1 + schema JSON: [docs/events.md](./docs/events.md).

Tipos MVP: `gift`, `like`, `follow`, `live.connected`, `live.disconnected`, `error`.  
`share` / `comment` existem no schema; gameplay ignora no MVP.

Ladder nomeada (7 gifts) + fallback por coins: [docs/gift-ladder.md](./docs/gift-ladder.md), runtime em `config/ladder.v1.toml`.

Like burst → empurrão, sem TTS. Follow → “Bem-vindo, {nome}” (throttle 15 s).

---

## 5. Bot (IA jogando = state machine, não LLM)

Estados:

`Idle → Climb → Avoid → Recover → Celebrate → Reset`

- **Climb:** move para cima / waypoint.
- **Avoid:** raycast/collider → desvia ou pula.
- **Recover:** após hit, i-frames curtos, retoma.
- **Celebrate / Reset:** topo ou queda → UI → novo round.

LLM **fora** do loop de gameplay. Opcional depois: narrador (“nossa, 3 rosas seguidas”) via fila de fala, não via controle do personagem.

---

## 6. TTS (ElevenLabs)

1. Evento gift → job `{ nome, templateId, priority }`.
2. Fila com **debounce** (max 1 fala / 2–3 s; extras viram só toast visual).
3. Cache: “Valeu, {nome}!” por nick recente (TTL 10 min) pra não regerar.
4. Player: arquivo WAV → Virtual Cable / input do LIVE Studio, ou sound alert do TikFinity.
5. Fallback: TTS local se ElevenLabs falhar/latência > 2,5 s.

---

## 7. Automação — checklist “mãos livres”

- [ ] Bot joga sem input humano
- [ ] Gift → obstáculo sem tecla
- [ ] Gift → áudio agradecimento sem tecla
- [ ] Round reset sozinho
- [ ] Overlay ladder sempre visível
- [ ] Fila TTS não explode com raid
- [ ] Reconnect do bridge se a live cair/subir de novo
- [ ] Log local de gifts (CSV) pra pós-live

**Manual aceitável:** 1× “Go Live” + monitorar ban/ToS.  
**Não aceitável:** reagir a gift na mão, pilotar o personagem, ler chat pra spawnar.

---

## 8. Fases

### Fase 0 — Paper (agora)
- [x] Este one-pager
- [x] Ladder de gifts v1 (7 gifts BR + fallback por coins)
- [x] Schema `LiveEvent` v1
- [x] Spec overlay/captura 9:16

### Fase 1 — Vertical slice (1–2 semanas)
- Cena Unity: torre + bot sobe + 3 obstáculos
- TikFinity: 3 gifts → 3 spawns (pode ser webhook → Unity `HttpListener` / UDP)
- TTS: 1 voz ElevenLabs + fila mínima
- LIVE Studio: captura + overlay HTML estático

**Aceite:** live teste 15 min sem tocar no teclado (só observar).

### Fase 2 — Monetização
- Ladder completa + overlay dinâmico
- Top gifters round / sessão
- Fallback TTS + métricas (gifts/min)

### Fase 3 — Produto
- Adapter próprio (sair do TikFinity se fizer sentido)
- Skins / temas
- Multi-cena (climb + arena avatar) com mesmo orquestrador

---

## 9. Riscos

| Risco | Mitigação |
|-------|-----------|
| ToS / bridge não oficial | conta secundária pra teste; não prometer 24/7 no dia 1 |
| Latência TTS | fila + cache + fallback local |
| Spam de gift barato | stack de obstáculos + debounce voz |
| Bot “burro” | dificuldade baixa no MVP; obstáculo divertido > bot perfeito |
| Windows vs Mac | build Windows como target de live |

---

## 10. Decisão

- Formato: **climb/caos automatizado**
- Stack MVP: **Unity + C# + TikFinity + ElevenLabs**
- Eventos **desacoplados** do TikTok
- Automação = requisito de aceite da Fase 1

---

## 11. Próximo passo

1. Confirmar na app BR (conta teste) os `giftName` da ladder — aliases se o TikTok localizar.
2. Scaffold Unity + listener `LiveEvent` v1 (`POST /v1/events` ou UDP).
3. Overlay HTML no canvas 1080×1920 + LIVE Studio (checklist em [overlay-capture.md](./docs/overlay-capture.md)).
