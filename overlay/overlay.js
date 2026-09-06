(() => {
  const canvas = document.querySelector('#overlay');
  const list = document.querySelector('#gifts');
  const roundLabel = document.querySelector('#round-label');
  const bridge = document.querySelector('#bridge');
  const toast = document.querySelector('#toast');
  const toastText = document.querySelector('#toast-text');
  const icons = { rose: '🌹', finger_heart: '💕', perfume: '💄', confetti: '🎉', galaxy: '🌌', sports_car: '🚙', lion: '🦁' };
  const integer = new Intl.NumberFormat('pt-BR');
  const compact = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 });
  const statuses = new Set(['idle', 'climbing', 'fell', 'summit']);
  let lastState = null;
  let disconnected = true;
  let toastTimer;
  let retryTimer;
  let stopped = false;
  let socket;

  function fit() {
    canvas.style.transform = `scale(${Math.min(innerWidth / 1080, innerHeight / 1920)})`;
  }

  function valid(state) {
    return state?.schemaVersion === 1 && Array.isArray(state.ladder) && state.ladder.length === 7 &&
      state.ladder.every(g => typeof g.slug === 'string' && typeof g.labelPt === 'string' &&
        Number.isSafeInteger(g.coins) && g.coins >= 0 && Number.isSafeInteger(g.countSession) && g.countSession >= 0) &&
      Number.isSafeInteger(state.round?.index) && state.round.index >= 1 && statuses.has(state.round.status) &&
      typeof state.bridge?.connected === 'boolean' && (state.toast === null ||
        (typeof state.toast?.text === 'string' && Array.from(state.toast.text).length <= 80 && Number.isFinite(Date.parse(state.toast.expiresAt))));
  }

  function refreshBridge() {
    bridge.hidden = !disconnected && lastState?.bridge.connected === true;
  }

  function refreshToast() {
    clearTimeout(toastTimer);
    const current = lastState?.toast;
    const remaining = current ? Date.parse(current.expiresAt) - Date.now() : 0;
    toast.hidden = remaining <= 0;
    if (remaining <= 0) { toastText.textContent = ''; return; }
    toastText.textContent = current.text;
    toastText.style.fontSize = Array.from(current.text).length > 40 ? "22px" : "28px";
    toastTimer = setTimeout(refreshToast, Math.min(remaining, 2147483647));
  }

  function render(state) {
    lastState = state;
    const rows = state.ladder.map(gift => {
      const row = document.createElement('li');
      row.dataset.slug = gift.slug;
      const icon = document.createElement('span');
      icon.className = 'icon';
      icon.setAttribute('aria-hidden', 'true');
      icon.textContent = icons[gift.slug] ?? '🎁';
      const description = document.createElement('div');
      const label = document.createElement('strong');
      label.textContent = gift.labelPt;
      const coins = document.createElement('small');
      coins.textContent = `${integer.format(gift.coins)} ${gift.coins === 1 ? 'moeda' : 'moedas'}`;
      description.append(label, coins);
      const count = document.createElement('span');
      count.className = 'count';
      count.textContent = `×${gift.countSession < 1000 ? integer.format(gift.countSession) : compact.format(gift.countSession)}`;
      count.setAttribute('aria-label', `${integer.format(gift.countSession)} na sessão`);
      count.title = integer.format(gift.countSession);
      row.append(icon, description, count);
      return row;
    });
    list.replaceChildren(...rows);
    roundLabel.textContent = `ROUND ${integer.format(state.round.index)} · ${state.round.status}`;
    refreshBridge();
    refreshToast();
  }

  function connect() {
    if (stopped) return;
    const connection = socket = new WebSocket('ws://127.0.0.1:8766/overlay');
    connection.addEventListener('open', () => { disconnected = false; refreshBridge(); });
    connection.addEventListener('message', event => {
      let state;
      try { state = JSON.parse(event.data); } catch { return; }
      if (valid(state)) render(state);
    });
    connection.addEventListener('close', () => {
      disconnected = true;
      refreshBridge();
      if (!stopped) retryTimer = setTimeout(connect, 1000);
    });
    connection.addEventListener('error', () => connection.close());
  }

  addEventListener('resize', fit);
  addEventListener('pageshow', () => { if (stopped) { stopped = false; connect(); } refreshToast(); });
  addEventListener('pagehide', () => { stopped = true; clearTimeout(retryTimer); clearTimeout(toastTimer); socket?.close(); });
  document.addEventListener('visibilitychange', refreshToast);
  fit();
  connect();
})();
