/* Coordinated presentation only. Showcase never posts commands or calls PC tools. */
(() => {
  'use strict';
  const doc=document.documentElement;
  let settings={Mode:'Auto'},packet={},events=[],lastRequest=0,lastReply='',lastActivity=0,stats={},showcaseTimer=null,demoStep=0,lastMode='',alertText='',alertSeverity='normal';
  const reduced=()=>playback.animations!=='Full'||matchMedia('(prefers-reduced-motion: reduce)').matches;
  const text=(node,value)=>{if(node)node.textContent=String(value??'');};
  function attach(u){
    if(u.root.querySelector('.command-deck'))return;
    const deck=document.createElement('section');deck.className='command-deck';deck.setAttribute('aria-label','JARVIS command centre');
    deck.innerHTML='<header><b class="scene-name">AMBIENT</b><span class="scene-link">COORDINATED SYSTEM LINK</span><i class="scene-model">MODELS / CHECKING</i></header><div class="showcase-notice" hidden>SHOWCASE / SIMULATED ACTIVITY / NO APPS CONTROLLED</div><div class="deck-content"><article class="context-card"><label>REQUEST CONTEXT</label><h3></h3><p></p><div class="context-metrics"></div></article><aside class="session-rail"><label>SESSION TIMELINE</label><ol></ol></aside></div><div class="system-attention" hidden></div>';
    u.root.querySelector('.center')?.appendChild(deck);
    const backdrop=document.createElement('div');backdrop.className='command-backdrop';backdrop.setAttribute('aria-hidden','true');u.root.prepend(backdrop);
  }
  function fitDeck(u){
    const deck=u.root.querySelector('.command-deck');if(!deck)return;
    if(u.el.dataset.interface==='SHIELD'){
      const center=u.root.querySelector('.center');if(deck.parentElement!==center)center.appendChild(deck);
      for(const property of ['left','top','bottom','width','transform'])deck.style.removeProperty(property);
      deck.style.maxHeight='none';return;
    }
    const stage=u.root.querySelector('.stage');if(deck.parentElement!==stage)stage.appendChild(deck);
    const root=u.root.getBoundingClientRect(),scale=u.actualScale||1;
    const box=e=>{const r=e.getBoundingClientRect();return {left:(r.left-root.left)/scale,top:(r.top-root.top)/scale,right:(r.right-root.left)/scale,bottom:(r.bottom-root.top)/scale};};
    const p=box(u.root.querySelector('.main>.panel:first-child')),ring=box(u.root.querySelector('.ringwrap'));
    const screen=(u.safeScreens||[]).find(s=>p.left+p.right>=s.x*2&&p.left+p.right<=(s.x+s.w)*2)||{x:0,y:0,w:root.width/scale,h:root.height/scale};
    const limits={left:screen.x+24,top:screen.y+24,right:screen.x+screen.w-24,bottom:screen.y+screen.h-24};
    const obstacles=[...u.root.querySelectorAll('.status-strip,.history-strip,.audio,.brand,.chips,.clockmeta,.main>.panel,.meta-row,.core-caption,.ringwrap,.core-label,.engineering-rail')].filter(e=>getComputedStyle(e).visibility!=='hidden'&&getComputedStyle(e).display!=='none').map(box);
    let best=null;
    for(const width of [...new Set([Math.min(640,screen.w-48),Math.min(440,screen.w-48)])]){
      Object.assign(deck.style,{width:width+'px',maxHeight:'none',transform:'none',bottom:'auto'});
      const height=Math.ceil(deck.scrollHeight)+2;
      const xs=[p.left,p.right+24,ring.left-width-24,ring.right+24,limits.left,limits.right-width];
      const ys=[limits.top,...obstacles.map(r=>r.bottom+20)];
      for(const x of xs)for(const y of ys){
        if(x<limits.left-1||x+width>limits.right+1||y<limits.top||y+height>limits.bottom)continue;
        if(obstacles.some(r=>Math.min(x+width,r.right+12)>Math.max(x,r.left-12)&&Math.min(y+height,r.bottom+12)>Math.max(y,r.top-12)))continue;
        const score=Math.abs(x-p.left)+Math.abs(y-p.bottom-20)+(640-width)*.4;
        if(!best||score<best.score)best={x,y,width,height,score};
      }
    }
    if(best){Object.assign(deck.style,{left:best.x+'px',top:best.y+'px',width:best.width+'px',height:'auto',maxHeight:'none',transform:'none',bottom:'auto'});deck.dataset.fit='full';}
    else{Object.assign(deck.style,{left:limits.left+'px',top:limits.top+'px',width:Math.min(640,screen.w-48)+'px',height:'auto',maxHeight:'none',transform:'none',bottom:'auto'});deck.dataset.fit='fallback';}
  }
  function activeScene(){if(settings.Mode==='Showcase')return 'showcase';if(settings.Mode==='Command')return 'command';if(settings.Mode==='Ambient')return 'ambient';const busy=/hearing|transcrib|thinking|acting|weather|response/.test((packet.status||'').toLowerCase());return busy||performance.now()-lastActivity<22000?'command':'ambient';}
  function render(){
    const scene=activeScene(),context=packet.centre?.context||{kind:'standby',title:'SYSTEM STANDBY',body:'Awaiting your request'},isDemo=scene==='showcase';
    doc.dataset.commandScene=scene;doc.dataset.commandMotion=reduced()?'quiet':'full';
    for(const u of units){
      attach(u);if(playback.paused||u.paused||reduced())u.commandEntrance?.cancel();u.el.dataset.commandScene=scene;u.el.dataset.commandRole=u.el.dataset.interface==='SHIELD'?'support':'lead';
      const deck=u.root.querySelector('.command-deck');if(!deck)continue;
      text(deck.querySelector('.scene-name'),isDemo?'SHOWCASE':scene.toUpperCase());
      text(deck.querySelector('.scene-link'),u.el.dataset.interface==='SHIELD'?'SUPPORTING OPERATIONS / SHARED REQUEST':'J.A.R.V.I.S. / REQUEST CONTROL');
      const models=packet.centre?.models;text(deck.querySelector('.scene-model'),models?(models.setup?.startsWith('Downloading')?models.setup:models.voice+' / '+models.recognition):'LOCAL MODELS / CHECKING');
      deck.querySelector('.showcase-notice').hidden=!isDemo;
      const card=deck.querySelector('.context-card');card.dataset.kind=context.kind||'conversation';
      text(card.querySelector('label'),(context.kind||'context').toUpperCase()+' / '+(isDemo?'SIMULATED':'LIVE SESSION'));
      text(card.querySelector('h3'),context.title);
      text(card.querySelector('p'),context.kind==='system'?`Processor ${Math.round(stats.cpu||0)}% / Memory ${stats.mem?.percent??'--'}% / ${stats.os||'Waiting for telemetry'}`:(packet.reply&&/response/.test(packet.status||'')?packet.reply:context.body));
      const metrics=card.querySelector('.context-metrics');metrics.replaceChildren();for(const value of (context.metrics||[]).slice(0,3)){const tag=document.createElement('span');tag.textContent=value;metrics.append(tag);}
      const list=deck.querySelector('ol');list.replaceChildren();for(const entry of events.slice(-3).reverse()){const li=document.createElement('li'),time=document.createElement('time'),body=document.createElement('span');time.textContent=entry.time+' / '+entry.type;body.textContent=entry.text;li.append(time,body);list.append(li);}
      if(!events.length){const li=document.createElement('li');li.textContent='No requests this session';list.append(li);}
      const notice=deck.querySelector('.system-attention');notice.hidden=!alertText;text(notice,alertText);u.el.dataset.attention=alertSeverity;fitDeck(u);
    }
  }
  function stopDemo(){if(showcaseTimer!==null)clearInterval(showcaseTimer);showcaseTimer=null;}
  function startDemo(){
    stopDemo();demoStep=0;
    const steps=[['listen','Listening / hearing you','Prepare the command centre.','Demonstration request acquired.'],['think','JARVIS / thinking','SYSTEM ANALYSIS','Demonstrating coordinated information panels.'],['act','JARVIS / acting','WORKSPACE SEQUENCE','Simulated task presentation. No applications are being opened.'],['speak','JARVIS / response','COMMAND CENTRE READY','Showcase complete. These are simulated results.']];
    const step=()=>{if(settings.Mode!=='Showcase'||playback.paused||units.every(u=>u.paused))return;const s=steps[demoStep++%steps.length];packet={...packet,status:s[1],reply:s[3],centre:{...packet.centre,context:{kind:'showcase',title:s[2],body:s[3],metrics:['Demonstration only','No PC actions executed']}}};events=[...events,{time:new Date().toLocaleTimeString(),type:'SIMULATED',text:s[3]}].slice(-8);for(const u of units)u.el.dataset.voicePhase=s[0];render();};step();showcaseTimer=setInterval(step,4200);
  }
  const apply=window.applyHudSettings;
  window.applyHudSettings=value=>{apply(value);settings=value?.commandCentre||{Mode:'Auto'};if(settings.Mode!==lastMode){lastMode=settings.Mode;if(settings.Mode==='Showcase')startDemo();else{stopDemo();packet=window.jarvisVoice||{};voice(packet);events=packet.centre?.events||[];}}render();};
  const voice=window.setJarvisVoice;
  window.setJarvisVoice=value=>{
    voice(value);if(settings.Mode==='Showcase')return;packet=value;events=value.centre?.events||events;
    const request=events.filter(e=>e.type==='REQUEST').at(-1);if(request&&request.id!==lastRequest){lastRequest=request.id;lastActivity=performance.now();doc.style.setProperty('--request-epoch',lastActivity);for(const u of units){u.el.dataset.requestId=String(lastRequest);const deck=u.root.querySelector('.command-deck');if(deck&&!reduced()&&!playback.paused&&!u.paused){u.commandEntrance?.cancel();u.commandEntrance=deck.animate([{opacity:.15,filter:'blur(3px)'},{opacity:1,filter:'blur(0)'}],{duration:480,delay:u.el.dataset.interface==='SHIELD'?140:0,easing:'ease-out'});}}}
    if(value.reply!==lastReply){lastReply=value.reply;lastActivity=performance.now();}render();
  };
  const renderTelemetry=renderStats;
  renderStats=data=>{stats=data||{};renderTelemetry(data);const problems=[];if((data.mem?.percent||0)>=90)problems.push('Memory usage above 90%');if((data.gpu?.temp_c||0)>=85)problems.push('GPU temperature above 85 C');for(const disk of data.disks||[])if((disk.percent||0)>=95)problems.push((disk.mount||'Drive')+' nearly full');alertText=problems.join(' / ');alertSeverity=(data.mem?.percent||0)>=97||(data.disks||[]).some(d=>(d.percent||0)>=99)?'critical':alertText?'attention':'normal';render();};
  const setPlayback=window.setHudPlayback;window.setHudPlayback=value=>{setPlayback(value);render();};
  const build=buildUnits;buildUnits=function(){build();render();};
  // Only an idle scene decision; animation stays on the existing capped HUD frame loop/CSS pause rules.
  setInterval(()=>{if(settings.Mode==='Auto')render();},2000);
  window.commandCentrePresentation={scene:activeScene,render,stopShowcase:stopDemo};
})();
