/* Cinematic JARVIS and S.H.I.E.L.D.: when the HUD starts, JARVIS displays ignite their reactor and SHIELD displays
   authenticate and light up the operations map, while a boot console reports real hardware. Afterwards the reactor
   (or SHIELD's comms trace) moves with JARVIS's actual voice. The host is told when the boot starts and when systems
   are "online", so the spoken greeting lands on cue. */
(() => {
  'use strict';
  const doc = document.documentElement;
  const reduceMotion = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  let ONLINE_MS=3500,END_MS=4400;const CONSOLE_FROM=350,CONSOLE_TO=2700,GRANTED_MS=1300;
  let state = 'pending', bootStart = 0, ticker = null, onlineSent = false;
  let appearance = {}, lastStats = null;
  doc.dataset.boot = 'pending';

  const tellHost = phase => { try { window.chrome?.webview?.postMessage({hud:'boot', phase}); } catch (e) {} };
  const isShield = u => u.el.dataset.interface === 'SHIELD';

  // Keep what the host sends so the boot can honour "Boot sequence at startup" and report real hardware.
  const applySettings = window.applyHudSettings;
  window.applyHudSettings = settings => { if (settings && settings.appearance) appearance = settings.appearance; applySettings(settings); };
  const render = renderStats;
  renderStats = d => { lastStats = d; render(d); };

  // The first real monitor layout means the host has finished setting up the page: boot shortly after, once the
  // playback settings (motion level, pause) have arrived too.
  const setMonitors = window.setHudMonitors;
  window.setHudMonitors = layouts => { setMonitors(layouts); if (state === 'pending') { state = 'armed'; setTimeout(() => startBoot(false), 180); } };
  document.addEventListener('DOMContentLoaded', () => setTimeout(() => { if (state === 'pending') { state = 'armed'; startBoot(false); } }, 1500));
  setTimeout(() => { if (doc.dataset.boot === 'pending') {doc.dataset.boot = 'done';window.stopJarvisMovieIntro?.();} }, 7000); // never leave the HUD hidden

  /** Plays the boot sequence (again). Reduced/Off motion, a paused HUD or the setting turned off skip straight to online. */
  window.hudBoot = replay => startBoot(!!replay);

  function startBoot(replay) {
    try {
      if (state === 'run') return;
      const movie=appearance.MovieIntro!==false&&units.some(u=>!isShield(u));ONLINE_MS=movie?7600:3500;END_MS=movie?8600:4400;
      tellHost('start');
      const skip = playback.paused || playback.animations !== 'Full' || reduceMotion || (!replay && appearance.BootSequence === false);
      if (skip) { finish(); online(); return; }
      if (replay) { doc.dataset.boot = 'reset'; void doc.offsetWidth; }
      state = 'run'; onlineSent = false; bootStart = performance.now();
      for (const u of units) prepare(u);
      doc.dataset.boot = 'run';if(movie)window.playJarvisMovieIntro?.();
      ticker = setInterval(tick, 33);
    } catch (e) { finish(); online(); }
  }

  const make = (cls, html) => Object.assign(document.createElement('div'), {className: cls, innerHTML: html || ''});

  function prepare(u) {
    if (u.paused) return;
    const shield = isShield(u), frame = u.root;
    for (const old of frame.querySelectorAll('.boot-sweep,.boot-console,.boot-shock,.boot-insignia')) old.remove();
    frame.appendChild(make('boot-sweep'));
    const consoleEl = frame.appendChild(make('boot-console'));
    u.boot = {console: consoleEl, lines: [], scrambles: [], script: shield ? SHIELD_LINES : JARVIS_LINES, shield};
    if (shield) {
      // Insignia authenticates in the middle of the operations board, then the map lights up west to east.
      const board = u.root.querySelector('.main>.center');
      if (board) u.boot.insignia = board.appendChild(make('boot-insignia',
        '<svg viewBox="37 176 554 554" aria-hidden="true"><image href="shield-logo.png" width="640" height="960"/></svg><div class="boot-auth">AUTHENTICATING</div>'));
      const map = u.root.querySelector('.geographic-map');
      if (map && !map.dataset.bootTimed) {
        map.dataset.bootTimed = 'true';
        const [vx, , vw] = (map.getAttribute('viewBox') || '0 0 1000 500').split(' ').map(Number);
        for (const country of map.querySelectorAll('.geo-country')) {
          let x = 500;
          try { const box = country.getBBox(); x = box.x + box.width / 2; } catch (e) {}
          country.style.setProperty('--boot-d', (1.25 + Math.max(0, Math.min(1, (x - vx) / vw)) * 1.35).toFixed(2) + 's');
        }
      }
      const heading = u.root.querySelector('.tactical-heading b');
      if (heading) u.boot.heading = {node: heading, text: heading.textContent};
    } else {
      const ringwrap = u.root.querySelector('.ringwrap');
      if (ringwrap) for (const cls of ['boot-shock', 'boot-shock online']) ringwrap.appendChild(make(cls));
    }
    const scramble = (el, delay, dur) => {
      const node = el && [...el.childNodes].find(n => n.nodeType === 3 && n.textContent.trim());
      if (node) u.boot.scrambles.push({node, text: node.textContent, delay, dur});
    };
    scramble(q(u, 'interface-core'), 300, 900);
    scramble(u.root.querySelector('.core-label span'), 450, 900);
    scramble(q(u, 'interface-title'), 1600, 700);
    const panels = u.root.querySelectorAll('.main>.panel h2');
    if (panels[0]) scramble(panels[0], 2350, 700);
    if (panels[1]) scramble(panels[1], 2550, 700);
    if (shield) scramble(u.root.querySelector('.tactical-heading span'), 1100, 900);
  }

  // Boot console: each line's value is read when it starts typing, so telemetry that arrives mid-boot still shows.
  // SHIELD lines stay factual (the map is an offline schematic, nothing is tracked), only the dressing is in-universe.
  const WIDTH = 58;
  const short = (s, n) => { s = String(s || '').replace(/\(R\)|\(TM\)|CPU|Processor/gi, '').replace(/\s+/g, ' ').trim().toUpperCase(); return s.length > n ? s.slice(0, n - 1) + '…' : s; };
  const row = (label, value) => { const v = ' ' + value; return label.padEnd(WIDTH - v.length, '.').slice(0, WIDTH - v.length) + v; };
  const user = () => short(window.HUD_USERNAME || lastStats?.user || 'USER', 20);
  const stats = () => lastStats || {};
  const cpuRow = label => () => row(label + (short(stats().cpu_name, 28) || 'SCANNING'), stats().cores ? stats().cores + 'C/' + stats().threads + 'T' : '--');
  const gpuRow = label => () => row(label + (short(stats().gpu_name, 30) || 'SCANNING'), stats().gpu_vram_gb ? stats().gpu_vram_gb + ' GB' : 'ONLINE');
  const memRow = label => () => row(label, stats().ram_total_gb ? stats().ram_total_gb + ' GB / ' + Math.round(stats().mem?.percent ?? 0) + '%' : 'SCANNING');
  const diskRow = label => () => { const v = (stats().disks || [])[0]; return row(label + (v ? v.mount : '--'), v ? Math.max(0, v.total_gb - v.used_gb).toFixed(0) + ' GB FREE' : 'SCANNING'); };
  const voiceState = () => { const s = (window.jarvisVoice?.status || '').toLowerCase(); return s.startsWith('listening') ? 'LISTENING' : s.includes('off') ? 'OFF' : 'CALIBRATED'; };
  const JARVIS_LINES = [
    () => 'J.A.R.V.I.S. // PERSONAL SYSTEM INTERFACE   BUILD 41',
    () => row('ARC REACTOR CORE', 'IGNITION OK'),
    cpuRow('PROCESSOR / '), gpuRow('GRAPHICS / '), memRow('MEMORY MATRIX'), diskRow('STORAGE / '),
    () => row('TELEMETRY LINK', ONLINE ? 'SYNCHRONIZED' : 'STANDBY'),
    () => row('VOICE CHANNEL', voiceState()),
    () => row('ASSISTANT CORE', (window.jarvisVoice?.ai || 'LOCAL').toUpperCase()),
    () => 'ALL SYSTEMS ONLINE',
  ];
  const SHIELD_LINES = [
    () => 'S.H.I.E.L.D. // STRATEGIC OPERATIONS TERMINAL   BUILD 41',
    () => row('OPERATOR / ' + user(), 'SIGNED IN'),
    () => row('LOCAL NODE / ' + short(stats().hostname || 'THIS PC', 26), 'ONLINE'),
    cpuRow('ASSET / '), gpuRow('ASSET / '), memRow('MEMORY ALLOCATION'),
    () => row('GLOBAL OPERATIONS GRID', 'NATURAL EARTH / OFFLINE'),
    () => row('COMMS CHANNEL', voiceState()),
    () => row('ASSISTANT LINK', (window.jarvisVoice?.ai || 'LOCAL').toUpperCase()),
    () => 'WELCOME BACK, AGENT ' + user(),
  ];

  function tick() {
    const t = performance.now() - bootStart;
    const charge = Math.round(100 * (1 - Math.pow(1 - Math.min(1, t / (ONLINE_MS - 200)), 2.2)));
    for (const u of units) {
      const boot = u.boot;
      if (!boot) continue;
      // console
      const script = boot.script, msPerChar = (CONSOLE_TO - CONSOLE_FROM) / (script.length * WIDTH);
      const typed = Math.max(0, Math.floor((t - CONSOLE_FROM) / msPerChar));
      const lineCount = Math.min(script.length, Math.floor(typed / WIDTH) + (typed > 0 ? 1 : 0));
      while (boot.lines.length < lineCount) boot.lines.push(script[boot.lines.length]());
      boot.console.innerHTML = boot.lines.map((text, i) => {
        const shown = i < lineCount - 1 ? text : text.slice(0, typed - i * WIDTH);
        const parts = shown.length === text.length && /^(.*\.{2,})( .+)$/.exec(shown); // value after the dot leader
        const body = parts ? escape(parts[1]) + '<span class="ok">' + escape(parts[2]) + '</span>' : escape(shown);
        return '<div class="' + (i === script.length - 1 ? 'final' : '') + '">' + (i === 0 ? '' : '&gt; ') + body +
          (i === lineCount - 1 && t < CONSOLE_TO + 300 ? '<span class="caret"></span>' : '') + '</div>';
      }).join('');
      // scrambled headings resolve left to right
      for (const s of boot.scrambles) {
        const p = (t - s.delay) / s.dur;
        if (p >= 1) { if (s.node.textContent !== s.text) s.node.textContent = s.text; continue; }
        if (p < 0) { s.node.textContent = s.text.replace(/\S/g, ' '); continue; }
        const keep = Math.floor(s.text.length * p);
        s.node.textContent = s.text.slice(0, keep) + s.text.slice(keep).replace(/\S/g, () => GLYPHS[(Math.random() * GLYPHS.length) | 0]);
      }
      // status counters: reactor output (JARVIS) or grid calibration (SHIELD)
      if (boot.shield) {
        if (boot.heading) boot.heading.node.textContent = t >= ONLINE_MS ? 'GLOBAL GRID / ONLINE' : 'GRID CALIBRATION / ' + String(charge).padStart(3, '0') + '%';
        const auth = boot.insignia?.querySelector('.boot-auth');
        if (auth && t >= GRANTED_MS && !auth.classList.contains('granted')) { auth.textContent = 'ACCESS GRANTED'; auth.classList.add('granted'); }
      } else {
        const label = q(u, 'reactor-state');
        if (label) label.textContent = t >= ONLINE_MS ? 'ALL SYSTEMS ONLINE' : 'REACTOR OUTPUT / ' + String(charge).padStart(3, '0') + '%';
      }
    }
    if (t >= ONLINE_MS) online();
    if (t >= END_MS) finish();
  }
  const GLYPHS = 'ABCDEF0123456789#/<>[]=+';
  const escape = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

  function online() { if (onlineSent) return; onlineSent = true; tellHost('online'); }

  function finish() {
    if (ticker !== null) clearInterval(ticker);
    ticker = null; state = 'done';
    doc.dataset.boot = 'done';window.stopJarvisMovieIntro?.();
    for (const u of units) {
      if (u.boot) {
        for (const s of u.boot.scrambles) s.node.textContent = s.text;
        if (u.boot.heading) u.boot.heading.node.textContent = u.boot.heading.text;
      }
      u.boot = null;
      for (const el of u.root.querySelectorAll('.boot-sweep,.boot-console,.boot-shock,.boot-insignia')) el.remove();
    }
    if (window.jarvisVoice) window.setJarvisVoice(window.jarvisVoice); // restores the orbital status label
  }

  // ---------------------------------------------------------------- voice
  // The host sends the loudness envelope of each spoken reply (20 ms steps) as playback starts, or "synthetic" for
  // the Windows voice whose audio it can't measure, and "stop" when speech is cut short.
  const voice = {env: null, step: 20, t0: 0, synthetic: false, level: 0, at: 0};
  try {
    window.chrome?.webview?.addEventListener('message', e => {
      const m = e.data;
      if (!m || m.hud !== 'voice' || !m.data) return;
      const d = m.data;
      if (d.stop) { voice.env = null; voice.synthetic = false; return; }
      if (Array.isArray(d.env)) { voice.env = d.env; voice.step = d.step || 20; voice.t0 = performance.now() + (d.latency || 0); voice.synthetic = false; }
      else if (d.synthetic) { voice.env = null; voice.synthetic = true; voice.t0 = performance.now(); }
    });
  } catch (e) {}

  function voiceLevel(now) {
    if (now - voice.at < 4) return voice.level;
    voice.at = now;
    let target = 0;
    if (voice.env) {
      const i = Math.floor((now - voice.t0) / voice.step);
      if (i >= voice.env.length + 10) voice.env = null;
      else if (i >= 0) target = voice.env[Math.min(i, voice.env.length - 1)] || 0;
    } else if (voice.synthetic) {
      if (units.some(u => u.el.dataset.voicePhase === 'speak')) target = Math.max(0, .42 + .38 * Math.sin(now * .019) * Math.sin(now * .0061 + 1.3));
      else voice.synthetic = false;
    }
    voice.level += (target - voice.level) * (target > voice.level ? .55 : .16);
    if (voice.level < .002) voice.level = 0;
    return voice.level;
  }

  function attachVoice(u) {
    const ringwrap = u.root.querySelector('.ringwrap');
    if (ringwrap && !ringwrap.querySelector('.voice-wave')) {
      ringwrap.insertBefore(make('voice-core'), ringwrap.firstChild);
      u.voiceCanvas = ringwrap.insertBefore(Object.assign(document.createElement('canvas'), {className: 'voice-wave'}), ringwrap.firstChild);
    }
    const board = u.root.querySelector('.shield-board');
    if (board && !board.querySelector('.comms-wave'))
      u.commsCanvas = board.appendChild(Object.assign(document.createElement('canvas'), {className: 'comms-wave'}));
  }

  /** Sizes a canvas's backing store to its box in design pixels; re-measured only after a rescale or interface change. */
  function fit(u, cv, key) {
    if (u[key] === u.actualScale + ':' + u.el.dataset.interface) return;
    u[key] = u.actualScale + ':' + u.el.dataset.interface;
    const r = cv.getBoundingClientRect(), s = u.actualScale || 1;
    cv.width = Math.max(1, Math.round(r.width / s)); cv.height = Math.max(1, Math.round(r.height / s));
  }

  function drawVoice(u, level, now) {
    const full = playback.animations === 'Full';
    u.el.style.setProperty('--voice', full ? level.toFixed(3) : '0');
    const shield = isShield(u);
    const active = shield ? u.commsCanvas : u.voiceCanvas, idle = shield ? u.voiceCanvas : u.commsCanvas;
    if (idle && u.drawn === idle) { idle.getContext('2d').clearRect(0, 0, idle.width, idle.height); u.drawn = null; }
    if (!active) return;
    const ctx = active.getContext('2d');
    if (level <= 0 || !full) { if (u.drawn === active) { ctx.clearRect(0, 0, active.width, active.height); u.drawn = null; } return; }
    fit(u, active, shield ? 'commsFit' : 'voiceFit');
    ctx.clearRect(0, 0, active.width, active.height);
    u.drawn = active;
    if (shield) drawComms(u, ctx, active.width, active.height, level, now);
    else drawRadial(u, ctx, active.width, level, now);
  }

  // JARVIS: radial voice bars around the reactor, mirrored left/right like a vocal print.
  function drawRadial(u, ctx, size, level, now) {
    const c = size / 2, R = c * .748, N = 144;
    ctx.beginPath();
    for (let i = 0; i < N; i++) {
      const a = i / N * Math.PI * 2 - Math.PI / 2, m = Math.min(i, N - i);
      const n = .5 + .5 * Math.sin(m * .37 + now * .012) * Math.cos(m * .11 - now * .0075);
      const len = 3 + level * (.25 + .75 * n) * 46, cos = Math.cos(a), sin = Math.sin(a);
      ctx.moveTo(c + cos * R, c + sin * R);
      ctx.lineTo(c + cos * (R + len), c + sin * (R + len));
    }
    ctx.lineCap = 'round';
    ctx.strokeStyle = u.accentRgba((.12 + level * .25).toFixed(3)); ctx.lineWidth = 9; ctx.stroke();
    ctx.strokeStyle = u.accentRgba((.45 + level * .5).toFixed(3)); ctx.lineWidth = 3; ctx.stroke();
    ctx.beginPath(); ctx.arc(c, c, R - 7, 0, Math.PI * 2);
    ctx.strokeStyle = u.secondaryRgba((.2 + level * .6).toFixed(3)); ctx.lineWidth = 1.5; ctx.stroke();
  }

  // SHIELD: a comms oscilloscope trace along the bottom of the operations map, tapering at both ends.
  function drawComms(u, ctx, w, h, level, now) {
    const mid = h / 2, amp = h * .42 * level;
    const trace = phase => {
      ctx.beginPath();
      for (let x = 0; x <= w; x += 3) {
        const p = x / w, taper = Math.sin(Math.PI * p);
        const y = mid + amp * taper * (Math.sin(p * 46 + now * .021 + phase) * .55 + Math.sin(p * 113 - now * .033) * .3 + Math.sin(p * 9 + now * .006) * .15);
        x ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
      }
    };
    trace(0);
    ctx.strokeStyle = 'rgba(159,216,255,' + (.16 + level * .2).toFixed(3) + ')'; ctx.lineWidth = 8; ctx.stroke();
    ctx.strokeStyle = 'rgba(214,236,255,' + (.55 + level * .4).toFixed(3) + ')'; ctx.lineWidth = 2; ctx.stroke();
    trace(2.1);
    ctx.strokeStyle = 'rgba(255,194,122,' + (.25 + level * .45).toFixed(3) + ')'; ctx.lineWidth = 1.2; ctx.stroke();
    ctx.fillStyle = 'rgba(159,216,255,.35)'; ctx.fillRect(0, mid, w, 1);
  }

  // Ride the HUD's own frame loop so voice visuals follow its FPS cap, pause rules and motion level.
  const spectrum = drawSpectrum;
  drawSpectrum = (u, t) => {
    spectrum(u, t);
    if (!u.voiceCanvas && !u.commsCanvas) attachVoice(u);
    const now = performance.now();
    drawVoice(u, voiceLevel(now), now);
  };
  const build = buildUnits;
  buildUnits = function () { build(); for (const u of units) attachVoice(u); if (state === 'run') for (const u of units) if (!u.boot) prepare(u); };
})();
