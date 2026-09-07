# Registro de validação #15

Evidência de entrega: `final/` e `import.json`. `before/` é a captura da
composição #14 antes da troca. Candidatas intermediárias foram preservadas
localmente em `tmp/issue15-development-evidence/` e não são apresentadas como
resultado final.

Falhas diagnosticadas durante o trabalho:

- Uma passagem inicial de silhueta demorou cerca de 11 s com a simulação
  parada. O validador contava esse custo como falta de progresso; passou a
  usar tempo do jogo para esse critério, mantendo tempo real no relatório.
- O reload de domínio ao entrar em Play zerava a nova flag `visualSafe`.
  Ela agora é inicializada no primeiro Tick, como as demais flags de execução.
- A leitura de bounds de `BakeMesh` aplicava a escala herdada duas vezes.
  A medição agora aplica somente rotação/translação ao resultado já escalado;
  a altura importada confere com Blender × 0,88: 1,529 m.
- A primeira rodada de fixtures expirou enquanto a porta 8765 ainda estava
  ocupada pelo preview Dito. A repetição começou com o Orchestrator confirmado
  e exigiu snapshot novo do Unity; passou com Small=2, Medium=1, Smoke=1.
- Algumas inicializações da cópia de Library registraram uma exceção do
  índice `UnityEditor.Search.SearchDatabase`. Ela não vem do código do jogo;
  os validadores seguintes e o build do player concluíram.

O servidor Dito foi restaurado em 8765. O benchmark foi executado com os testes
Unity e o navegador de QA encerrados. Seu resultado não inclui LIVE Studio.
