(() => {
  'use strict';
  const svgNS='http://www.w3.org/2000/svg';let minutes=5,rich=true,next=performance.now()+300000;
  function decorate(){for(const u of units){
    const map=u.root.querySelector('.geographic-map');if(!map)continue;
    let layer=map.querySelector('.map-rich-fx');
    if(!layer){layer=document.createElementNS(svgNS,'g');layer.classList.add('map-rich-fx');layer.setAttribute('aria-hidden','true');map.appendChild(layer);
      const pass=document.createElementNS(svgNS,'path');pass.classList.add('map-horizontal-pass');layer.appendChild(pass);
      const sites=[['CALIFORNIA',-118.78,34.03,-105,30],['NEW MEXICO',-106,34.5,-105,55],['NEW YORK',-74.01,40.71,26,22],['GREENLAND',-42,72,-25,-26],['NORTH ATLANTIC',-35,18,25,30],['EAST AFRICA',30,-1,25,24],['NORTHERN EUROPE',9,58,22,-20]];
      let i=0;for(const [name,lon,lat,dx,dy] of sites){
        const x=(lon+180)/360*1000,y=(90-lat)/180*500,group=document.createElementNS(svgNS,'g');group.classList.add('movie-reference-site');layer.appendChild(group);
        const ring=document.createElementNS(svgNS,'circle');ring.classList.add('map-signal-wave');for(const [k,v] of Object.entries({cx:x,cy:y,r:11}))ring.setAttribute(k,String(v));ring.style.animationDelay=(-i*1.4)+'s';group.appendChild(ring);
        for(const radius of [9,16]){const reticle=document.createElementNS(svgNS,'circle');reticle.classList.add('map-reference-reticle');for(const [k,v] of Object.entries({cx:x,cy:y,r:radius}))reticle.setAttribute(k,String(v));reticle.style.animationDirection=i%2?'reverse':'normal';group.appendChild(reticle);}
        const leader=document.createElementNS(svgNS,'path');leader.classList.add('movie-site-leader');leader.setAttribute('d',`M${x} ${y+18}L${x+dx*.45} ${y+dy}H${x+dx}`);group.appendChild(leader);
        const label=document.createElementNS(svgNS,'text');label.classList.add('movie-site-label');label.setAttribute('x',x+dx);label.setAttribute('y',y+dy-3);label.textContent='ARCHIVE '+String(++i).padStart(2,'0')+' / '+name;group.appendChild(label);
        const detail=document.createElementNS(svgNS,'text');detail.classList.add('movie-site-detail');detail.setAttribute('x',x+dx);detail.setAttribute('y',y+dy+7);detail.textContent=Math.abs(lat).toFixed(1)+(lat<0?' S':' N')+' / '+Math.abs(lon).toFixed(1)+(lon<0?' W':' E');group.appendChild(detail);
      }
      const countries=[...map.querySelectorAll('.geo-country')];for(let j=0;j<12;j++){const country=countries[Math.floor(j*countries.length/12)];if(country){country.classList.add('map-country-scan');country.style.setProperty('--illumination-delay',(-j*1.7)+'s');}}
      const label=document.createElement('div');label.className='map-animation-label';label.textContent='ARCHIVE VISUALIZATION / REFERENCE LOCATIONS / NO LIVE TRACKING';u.root.querySelector('.shield-board')?.appendChild(label);
    }
    const legend=u.root.querySelector('.geo-legend span:last-child');if(legend)legend.textContent=rich?'ARCHIVE REFERENCE SITES / OFFLINE':'REFERENCE CITIES / GEOGRAPHIC POSITIONS';
    const [x,y,w,h]=(map.getAttribute('viewBox')||'0 0 1000 500').split(' ').map(Number);
    layer.querySelector('.map-horizontal-pass').setAttribute('d',`M${x} ${y}H${x+w}`);map.style.setProperty('--vertical-map-distance',h+'px');
  }}
  function eligible(){const status=(window.jarvisVoice?.status||'').toLowerCase();return minutes>0&&document.documentElement.dataset.boot==='done'&&!playback.paused&&playback.animations==='Full'&&!matchMedia('(prefers-reduced-motion: reduce)').matches&&units.length>0&&!units.some(u=>u.paused)&&!/(hearing|transcrib|thinking|acting|weather|response)/.test(status)&&document.documentElement.dataset.commandScene!=='showcase';}
  function tick(now=performance.now()){
    if(!eligible()){next=now+minutes*60000;return false;}
    if(now<next)return false;
    next=now+minutes*60000;window.hudBoot?.(true);return true;
  }
  const apply=window.applyHudSettings;window.applyHudSettings=value=>{apply(value);if(value.appearance){const requested=value.appearance.RepeatMinutes??5;if(requested!==minutes){minutes=requested;next=performance.now()+minutes*60000;}rich=value.appearance.RichMapAnimation!==false;}document.documentElement.dataset.richMap=String(rich);decorate();};
  const build=buildUnits;buildUnits=function(){build();decorate();};
  const play=window.setHudPlayback;window.setHudPlayback=value=>{play(value);decorate();};
  setInterval(()=>tick(),2000);
  window.avengersMotion={tick,eligible,decorate,interval:()=>minutes};
})();
