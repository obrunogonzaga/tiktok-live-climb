# Texturas da Maya

Produção: tool `imagegen` integrado, a partir das referências fornecidas por
Bruno. Os originais em `~/Documents/Maya Prado` não foram modificados.
As texturas abaixo são entradas do trabalho 3D, não evidência de gameplay.

## MayaFace.png

Referências: `rosto.jpg` e `gamer.jpg`.

> Use case: identity-preserve. Asset type: production source texture photograph for a Blender 3D avatar's face, NOT a game screenshot or concept illustration. The provided images show the fictional adult persona Maya Prado, age 27. Preserve this exact facial identity, facial proportions, warm tan complexion, brown eyes, full natural dark brows, fine freckles across cheeks and nose, brown-pink lips. Produce ONE perfectly frontal, straight-on, symmetric camera photograph of her head and neck, from top of scalp to collarbone. Face aimed dead straight, zero head roll/yaw/pitch, eyes looking forward, relaxed closed mouth with very slight friendly expression, lips gently touching; NO smile showing teeth. Hair dark brown, temporarily pulled completely behind the ears and off the forehead to expose hairline, eyebrows, temples, cheek contours and ears. We will build long hair separately in 3D. Neutral gray plain background. Broad flat diffuse cross-polarized photography lighting from all directions, no cast shadows, no rim light, no dramatic highlights, no beauty retouching; retain fine pores and freckles. No clothing visible except a small black tank top edge at the bottom. Face fills most of the square 2048x2048 image, enough margin to see both ears and full scalp. No text, no labels, no split panel, no cartoon, no illustration. This is texture source material, preserve anatomy and identity faithfully.

## MayaHair.png

> Asset type: seamless PBR base-color texture for long dark-brown human hair meshes in a 3D game. Create a square 2048x2048 texture, entirely filled edge-to-edge by dense, fine, naturally wavy parallel hair strands flowing vertically from top to bottom. The hair is very dark chocolate brown with subtle warm chestnut strands, like a brunette woman's long hair, not black ink and not blonde. Individual fine strands visible, tasteful tonal depth. Flat cross-polarized diffuse lighting, no directional shadows or specular highlights baked into the image. Uniform density, no bald patches, no gaps, no scalp, no face, no body, no background, no text. It should tile horizontally without an obvious seam. Slight gentle S waves in the strand flow, continuous vertical direction. This is a material texture, not a hairstyle portrait, not a game screenshot.

O tool entregou ambas em 1254×1254. O tamanho solicitado no prompt não é
apresentado como tamanho efetivamente entregue. O atlas da pele é um bake
Blender de 2048×2048; tecido/normal são mapas de 512×512.

Uma tentativa de textura de hairline com transparência retornou RGB com um
padrão de fundo e foi descartada. A hairline usada pelo modelo tem canal alfa
real produzido pelo gerador de material Blender a partir de `MayaHair.png`.
