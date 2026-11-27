'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path'),crypto=require('node:crypto'),{spawn}=require('node:child_process');
const root=fs.mkdtempSync(path.join(os.tmpdir(),'cf-social-')),port=49373,secret=crypto.randomBytes(32).toString('hex');let child,checks=[];
const pause=ms=>new Promise(r=>setTimeout(r,ms));
function check(value,name){assert(value,name);checks.push(name);}
async function call(route,data,auth=secret){const response=await fetch('http://127.0.0.1:'+port+route,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+auth},body:JSON.stringify(data)});return{status:response.status,...await response.json()};}
async function start(){child=spawn(process.execPath,[path.join(__dirname,'server_profiles.cjs')],{env:{...process.env,CF_PROFILE_FILE:path.join(root,'profiles.json'),CF_PROFILE_SECRET:secret,CF_PROFILE_PORT:String(port)},stdio:['ignore','pipe','pipe']});await new Promise((resolve,reject)=>{child.stdout.once('data',resolve);child.once('exit',code=>reject(Error('exit '+code)));child.once('error',reject);});}
async function stop(){const p=child;if(!p)return;child=null;await new Promise(resolve=>{p.once('exit',resolve);p.kill();});}
const social=(token,action,extra={})=>call('/social',{token,action,...extra});
(async()=>{try{
 await start();check((await call('/social',{},'bad')).status===403,'Loopback profile API requires server credential');
 const accounts=[];for(const name of ['SocialAlpha','SocialBeta','SocialGamma']){const p=await call('/auth',{action:'register',name,password:'LocalTest!2026'});check(p.ok,'Register isolated '+name);accounts.push(p);}
 const [a,b,c]=accounts;const peer=b.profile.id,aid=a.profile.id;
 check(!(await social('', 'state')).ok,'Unauthenticated social request rejected');
 const strangerNote=await social(c.token,'send',{peer:aid,kind:'text',text:'Первое сообщение',nonce:crypto.randomUUID()});
 check(strangerNote.ok,'Signed-in players can DM before accepting a friend request');
 check((await social(a.token,'search',{query:'soc'})).found.length===2,'Search excludes current account');
 const safe=(await social(a.token,'search',{query:'SocialBeta'})).found[0];check(!('password'in safe)&&!('salt'in safe)&&!('login'in safe),'Search exposes only public profile data');
 check(!(await social(a.token,'request',{peer:aid})).ok,'Cannot friend self');
 await social(a.token,'request',{peer});await social(a.token,'request',{peer});
 check((await social(b.token,'state')).requests.length===1,'Duplicate requests idempotent');
 check(!(await social(c.token,'accept',{peer:aid})).ok,'Third party cannot accept request');
 await social(b.token,'accept',{peer:aid});check((await social(a.token,'state')).friends[0].id===peer,'Acceptance creates mutual friendship');
 check((await social(b.token,'state')).friends[0].id===aid,'Reverse friendship visible');
 const nonce=crypto.randomUUID();const sent=await social(a.token,'send',{peer,kind:'text',text:'Привет, друг!',nonce});check(sent.ok,'Friend DM accepted');
 check((await social(a.token,'send',{peer,kind:'text',text:'Привет, друг!',nonce})).sent.id===sent.sent.id,'Message retry deduplicated by nonce');
 check((await social(b.token,'state')).conversations[0].unread===1,'Recipient unread count');
 check((await social(c.token,'history',{peer:aid})).messages.every(row=>row.id!==sent.sent.id),'Third party cannot read another conversation');
 let history=await social(b.token,'history',{peer:aid});check(history.messages[0].text==='Привет, друг!','Recipient receives actual text');
 await social(b.token,'history',{peer:aid,read:history.messages[0].id});check((await social(b.token,'state')).conversations[0].unread===0,'Explicit read receipt clears unread');
 await pause(710);check(!(await social(a.token,'send',{peer,kind:'sticker',sticker:'../../secret',nonce:crypto.randomUUID()})).ok,'Unknown sticker denied');
 check((await social(a.token,'send',{peer,kind:'sticker',sticker:'orc',nonce:crypto.randomUUID()})).ok,'Original classic sticker accepted');
 await pause(710);const packs=JSON.parse(fs.readFileSync(path.join(__dirname,'match_emotes.json')));const native=packs[0].items[0].id;
 check((await social(a.token,'send',{peer,kind:'sticker',sticker:native,nonce:crypto.randomUUID()})).ok,'Original race sticker accepted');
 await pause(710);check((await social(a.token,'send',{peer,kind:'invite',text:'ABCD23',nonce:crypto.randomUUID()})).ok,'Friend room invitation persisted');
 check((await social(a.token,'history',{peer,before:sent.sent.id})).messages.length===0,'History cursor excludes newer messages');
 check(!(await call('/profile',{token:a.token,nickname:'../evil'})).ok,'Nickname validation');
 const rename=await call('/profile',{token:a.token,nickname:'Альфа Страж'});check(rename.profile.name==='Альфа Страж'&&rename.profile.login==='SocialAlpha','Nickname changes independently of login');
 check(!(await call('/profile',{token:b.token,nickname:'Альфа Страж'})).ok,'Duplicate nickname refused');
 check((await call('/auth',{action:'login',name:'SocialAlpha',password:'LocalTest!2026'})).ok,'Original login still works after rename');
 check((await social(b.token,'search',{query:'Страж'})).found[0].id===aid,'Search finds new nickname by immutable identity');
 const result={match:'social-test-round',players:[aid,peer],winner:0,races:['Human Alliance','Naga Faction'],round:2};await call('/result',result);await call('/result',result);
 const matches=await social(a.token,'matches');check(matches.matches.length===1&&matches.matches[0].won&&matches.matches[0].round===2,'Idempotent authoritative match history');
 const publicProfile=await social(c.token,'player',{peer:aid});check(publicProfile.player.wins===1&&publicProfile.matches[0].won,'Authenticated user can view public rank and history');
 check(!('conversations'in publicProfile)&&!('messages'in publicProfile),'Public profile never exposes DMs');
 check((await social(b.token,'matches')).matches[0].won===false,'Opponent receives defeat history');
 await stop();await start();check((await social(b.token,'history',{peer:aid})).messages.length===4,'DM history survives service restart');
 check((await social(a.token,'state')).friends.length===1,'Friends survive service restart');
 check((await social(a.token,'state')).friends[0].last_seen>0,'Last seen persisted');
 await social(a.token,'remove',{peer});check((await social(b.token,'send',{peer:aid,text:'После удаления',nonce:crypto.randomUUID()})).ok,'Removing a friend leaves direct messaging available');
 check((await social(b.token,'history',{peer:aid})).messages.length===5,'Existing private history retained after removal');
 check((await social(a.token,'state')).friends.length===0&&(await social(b.token,'state')).friends.length===0,'Friend removal mutual');
 await stop();const report={status:'PASS',checks,count:checks.length};fs.writeFileSync(path.join(__dirname,'social-service-verification.json'),JSON.stringify(report,null,2));console.log('SOCIAL_SERVICE_PASS',checks.length);
 }finally{await stop();fs.rmSync(root,{recursive:true,force:true});}})().catch(e=>{console.error(e);process.exitCode=1;});
