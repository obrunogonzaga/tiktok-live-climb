# Overlay 9:16 — issue #4

Na pasta `orchestrator/`, use Node 22 e execute `npm ci && npm start`.
Abra `http://127.0.0.1:8790/` em uma viewport de **1080×1920**. Unity é opcional para conferir a ladder; sem o jogo, o round começa como `ROUND 1 · idle`.

A página é transparente e muda de escala proporcionalmente em viewports menores. As únicas superfícies são:

- Ladder: x 24–300, y 160–720.
- Round: x 320–940, y 160–240; indicador `bridge?` dentro desta caixa.
- Toast: x 200–880, y 760–900.

Sem áudio, comentários, likes, placar ou Bot em HTML. O WS `/overlay` é a fonte de estado; as sete linhas iniciais usam os valores v1 para continuar visíveis mesmo antes da primeira conexão.

POST de `fixtures/events/gift-rose.json` aumenta Rosa em 1 e mostra o toast até `expiresAt`. Ticks intermediários de combo só atualizam a contagem. Reenviar o mesmo fixture é idempotente: reinicie o orchestrator para repetir o teste do zero.

Se o WS cair, a última ladder permanece e `bridge?` aparece. A página tenta reconectar a cada segundo; o toast expira pelo relógio local, mesmo desconectada.
