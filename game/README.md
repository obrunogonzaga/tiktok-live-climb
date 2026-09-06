# Climb — composição candidata da issue #14

Abra esta pasta no Unity **6000.6.0f1** (Unity 6 / URP), abra
`Assets/Scenes/Climb.unity` e selecione Game em **1080×1920**.
Clique **Play**, sem teclado: o Bot sobe os oito apoios, chega ao topo,
celebra e reinicia. `Round Index` conta os rounds concluídos a partir de zero.

A câmera é fixa em perspectiva. O Player usa **1080×1920 Windowed** e roda
em segundo plano. Bot e materiais são provisórios; a composição aguarda
aprovação visual do Bruno. [Parâmetros e evidências](../docs/issue14-composition.md).

## Recriar e validar

**Climb → Rebuild tower composition** recria a cena e reinstala os três
prefabs/cliente de eventos. Sobrescreve edições manuais da cena; não é
necessário para abrir ou jogar. Ajuste o gerador para mudanças reproduzíveis.

**Climb → Validate composition** reabre a cena, observa os oito pousos na
frequência da física e grava pelo menos 30 segundos de PlayMode sem input.
Salva frames reais 1080×1920 em `Logs/issue14-frames/`, capturas de
início/meio/topo e relatório em `../docs/evidence/issue14/`. As capturas são
frames da mesma gravação, sem render externo. O teste verifica câmera fixa,
safe zone, oito apoios e incremento do round; não substitui o OK visual.

**Climb → Validate fall reset** injeta separadamente uma posição abaixo do
spawn e verifica `fell` seguido de incremento do round.

Com o projeto fechado no editor, a partir da raiz do repo:

```sh
UNITY='/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD/game" \
  -executeMethod CompositionValidation.Run -logFile /tmp/climb-composition.log
"$UNITY" -batchmode -projectPath "$PWD/game" \
  -executeMethod CompositionFallValidation.Run -logFile /tmp/climb-fall.log
ffmpeg -framerate 15 -i game/Logs/issue14-frames/%05d.png \
  -c:v libx264 -crf 23 -pix_fmt yuv420p -movflags +faststart \
  game/Logs/issue14-playthrough.mp4
```

Os validadores encerram o editor. Não passe `-quit`. A captura usa
`Time.captureDeltaTime=1/15` somente durante a validação; o relatório separa
tempo de jogo de tempo real. O validador antigo `Climb/Validate 30 seconds`
é específico da rota greybox da #2; use o de composição nesta cena.

## Eventos e Overlay

Com Node 22, execute `npm ci && npm start` em `orchestrator/` e clique Play.
O cliente usa `ws://127.0.0.1:8766/game` e reconecta automaticamente.
Abra o Overlay HTML transparente em `http://127.0.0.1:8790/` a 1080×1920.

Na raiz do repo:

```sh
curl -sS -H 'Content-Type: application/json' \
  --data-binary @fixtures/events/gift-rose.json http://127.0.0.1:8765/v1/events
```

Rosa cria Small, Perfume cria Smoke e Confete cria Medium. Contadores no
Inspector de `GameEventClient` e logs `SPAWN` confirmam cada action. Objetos
expiram após seis segundos. Reenvio do mesmo fixture é deduplicado; reinicie
o Orchestrator para repetir uma sessão sintética do zero.

Unity publica `{index,status}` em `/game`; o Overlay numera rounds a partir
de 1. Não há HUD Unity. O toast fica na coluna esquerda, fora da rota,
conforme o ajuste autorizado na #14.
