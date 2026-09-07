# Maya — avatar da #15

Bruno escolheu a persona Maya para substituir o robô e delegou o acabamento
ao melhor resultado visual. O teto de 4.000 triângulos do robô foi substituído
por até 12.000 na Maya próxima e menos de 4.000 no LOD distante. A aprovação
final da experiência composta permanece na #17.

## Fonte e reconstrução

- `maya.blend`: malha principal, rig genérico, cabelo e roupas editáveis;
  texturas empacotadas no arquivo.
- `maya-lod.blend`: versão simplificada, com a mesma anatomia, rig e UVs.
- `build_maya.py`: reconstrói ambos os níveis. O padrão produz o principal;
  `MAYA_VARIANT=lod` produz o LOD.
- `maya-report.json` e `maya-lod-report.json`: contagem de todas as peças e bounds
  medidos no Blender. A importação Unity é medida separadamente.

```sh
MAYA_VARIANT=lod blender --background --python-exit-code 1 --python art/build_maya.py
blender --background --python-exit-code 1 --python art/build_maya.py
```

O script deve rodar numa instância dedicada: inicia uma cena vazia de autoria.
Blender utilizado: **4.5.12 LTS**. A origem fica no plano das solas, em metros;
Blender usa Z para cima e -Y para frente; FBX usa -Z forward/Y up.

A cena usa `Assets/Art/Maya/Maya.fbx`, com escala visual `0.88`. O LOD é
`Assets/Art/Maya/LOD/MayaLOD.fbx`. Ambos compartilham os mesmos ossos no Unity;
não existem dois animadores independentes. O nível principal cobre a câmera
do jogo; o LOD é selecionado abaixo de 6,5% da altura da tela.

A pele é uma malha anatômica contínua, com a textura da persona projetada e
baked no UV do modelo. A cabeça não é um retrato em plano nem uma máscara
flutuante. A regata, calça, botas, costuras, bolso, cadarços e colar são partes
do asset; não são novos elementos do Overlay. O rig usa 18 ossos e movimentos
visuais dirigidos pelos estados já existentes do Bot. Não move o controller.

## Referências, assets e licenças

Os retratos `rosto.jpg` e `gamer.jpg` de `~/Documents/Maya Prado` foram fornecidos
por Bruno. São referência da persona; dados de cadastro não entram no projeto.

- Topologia corporal e morph feminino: assets **CC0** do MakeHuman Community.
  Foram adaptados em Blender; código do aplicativo MakeHuman não faz parte
  do runtime. [Licença oficial](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.md).
- Coordenadas faciais para alinhar a textura: extraídas localmente com
  MediaPipe 0.10.21, **Apache-2.0**. O Unity não executa inferência nem depende
  do pacote Python. [Fonte oficial](https://github.com/google-ai-edge/mediapipe).
- `MayaFace.png` e `MayaHair.png`: texturas de autoria produzidas com o tool
  `imagegen` integrado. Prompts e procedência estão em
  `sources/maya/texture-prompts.md`.
- `MayaSkinAtlas.png`: bake de cor no Blender. `MayaFabric*.png` e
  `MayaHairline.png`: mapas de material produzidos pelo gerador Blender.
- `sources/maya/sources.json`: URLs e SHA-256 dos arquivos externos utilizados.
  As licenças estão junto das fontes.

As imagens geradas são **assets de textura**, não provas de execução. As
capturas e vídeos em `docs/evidence/issue15/` devem identificar câmera do jogo,
câmera de inspeção e composição de capturas/HTML separadamente.

## Limites preservados

CharacterController, seis estados, hélice, câmera, pools/rebase, portas e actions
permanecem os mesmos. O robô e `ReferenceStudy.unity` ficam como histórico da
#14. VFX finais dos gifts, TTS, TikFinity e LIVE Studio não pertencem a esta fase.
