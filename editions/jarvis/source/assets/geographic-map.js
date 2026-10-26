(() => {
 'use strict';
 const views={'World':'0 13.89 1000 402.78','North America':'35 35 365 280','Europe':'465 58 180 150','Asia-Pacific':'690 65 310 310'};
 let selected='World';
 function update(){for(const u of units){const map=u.root.querySelector('.geographic-map');if(!map)continue;map.setAttribute('viewBox',views[selected]);map.setAttribute('preserveAspectRatio','xMidYMid meet');map.dataset.view=selected;const [x,y,w,h]=views[selected].split(' ').map(Number);const band=map.querySelector('.geo-sweep-band');if(band){band.setAttribute('x',x-80);band.setAttribute('y',y);band.setAttribute('height',h);map.style.setProperty('--scan-distance',(w+160)+'px');const gradient=map.querySelector('.geo-sweep-gradient');gradient.id='geo-sweep-'+units.indexOf(u);band.setAttribute('fill','url(#'+gradient.id+')');}map.style.setProperty('--map-scan-duration',(12/playback.speed)+'s');map.style.setProperty('--map-pulse-duration',(4/playback.speed)+'s');const heading=u.root.querySelector('.tactical-heading b');if(heading)heading.textContent='WGS84 / '+selected.toUpperCase()+' / OFFLINE';}}
 const apply=window.applyHudSettings;window.applyHudSettings=function(settings){if(settings.appearance)document.documentElement.dataset.mapMotion=settings.appearance.MapAnimation===false?'Off':'On';if(settings.appearance&&views[settings.appearance.MapView])selected=settings.appearance.MapView;apply(settings);update();};
 const play=window.setHudPlayback;window.setHudPlayback=function(settings){play(settings);update();};
 const build=buildUnits;buildUnits=function(){build();update();};
})();
