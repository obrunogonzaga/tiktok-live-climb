# Style bible — pegada visual

Norte pra modelar o **Bot** e os 3 props MVP. Hex e silhueta saem dos concepts, não de gosto.

Refs: [gameplay.png](./refs/gameplay.png) (o jogo) · [viewer.png](./refs/viewer.png) (a live no celular).

O Unity desenha o **Bot** e os obstáculos. O **Overlay** (Ladder, Placar, toast) é HTML — Unity não desenha HUD. Greybox (cápsula e cubos) até o issue [#7](https://github.com/obrunogonzaga/tiktok-live-climb/issues/7).

## Paleta

Amostrada nos PNGs. Albedo destes hex; o glow vem de emissive + bloom, não de pintar o mesh mais claro.

| Papel | Hex | Onde |
| --- | --- | --- |
| Noite | `#010E34` | céu, fog, fill da cena |
| Neon ciano | `#08D7FF` | holds da torre, jatos nas solas |
| Neon magenta | `#A417F4` | fumaça, acentos mágicos |
| Viseira | `#FD8704` | ovais da face, ponta da antena, faixas da mochila |
| Pedra | `#3A4166` | blocos da torre (cinza-azulado, não concreto quente) |
| Casco | `#E8ECEC` | placas brancas do Bot (albedo; a luz magenta/ciano tinge) |

Rosa do gift: pétalas `#C92130`, folha verde chunky. Caixa: madeira média com X, não pallet IKEA.

## Luz (URP)

Cena noturna. A luz sai dos emissivos (viseira, holds, jatos, fumaça), não de um sol.

- **Bloom curto:** threshold alto, raio pequeno. Neon brilha no pixel vizinho; não vira halo que come a silhueta do Bot no 9:16.
- **Fog:** exponential, cor `#010E34` puxando magenta na distância. Dá altura na torre. Densidade baixa o bastante pra ler o Bot no centro-esquerda da safe zone.
- **Key:** nenhum Directional Light de dia. Rim frio mínimo no casco branco, se precisar separar do céu.

Evitar: sol de meio-dia, skybox diurno, pele realista / SSS, GI de estúdio, bloom infinito, lens flare.

## Silhueta do Bot

Não é humano. Não é webcam. Não é mascote de marca.

Leitura: toy / robô de serviço compacto. Cabeça enorme, corpo curto, membros stubby. Silhueta tem que fechar em ~200 px de altura no canvas 1080×1920.

```
         •          antena curta, ponta #FD8704
      /███████\     capacete esfera, ~1/3 da altura
      |  ●   ●  |     viseira preta wrap-around + 2 ovais emissivos
     /|         |\  ombros = largura da cabeça; sem pescoço
    / |  [███]  | \ torso curto; mochila cilíndrica nas costas
      |         |
      |_  |  _|     pernas 2 segmentos; pés bloco
        ░   ░       jato #08D7FF nas solas (só no Climb)
```

Proporção (1 cabeça = diâmetro do capacete ≈ 0,38 m; altura total ≈ 1,1 m ≈ **2,9 cabeças** — humano é ~7,5):

| Peça | Medida | Forma |
| --- | --- | --- |
| Cabeça | esfera Ø 1,0 | capacete liso, plástico branco. Sem queixo, sem nariz, sem orelha humana |
| Viseira | faixa horizontal no terço médio, wrap-around | faceplate **preto fosco**; dois ovais `#FD8704` emissivos. Sem íris, sem pupila, sem boca |
| Laterais da cabeça | disco Ø ~0,22 em cada lado | porta/fone, acento laranja — não orelha |
| Antena | 1 stub no topo, ~0,18 de alto | uma só; ponta emissiva. Sem par de orelhas de gato, sem halo |
| Torso | 0,7 de alto × 1,0 de largo | placas arredondadas brancas; peito fechado, sem abs |
| Mochila | cilindro 0,45 × 0,7, nas costas | anéis pretos + faixa emissiva laranja. Não jetpack cinematográfico |
| Braços | 2 segmentos, espessura ~0,28 | mãos em bloco/luva, **sem dedos** |
| Pernas | 2 segmentos, mais curtas que os braços | pés cubóides estáveis pro CharacterController |
| Juntas | ombro, cotovelo, joelho | dobradiça **preta** visível. Sem músculo, sem tecido |

Material: branco `#E8ECEC` semibrilho (não cromo, não borracha, não pele). Juntas e viseira: preto fosco. Emissive só na viseira, antena, faixas da mochila e jatos.

O que **não** modelar: corpo 8 cabeças, astronauta, PNG tuber, facecam, logo TikTok, mascote de marca, cabelo, roupa, arma.

## 3 props MVP — regra “prop + VFX”

Gift = mesh que lê em silhueta **mais** partícula. Nunca só ícone flutuando, nunca cutscene, nunca VFX sem âncora física.

| Gift | Prop (mesh) | VFX |
| --- | --- | --- |
| Rosa | rosa chunky (pétalas `#C92130`, folha verde) **e** caixa de madeira com X | pétalas caindo / orbitando a caixa |
| Perfume | âncora baixa (frasco low-poly **ou** esfera) | fumaça volume `#A417F4` — a leitura é a nuvem, não o frasco |
| Confete | caixa de madeira com X | burst de quadradinhos (ciano / magenta / laranja / branco). Não streamer, não PNG do gift TikTok |

A caixa é o obstáculo que o Bot desvia. Pétalas, fumaça e burst são o espetáculo. Mesmo asset de caixa na rosa e no confete; o VFX diferencia.

## Fora disto

Blender e Unity entram no [#7](https://github.com/obrunogonzaga/tiktok-live-climb/issues/7) e no greybox [#2](https://github.com/obrunogonzaga/tiktok-live-climb/issues/2). Este arquivo não gera mesh, textura, nem look novo.
