// RVM.UI.Tutor — o ÚNICO módulo JS da biblioteca (JS isolation, sem script global).
// Acha, espera e rola até o alvo; observa scroll/resize/tamanho; escuta a ação do passo (clique ou
// campo); prende o foco no balão, trata o teclado do tour e toca a narração do passo.
// Avisa o .NET no máximo uma vez por frame e só quando o retângulo mudou de verdade: no Blazor
// Server cada aviso atravessa o SignalR.

const FOCUSABLE = 'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),' +
  'textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';
const FIELD = 'input,textarea,select';
const SHEET_BREAKPOINT = 480; // o mesmo de Positioner.SheetBreakpoint

export function createHost(dotnet) {
  let target = null, last = null, frame = 0, resize = null, unwatch = null, cancelWait = null;
  let popover = null, trap = null, returnTo = null, generation = 0, actionEl = null;
  let reservedPad = null; // padding-bottom inline do body antes de a faixa reservar espaço (null = nada reservado)
  let voice = null, stopWaiting = null;

  // A pose animada do apresentador só aparece enquanto a voz toca (#60): o atributo no <html> liga o CSS,
  // e o vídeo roda do começo a cada fala e para junto com ela. Menos movimento pedido: nunca.
  // O atributo é da página: vale um TutorHost por página (o layout do app).
  const reduce = matchMedia('(prefers-reduced-motion: reduce)');
  let speaking = false;
  const talk = on => {
    speaking = on && !reduce.matches;
    document.documentElement.toggleAttribute('data-tutor-speaking', speaking);
    for (const v of document.querySelectorAll('video.tutor-presenter--talking')) {
      if (speaking) { v.currentTime = 0; v.play().catch(() => {}); } else v.pause();
    }
  };
  // Vídeo que entra (ou troca de src) no meio da fala — "Mostrar Aly", re-render — começa a rodar ao carregar.
  // Eventos de mídia não sobem; na captura chegam ao documento.
  document.addEventListener('loadeddata', e => {
    if (speaking && e.target.matches?.('video.tutor-presenter--talking')) e.target.play().catch(() => {});
  }, true);
  // Safari toca WebM sem o canal alfa (fundo opaco): lá o vídeo fica escondido e a pose, parada.
  if (/^((?!chrome|chromium|android|crios|fxios|edg).)*safari/i.test(navigator.userAgent))
    document.documentElement.setAttribute('data-tutor-no-alpha-video', '');

  // Narração. Sem gesto do usuário na página (tour aberto sozinho no 1º login) o som não começa:
  // espera o primeiro clique/tecla (pointerup, não pointerdown: no toque a ativação vem ao soltar).
  // Navegador sem userActivation tenta, e a recusa vira a mesma espera.
  const silence = () => {
    stopWaiting?.();
    stopWaiting = null;
    if (voice) {
      voice.pause();
      voice.removeAttribute('src'); // sem isso o mp3 continua baixando
      voice.load();
    }
    voice = null;
    talk(false);
  };
  const afterGesture = play => {
    const go = () => { stop(); play(); };
    const stop = () => {
      removeEventListener('pointerup', go, true);
      removeEventListener('keydown', go, true);
    };
    addEventListener('pointerup', go, true);
    addEventListener('keydown', go, true);
    stopWaiting = stop;
  };
  const speak = url => {
    silence();
    if (!url) return;
    const audio = voice = new Audio(url);
    audio.addEventListener('playing', () => { if (voice === audio) talk(true); });
    for (const e of ['pause', 'ended', 'error']) audio.addEventListener(e, () => { if (voice === audio) talk(false); });
    const play = () => {
      if (voice !== audio) return;
      audio.play().catch(e => { if (e?.name === 'NotAllowedError' && voice === audio) afterGesture(play); });
    };
    if (navigator.userActivation?.hasBeenActive === false) afterGesture(play);
    else play();
  };

  // Página curta: no fim da rolagem o alvo do pé ainda fica atrás da faixa. Reserva a altura dela
  // no fim do documento para a janela poder rolar o alvo para cima; devolvida ao sair da faixa.
  const reserve = px => {
    if (reservedPad === null) reservedPad = document.body.style.paddingBottom;
    document.body.style.paddingBottom = '';
    const base = parseFloat(getComputedStyle(document.body).paddingBottom) || 0;
    document.body.style.paddingBottom = `${base + px}px`;
  };
  const release = () => {
    if (reservedPad === null) return;
    document.body.style.paddingBottom = reservedPad;
    reservedPad = null;
  };

  // Ausente: mede 0x0 (display:none) ou está escondido (visibility:hidden, content-visibility).
  const visible = el => {
    const r = el.getBoundingClientRect();
    if (r.width === 0 && r.height === 0) return false;
    return !el.checkVisibility || el.checkVisibility({ visibilityProperty: true });
  };
  const onScreen = el => {
    const r = el.getBoundingClientRect();
    return r.right > 0 && r.bottom > 0 && r.left < innerWidth && r.top < innerHeight;
  };

  // Oculto (display:none, aba ou gaveta fechada) conta como ausente.
  const find = name => {
    const el = document.querySelector(`[data-tour="${CSS.escape(name)}"]`);
    return el && visible(el) ? el : null;
  };

  // Alvo que aparece depois (dado que carrega, componente que abre): observa o DOM até o prazo.
  const waitFor = (name, ms) => new Promise(resolve => {
    const done = el => { observer.disconnect(); clearTimeout(timer); cancelWait = null; resolve(el); };
    const observer = new MutationObserver(() => { const el = find(name); if (el) done(el); });
    observer.observe(document.body, { childList: true, subtree: true, attributes: true });
    // No prazo, uma última olhada: alvo que ficou visível só por CSS não gera mutação.
    const timer = setTimeout(() => done(find(name)), ms);
    cancelWait = () => done(null);
  });

  const layout = () => {
    const base = { viewportWidth: innerWidth, viewportHeight: innerHeight };
    const r = target?.isConnected && visible(target) ? target.getBoundingClientRect() : null;
    if (!r) return { found: false, x: 0, y: 0, width: 0, height: 0, ...base };
    return { found: true, x: r.x, y: r.y, width: r.width, height: r.height, ...base };
  };

  const same = (a, b) => !!a && !!b && a.found === b.found && a.x === b.x && a.y === b.y &&
    a.width === b.width && a.height === b.height &&
    a.viewportWidth === b.viewportWidth && a.viewportHeight === b.viewportHeight;

  const tick = () => {
    frame = 0;
    const now = layout();
    if (same(now, last)) return;
    last = now;
    dotnet.invokeMethodAsync('OnLayoutChanged', now);
  };

  const schedule = () => { if (!frame) frame = requestAnimationFrame(tick); };

  const unobserve = () => {
    cancelWait?.();
    unwatch?.();
    unwatch = null;
    resize?.disconnect();
    resize = null;
    removeEventListener('scroll', schedule, true);
    removeEventListener('resize', schedule);
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
  };

  const fieldOf = el => el.matches(FIELD) ? el : el.querySelector(FIELD);

  // Passo que espera ação. Escuta no documento, por delegação: se o Blazor trocar o elemento num
  // re-render, a escuta continua valendo. O clique é escutado em captura e SEGUE para o app.
  // Devolve se a ação pode acontecer; se não pode (botão desabilitado, campo já preenchido ou
  // inexistente), o host mostra o "Próximo" em vez de prender o usuário no passo.
  const watch = (name, advance) => {
    actionEl = null;
    if (!target || advance === 'Button') return false;
    const inside = e => e.target instanceof Element && !!e.target.closest(`[data-tour="${CSS.escape(name)}"]`);
    const action = () => dotnet.invokeMethodAsync('OnTargetAction');
    if (advance === 'ClickTarget') {
      if (target.matches(':disabled')) return false;
      const clicked = e => { if (inside(e)) action(); };
      document.addEventListener('click', clicked, true);
      unwatch = () => document.removeEventListener('click', clicked, true);
      actionEl = target;
      return true;
    }
    const field = fieldOf(target);
    if (!field || field.disabled || field.value.trim() !== '') return false;
    // change, não input: avançar na 1ª letra tiraria o foco do campo no meio da digitação.
    const filled = e => { if (inside(e) && e.target.value.trim() !== '') action(); };
    document.addEventListener('change', filled, true);
    unwatch = () => document.removeEventListener('change', filled, true);
    actionEl = field;
    return true;
  };

  const typing = t => t instanceof HTMLElement &&
    (t.isContentEditable || t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT');

  // Tab e Shift+Tab giram dentro do balão (RNF03). No passo que espera ação, o alvo entra no ciclo:
  // quem só usa teclado chega nele e aciona com Enter/Espaço.
  const keepFocus = e => {
    if (!popover?.isConnected) return;
    const action = actionEl?.isConnected ? [actionEl] : [];
    const items = [...action, ...popover.querySelectorAll(FOCUSABLE)];
    if (!items.length) { e.preventDefault(); popover.focus(); return; }
    const first = items[0], end = items[items.length - 1], at = document.activeElement;
    if (!popover.contains(at) && !action.includes(at)) { e.preventDefault(); first.focus(); }
    else if (e.shiftKey && at === first) { e.preventDefault(); end.focus(); }
    else if (!e.shiftKey && at === end) { e.preventDefault(); first.focus(); }
  };

  // Teclado do tour no documento inteiro, em captura: entre um passo e outro o balão sai do DOM e o
  // foco cai no body, e um ESC ali não pode se perder. Setas não navegam quando se está digitando.
  const onKey = e => {
    if (e.key === 'Escape' || ((e.key === 'ArrowRight' || e.key === 'ArrowLeft') && !typing(e.target))) {
      e.preventDefault();
      dotnet.invokeMethodAsync('OnKey', e.key);
      return;
    }
    if (e.key === 'Tab') keepFocus(e);
  };

  // Início do tour/destaque (primeiro locate): guarda quem tinha o foco e liga o teclado já aqui,
  // não no primeiro foco — um ESC no instante em que o balão aparece não pode se perder.
  const arm = () => {
    if (trap) return;
    returnTo = document.activeElement;
    trap = onKey;
    document.addEventListener('keydown', trap, true);
  };

  return {
    // O tour começou numa página que ainda vai abrir: liga o teclado antes de navegar.
    arm,

    // Acha o alvo (esperando até waitMs se ainda não existe), rola até ele, observa e escuta a ação.
    async locate(name, waitMs, advance) {
      arm();
      unobserve();
      const mine = ++generation;
      let found = name ? find(name) : null;
      if (!found && name && waitMs > 0) found = await waitFor(name, waitMs);
      // Superado por outro locate durante a espera: não mexe no alvo do passo novo.
      if (mine !== generation) return { found: false, x: 0, y: 0, width: 0, height: 0, viewportWidth: innerWidth, viewportHeight: innerHeight };
      target = found;
      let armed = false;
      if (target) {
        // Na tela estreita o balão ocupa a parte de baixo: o alvo precisa ficar na metade de cima.
        const narrow = innerWidth < SHEET_BREAKPOINT;
        const r = target.getBoundingClientRect();
        if (r.top < 0 || r.left < 0 || r.right > innerWidth || r.bottom > (narrow ? innerHeight / 2 : innerHeight))
          target.scrollIntoView({ block: narrow ? 'start' : 'center', inline: 'nearest', behavior: 'instant' });
        // Nem rolando (a janela ou o contêiner dele) o alvo chega à tela: gaveta fechada por transform.
        // instant, não o scroll-behavior do app: a rolagem suave ainda não teria andado na medida.
        if (!onScreen(target)) target = null;
      }
      if (target) {
        resize = new ResizeObserver(schedule);
        resize.observe(target);
        armed = watch(name, advance);
      }
      addEventListener('scroll', schedule, { capture: true, passive: true });
      addEventListener('resize', schedule);
      last = layout();
      return { ...last, actionArmed: armed };
    },

    measure(el) {
      const r = el.getBoundingClientRect();
      return { width: r.width, height: r.height };
    },

    // Foca o botão principal do balão; no passo que espera ação, o próprio alvo (campo para digitar,
    // botão para Enter/Espaço).
    focus(el, onTarget, sheet) {
      arm();
      popover = el;
      // Faixa inferior: só agora se sabe a altura dela. Rola o alvo para cima da faixa (scroll-margin
      // vale para qualquer contêiner que role, não só a janela).
      if (sheet && target?.isConnected && el) {
        const h = el.getBoundingClientRect().height;
        if (target.getBoundingClientRect().bottom > innerHeight - h - 8) {
          reserve(h + 8);
          const previous = target.style.scrollMarginBottom;
          target.style.scrollMarginBottom = `${h + 8}px`;
          target.scrollIntoView({ block: 'end', inline: 'nearest' });
          target.style.scrollMarginBottom = previous;
        }
      } else release();
      const goal = (onTarget && actionEl?.isConnected ? actionEl : null) ??
        popover?.querySelector('[data-tutor-autofocus]') ?? popover;
      goal?.focus({ preventScroll: true });
    },

    // Toca o áudio do passo (para o anterior); null só para.
    speak,

    // Fim do tour/destaque: para de observar, cala, solta o teclado e devolve o foco a quem o tinha.
    deactivate() {
      generation++;
      silence();
      unobserve();
      release();
      target = last = popover = actionEl = null;
      if (trap) document.removeEventListener('keydown', trap, true);
      trap = null;
      if (returnTo?.isConnected) returnTo.focus({ preventScroll: true });
      returnTo = null;
    },
  };
}
