# Greybox climb — issue #2

Abra esta pasta no Unity **6000.6.0f1** (Unity 6, template Universal 3D/URP).
Abra `Assets/Scenes/Climb.unity` e selecione **Game → 9:16** (ou resolução fixa 1080×1920).

1. Selecione `Bot` na Hierarchy e clique **Play**. Não use teclado.
2. Observe a cápsula subir as oito plataformas e pular o obstáculo.
3. No topo, ela celebra brevemente e volta ao spawn. Confira `Round Index` no Inspector e o log de round na Console.
4. Para exercitar queda, durante Play altere o Y do Bot para -3 no Inspector: ele deve voltar ao spawn e incrementar o round.

A câmera é fixa; o Player abre em 1080×1920 **Windowed**, com execução em segundo plano. Não há HUD nesta issue.

## Verificação reproduzível

**Climb → Validate 30 seconds** entra em PlayMode sem input, acompanha altura, estados e rounds, salva o resultado em `Logs/issue2-playmode.txt` e uma captura 1080×1920 em `Logs/issue2-playmode.png`. `Logs/` não é versionado. O playtest visual do Bruno continua sendo o gate do merge.

Também pode executar pelo terminal, com o projeto fechado no editor:

```sh
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD/game" \
  -executeMethod ClimbValidation.Run -logFile /tmp/climb-playmode.log
```

Execute a partir da raiz do repositório. Não use `-quit`: a verificação encerra o editor após os 30 segundos.

**Climb → Rebuild greybox scene** recria a cena e as configurações deste greybox; sobrescreve edições na cena. Não é necessário para jogar.

## Eventos sintéticos — issue #3

Com Node 22, execute `npm ci && npm start` em `orchestrator/`, depois **Play** na cena `Climb`.
O componente `GameEventClient` conecta automaticamente a `ws://127.0.0.1:8766/game` e reconecta se o processo reiniciar.

Na raiz do repositório:

```sh
curl -sS -H 'Content-Type: application/json' \
  --data-binary @fixtures/events/gift-rose.json http://127.0.0.1:8765/v1/events
```

Rosa cria um cubo pequeno, Confete um cubo maior e Perfume uma esfera placeholder. Objetos aparecem à frente do Bot e duram seis segundos. Os contadores `Small Count`, `Medium Count` e `Smoke Count` no Inspector e os logs `SPAWN` permitem conferir os fixtures, sem exibir dados do viewer.

O jogo publica `{index,status}` no mesmo WS. `index` identifica o round atual a partir de 1; `Round Index` do Bot continua contando rounds concluídos a partir de 0. Não há HUD no Unity.

Após recriar o greybox por **Climb → Rebuild greybox scene**, execute **Climb → Install event client** para reinstalar os prefabs e a conexão.
