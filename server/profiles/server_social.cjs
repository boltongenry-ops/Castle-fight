'use strict';
// Uses the existing profile database and authenticated session. Never trust caller ids.
module.exports=function social(db,user,data,save,stickerIds){
 const now=Date.now(),action=data.action||'state';
 db.social ||= {friends:[],requests:[],messages:[],reads:{},next:1};
 const s=db.social,id=user.id,fail=m=>{throw Error(m);};
 const pair=(a,b)=>[a,b].sort().join(':');
 const friend=(a,b)=>s.friends.some(f=>f.key===pair(a,b));
 const publicPeer=u=>({id:u.id,name:u.nickname||u.name,avatar:u.avatar,avatar_ref:u.avatar_ref||'',wins:u.wins||0,last_seen:u.last_seen||0});
 const peer=(allowSelf=false)=>{const u=db.users.find(u=>u.id===data.peer);if(!u||u.id===id&&!allowSelf)fail('Игрок не найден');return u;};
 const readKey=other=>id+':'+other;
 if(!user.last_seen||now-user.last_seen>=60000){user.last_seen=now;save();}
 if(action==='presence'){user.last_seen=now;save();return{ok:true};}
 if(action==='avatar'){
  const other=peer(true);
  return{ok:true,avatar_owner:other.id,avatar_ref:other.avatar_ref||'',avatar_image:other.avatar_image||''};
 }
 if(action==='player'){const other=peer();return{ok:true,player:{...publicPeer(other),games:other.games||0,completed:other.completed||[],best_ms:other.best_ms||{}},matches:(other.match_history||[]).slice(-100).reverse(),friend:friend(id,other.id)};}
 if(action==='search'){
  const query=String(data.query||'').trim().toLocaleLowerCase('ru');if(query.length<3)return{ok:true,found:[]};
  return{ok:true,found:db.users.filter(u=>u.id!==id&&[(u.nickname||u.name),u.name].some(n=>n.toLocaleLowerCase('ru').includes(query))).slice(0,20).map(publicPeer)};
 }
 if(action==='request'){
  const other=peer();if(friend(id,other.id))return{ok:true};
  if(s.friends.filter(f=>f.a===id||f.b===id).length>=100)fail('Не более 100 друзей');
  if(s.requests.some(r=>r.from===other.id&&r.to===id))fail('У вас уже есть входящая заявка от этого игрока');
  if(!s.requests.some(r=>r.from===id&&r.to===other.id)){
   if(s.requests.filter(r=>r.from===id).length>=30||s.requests.filter(r=>r.to===other.id).length>=50)fail('Слишком много заявок');
   s.requests.push({from:id,to:other.id,at:now});save();
  }
 }else if(action==='accept'){
  const other=peer(),request=s.requests.find(r=>r.from===other.id&&r.to===id);if(!request)fail('Заявка уже недоступна');
  if([id,other.id].some(who=>s.friends.filter(f=>f.a===who||f.b===who).length>=100))fail('Список друзей заполнен');
  if(!friend(id,other.id))s.friends.push({key:pair(id,other.id),a:id,b:other.id,at:now});
  s.requests=s.requests.filter(r=>!((r.from===id&&r.to===other.id)||(r.from===other.id&&r.to===id)));save();
 }else if(action==='decline'||action==='cancel'){
  const other=peer();s.requests=s.requests.filter(r=>action==='decline'?!(r.from===other.id&&r.to===id):!(r.from===id&&r.to===other.id));save();
 }else if(action==='remove'){
  const other=peer();s.friends=s.friends.filter(f=>f.key!==pair(id,other.id));save();
 }else if(action==='send'){
  const other=peer();
  const kind=data.kind||'text',nonce=String(data.nonce||'');
  if(!/^[a-f0-9-]{16,64}$/i.test(nonce))fail('Некорректный номер сообщения');
  const previous=s.messages.find(m=>m.from===id&&m.nonce===nonce);
  if(previous)return{ok:true,sent:previous};
  const last=s.messages.filter(m=>m.from===id).slice(-1)[0];if(last&&now-last.at<700)fail('Отправляете слишком быстро');
  let text=String(data.text||'').trim(),sticker=String(data.sticker||'');
  if(kind==='text'){text=text.replace(/[\u0000-\u0008\u000b-\u001f\u007f]/g,'');if(!text||text.length>500)fail('Сообщение: от 1 до 500 символов');}
  else if(kind==='sticker'){if(!stickerIds.has(sticker))fail('Неизвестный стикер');text='';}
  else if(kind==='invite'){if(!/^[A-Z0-9]{4,12}$/i.test(text))fail('Некорректный код комнаты');}
  else fail('Неизвестный тип сообщения');
  const message={id:s.next++,from:id,to:other.id,at:now,kind,text,sticker:kind==='sticker'?sticker:'',nonce};s.messages.push(message);
  const own=s.messages.filter(m=>pair(m.from,m.to)===pair(id,other.id));if(own.length>500){const expired=new Set(own.slice(0,-500).map(m=>m.id));s.messages=s.messages.filter(m=>!expired.has(m.id));}
  s.messages=s.messages.slice(-20000);save();return{ok:true,sent:message};
 }else if(action==='history'){
  const other=peer(),messages=s.messages.filter(m=>pair(m.from,m.to)===pair(id,other.id)),before=Number(data.before)||Infinity;
  const page=messages.filter(m=>m.id<before).slice(-50);
  // Read receipts advance only to a message actually supplied by this client.
  const upto=Math.min(Number(data.read)||0,messages.length?messages[messages.length-1].id:0);
  if(upto>(s.reads[readKey(other.id)]||0)){s.reads[readKey(other.id)]=upto;save();}
  return{ok:true,peer:publicPeer(other),messages:page,has_more:page.length>0&&messages.some(m=>m.id<page[0].id),friend:friend(id,other.id)};
 }else if(action==='matches')return{ok:true,matches:(user.match_history||[]).slice(-100).reverse()};
 else if(!['state','request','accept','decline','cancel','remove'].includes(action))fail('Неизвестное действие');
 const friends=s.friends.filter(f=>f.a===id||f.b===id).map(f=>db.users.find(u=>u.id===(f.a===id?f.b:f.a))).filter(Boolean).map(publicPeer);
 const requests=s.requests.filter(r=>r.to===id||r.from===id).map(r=>({incoming:r.to===id,at:r.at,player:publicPeer(db.users.find(u=>u.id===(r.to===id?r.from:r.to)))}));
 const others=new Set(s.messages.filter(m=>m.from===id||m.to===id).map(m=>m.from===id?m.to:m.from));
 const conversations=[...others].map(other=>{const rows=s.messages.filter(m=>pair(m.from,m.to)===pair(id,other));return{player:publicPeer(db.users.find(u=>u.id===other)),last:rows[rows.length-1],unread:rows.filter(m=>m.to===id&&m.id>(s.reads[readKey(other)]||0)).length};}).sort((a,b)=>b.last.id-a.last.id);
 return{ok:true,friends,requests,conversations};
};
