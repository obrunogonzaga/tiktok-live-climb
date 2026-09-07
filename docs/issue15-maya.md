# Maya e kit Blender — #15

Bruno substituiu o robô pela Maya e autorizou escolher o acabamento pelo melhor
resultado visual. Esta é a candidata da #15; o aceite visual final do conjunto
permanece na #17. A câmera/hélice aprovadas no PR #18 foram preservadas.

## Abrir e recriar

Unity **6000.6.0f1** → `game/Assets/Scenes/Climb.unity` → Game **1080×1920** → Play.
Maya sobe sem input. `Climb/Rebuild continuous game` recria a cena e
`Climb/Validate Maya import` verifica assets, rig, LODs e configuração salva.

Fontes/reprodução: [Maya](../art/maya-notes.md) · [kit](../art/tower-kit-notes.md).
Blender utilizado: **4.5.12 LTS**. Arquivos `.blend` e exportações FBX são reais.

## Parâmetros

- Câmera perspectiva: FOV 52°, posição inicial `(5.658, -1.85, -11.561)`;
  acompanha altura e azimute com os parâmetros da #14.
- Hélice: raio 4,05 m, passo 24°, subida 0,65 m; 32 plataformas e quatro corpos
  de 7,8 m em pools. CharacterController e seis estados mantidos.
- Maya: escala visual 0,88; rig genérico de 18 ossos. Versão principal abaixo
  de 12.000 triângulos e LOD abaixo de 4.000. Ambos usam o mesmo esqueleto.
- Materiais URP para pele, cabelo, tecido, couro e metal. A hairline tem alfa
  real; a pele é uma malha contínua com atlas baked a partir da referência.
- Kit: 12 módulos com pivôs e unidades documentados, usados para montar os
  FBXs de runtime. [Manifesto e contagens](../art/kit-manifest.json).

Contagens e dimensões finais importadas: [import.json](evidence/issue15/import.json).
As medidas em pixels vêm de uma passagem de silhueta da câmera Unity, com a
mesma pose/projeção, avatar isolado e pós-processamento desligado; a leitura
com cenário/Overlay também precisa ser inspecionada visualmente.

## Evidência

- [Antes — Unity, personagem anterior](evidence/issue15/before/start.png).
- [Percurso atual em tamanho de celular](evidence/issue15/final/mobile-journey.png).
- [Vídeo real de subida e queda/reset](evidence/issue15/final/playthrough.mp4).
- [Autonomia e reposicionamento da origem](evidence/issue15/final/autonomy.txt).
- [Fixtures e inválido sem spawn](evidence/issue15/final/events.json).
- [Desempenho do player](evidence/issue15/final/performance.json).

As capturas `maya-inspection-*` usam uma câmera aproximada apenas para inspecionar
rosto/perfil, restaurada em seguida; não substituem os frames da câmera de jogo.
Os arquivos `*-overlay.png` são composições de captura Unity com o HTML real
renderizado no navegador. Esse método verifica a sobreposição, sem apresentá-la
como uma gravação contínua do LIVE Studio. 360×640 é emulação de tamanho de celular.

Texturas geradas são assets de autoria identificados em
[texture-prompts.md](../art/sources/maya/texture-prompts.md); não são prova do Unity.
As primeiras candidatas de cabeça/UV foram reprovadas durante a inspeção e
ficam separadas da evidência final.

## Escopo

As três actions existentes continuam com seus placeholders; nenhum VFX final,
TTS, TikFinity, placar novo ou captura LIVE Studio foi adicionado. O recorde
continua valendo somente durante a execução. #1 e parent #7 mantêm seus próprios
gates; a #16 não foi iniciada por esta entrega.

## Resultados e divergências restantes

- Unity importado: **11.857 tris** no principal, **3.976** no LOD, 18 ossos,
  11 renderizadores por nível e 1,529 m de altura efetiva.
- Silhueta na câmera: aproximadamente **237–242 px**, acima da referência
  nominal de 200 px para preservar a leitura do avatar humano, dentro da safe zone.
- Autonomia: 45 s, 48 m, 72 apoios, cinco reposicionamentos de origem e pools
  constantes. O vídeo de 30 s inclui queda induzida após 20 m, reset 0→1 e
  retomada da subida; recorde preservado.
- Fixtures: Small=2 (rosa + combo), Medium=1, Smoke=1; inválido 400 sem spawn.
  Orchestrator: 11/11 testes passam, com ~87% de cobertura de linhas de produção.
- Player Development no Apple M3, RenderTexture 1080×1920: **30,4 FPS** médios,
  32,9 ms/frame e máximo de 70,8 ms, em 15 s após 3 s de aquecimento.
  Não garante 60 FPS nem mede o custo do LIVE Studio.
- Na inspeção próxima, franja/mechas ainda têm rigidez de um asset de jogo.
  A revisão visual não encontrou cabeça/corpo desconectados, features duplicadas,
  personagem cortada ou interferência do Overlay no enquadramento atual.

O HUD das composições usa estado sintético de QA; o frame final reproduz round 2,
12 m e recorde de 20 m observados no percurso. Essas imagens verificam layout,
sem afirmar sincronização contínua entre os dois programas.
