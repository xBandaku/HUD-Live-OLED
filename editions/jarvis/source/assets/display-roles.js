(() => {
  'use strict';let packet={},stats={},taskbar={Enabled:false},depth=true;
  function set(e,value){if(e)e.textContent=String(value??'--');}
  function roles(){
    const split=units.some(u=>u.el.dataset.interface==='JARVIS')&&units.some(u=>u.el.dataset.interface==='SHIELD');document.documentElement.dataset.splitRoles=String(split);document.documentElement.dataset.atlasDepth=String(depth);
    for(const u of units){
      const jarvis=u.el.dataset.interface==='JARVIS',ring=u.root.querySelector('.ringwrap');
      if(!u.root.querySelector('.jarvis-emblem')){
        const image=document.createElement('img');image.className='jarvis-emblem';image.src='jarvis-logo.png';image.alt='JARVIS emblem supplied by the user';ring.appendChild(image);
        const brand=image.cloneNode();brand.className='jarvis-brand-emblem';u.root.querySelector('.mark')?.appendChild(brand);
        const movie=document.createElement('video');movie.className='jarvis-movie-intro';movie.src='jarvis-intro.mp4';movie.muted=true;movie.playsInline=true;movie.preload='metadata';ring.appendChild(movie);
      }
      const clock=u.root.querySelector('.clock');let clockHolder=u.root.querySelector('.role-clock-holder');
      if(!clockHolder){clockHolder=document.createElement('div');clockHolder.className='role-clock-holder';u.root.querySelector('.main>.panel:last-child h2').after(clockHolder);}
      if(jarvis&&clock.parentElement!==clockHolder)clockHolder.appendChild(clock);else if(!jarvis&&clock.parentElement!==ring)ring.appendChild(clock);
      u.root.style.setProperty('--hud-taskbar-inset',taskbar.Enabled?Math.ceil(((taskbar.reserved||[]).find(b=>b.id===u.id)?.height||Math.max(0,...(taskbar.reserved||[]).map(b=>b.height))||72)/(u.actualScale||1)+12)+'px':'0px');
      const panels=u.root.querySelectorAll('.main>.panel');
      for(const [index,panel] of [...panels].entries()){
        let extra=panel.querySelector('.operations-content');if(!extra){extra=document.createElement('div');extra.className='operations-content';panel.appendChild(extra);}
        if(split&&!jarvis){for(const child of panel.children)if(child!==extra)child.classList.add('role-original');extra.hidden=false;
          if(index===0){extra.replaceChildren();const h=document.createElement('h2');h.textContent='SESSION ACTIVITY / ACTUAL EVENTS';extra.append(h);const entries=(packet.centre?.events||[]).slice(-6).reverse();for(const entry of entries){const row=document.createElement('div');row.className='ops-event';const time=document.createElement('small'),text=document.createElement('p');time.textContent=entry.time+' / '+entry.type;text.textContent=entry.text;row.append(time,text);extra.append(row);}if(!entries.length){const p=document.createElement('p');p.textContent='No requests this session';extra.append(p);}}
          else{extra.replaceChildren();const h=document.createElement('h2');h.textContent='NETWORK / STORAGE';extra.append(h);for(const [label,value] of [['DOWNLOAD',stats.net?.down_mbps===undefined?'--':stats.net.down_mbps+' Mbps'],['UPLOAD',stats.net?.up_mbps===undefined?'--':stats.net.up_mbps+' Mbps'],...(stats.disks||[]).slice(0,6).map(d=>[d.mount,Number.isFinite(d.total_gb-d.used_gb)?Math.max(0,d.total_gb-d.used_gb).toFixed(1)+' GB FREE / '+(d.percent??'--')+'% USED':'--'])]){const row=document.createElement('div');row.className='ops-reading';const k=document.createElement('small'),v=document.createElement('strong');k.textContent=label;v.textContent=value;row.append(k,v);extra.append(row);}}
        }else{for(const child of panel.children)child.classList.remove('role-original');extra.hidden=true;}
      }
      const deck=u.root.querySelector('.command-deck');if(deck)deck.hidden=split&&!jarvis;
      const timeline=u.root.querySelector('.session-rail');if(timeline)timeline.hidden=split&&jarvis;
      const heading=u.root.querySelector('.tactical-heading b');if(split&&!jarvis&&heading)heading.textContent='ARCHIVE ATLAS / '+(u.root.querySelector('.geographic-map')?.dataset.view||'WORLD').toUpperCase();
      decorateAtlas(u);
    }
  }
  function decorateAtlas(u){
    const map=u.root.querySelector('.geographic-map');if(!map||map.querySelector('.atlas-dots'))return;
    const ns='http://www.w3.org/2000/svg';let defs=map.querySelector('defs');if(!defs){defs=document.createElementNS(ns,'defs');map.prepend(defs);}const pattern=document.createElementNS(ns,'pattern');pattern.classList.add('atlas-dots');const id='atlas-dots-'+units.indexOf(u);pattern.id=id;pattern.setAttribute('width','4');pattern.setAttribute('height','4');pattern.setAttribute('patternUnits','userSpaceOnUse');const dot=document.createElementNS(ns,'circle');dot.setAttribute('cx','1');dot.setAttribute('cy','1');dot.setAttribute('r','.7');dot.setAttribute('fill','#82c5e3');pattern.append(dot);defs.append(pattern);map.style.setProperty('--atlas-fill','url(#'+id+')');
    const layer=document.createElementNS(ns,'g');layer.classList.add('atlas-links');layer.setAttribute('aria-hidden','true');for(const [a,b] of [[[170,155],[295,137]],[[295,137],[390,88]],[[390,88],[525,89]],[[525,89],[585,253]],[[170,155],[585,253]]]){const path=document.createElementNS(ns,'path');path.setAttribute('d',`M${a[0]} ${a[1]}Q${(a[0]+b[0])/2} ${Math.min(a[1],b[1])-55} ${b[0]} ${b[1]}`);layer.append(path);}map.append(layer);
    const label=u.root.querySelector('.map-animation-label');if(label)label.textContent='ARCHIVE ATLAS / ILLUSTRATIVE LINKS / NOT LIVE TRACKING';
  }
  window.playJarvisMovieIntro=()=>{for(const u of units)if(u.el.dataset.interface==='JARVIS'&&!u.paused){const v=u.root.querySelector('.jarvis-movie-intro');if(v){u.el.dataset.movieIntro='true';v.currentTime=0;v.play().catch(()=>{u.el.dataset.movieIntro='false';});}}};
  window.stopJarvisMovieIntro=()=>{for(const u of units){u.el.dataset.movieIntro='false';u.root.querySelector('.jarvis-movie-intro')?.pause();}};
  const apply=window.applyHudSettings;window.applyHudSettings=value=>{apply(value);if(value.taskbar)taskbar=value.taskbar;if(value.appearance)depth=value.appearance.MapDepth!==false;roles();window.commandCentrePresentation?.render();};
  const build=buildUnits;buildUnits=function(){build();roles();window.commandCentrePresentation?.render();};
  const voice=window.setJarvisVoice;window.setJarvisVoice=value=>{voice(value);packet=value;roles();};
  const telemetry=renderStats;renderStats=data=>{telemetry(data);stats=data||{};roles();};
  matchMedia('(prefers-reduced-motion: reduce)').addEventListener('change',e=>{if(e.matches)window.stopJarvisMovieIntro();});
  const play=window.setHudPlayback;window.setHudPlayback=value=>{play(value);if(playback.paused||playback.animations!=='Full')window.stopJarvisMovieIntro();for(const u of units)if(u.paused){u.el.dataset.movieIntro='false';u.root.querySelector('video')?.pause();}roles();};
})();
