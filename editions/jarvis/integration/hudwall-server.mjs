import {preview} from 'vite';
import {writeFile} from 'node:fs/promises';
const owner=Number(process.argv[2]);const port=Number(process.argv[3]);
if(!Number.isInteger(owner)||!Number.isInteger(port)||port<1024)throw new Error('Missing owner or port');
process.env.HOST='127.0.0.1';process.env.PORT=String(port);process.env.GEV_EMBED_FRAME_ANCESTORS='https://hud.local';
const server=await preview({root:process.cwd(),configFile:'vite.config.js',preview:{host:'127.0.0.1',port,strictPort:true,open:false},plugins:[{name:'hudwall-health',configurePreviewServer(s){s.middlewares.use('/__hudwall_health',(_req,res)=>{res.setHeader('Content-Type','application/json');res.end(JSON.stringify({hudwall:true,pid:process.pid,owner}));});}}]});
console.log('HUDWALL_GODS_EYE_READY '+port);
setInterval(()=>{try{process.kill(owner,0);}catch{server.httpServer.close();process.exit(0);}},2000).unref();
process.on('SIGTERM',()=>{server.httpServer.close();process.exit(0);});
