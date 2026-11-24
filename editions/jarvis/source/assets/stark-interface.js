(() => {
  'use strict';
  function render(state){
    const status=state.status||'',lower=status.toLowerCase();
    const phase=lower.includes('hearing')?'listen':lower.includes('transcrib')?'parse':lower.includes('thinking')?'think':lower.includes('acting')||lower.includes('weather')?'act':lower.includes('response')?'speak':lower.includes('off')?'off':'ready';
    for(const u of units){
      u.el.dataset.voicePhase=phase;
      const text=(key,value)=>{const node=u.root.querySelector('[data-k="'+key+'"]');if(node)node.textContent=value;};
      text('heard-text',state.heard||'Awaiting your voice…');
      text('reactor-state',({listen:'VOICE SIGNAL ACQUIRED',parse:'DECODING SPEECH',think:'PROCESSING REQUEST',act:'EXECUTING REQUEST',speak:'RESPONSE CHANNEL ACTIVE',off:'VOICE CHANNEL OFF',ready:'SYSTEM LINK / READY'})[phase]);
      text('input-channel',state.device?'INPUT / '+state.device:'LOCAL SPEECH CHANNEL');
      text('action-result',state.action||'No desktop actions this session');
      for(const node of u.root.querySelectorAll('.voice-flow span'))node.classList.toggle('active',node.dataset.phase===phase||(phase==='ready'&&node.dataset.phase==='listen'));
    }
  }
  const original=window.setJarvisVoice;
  window.setJarvisVoice=state=>{original(state);render(state);};
  const build=buildUnits;
  buildUnits=function(){build();render(window.jarvisVoice||{});};
})();
