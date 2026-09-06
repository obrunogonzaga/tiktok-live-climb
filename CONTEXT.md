# TikTok LIVE Climb

Glossário do produto. Sem stack, sem arquivos, sem APIs — só a língua do domínio.

## Language

**Round**:
Uma subida completa da torre, do spawn até o topo ou a queda.
_Avoid_: partida, game, sessão (sessão é o LIVE inteiro)

**Sessão**:
O LIVE do início ao “end stream”. Contém vários **Rounds**.
_Avoid_: live (ambíguo com a plataforma)

**Bot**:
O personagem que sobe sozinho. State machine, não LLM.
_Avoid_: IA, agent, NPC (NPC fica para obstáculos animados)

**Gift**:
Item pago que um viewer manda no TikTok LIVE. Unidade de monetização.
_Avoid_: doação, donate, tip

**Ladder**:
Tabela ordenada de **Gifts** nomeados e o efeito de cada um na cena. Editável ao vivo.
_Avoid_: menu de gifts, price list, ranking (ranking é o **Placar**)

**Placar**:
Ordenação de viewers por coins gastos no **Round** ou na **Sessão**.
_Avoid_: leaderboard, ladder (ladder é a tabela de preços/efeitos)

**Evento interno** (`LiveEvent`):
Envelope normalizado que o jogo consome. Nasce no **Bridge**, nunca no TikTok direto.
_Avoid_: webhook, payload do TikTok, gift event (esses são a forma crua)

**Bridge**:
Adaptador que traduz a plataforma (hoje TikFinity) para **Evento interno**.
_Avoid_: TikFinity (é um vendor, não o conceito), API do TikTok

**Orchestrator**:
Worker que recebe o **Evento interno**, aplica a **Ladder**, enfileira TTS e atualiza o **Overlay**.
_Avoid_: backend, server, bot (bot é o personagem)

**Overlay**:
HTML transparente sobre o jogo: **Ladder**, **Placar**, últimos doadores, toast de TTS.
_Avoid_: HUD (HUD é o que o Unity desenha na cena), alerta, widget

**Captura**:
O que o LIVE Studio / OBS manda ao ar: jogo + **Overlay** + áudio (SFX + TTS).
_Avoid_: stream (é o resultado na plataforma), canvas (é o retângulo 9:16)

**Safe zone**:
Área do canvas 9:16 que o chrome do TikTok LIVE não cobre. Onde a **Ladder** e o **Placar** têm que caber.

**Combo**:
Rajada do mesmo **Gift** do mesmo viewer (repeat / streak). Um **Evento interno**, vários repeats.
_Avoid_: spam (spam é volume de gifts distintos baratos)

## Relationships

- Uma **Sessão** contém vários **Rounds**
- Um **Gift** dispara um **Evento interno** via **Bridge**
- O **Orchestrator** lê a **Ladder** para decidir spawn + TTS
- O **Overlay** espelha **Ladder** + **Placar**; o Unity desenha o **Bot** e os obstáculos
- A **Captura** empilha jogo + **Overlay** no canvas 9:16

## Example dialogue

> **Dev:** “O **Overlay** desenha o **Bot**?”
> **Domain:** “Não. O Unity desenha o **Bot**. O **Overlay** só mostra **Ladder**, **Placar** e toasts.”
>
> **Dev:** “Chegou um webhook do TikFinity. O jogo lê isso?”
> **Domain:** “Não. O **Bridge** vira **Evento interno**. O jogo só conhece `gift` / `like` / `follow`.”
>
> **Dev:** “A **Ladder** é o ranking de quem mais doou?”
> **Domain:** “Não. **Ladder** é o cardápio gift → efeito. Quem mais doou é o **Placar**.”

## Flagged ambiguities

- “live” era plataforma, sessão e captura — resolvido: plataforma = TikTok LIVE; tempo no ar = **Sessão**; o que vai ao ar = **Captura**.
- “bot” era personagem e worker — resolvido: personagem = **Bot**; worker = **Orchestrator**.
- “ladder” era cardápio e ranking — resolvido: cardápio = **Ladder**; ranking = **Placar**.
