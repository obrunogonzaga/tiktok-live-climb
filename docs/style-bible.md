# Style bible — pegada visual

Norte para o **Bot** e os 3 props MVP. O concept orienta torre/ambiente/gifts.
Na #15, Bruno substituiu o robô pela persona fictícia **Maya Prado**; os retratos
fornecidos da Maya orientam rosto, cabelo e roupa do personagem.

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

Evitar: sol de meio-dia, skybox diurno, GI de estúdio, bloom infinito e lens flare.
A pele da Maya usa textura e luz URP; SSS caro não é necessário para a escala do jogo.

## Maya — personagem da #15

Decisão explícita de Bruno: usar um avatar da Maya no lugar do robô, com detalhes
e a mesma direção de acabamento da torre aprovada. A proibição anterior de
aparência humana e as proporções de 2,9 cabeças aplicavam-se ao robô e foram
substituídas por esta decisão. **Bot** continua sendo o papel do personagem
autônomo; não implica uma aparência robótica.

Referências da persona: `rosto.jpg` e `gamer.jpg` fornecidos em `~/Documents/Maya Prado`.
Somente os assets visuais necessários entram no projeto; o cadastro da persona
não faz parte do jogo. A textura frontal em `game/Assets/Art/Maya/Textures/MayaFace.png`
é material de autoria gerado a partir desses retratos, nunca evidência de runtime.

- Adulta fictícia, rosto oval, olhos castanhos, sobrancelhas marcadas, sardas sutis
  e cabelo castanho longo com mechas e volume. Preservar esses sinais em 3/4.
- Proporções humanas semirrealistas; regata preta, calça cargo grafite, botas
  escuras, costuras, cadarços, bolso e o pequeno colar dourado da referência.
- Malha facial com relevo de nariz, lábios, mandíbula e crânio; cabelo com
  geometria lateral/traseira. Não usar retrato em billboard como personagem.
- Bruno delegou a escolha do acabamento ao melhor resultado visual. Usar **até
  12.000 triângulos no avatar principal** e **menos de 4.000 no LOD distante**,
  com desempenho medido. O teto anterior foi definido para o robô. Registrar
  corpo, rosto, cabelo e acessórios em cada nível, sem omitir peças.
- Rig genérico simples para deformar os membros. Sem Mixamo, dependência de
  retarget humanoide, IA de movimento ou alteração do CharacterController.
- Preservar câmera, hélice, escala do percurso e posição dos pés. Ajustar a escala
  visual para a leitura próxima de 200 px e conferir face/cabelo em tamanho de celular.
- Materiais de pele/tecido/cabelo recebem a luz noturna existente. Neon continua
  nos apoios da torre; evitar transformar pele ou roupa em emissores.

O robô e `ReferenceStudy.unity` são a referência histórica aprovada da #14.
Não reconstruir a Maya a partir das antigas regras de capacete/viseira/antena.

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
