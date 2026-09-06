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
