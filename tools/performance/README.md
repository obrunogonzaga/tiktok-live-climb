# Medição do player macOS

Harness usado para separar a renderização real do custo de PNG/ReadPixels do
editor. Não faz parte da cena distribuída: copie os dois `.cs` para uma cópia
isolada do projeto (`Assets/Performance/` e `Assets/Editor/`, respectivamente).

Com Unity 6000.6.0f1, execute `-batchmode -executeMethod ContinuousPerformanceBuild.Run -quit`.
O builder cria somente na cópia a cena `Assets/Performance/PerformanceProbe.unity`
e o player `Builds/ContinuousProbe.app`, em modo Development.

Execute o binário de `ContinuousProbe.app/Contents/MacOS/` **sem `-batchmode`**
(por exemplo com `-screen-width 540 -screen-height 960 -screen-fullscreen 0`).
O modo batch pode omitir renderização e gerar FPS fictício; não serve para esta medição.
A câmera renderiza continuamente para um RenderTexture 1080×1920. Após 3 s de
aquecimento, mede 15 s sem ler pixels nem gravar imagens por frame e encerra o
player. O relatório fica em `/tmp/continuous-player-performance.json`.

O VSync fica desligado e não há limite artificial de FPS nesse player de teste.
Reportar hardware, resolução, duração e o fato de ser um build Development;
não extrapolar o resultado para outras máquinas ou para o LIVE Studio.
