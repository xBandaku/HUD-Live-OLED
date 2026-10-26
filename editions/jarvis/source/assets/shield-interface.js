(() => {
 'use strict';
 function update(){for(const u of units){const selected=['JARVIS','SHIELD'].includes(u.layout.interfaceStyle)?u.layout.interfaceStyle:(window.hudInterface||'JARVIS');const shield=selected==='SHIELD';u.el.dataset.interface=selected;const pattern=u.root.querySelector('.tactical-map pattern');if(pattern){pattern.id='shield-grid-'+units.indexOf(u);u.root.querySelector('.tactical-map>rect').setAttribute('fill','url(#'+pattern.id+')');}for(const key of ['interface-title','interface-core']){const node=q(u,key);if(node)node.textContent=shield?'S.H.I.E.L.D.':'J.A.R.V.I.S.';}const subtitle=u.root.querySelector('.core-label span');if(subtitle)subtitle.textContent=shield?'STRATEGIC OPERATIONS / COMMAND CENTRE':'PERSONAL ASSISTANT / COMMAND CENTRE';const headings=u.root.querySelectorAll('.main>.panel h2');if(headings[0])headings[0].firstChild.nodeValue=shield?'Asset inventory ':'Hardware matrix ';if(headings[1])headings[1].firstChild.nodeValue=shield?'Resource allocation ':'System telemetry ';const audio=u.root.querySelector('.audio .lbl');if(audio)audio.textContent=shield?'COMMUNICATIONS / AUDIO LINK':'Audio Spectrum';}}
 function readings(){for(const u of units){const put=(key,value)=>{const node=q(u,key);if(node)node.textContent=value;};put('shield-ai',q(u,'ai-state').textContent);put('shield-voice',q(u,'voice-chip').textContent);put('shield-network',q(u,'chip-net').textContent);}}
 const stats=renderStats;renderStats=function(data){stats(data);readings();};
 const voice=window.setJarvisVoice;window.setJarvisVoice=function(state){voice(state);readings();};
 const apply=window.applyHudSettings;window.applyHudSettings=function(settings){apply(settings);update();};
 const build=buildUnits;buildUnits=function(){build();update();readings();};
})();
