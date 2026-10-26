(() => {
  'use strict';
  const owned=new WeakMap();
  function clear(u){for(const element of owned.get(u)||[])for(const property of ['position','left','top','width','height','max-height'])element.style.removeProperty(property);owned.delete(u);u.el.style.removeProperty('--core-scale');u.el.dataset.bezelSafe='false';}
  function arrange(u){
    clear(u);
    const layout=u.layout,screens=layout.screens;
    if(!JARVIS_STATIC||layout.id!=='span'||layout.bezelSafe===false||!Array.isArray(screens)||screens.length<2)return;
    const sx=parseFloat(u.root.style.width)/layout.width,sy=parseFloat(u.root.style.height)/layout.height;
    const rects=screens.filter(s=>s.width>0&&s.height>0).map(s=>({...s,x:s.x*sx,y:s.y*sy,w:s.width*sx,h:s.height*sy}));
    if(rects.length<2)return;
    const core=rects.find(s=>s.primary)||rects[0];
    const auxiliary=rects.filter(s=>s!==core).sort((a,b)=>b.w*b.h-a.w*a.h)[0];
    u.el.dataset.bezelSafe='true';const changed=[];owned.set(u,changed);
    function place(selector,screen,x,y,w,h){const element=u.root.querySelector(selector);if(!element)return;changed.push(element);Object.assign(element.style,{position:'absolute',left:(screen.x+x)+'px',top:(screen.y+y)+'px',width:w+'px'});if(h!==null)element.style.height=h+'px';}
    const margin=48,content=core.w-margin*2,auxWidth=auxiliary.w-margin*2;
    place('.brand',core,margin,54,content,null);
    place('.chips',auxiliary,margin,54,Math.max(200,auxWidth-260),null);
    place('.clockmeta',auxiliary,auxiliary.w-240,54,192,null);
    u.el.style.setProperty('--core-scale',String(Math.min(1,(core.h-470)/760,(core.w-96)/650)));
    place('.center',core,margin,140,content,Math.max(300,core.h-470));
    const panelWidth=Math.min(680,(auxWidth-32)/2),panelHeight=Math.min(760,auxiliary.h-580);
    place('.main > .panel:first-child',auxiliary,margin,180,panelWidth,panelHeight);
    place('.main > .panel:last-child',auxiliary,auxiliary.w-margin-panelWidth,180,panelWidth,panelHeight);
    place('.status-strip',auxiliary,margin,auxiliary.h-350,auxWidth,48);
    place('.history-strip',auxiliary,margin,auxiliary.h-260,auxWidth,110);
    place('.audio',core,margin,core.h-280,content,230);
    // Expose physical rectangles for geometric regression checks, independent of screen order.
    u.safeScreens=rects;
  }
  const original=rescale;
  rescale=function(){original();for(const u of units)arrange(u);};
})();
