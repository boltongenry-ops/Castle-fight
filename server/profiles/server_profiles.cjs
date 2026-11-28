'use strict';
// Private loopback service. Only the game server holds CF_PROFILE_SECRET.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{promisify}=require('node:util'),{execFile}=require('node:child_process');
const scrypt=promisify(crypto.scrypt),file=process.env.CF_PROFILE_FILE||path.join(__dirname,'profiles.json');
const secret=process.env.CF_PROFILE_SECRET;if(!secret)throw Error('CF_PROFILE_SECRET is required');
const avatars=['hfoo','hrif','ugho','ogru','earc','n000','edry','uske'];
const catalogFile=process.env.CF_PROGRESSION_CATALOG||[path.join(__dirname,'progression_catalog.json'),path.join(__dirname,'../assets/data/progression_catalog.json')].find(fs.existsSync);
const catalog=catalogFile?JSON.parse(fs.readFileSync(catalogFile,'utf8')):{challenges:[],items:[]};
const accountCatalogFile=path.join(__dirname,'account_progression.json');
const accountCatalog=fs.existsSync(accountCatalogFile)?JSON.parse(fs.readFileSync(accountCatalogFile,'utf8')):{challenges:[],items:[]};
const challenges=new Map([...catalog.challenges,...accountCatalog.challenges].map(row=>[row.id,row]));
const wardrobeFile=path.join(__dirname,'wardrobe_catalog.json');
const wardrobe=fs.existsSync(wardrobeFile)?JSON.parse(fs.readFileSync(wardrobeFile,'utf8')).items:[];
const items=new Map([...catalog.items,...accountCatalog.items,...wardrobe].map(row=>[row.id,row]));
let db=fs.existsSync(file)?JSON.parse(fs.readFileSync(file,'utf8')):{users:[],sessions:[],results:[]};
const social=require('./server_social.cjs');
const emotesFile=process.env.CF_EMOTES_CATALOG||path.join(__dirname,'match_emotes.json');
const stickerIds=new Set(['orc','knight','undead','naga','elf','goblin']);
if(fs.existsSync(emotesFile))for(const pack of JSON.parse(fs.readFileSync(emotesFile,'utf8')))for(const item of pack.items||[])stickerIds.add(item.id);
let busy=0;const attempts=new Map();
function save(){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file+'.tmp',JSON.stringify(db),{mode:0o600});fs.renameSync(file+'.tmp',file);}
function tokenHash(value){return crypto.createHash('sha256').update(String(value)).digest('hex');}
function publicUser(user){return{id:user.id,name:user.nickname||user.name,login:user.guest?'':user.name,guest:!!user.guest,avatar:user.avatar,avatar_ref:user.avatar_ref||'',wins:user.wins||0,games:user.games||0,solo_wins:user.solo_wins||0,solo_games:user.solo_games||0,admin:!!user.admin,coins:user.coins||0,unlocked:user.unlocked||[],completed:user.completed||[],best_ms:user.best_ms||{},equipped:user.equipped||{},account_progression:accountCatalog.challenges.length>0?1:0,wardrobe:wardrobe.length>0?1:0};}
function active(s){return s.expires===null||s.expires>Date.now();}
function session(token){const found=db.sessions.find(s=>s.hash===tokenHash(token)&&active(s));return found&&db.users.find(u=>u.id===found.user);}
function issue(user){
 const token=crypto.randomBytes(32).toString('hex');
 db.sessions=db.sessions.filter(active);
 db.sessions.push({hash:tokenHash(token),user:user.id,expires:user.guest?253402300799999:Date.now()+90*86400000});
 const own=db.sessions.filter(s=>s.user===user.id);for(const old of own.slice(0,-8))db.sessions.splice(db.sessions.indexOf(old),1);
 save();return{ok:true,profile:publicUser(user),token};
}
function fail(message){throw Error(message);}
function password(value){if(typeof value!=='string'||value.length<6||value.length>128)fail('Пароль: от 6 до 128 символов');return value;}
async function makeUser(name,pw,admin=false){const salt=crypto.randomBytes(16).toString('hex');return{id:crypto.randomUUID(),name,key:name.toLocaleLowerCase('ru'),salt,password:Buffer.from(await scrypt(password(pw),salt,64)).toString('hex'),avatar:'hfoo',wins:0,games:0,admin};}
function avatarImage(value){
 if(typeof value!=='string'||value.length>21848||!/^[A-Za-z0-9+/]+={0,2}$/.test(value))fail('Изображение слишком большое или повреждено');
 const image=Buffer.from(value,'base64');
 if(image.length<32||image.length>16384||image.readUInt16BE(0)!==0xffd8||image.readUInt16BE(image.length-2)!==0xffd9)
  fail('Не удалось принять изображение');
 let pos=2,valid=false;
 while(pos+4<image.length&&image[pos]===255){
  const marker=image[pos+1],size=image.readUInt16BE(pos+2);
  if(marker===0xda)break;
  if(size<2||pos+2+size>image.length)break;
  if(marker===0xc0){
   valid=size===17&&image[pos+4]===8&&image.readUInt16BE(pos+5)===128&&image.readUInt16BE(pos+7)===128&&image[pos+9]===3;
  }
  pos+=2+size;
 }
 if(!valid)fail('Изображение должно быть подготовлено игрой');
 return {image:value,ref:crypto.createHash('sha256').update(image).digest('hex')};
}
async function handle(route,data){
 if(route==='/auth'){
  if(data.action==='token'){const user=session(data.token);if(!user)fail('Сессия истекла. Войдите в аккаунт снова');return{ok:true,profile:publicUser(user),token:data.token};}
  if(data.action==='guest'){
   const id=crypto.randomUUID(),name='Гость '+id.slice(0,8).toUpperCase();
   const user={id,name,guest:true,avatar:'hfoo',wins:0,games:0};
   db.users.push(user);
   return issue(user);
  }
  const name=String(data.name||'').trim();if(!/^[\p{L}\p{N}_-]{3,20}$/u.test(name))fail('Имя: 3–20 букв, цифр, _ или -');
  const key=name.toLocaleLowerCase('ru'),now=Date.now(),rate=attempts.get(key)||{at:now,count:0};
  if(now-rate.at>60000){rate.at=now;rate.count=0;}if(++rate.count>8)fail('Слишком много попыток. Подождите минуту');attempts.set(key,rate);
  let user=db.users.find(u=>!u.guest&&u.key===key);
  if(data.action==='register'){
   if(user)fail('Это имя уже занято');
   user=await makeUser(name,data.password);
   if(db.users.some(u=>u.key===key))fail('Это имя уже занято');
   db.users.push(user);
  }else if(data.action==='login'){
   password(data.password);const salt=user?.salt||'00000000000000000000000000000000';
   const digest=Buffer.from(await scrypt(data.password,salt,64));
   if(!user||!crypto.timingSafeEqual(digest,Buffer.from(user.password,'hex')))fail('Неверное имя или пароль');
  }else fail('Неизвестное действие');
  return issue(user);
 }
 if(route==='/result'){
  if(typeof data.match!=='string'||!Array.isArray(data.players)||data.players.length!==2||![0,1].includes(data.winner))fail('Invalid result');
  if(db.results.includes(data.match))return{ok:true};
  if(data.players[0]&&data.players[0]===data.players[1])return{ok:true};
  for(let i=0;i<2;i++){const user=db.users.find(u=>u.id===data.players[i]);if(user){user.games=(user.games||0)+1;if(i===data.winner)user.wins=(user.wins||0)+1;user.match_history ||= [];const other=db.users.find(u=>u.id===data.players[1-i]);user.match_history.push({id:data.match,at:Date.now(),won:i===data.winner,opponent_id:other?other.id:'',opponent:other?(other.nickname||other.name):String(data.names?.[1-i]||'Гость').slice(0,24),race:String(data.races?.[i]||''),opponent_race:String(data.races?.[1-i]||''),duration:Math.max(0,Number(data.duration)||0),round:Number(data.round)||1});user.match_history=user.match_history.slice(-100);}}
  db.results.push(data.match);db.results=db.results.slice(-10000);save();return{ok:true};
 }
 const user=session(data.token);if(!user)fail('Войдите в аккаунт');
 if(route==='/social')return social(db,user,data,save,stickerIds);
 if(route==='/solo_result'){
  if(typeof data.result!=='string'||!/^[a-f0-9]{32}$/.test(data.result)||typeof data.won!=='boolean')fail('Некорректный результат');
  user.solo_results ||= [];
  if(!user.solo_results.includes(data.result)){
   user.solo_results.push(data.result);user.solo_results=user.solo_results.slice(-10000);
   user.solo_games=(user.solo_games||0)+1;if(data.won)user.solo_wins=(user.solo_wins||0)+1;save();
  }
  return{ok:true,profile:publicUser(user)};
 }
 if(route==='/challenge'){
  const results=data.results??[{challenge:data.challenge,time_ms:data.time_ms}];
  if(!Array.isArray(results)||results.length>60)fail('Некорректный список испытаний');
  for(const row of results){
   if(!row||!challenges.has(row.challenge))fail('Неизвестное испытание');
   if(row.time_ms!=null&&(!Number.isInteger(row.time_ms)||row.time_ms<1||row.time_ms>86400000))fail('Некорректное время испытания');
  }
  user.completed ||= [];user.best_ms ||= {};
  const completed=new Set(user.completed);let awarded=0,changed=false;
  for(const row of results){
   const id=row.challenge;
   if(!completed.has(id)){completed.add(id);awarded+=challenges.get(id).reward;changed=true;}
   if(row.time_ms!=null&&(!user.best_ms[id]||row.time_ms<user.best_ms[id])){user.best_ms[id]=row.time_ms;changed=true;}
  }
  if(changed){user.completed=[...completed];user.coins=(user.coins||0)+awarded;save();}
  return{ok:true,profile:publicUser(user),awarded};
 }
 if(route==='/cosmetic'){
  const item=items.get(data.item);if(!item)fail('Неизвестное оформление');
  user.unlocked ||= [];user.equipped ||= {};
  const pieces=(item.includes||[]).map(id=>items.get(id));
  const targets=pieces.length?pieces:[item];
  if(data.action==='buy'){
   if(!user.unlocked.includes(item.id)){
    const missing=pieces.filter(piece=>!user.unlocked.includes(piece.id));
    const cost=pieces.length?Math.min(item.cost,missing.reduce((sum,piece)=>sum+piece.cost,0)):item.cost;
    if((user.coins||0)<cost)fail('Недостаточно жетонов');
    user.coins=(user.coins||0)-cost;
    user.unlocked.push(item.id,...missing.map(piece=>piece.id));
   }
  }else if(data.action==='equip'){
   if(!user.unlocked.includes(item.id))fail('Сначала откройте оформление');
   for(const target of targets)user.equipped[target.kind]=target.id;
  }else if(data.action==='unequip'){
   for(const target of targets)
    if(user.equipped[target.kind]===target.id)delete user.equipped[target.kind];
  }else fail('Неизвестное действие');
  save();return{ok:true,profile:publicUser(user)};
 }
 if(route==='/profile'){
  const image=data.avatar_image===undefined?null:avatarImage(data.avatar_image);
  if(data.avatar!==undefined&&!avatars.includes(data.avatar))fail('Неизвестный аватар');
  if(data.nickname!==undefined){const nickname=String(data.nickname).trim();if(!/^[\p{L}\p{N}_ -]{3,24}$/u.test(nickname))fail('Ник: 3–24 буквы, цифры, пробел, _ или -');if(db.users.some(u=>u.id!==user.id&&(u.nickname||u.name).toLocaleLowerCase('ru')===nickname.toLocaleLowerCase('ru')))fail('Этот ник уже занят');user.nickname=nickname;}
  if(data.avatar!==undefined||image){
   if(data.avatar!==undefined)user.avatar=data.avatar;
   if(image){user.avatar_ref=image.ref;user.avatar_image=image.image;}
   else{delete user.avatar_ref;delete user.avatar_image;}
   if(user.equipped)delete user.equipped.avatar;
  }
  if(data.nickname!==undefined||data.avatar!==undefined||image)save();
  return{ok:true,profile:publicUser(user)};
 }
 if(route==='/logout'){db.sessions=db.sessions.filter(s=>s.hash!==tokenHash(data.token));save();return{ok:true};}
 if(route==='/password'){
  if(user.guest)fail('У гостевого аккаунта нет пароля');
  const current=Buffer.from(await scrypt(String(data.current||''),user.salt,64));
  if(!crypto.timingSafeEqual(current,Buffer.from(user.password,'hex')))fail('Текущий пароль неверен');
  const replacement=await makeUser(user.name,data.password);user.salt=replacement.salt;user.password=replacement.password;
  db.sessions=db.sessions.filter(s=>s.user!==user.id||s.hash===tokenHash(data.token));save();return{ok:true,profile:publicUser(user)};
 }
 if(route==='/restart'){
  if(!user.admin)fail('Перезапуск доступен только администратору');
  if(process.env.CF_ALLOW_RESTART!=='1')fail('Перезапуск недоступен на тестовом сервере');
  setTimeout(()=>execFile('/usr/bin/sudo',['/usr/bin/systemctl','restart','castle-fight.service'],{timeout:15000},error=>{if(error)console.error('Game restart failed:',error.code);}),800);
  return{ok:true,restarting:true};
 }
 fail('Неизвестный запрос');
}
(async()=>{
 if(process.env.CF_ADMIN_PASSWORD&&!db.users.some(u=>u.admin)){
  if(db.users.some(u=>u.key==='darkness'))throw Error('Admin name already registered');
  db.users.push(await makeUser('Darkness',process.env.CF_ADMIN_PASSWORD,true));save();
 }
 if(process.argv.includes('--init-only'))return;
 http.createServer(async(req,res)=>{
  res.setHeader('Content-Type','application/json; charset=utf-8');res.setHeader('Cache-Control','no-store');
  if(req.headers.authorization!=='Bearer '+secret){res.writeHead(403);res.end('{"ok":false}');return;}
  if(req.method!=='POST'||busy>=4){res.writeHead(429);res.end('{"ok":false,"message":"Сервис занят, попробуйте ещё раз"}');return;}
  busy++;try{
   const limit=req.url==='/profile'?24576:8192;
   let text='';for await(const chunk of req){text+=chunk;if(Buffer.byteLength(text)>limit)fail('Запрос слишком большой');}
   const data=JSON.parse(text);const result=await handle(req.url,data);res.end(JSON.stringify(result));
  }catch(error){res.end(JSON.stringify({ok:false,message:error.message}));}finally{busy--;}
 }).listen(Number(process.env.CF_PROFILE_PORT||49173),'127.0.0.1',()=>console.log('[PROFILES] listening on loopback'));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
