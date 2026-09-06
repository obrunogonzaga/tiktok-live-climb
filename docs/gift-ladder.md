# Ladder v1 — gifts BR

Fonte da verdade do cardápio. Runtime: [`config/ladder.v1.toml`](../config/ladder.v1.toml).

**Por que 7, não 40:** o overlay tem que caber na **safe zone** esquerda. Viewer médio no BR manda Rosa; o FOMO mora em 2–3 degraus visíveis, não num menu.

Coins são a unidade. BRL flutua com o pacote de coins e com iOS vs web — não colocar reais no overlay.

Catálogo TikTok muda. Nomes abaixo são os **inglês da API** (`giftName`). Label do overlay é PT-BR. Gift fora da lista cai no **fallback por coins**, não no chão.

## Degraus nomeados

| # | slug | `giftName` TikTok | coins | Por que este | Ação no jogo | TTS (`{nome}` = nickname) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `rose` | Rose | 1 | Volume. Todo mundo tem. | `SpawnObstacle.Small` | Valeu, {nome}! |
| 2 | `finger_heart` | Finger Heart | 5 | Primeiro “paguei alguma coisa” | Small + knockback leve | {nome} mandou! |
| 3 | `perfume` | Perfume | 20 | Barato, animação visível | Zona de fumaça / slow | Cheiro de caos, {nome}! |
| 4 | `confetti` | Confetti | 100 | Primeiro real de verdade (~R$ 8–12) | `SpawnObstacle.Medium` | {nome} bagunçou a torre! |
| 5 | `galaxy` | Galaxy | 1_000 | Flex da noite pra 99% do chat | Knockback forte + shake | Galáxia do {nome}! |
| 6 | `sports_car` | Sports Car | 7_000 | Caro o bastante pra parar o chat | Multi-spawn + vento | {nome} atropelou a live! |
| 7 | `lion` | Lion | 29_999 | Whale. 1× por sessão, se vier | `SpawnBoss` + highlight de round | Leão! Obrigado, {nome}! |

Aliases 1-coin (`GG`, `TikTok`, `Ice Cream Cone`, `Heart`, `Thumbs Up`) estão no mesmo `slug = rose` (`gift_names` no TOML). Não ganham linha extra no overlay. Gift 1-coin fora dessa lista cai no fallback `1–4`.

Fora do MVP de propósito: Universe (raro demais pra testar), gifts sazonais, likes como degrau de ladder (like é empurrão, ver eventos).

## Fallback por coins

O **Orchestrator** resolve nesta ordem:

1. `giftName` (ou `giftId`) casa com um degrau nomeado → usa esse degrau.
2. Senão, `coins * repeat` cai no bucket:

| coins (total do evento) | Ação | TTS |
| --- | --- | --- |
| 1–4 | `SpawnObstacle.Small` | Valeu, {nome}! |
| 5–19 | Small + knockback leve | {nome} mandou! |
| 20–99 | Fumaça / slow | {nome} complicou! |
| 100–499 | `SpawnObstacle.Medium` | {nome} bagunçou a torre! |
| 500–1_999 | Knockback forte | {nome} pesou! |
| 2_000–9_999 | Multi-spawn | {nome} bagunçou geral! |
| 10_000+ | `SpawnBoss` | {nome} mandou o caos! |

`repeat` conta no total (`coins * repeat`). Combo do mesmo gift = um spawn escalado, não 20 Small empilhados no mesmo frame. Cap: 8 obstáculos novos por segundo; o resto vira só toast no overlay.

## TTS

- Máximo 1 fala / 2,5 s. Gift que perde a vez: toast visual, sem voz.
- Cache 10 min por `(templateId, nickname)`.
- Like burst: sem voz. Follow: “Bem-vindo, {nome}” com throttle 1 / 15 s.

## Overlay

Os 7 degraus nomeados aparecem o tempo todo (ver [overlay-capture.md](./overlay-capture.md)). Contador = quantidade na **Sessão**, não no round. Placar de top gifter é outra superfície.

## Como editar

1. Mudar `config/ladder.v1.toml`.
2. Orchestrator recarrega no save (watch) ou no `SIGHUP` — sem rebuild Unity.
3. Gift novo da TikTok: ou alias no TOML, ou deixa o fallback de coins.

Confirmar na app BR (conta de teste) os `giftName` exatos antes da primeira live. Se o TikTok localizar (ex. “Rosa”), adicionar alias, não duplicar degrau.
