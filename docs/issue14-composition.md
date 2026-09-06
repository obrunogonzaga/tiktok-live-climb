# #14 — composição candidata, aguardando OK visual do Bruno

Sem merge autorizado. Unity **6000.6.0f1 / URP**, cena
`game/Assets/Scenes/Climb.unity`; base `main` em `534a1fc`, com #2–#4 presentes.

## Parâmetros

- Torre contínua com frente recuada, lateral, base, bordas e cintas; largura 5,4.
  Fachada inclinada 42,71° para acomodar o recuo dos apoios, sem tetos sobre os saltos.
- Oito apoios: X alterna −0,55/+0,85; superfície Y=(i+1)×0,65; Z=i×0,6.
  Slabs 0,9×0,3×1,65, encaixados no corpo; waypoint Z=i×0,6−0,3.
- Spawn (−0,2; 0,72; −1,1), velocidade 2,8; CharacterController e seis estados intactos.
- Câmera fixa em perspectiva: posição (9; 1; −30), alvo (0,4; 1,9; 0), FOV 23°.
- 1080×1920 **Windowed**, HUD HTML, portas 8765/8766/8790 preservadas.
- Cápsula, pedra, casco, ciano e magenta provisórios. Modelagem/VFX finais fora do escopo.
- Gerador `ClimbSceneBuilder.Build` recria mesh, materiais, rota, câmera e cliente de eventos.
  Todas as validações reabriram a cena gravada em processos Unity novos.

A colisão das tentativas anteriores vinha dos apoios superiores: ao retornar entre
colunas, o Bot encontrava uma lateral e um teto antes de alcançar o waypoint.
O recuo progressivo em Z e a fachada correspondente liberaram o corredor de salto.
Nenhuma lógica da state machine ou contrato de eventos foi alterado.

## Evidência real e medidas

[Antes/depois](evidence/issue14/before-after.png) ·
[Referência × execução](evidence/issue14/comparison.png) ·
[Percurso em oito instantes](evidence/issue14/journey.png) ·
[Vídeo completo, 1080×1920](evidence/issue14/playthrough.mp4)

| Momento | Frame do vídeo | Silhueta real | Captura Unity | Com Overlay |
| --- | ---: | ---: | --- | --- |
| Início | 0 | 217 px | [abrir](evidence/issue14/start.png) | [abrir](evidence/issue14/start-overlay.png) |
| Meio | 38 | 202 px | [abrir](evidence/issue14/middle.png) | [abrir](evidence/issue14/middle-overlay.png) |
| Topo | 75 | 186 px | [abrir](evidence/issue14/summit.png) | [abrir](evidence/issue14/summit-overlay.png) |

Silhueta medida nos pixels dos PNGs, não somente no bounding box: **186–218 px**
em todos os **646 frames**, margem de aproximadamente ±2 px nas bordas.
[Medidas por máscara RGB](evidence/issue14/silhouette.json). Bounds projetados
conservadores ao longo do vídeo: x=308–782, y=247–1251, fora do Round HUD e do toast.

[Contato em tamanho de celular, 360×640 por painel](evidence/issue14/mobile.png).
Bot e apoios ficam visíveis com o toast ativo. O toast foi reposicionado, com
autorização do Bruno, para x=24–300/y=760–940; TTL e conteúdo não mudaram.

Os PNGs de jogo e o vídeo vêm da câmera do Unity em PlayMode. O vídeo reúne
43,07 s de jogo a 15 fps, correspondentes a 30,02 s reais de validação;
`Time.captureDeltaTime` controla apenas a gravação. Não há input no percurso.
As imagens com Overlay compõem esses PNGs com uma captura transparente do HTML
real, com toast ativo; o texto de round dessa camada é de outra sessão de QA.
Não são uma captura sincronizada do Studio nem um render externo do jogo.
A referência do README foi mantida na proporção original, sem recorte ou geração.

## Validate

- [x] Antes/depois reais 1080×1920 e referência lado a lado.
- [x] Início/meio/topo e percurso mostram corpo contínuo, lateral e apoios integrados.
- [x] Silhueta medida, safe zone e Overlay conferidos em 1080×1920 e tamanho de celular.
- [x] Play ≥30 s sem input: oito pousos em ordem, collider de suporte conferido,
  status summit e sete resets. [Relatório](evidence/issue14/measurements.txt).
- [x] Queda injetada separadamente: `fell`, round 0→1, retorno ao spawn em `Climb`.
  [Relatório](evidence/issue14/fall-reset.txt).
- [x] Rosa/Perfume/Confete criam Small/Smoke/Medium; combo cria segundo Small.
  Inválido retorna 400 e não altera contadores. [Relatório](evidence/issue14/events.json).
- [x] Cena reaberta, câmera fixa, Windowed e cliente/contratos preservados.
- [x] Orchestrator: 9/9 testes, cobertura de linhas de produção ~87%.
  [Saída](evidence/issue14/orchestrator-tests.txt).
- [ ] **Bruno aprova explicitamente a composição antes do merge e da fase 2.**

## Limites visuais da candidata

A fachada ainda é um bloco regular inclinado; o concept tem pedra irregular,
mais variação de silhueta e arte/luz que pertencem às próximas fases. O Bot é
uma cápsula provisória. Esta entrega propõe o volume, percurso e enquadramento
como base para aprovação; não declara equivalência estética nem aprovação humana.

Como abrir/reproduzir: [game/README.md](../game/README.md). #15 não iniciada;
#1 e parent #7 permanecem abertas. O servidor Python do Dito foi pausado somente
para liberar 8765 durante os fixtures e restaurado após a validação.
