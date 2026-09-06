# Eventos internos v1

Fonte da verdade do contrato. Schema: [`schemas/live-event.v1.schema.json`](../schemas/live-event.v1.schema.json).

O **jogo nunca** lê TikFinity, TikTok, nem placeholder `%giftName%`. Só consome `LiveEvent`. Trocar o **Bridge** = só o adapter.

## Regras de versão

- Campo `schemaVersion` é inteiro. Agora: `1`.
- Campos novos opcionais **não** bumpam versão.
- Remover / renomear / mudar tipo / mudar significado de `type` → `schemaVersion: 2` e adapter paralelo até o Unity migrar.
- Evento inválido (schema) → `type: "error"` internamente; **não** chega no Unity como gift fantasma.

## Envelope

```json
{
  "schemaVersion": 1,
  "id": "01J6Z3K4N5EXAMPLE000000000",
  "type": "gift",
  "occurredAt": "2026-09-05T22:00:00.120Z",
  "receivedAt": "2026-09-05T22:00:00.180Z",
  "source": {
    "bridge": "tikfinity",
    "raw": { "giftName": "Rose", "coins": "1" }
  },
  "user": {
    "id": "123456789",
    "handle": "ana",
    "nickname": "Ana",
    "avatarUrl": "https://..."
  },
  "payload": {}
}
```

| Campo | Obrigatório | Notas |
| --- | --- | --- |
| `schemaVersion` | sim | `1` |
| `id` | sim | ULID (preferido) ou UUID. Idempotência no orchestrator. |
| `type` | sim | ver união abaixo |
| `occurredAt` | sim | RFC 3339 UTC — quando o viewer agiu |
| `receivedAt` | sim | quando o **Bridge** emitiu |
| `source.bridge` | sim | `tikfinity` \| `native` \| `replay` \| `synthetic` |
| `source.raw` | não | payload cru, só log/debug. Jogo ignora. |
| `user` | quase sempre | obrigatório em `gift`, `like`, `follow`, `share`, `comment`. Ausente em `live.*` e `error` se o bridge cair. |
| `payload` | sim | objeto; shape depende de `type` |

`user.id` é o userId numérico do TikTok **como string** (não cabe em int32). `handle` sem `@`.

## Tipos

| `type` | MVP | `payload` |
| --- | --- | --- |
| `gift` | sim | gift |
| `like` | sim | like |
| `follow` | sim | `{}` |
| `share` | opcional | `{}` |
| `comment` | opcional | comment |
| `live.connected` | sim | live |
| `live.disconnected` | sim | live |
| `error` | sim | error |

### `gift`

```json
{
  "giftId": "5655",
  "giftName": "Rose",
  "slug": "rose",
  "coins": 1,
  "repeat": 1,
  "comboId": "c-ana-5655-1725",
  "comboFinished": true
}
```

| Campo | Tipo | Notas |
| --- | --- | --- |
| `giftId` | string | ID TikTok. String, sempre. |
| `giftName` | string | Nome da API. Lookup primário da ladder. |
| `slug` | string \| omitido | Preenchido pelo orchestrator **depois** da ladder. Bridge pode omitir. |
| `coins` | integer ≥ 0 | Coins **unitários** do gift, não o total. Total = `coins * repeat`. |
| `repeat` | integer ≥ 1 | Streak. Default 1. |
| `comboId` | string \| omitido | Mesmo combo enquanto o viewer segura o gift. |
| `comboFinished` | boolean | `true` no último tick do combo. Unity spawna **só** quando `true` (ou quando não há combo). Ticks intermediários atualizam overlay. |

### `like`

```json
{ "count": 15, "userTotal": 420 }
```

`count` = este burst. `userTotal` opcional. Orchestrator agrega: N likes num intervalo → um `LikeBurst` pro jogo, sem TTS.

### `comment`

```json
{ "text": "sobe", "command": null }
```

MVP: ignorar pra gameplay. Guardar no CSV. `command` só existe se o orchestrator parsear `!x`; senão `null`.

### `live.connected` / `live.disconnected`

```json
{ "roomId": "7123...", "reason": "socket_close" }
```

`reason` só em disconnect. Orchestrator: disconnect → pausa spawns, fila TTS flush visual, tenta reconnect do bridge. Unity **não** reseta o round só por disconnect.

### `error`

```json
{
  "code": "bridge_timeout",
  "message": "TikFinity webhook 5s sem ACK",
  "retryable": true
}
```

Nunca vira obstáculo. Log + toast discreto no overlay se `retryable: false`.

## TikFinity → `LiveEvent`

TikFinity não tem schema estável. O adapter **POST** esperado (Action = Trigger WebHook, body JSON):

```json
{
  "event": "gift",
  "userId": "%userId%",
  "username": "%username%",
  "nickname": "%nickname%",
  "avatarUrl": "%profilePicturUrl%",
  "giftId": "%giftId%",
  "giftName": "%giftName%",
  "coins": "%coins%",
  "repeatCount": "%repeatCount%",
  "likeCount": "%likeCount%",
  "totalLikeCount": "%totalLikeCount%",
  "comment": "%commandParams%"
}
```

Placeholders chegam **string**. Coercer: `coins`, `repeatCount`, `likeCount` → int; vazio / literal `%coins%` → evento `error` (`code: "bridge_unexpanded"`).

Mapa `event` (o que o TikFinity mandar no campo, ou a action name):

| Entrada TikFinity | `type` |
| --- | --- |
| gift / Gift | `gift` |
| like / Like | `like` |
| follow / Follow | `follow` |
| share / Share | `share` |
| comment / chat / Chat | `comment` |
| (socket up) | `live.connected` |
| (socket down) | `live.disconnected` |

`source.raw` = body original. Não validar o cru além de “é objeto”. Validar só o `LiveEvent` emitido.

Endpoint MVP do orchestrator: `POST /v1/events` (TikFinity chama via ngrok / Cloudflare Tunnel / LAN se o TikFinity rodar na mesma máquina). Alternativa: UDP `127.0.0.1:8765` JSON uma linha — Unity `HttpListener` ou o worker, não os dois.

Idempotência: chave `source.bridge + giftId + user.id + occurredAt + repeat` se o TikFinity não mandar id. Se o mesmo combo tick chegar 2×, o segundo `id` novo ainda bate a chave e dropa.

## O que o Unity recebe

Subset, via WebSocket `ws://127.0.0.1:8766/game` ou UDP:

- `gift` com `comboFinished: true` (já com `slug` e ação resolvida — ver campo extra `action` abaixo)
- `like` já agregado (`count` = burst)
- `follow` throttled
- `round.reset` **não** é evento TikTok: o Unity emite quando o **Bot** cai/topo; o orchestrator só observa

Extensão de `payload` **depois** da ladder, ainda `schemaVersion: 1` (campo opcional):

```json
"payload": {
  "giftName": "Rose",
  "slug": "rose",
  "coins": 1,
  "repeat": 1,
  "comboFinished": true,
  "action": "SpawnObstacle.Small"
}
```

`action` é string estável da ladder. Unity faz switch nela. Não parseia coins de novo.

## Replay / teste

`source.bridge: "synthetic"` + `POST /v1/events` com o envelope já válido. Fixture: `fixtures/events/` (ainda não existe). Live de 15 min sem TikTok usa só isso.

## Progresso da subida contínua

Unity → WS `/game` continua aceitando o formato `{index,status}`. No modo
contínuo, a mensagem inclui opcionalmente o par `height`/`record`:

```json
{"index":1,"status":"climbing","height":25,"record":32}
```

São metros inteiros não negativos; record deve ser ≥ height. O Orchestrator
rejeita par incompleto, tipos inválidos e propriedades desconhecidas. Alterar
somente a altura também publica um novo OverlayState, respeitando o limite
vigente de atualização. Os campos são opcionais no schema v1; os nomes de
actions e o contrato HTTP LiveEvent não mudam.
