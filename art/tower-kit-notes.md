# Kit modular da torre

O kit preserva a alvenaria azul-cinza, pedra chanfrada irregular, arco recuado,
cornijas, pilares, apoio e hera da composição aprovada. As fontes ficam em
`art/tower-kit.blend`, separadas nas coleções `ASSEMBLY · approved tower` e
`KIT · modular source`.

Reproduza tudo com Blender 4.5.12:

```sh
BLENDER="$HOME/Applications/Blender.app/Contents/MacOS/Blender"
"$BLENDER" --background --python art/build_tower.py
"$BLENDER" --background --python art/build_continuous_body.py
```

Para atualizar só as doze peças sem tocar na torre aprovada, Ledge ou corpos
contínuos:

```sh
"$BLENDER" --background --python art/build_tower.py -- --kit-only
```

Os FBXs em `game/Assets/Art/Environment/Kit/` exportam uma raiz vazia nomeada,
mais a geometria selecionada. Cada origem está descrita no
[`kit-manifest.json`](kit-manifest.json); as peças de parede usam `+Y` local
como lado externo. Blender usa Z para cima e os FBXs usam `-Z forward / Y up`.

`Tower.fbx` continua a ser a montagem aprovada detalhada, com oito grupos de
material batched para renderização. `ContinuousBody.fbx` é seu corte de 7,8 m;
`ContinuousBodyLOD.fbx` é a variante decimada. O kit não inclui colisores:
Unity deve manter colisores de rota independentes da decoração, especialmente
nos LedgeDecks e corbels.

Round-trip Blender confirmou as doze raízes, materiais Stone0–Stone5/Mortar/Ivy
e os 132.360 triângulos da montagem. As medidas e triângulos de cada peça estão
no manifesto. Nenhuma câmera, luz ou imagem foi exportada nos FBXs.
