import { createOrchestrator } from './orchestrator.js';

const orchestrator = createOrchestrator();

await orchestrator.start();
console.info('orchestrator listening on http://127.0.0.1:8765 and ws://127.0.0.1:8766');

let closing = false;
async function close() {
  if (closing) {
    return;
  }
  closing = true;
  await orchestrator.close();
}

process.once('SIGINT', close);
process.once('SIGTERM', close);
