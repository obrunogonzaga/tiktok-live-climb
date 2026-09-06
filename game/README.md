# Climb — subida contínua

Unity **6000.6.0f1 / URP** → `Assets/Scenes/Climb.unity` → Game **1080×1920** → **Play**.
O Bot sobe sozinho, a câmera acompanha a hélice e os trechos distantes são
reaproveitados. Queda definitiva reinicia o round; o recorde permanece durante
a execução. Player em **Windowed**, com execução em segundo plano.

`Climb/Rebuild continuous game` recria a cena atual com os assets do estudo.
O menu antigo `Climb/Rebuild tower composition` aponta para o mesmo gerador.
`ReferenceStudy.unity` preserva a referência visual aprovada.

Na raiz do repo, com Node 22:

```sh
npm --prefix orchestrator ci
npm --prefix orchestrator start
```

Abra `http://127.0.0.1:8790/` em 1080×1920 para o Overlay transparente, incluindo
altura e recorde. Rode o fixture com:

```sh
curl -sS -H 'Content-Type: application/json' \
  --data-binary @fixtures/events/gift-rose.json http://127.0.0.1:8765/v1/events
```

Rosa cria Small, Perfume cria Smoke e Confete cria Medium. Objetos expiram em
6 s ou no reset do round. Reenvios são deduplicados; reinicie o Orchestrator
para repetir fixtures do zero. Nenhuma action nova de ajuda foi adicionada.

`Climb/Validate continuous game` verifica progressão e pools. Os comandos para
queda, reposicionamento de origem, gravação e seus limites estão em
[docs/continuous-climb.md](../docs/continuous-climb.md).

[Estudo visual e fontes dos assets](../docs/visual-rebuild.md).
Sem merge automático; a revisão da integração permanece HITL.
