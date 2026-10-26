(() => {
 'use strict';let options={enabled:false,url:'',preset:'Global'},revision=0;
 const presets={Global:['flights','satellites','earthquakes'],'Air traffic':['flights'],Space:['satellites'],Earthquakes:['earthquakes']};
 function camera(){return options.focus==='United States'?{lat:39,lon:-98,altitude_m:6500000,heading_deg:0,pitch_deg:-90}:{lat:20,lon:-25,altitude_m:20000000,heading_deg:0,pitch_deg:-90};}
 function playbackFor(u){return {type:'hudwall:playback',paused:!!(playback.paused||u.paused),fps:Math.min(30,playback.fps||30)};}
 function send(u){const f=u.godsEyeFrame;if(!f||!u.godsEyeReady)return;const origin=new URL(f.src).origin;f.contentWindow.postMessage(playbackFor(u),origin);if(u.godsEyeRevision===revision)return;u.godsEyeRevision=revision;f.contentWindow.postMessage({type:'gev:view',id:revision,view:{camera:camera(),layers:presets[options.preset]||presets.Global,style:'normal',map:'esri-imagery',annotations:[]}},origin);}
 function clear(u){clearTimeout(u.godsEyeTimeout);u.godsEyeFrame?.remove();u.godsEyeLabel?.remove();u.godsEyeFrame=null;u.godsEyeLabel=null;u.godsEyeSource=null;u.godsEyeReady=false;u.el.dataset.godsEye='false';}
 function update(){for(const u of units){
  const active=options.enabled&&u.el.dataset.interface==='SHIELD';
  if(!active){if(u.godsEyeFrame||u.godsEyeLabel)clear(u);continue;}
  const board=u.root.querySelector('.shield-board');if(!board)continue;
  if(!u.godsEyeLabel){const label=document.createElement('div');label.className='gods-eye-label';u.godsEyeLabel=label;board.append(label);}
  if(!options.url){if(u.godsEyeFrame){u.godsEyeFrame.remove();u.godsEyeFrame=null;u.godsEyeReady=false;}u.el.dataset.godsEye='false';u.godsEyeLabel.textContent=options.status||'Starting local globe / archive atlas available';continue;}
  if(u.godsEyeSource!==options.url){clear(u);u.godsEyeSource=options.url;u.godsEyeRevision=-1;const label=document.createElement('div');label.className='gods-eye-label';u.godsEyeLabel=label;board.append(label);const frame=document.createElement('iframe');frame.className='gods-eye-frame';frame.title="God's Eye View / public geospatial data";frame.setAttribute('sandbox','allow-scripts allow-same-origin allow-forms');frame.referrerPolicy='no-referrer';const c=camera();frame.src=options.url+'/?embed=1#v=2&lat='+c.lat+'&lon='+c.lon+'&alt='+c.altitude_m+'&heading=0&pitch=-90&roll=0&map=esri-imagery';u.godsEyeFrame=frame;board.append(frame);u.el.dataset.godsEye='loading';u.godsEyeTimeout=setTimeout(()=>{if(!u.godsEyeReady){frame.remove();u.godsEyeFrame=null;u.el.dataset.godsEye='false';u.godsEyeLabel.textContent='Globe not ready / archive atlas fallback / retry from SHIELD menu';}},90000);}
  if(u.godsEyeReady){u.godsEyeLabel.textContent="GOD'S EYE VIEW / "+options.preset.toUpperCase()+' / '+(options.focus||'World').toUpperCase()+' / PUBLIC DATA / NETWORK REQUIRED';send(u);}else if(u.godsEyeFrame)u.godsEyeLabel.textContent='Loading 3D globe / archive atlas stays available';
 }}
 addEventListener('message',e=>{for(const u of units){if(e.source!==u.godsEyeFrame?.contentWindow||e.origin!==new URL(options.url||'http://localhost').origin)continue;if(e.data?.type==='gev:ready'){clearTimeout(u.godsEyeTimeout);u.godsEyeReady=true;u.el.dataset.godsEye='ready';update();}else if(e.data?.type==='hudwall:playback-applied'){u.godsEyePlayback=e.data;}else if(e.data?.type==='gev:view-applied'&&e.data.ok===false){u.godsEyeLabel.textContent='Globe ready / some public layers unavailable';}}});
 const apply=window.applyHudSettings;window.applyHudSettings=value=>{apply(value);if(value.godsEye){const next=value.godsEye;if(next.preset!==options.preset||next.focus!==options.focus)revision++;options=next;}update();};
 const build=buildUnits;buildUnits=function(){for(const u of units)clear(u);build();update();};
 const play=window.setHudPlayback;window.setHudPlayback=value=>{play(value);update();};
 window.hudGodsEye={update};
})();
