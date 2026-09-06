# Overlay e captura v1

Canvas **1080×1920** (9:16). O viewer vê três camadas, nesta ordem de baixo pra cima:

1. **Jogo** (Unity, opaco)
2. **Overlay** (HTML transparente: ladder, placar, toasts)
3. **Chrome do TikTok** (app do viewer — a gente não controla)

Fonte da verdade do estado do overlay: [`schemas/overlay-state.v1.schema.json`](../schemas/overlay-state.v1.schema.json).

## Safe zone (LIVE, não VOD)

O chrome de LIVE não é o de For You. Números conservadores no canvas 1080×1920 — confirmar no celular de teste antes da primeira sessão:

| Zona | Pixels | O que cobre |
| --- | --- | --- |
| Topo | y 0–150 | LIVE, viewers, host, share |
| Direita | x 960–1080 | like, gift, mais |
| Baixo | y 1580–1920 | composer de comentário |
| Esquerda-baixo | x 0–420, y 1280–1580 | stack de comentários |

**Safe zone útil:** x **24–940**, y **160–1260**. Ladder e placar **só** aqui. O bot e os obstáculos importantes do Unity também devem viver no centro-esquerda dessa caixa — se o climb for colado na direita, o like come o personagem.

```
1080 × 1920
┌──────────────────────────┬────┐
│ LIVE · viewers           │    │  y 0–150
├──────────────────────────┤  ♥ │
│  LADDER                  │  🎁│
│  (24,160)                │    │
│                          │    │
│     [ torre / bot ]      │    │  safe
│                          │    │
│  PLACAR / last donors    │    │  y ~1100
├─────────────── comments ─┤    │  y 1280
│ composer                 │    │  y 1580+
└──────────────────────────┴────┘
```

Medidas de VOD (caption 320px, ads) **não** se aplicam. Não copiar “TikTok safe zone 2026” de thumbnail.

## Camadas na captura (Windows)

Cena única vertical 1080×1920, 30 ou 60 fps. Target: **TikTok LIVE Studio** (Gaming). OBS é fallback se o Studio recusar Browser Source estável.

| Ordem (fundo → frente) | Fonte | Tamanho | Áudio |
| --- | --- | --- | --- |
| 1 | Game Capture — Unity, windowed / borderless | 1080×1920 | SFX do jogo |
| 2 | Browser / Link — overlay HTML | 1080×1920, fundo transparente | mudo (TTS não passa daqui) |
| 3 | (opcional) TikFinity Screen — **desligado no MVP** | — | — |

Não empilhar alerta TikFinity **e** overlay nosso: gift animation nativo do TikTok já invade o centro. O nosso overlay é só HUD (ladder / placar / toast texto).

Unity: **não** fullscreen exclusive (quebra captura e overlay do Studio). Borderless window 1080×1920.

### LIVE Studio

1. Cena Gaming, canvas vertical.
2. Source **Game** → janela do Unity.
3. Source **Link** → `http://127.0.0.1:8790/` (overlay). Resolução custom **1080×1920**. Som **off**.
4. Preview no celular via “Share / watch as viewer”. Ajustar ladder se o chrome real cobrir.

### OBS (fallback)

- Base canvas 1080×1920.
- Game Capture + Browser Source (`http://127.0.0.1:8790/`, width 1080, height 1920, shutdown when not visible = off).
- Stream key do TikTok se o Studio não for opção. Preferir Studio: ToS e qualidade de LIVE.

## Contrato do overlay HTML

Página única, sem build no MVP. Abre WS `ws://127.0.0.1:8766/overlay` e pinta o último `OverlayState`. Se o WS cair: mostra “bridge?” no canto; **não** esconde a ladder (último estado fica).

Superfícies (todas na safe zone):

| Superfície | Posição (px, top-left origin) | Conteúdo |
| --- | --- | --- |
| Ladder | x 24–300, y 160–720 | 7 degraus: ícone/emoji, label PT, coins, count da **Sessão** |
| Round HUD | x 320–940, y 160–240 | round #, status (`climbing` / `fell` / `summit`) |
| Toast TTS | x 24–300, y 760–940 | texto quebra dentro da coluna, some em 2,5 s. Só texto (voz é outro canal) |
| Placar / last donors | x 24–360, y 1000–1260 | top 3 da sessão + 3 últimos gifts |

Não desenhar: comentários, likes, follow list (o TikTok já desenha). Não desenhar o **Bot**.

`OverlayState` chega no máximo 10 Hz. Toast: o HTML respeita `toast.expiresAt`; se o WS mandar `toast: null`, some na hora.

## Áudio

Dois barramentos. Não misturar no Unity.

| Barramento | Origem | Destino |
| --- | --- | --- |
| SFX | Unity (queda, spawn, boss) | saída padrão → Game Capture |
| TTS | orchestrator (ElevenLabs wav → player local) | **VB-Audio Cable** (ou equivalente) → source de áudio extra no LIVE Studio |

Fallback se o Cable falhar: TikFinity sound alert **só** pra gifts nomeados — aí o overlay toast continua, mas a voz não é a nossa. Documentar no runbook da sessão, não no código.

Fila TTS: ver [gift-ladder.md](./gift-ladder.md). Overlay toast dispara mesmo quando a fala é dropada.

## Checklist pré-live (captura)

- [ ] Unity 1080×1920 borderless, não exclusive fullscreen
- [ ] Overlay HTML abre em `127.0.0.1` e mostra ladder v1 sem eventos
- [ ] Celular viewer: ladder visível, não coberta por likes/comentários
- [ ] SFX do jogo no ar; TTS no Cable (teste: gift synthetic)
- [ ] TikFinity overlay screens **off**
- [ ] Round reset não corta a captura (mesma janela)

Aceite: 15 min de sessão sintética + 1 gift real de teste, HUD legível no iPhone e no Android.
