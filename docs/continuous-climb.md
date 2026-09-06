# Subida contínua

Bruno aprovou a direção visual do estudo e, em seguida, a proposta de subida
contínua com trechos reaproveitados, câmera acompanhando o Bot, altura/recorde
e novo round após queda definitiva. Isso substitui a câmera parada e o topo
fixo da versão anterior. O OK não autoriza merge automático.

## Jogar

Abra `game/Assets/Scenes/Climb.unity` no Unity **6000.6.0f1**, selecione
Game **1080×1920** e clique Play. O Bot sobe sem teclado. Use
`Climb/Rebuild continuous game` para recriar a cena; o menu antigo
`Climb/Rebuild tower composition` também chama esse gerador.

O corpo é vertical, com os materiais, céu, nuvens e Bot do estudo aprovado.
A rota forma uma hélice em volta da torre. A câmera acompanha altura e volta,
com o mesmo deslocamento relativo e ângulo de leitura da referência.
`ReferenceStudy.unity` continua disponível como comparação visual estática.

## Regras desta versão

- 32 plataformas e 4 corpos de torre de 7,8 m são reaproveitados.
- Hélice determinística: raio 4,05 m, passo angular 24°, subida 0,65 m por apoio.
  A posição inicial é −12°; o primeiro alvo é o apoio 1, após o spawn no apoio 0.
- Mantém 10 apoios atrás do alvo para recuperação; a reciclagem ocorre longe do Bot.
- Sem topo final no modo contínuo. Cair mais de 3 m abaixo do maior apoio
  alcançado encerra o round; tentativas de salto/travamento têm recuperação limitada.
- Reset devolve Bot e pools ao início, limpa os objetos de gifts e mantém o recorde.
- A cada 128 m de coordenada local, reposiciona objetos para manter precisão.
  Altura lógica usa double; coordenadas da física permanecem próximas da origem.
- O recorde vale durante a execução do jogo e atravessa os resets de round.
  Reiniciar o Unity inicia outro recorde; persistência entre sessões não foi adicionada.
- O runtime emite marcos a cada 25 m alcançados. Celebrações específicas desses
  marcos e novas ajudas, como escudo/impulso, ficam para a próxima etapa.

Os seis estados e CharacterController continuam sendo usados. O modo finito
permanece disponível em `ClimbBot.Configure` para compatibilidade; o jogo atual
usa `ConfigureContinuous`.

## Gifts e Overlay

Com Node 22, execute `npm --prefix orchestrator ci` e
`npm --prefix orchestrator start` na raiz do repo. O Overlay está em
`http://127.0.0.1:8790/`; a entrada de eventos continua em
`POST http://127.0.0.1:8765/v1/events` e os WS em `:8766/game` e `:8766/overlay`.

Rosa/Small, Confete/Medium e Perfume/Smoke continuam funcionando, com TTL 6 s,
contadores, combo e deduplicação. Os objetos pertencem ao round e acompanham
reposicionamentos do mundo. Arte/VFX finais dos gifts e novas actions de ajuda
não foram acrescentados nesta integração.

O Round HUD HTML agora mostra altura e recorde quando esses dados chegam do
modo contínuo. `{index,status}` continua aceito; `height`/`record` são opcionais,
mas devem vir juntos como inteiros não negativos, com record ≥ height.
Nenhuma UI foi desenhada no Unity.

## Reprodução e verificação

O projeto original estava aberto no editor. Os testes automatizados usaram uma
cópia isolada em `tmp/continuous-validation/game`, com os mesmos fontes/assets;
as capturas vêm da câmera do Unity, com a API de renderização URP.

```sh
UNITY='/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity'
# Com o projeto fechado no editor:
CLIMB_SECONDS=45 CLIMB_REBASE=16 "$UNITY" -batchmode -projectPath "$PWD/game" \
  -executeMethod ContinuousValidation.Run -logFile /tmp/continuous-rebase.log
CLIMB_SECONDS=45 CLIMB_REBASE=16 CLIMB_FALL=1 CLIMB_FALL_AT=25 \
  "$UNITY" -batchmode -projectPath "$PWD/game" \
  -executeMethod ContinuousValidation.Run -logFile /tmp/continuous-fall.log
```

O limiar 16 é apenas uma configuração de teste não salva, para exercitar vários
reposicionamentos em 45 s. O padrão salvo permanece 128. Não passe `-quit`:
o validador encerra o editor quando terminar. `CLIMB_RECORD=1` grava frames
1080×1920 em `game/Logs/continuous-frames/`; o tempo de captura é separado do
custo de renderização e da codificação PNG, sem apresentá-lo como FPS do player.

Fontes Blender: `art/continuous-body.blend` e `art/build_continuous_body.py`.
O corpo tem dois níveis de detalhe. As nuvens do jogo usam 16 passos e iluminação
simplificada; o estudo original conserva sua configuração de 40 passos.

## Evidências desta integração

- [Subida de 45 s com cinco reposicionamentos](evidence/continuous/autonomy-rebase.txt).
- [Queda após 25 m e dois reposicionamentos](evidence/continuous/fall-after-rebase.txt).
- [Fixtures no Unity e inválido sem spawn](evidence/continuous/events.json).
- [Vídeo real da subida](evidence/continuous/playthrough.mp4).
- [Início, volta e 32 m em tamanho de celular](evidence/continuous/mobile-journey.png).
- [Testes do Orchestrator](evidence/continuous/orchestrator-tests.txt).
- [Harness de desempenho do player](../tools/performance/README.md).

O teste de 45 s manteve 32 plataformas/4 corpos, chegou a 48 m e realizou 68
reciclagens. O teste de queda voltou ao spawn/round 1 e preservou 25 m de recorde.
Os gifts continuam com seus placeholders funcionais; esta etapa não afirma ter
entregado arte final de Rosa/Confete/Perfume ou novas ajudas.

## Desempenho medido

Player macOS Development, Apple M3, RenderTexture 1080×1920, VSync desligado:
3 s de aquecimento + 15 s medidos. A média passou de **18,9 para 36,3 FPS**
após evitar amostras vazias e iluminação desnecessária nas nuvens. No teste
final, média de 27,6 ms/frame e máximo de 51,0 ms; isso não garante 60 FPS nem
representa o custo do LIVE Studio/Overlay ou de outro hardware.

[Antes](evidence/continuous/performance-before.json) ·
[Resultado final](evidence/continuous/performance.json).
O player foi executado sem `-batchmode`: o modo batch pode omitir renderização
e produzir uma taxa de frames inválida para esse propósito.
