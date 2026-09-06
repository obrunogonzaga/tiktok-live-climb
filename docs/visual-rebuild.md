# Reconstrução visual — primeiro trecho em revisão

Em 06/09/2026, Bruno reprovou visualmente a composição do PR #18 e autorizou
reconstruir a cena do zero. O novo gate é um **trecho representativo no Unity
com geometria, materiais, céu, nuvens, luz e Bot**, antes de expandir o percurso.
Essa decisão substitui, para este estudo, o adiamento de toda a aparência para
as fases seguintes. Não aprova o resultado nem autoriza merge.

## Abrir

Unity **6000.6.0f1** → `game/Assets/Scenes/ReferenceStudy.unity` → Game **1080×1920**.

- `Climb/Rebuild reference study`: recria este estudo e seus materiais.
- `Climb/Capture reference study`: entra em PlayMode e salva PNG real.
- `Climb/Record reference study`: grava 12 s a 15 fps; primeiros 5 s na câmera
  proposta, depois uma pequena órbita para inspecionar a profundidade.
- `ReferenceStudy` contém uma pose estática do Bot e nuvens animadas pelo shader.
  **Não é demonstração da subida autônoma nem da rota completa.**

A cena `Climb.unity`, CharacterController, seis estados, Orchestrator, actions,
Overlay e contratos anteriores não foram alterados nesta reconstrução. A rota
completa será integrada somente depois da revisão deste trecho, com colisores
independentes da aparência e nova regressão funcional.

## O que mudou

- Torre vertical em Blender: pedras com bordas chanfradas/irregulares, juntas,
  cornijas, pilares de canto, coroamento, arco recuado, folhas e apoios de pedra.
  O volume não é mais uma parede inclinada para acomodar a rota.
- Materiais URP com cor, normal, oclusão e rugosidade convertida para smoothness.
- Céu fotografado com estrelas; o shader amostra a parte celeste, excluindo
  terreno/horizonte. Nuvens em volumes 3D com ruído, absorção e sombreamento.
- Luz de lua, preenchimento frio, acento magenta, emissivos ciano/magenta e
  pós-processamento ACES/bloom. As luzes dos apoios afetam a pedra próxima.
- Bot Blender de **1,400 m / 3.932 tris**, capacete arredondado, viseira, emissores
  laranja, juntas, membros, mão levantada, antena e mochila. A instância do estudo
  usa escala 1,3 para dar mais presença ao personagem no enquadramento. Export FBX reimportado
  para conferir hierarquia, bounds e cinco materiais.
- Câmera do estudo: `(5,7; 1; −11)`, alvo `(−0,8; 5; 2)`, perspectiva FOV 52°.
  Janela 1080×1920 Windowed.

## Evidências

- [Captura Unity 1080×1920](evidence/rebuild/study.png)
- [Referência × reconstrução](evidence/rebuild/reference-comparison.png)
- [Composição reprovada × reconstrução](evidence/rebuild/before-after.png)
- [Inspeção lateral](evidence/rebuild/side.png)
- [Tamanho de celular 360×640](evidence/rebuild/mobile.png)
- [Vídeo real de inspeção](evidence/rebuild/study.mp4)
- [Proveniência da captura](evidence/rebuild/capture.txt)

O menu de gravação salva os frames em `game/Logs/reference-frames/`. Para codificar o MP4:

```sh
ffmpeg -y -framerate 15 -i game/Logs/reference-frames/%05d.png \
  -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart \
  docs/evidence/rebuild/study.mp4
```

Os PNGs do estudo saem da câmera do Unity em PlayMode. O MP4 codifica esses
frames; as comparações apenas diagramam imagens existentes com rótulos.
Nenhum render Blender ou imagem gerada foi usado como prova da execução Unity.
A câmera da órbita serve para inspeção e não define acompanhamento do gameplay.

## Fontes e reprodução

Blender **4.5.12 LTS**, instalação local em `~/Applications/Blender.app`, baixada
do distribuidor oficial e conferida contra seu manifesto SHA256.

```sh
BLENDER="$HOME/Applications/Blender.app/Contents/MacOS/Blender"
"$BLENDER" --background --python art/build_tower.py
"$BLENDER" --background --python art/build_bot.py
```

Fontes editáveis: `art/tower-kit.blend` e `art/bot.blend`; exportados em
`game/Assets/Art/`. `Climb/Rebuild reference study` preserva a rotação de eixo
importada do FBX e remapeia seus materiais para URP.

Texturas CC0, autoria e hashes em [art/environment-sources.json](../art/environment-sources.json):
[Stone Wall 05, Charlotte Baglioni](https://polyhaven.com/a/stone_wall_05) e
[Moonless Golf, Greg Zaal](https://polyhaven.com/a/moonless_golf).
A composição de cor/noite é aplicada no material/shader, sobre as fontes listadas.

## Gate

- [x] Assets Blender/FBX reais integrados no Unity.
- [x] Materiais, céu, estrelas, nuvens e iluminação presentes na captura real.
- [x] Antes/depois, comparação, inspeção lateral e escala de celular conferidos.
- [x] Câmera reaberta e gravação reproduzida; nenhum contrato/runtime anterior alterado.
- [ ] Bruno aprova a direção visual deste trecho.
- [ ] Ajustes pedidos na revisão, extensão da rota e regressão funcional completa.

O estudo ainda não reproduz a densidade de detalhes/VFX do concept: não contém
Leão, carro, galáxia, gifts finais, pétalas ou fumaça de Perfume. As nuvens,
o desgaste da pedra e a silhueta do Bot permanecem sujeitos à revisão visual.
Não fechar #14/#1/#7 nem tratar a captura como aprovação humana.

Orçamento deste estudo: torre com 132.360 triângulos e três volumes de nuvens
com 40 passos por raio. LODs, custo de GPU e estabilidade de frame time precisam
ser medidos na integração do gameplay; os 15 fps da gravação não são benchmark.
