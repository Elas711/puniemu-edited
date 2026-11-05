namespace Puniemu.Src.Server.Admin
{
    public static class AdminPage
    {
        public const string HTML = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Puniemu Admin</title>
<style>
:root{--bg:#f6f4f0;--card:#fff;--ink:#22201c;--muted:#6f6a62;--line:#e6e1d8;--accent:#d9480f;--ok:#2b8a3e;--bad:#c92a2a}
@media (prefers-color-scheme:dark){:root{--bg:#171614;--card:#211f1c;--ink:#ece8e1;--muted:#a39d93;--line:#353129;--accent:#ff8a3d;--ok:#69db7c;--bad:#ff6b6b}}
*{box-sizing:border-box}body{margin:0;font:14px/1.45 -apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--ink)}
header{display:flex;align-items:center;gap:12px;padding:12px 16px;border-bottom:1px solid var(--line);background:var(--card);position:sticky;top:0;z-index:3;flex-wrap:wrap}
header b{font-size:16px;color:var(--accent)} nav{display:flex;gap:4px;flex-wrap:wrap}
nav button{background:none;border:0;padding:8px 12px;border-radius:8px;color:var(--muted);font-weight:600;cursor:pointer}
nav button.on{background:var(--bg);color:var(--ink)}
main{max-width:1100px;margin:0 auto;padding:16px}
.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px;margin-bottom:14px}
h2{font-size:15px;margin:0 0 10px} .muted{color:var(--muted)} .tiles{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:10px}
.tile{border:1px solid var(--line);border-radius:10px;padding:12px}.tile .v{font-size:22px;font-weight:700;font-variant-numeric:tabular-nums}.tile .k{color:var(--muted);font-size:12px}
table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}th,td{text-align:left;padding:7px 8px;border-bottom:1px solid var(--line);vertical-align:top}th{color:var(--muted);font-weight:600;font-size:12px}
input,select,textarea{font:inherit;color:inherit;background:var(--bg);border:1px solid var(--line);border-radius:8px;padding:8px}
textarea{width:100%;min-height:60px} button.act{background:var(--accent);color:#fff;border:0;border-radius:8px;padding:8px 14px;font-weight:600;cursor:pointer}
button.ghost{background:none;border:1px solid var(--line);color:var(--ink);border-radius:8px;padding:7px 12px;cursor:pointer}
.row{display:flex;gap:8px;flex-wrap:wrap;align-items:center;margin-bottom:8px}.row label{display:flex;gap:6px;align-items:center}
.pill{display:inline-block;padding:1px 7px;border-radius:999px;font-size:11px;border:1px solid var(--line);color:var(--muted)}
.pill.ok{color:var(--ok);border-color:var(--ok)}.pill.bad{color:var(--bad);border-color:var(--bad)}
#toast{position:fixed;right:16px;bottom:16px;background:var(--ink);color:var(--bg);padding:10px 14px;border-radius:10px;display:none}
pre{white-space:pre-wrap;font-size:12px;max-height:520px;overflow:auto;margin:0}
.hidden{display:none}.scroll{max-height:560px;overflow:auto}
</style></head><body>
<header><b>Puniemu Admin</b><span class=""muted"" id=""srv""></span>
<nav id=""nav""><button data-t=""overview"" class=""on"">Overview</button><button data-t=""players"">Players</button><button data-t=""events"">Events</button><button data-t=""gifts"">Gifts</button><button data-t=""settings"">Settings</button><button data-t=""logs"">Logs</button></nav>
<span style=""flex:1""></span><input id=""token"" type=""password"" placeholder=""Admin token"" size=""16""><button class=""ghost"" id=""login"">Connect</button></header>
<main>
<section id=""t-overview""><div class=""card""><h2>Server</h2><div class=""tiles"" id=""tiles""></div></div>
<div class=""card""><h2>Endpoints</h2><div class=""scroll""><table id=""endpoints""></table></div></div></section>

<section id=""t-players"" class=""hidden""><div class=""card""><h2>Find players</h2>
<div class=""row""><input id=""pq"" placeholder=""Name, friend code, user id or gdkey"" style=""flex:1""><button class=""act"" id=""psearch"">Search</button></div>
<div class=""scroll""><table id=""plist""></table></div></div>
<div class=""card hidden"" id=""pcard""><h2 id=""ptitle""></h2><div id=""pinfo"" class=""muted""></div>
<h2 style=""margin-top:14px"">Give / take</h2>
<div class=""row""><label>Y-Money <input id=""gy"" type=""number"" value=""0"" style=""width:110px""></label><label>Spirits <input id=""gs"" type=""number"" value=""0"" style=""width:80px""></label></div>
<div class=""row""><label>Items <input id=""gi"" placeholder=""itemId:count, itemId:count"" size=""32""></label><label>Yo-kai <input id=""gk"" placeholder=""youkaiId, youkaiId"" size=""26""></label><button class=""act"" id=""grant"">Apply</button></div>
<div class=""row""><label>Look up <select id=""lt""><option value=""youkai"">Yo-kai</option><option value=""item"">Item</option></select></label><input id=""lq"" placeholder=""name or id"" size=""18""><button class=""ghost"" id=""lbtn"">Find</button><span id=""lres"" class=""muted""></span></div>
<h2 style=""margin-top:14px"">Save</h2>
<div class=""row""><input id=""rname"" placeholder=""New player name"" size=""20""><button class=""ghost"" id=""rbtn"">Rename</button>
<button class=""ghost"" id=""xbtn"">Export save</button><label class=""ghost"">Import save <input type=""file"" id=""ifile"" accept="".json""></label></div>
<h2 style=""margin-top:14px"">Moderation</h2>
<div class=""row""><input id=""breason"" placeholder=""Ban reason (shown to the player)"" style=""flex:1""><button class=""ghost"" id=""ban"">Ban</button><button class=""ghost"" id=""unban"">Unban</button></div>
</div></section>

<section id=""t-events"" class=""hidden""><div class=""card""><h2>Event selection</h2>
<div class=""row""><label><input type=""radio"" name=""emode"" value=""default""> Original data (serve the events exactly as in Resources)</label></div>
<div class=""row""><label><input type=""radio"" name=""emode"" value=""custom""> Custom: only the events ticked below are on</label></div>
<div class=""row""><label><input type=""radio"" name=""emode"" value=""rotation""> Rotation: automatically cycle through the events (the ticked ones, or all playable ones if none is ticked)</label>
<label>change every <input id=""rdays"" type=""number"" min=""1"" style=""width:70px""> days,</label><label><input id=""rcount"" type=""number"" min=""1"" style=""width:60px""> at once</label></div>
<p class=""muted"" id=""enow""></p>
<div class=""row""><label><input type=""checkbox"" id=""eplayer""> Players may choose their own events (in-game Help &rarr; Events &amp; Server)</label></div>
<div class=""row""><label><input type=""checkbox"" id=""ehide""> Close the maps of events that are not selected</label><label>Event length <input id=""edays"" type=""number"" min=""1"" style=""width:80px""> days</label></div>
<div class=""row""><input id=""efilter"" placeholder=""Filter..."" style=""flex:1""><button class=""ghost"" id=""eall"">Select all</button><button class=""ghost"" id=""enone"">Clear</button><button class=""ghost"" id=""ereload"">Rescan data</button><button class=""act"" id=""esave"">Save</button></div>
<p class=""muted"">Events are discovered from the loaded master data and from Events/*.json packs. Players must return to the title screen to receive the new event data.</p>
<div class=""scroll""><table id=""elist""></table></div></div>
<div class=""card""><h2>Event history</h2><p class=""muted"">Every event of the live game (Events/history.json). Enter the event ids from the list above that belong to an entry: those events get its name and series, and players see it as playable.</p>
<div class=""row""><button class=""ghost"" id=""hauto"">Auto-link strong matches</button><span class=""muted"" id=""hsug""></span></div>
<input id=""hfilter"" placeholder=""Filter..."" style=""width:100%;margin-bottom:8px""><div class=""scroll""><table id=""hlist""></table></div></div></section>

<section id=""t-gifts"" class=""hidden""><div class=""card""><h2>New server-wide gift</h2><p class=""muted"">Every player receives it once, on their next login (new players too).</p>
<div class=""row""><input id=""fm"" placeholder=""Message / note"" style=""flex:1""></div>
<div class=""row""><label>Y-Money <input id=""fy"" type=""number"" value=""0"" style=""width:110px""></label><label>Spirits <input id=""fs"" type=""number"" value=""0"" style=""width:80px""></label>
<label>Items <input id=""fi"" placeholder=""itemId:count, ..."" size=""26""></label><label>Yo-kai <input id=""fk"" placeholder=""youkaiId, ..."" size=""20""></label><button class=""act"" id=""fsend"">Create gift</button></div></div>
<div class=""card""><h2>Active gifts</h2><table id=""flist""></table></div></section>

<section id=""t-settings"" class=""hidden""><div class=""card""><h2>Gameplay</h2>
<div class=""row""><label>Y-Money multiplier <input id=""sy"" type=""number"" step=""0.1"" min=""0"" style=""width:90px""></label><label>EXP multiplier <input id=""sx"" type=""number"" step=""0.1"" min=""0"" style=""width:90px""></label><label><input type=""checkbox"" id=""si""> Infinite spirits</label></div>
<h2 style=""margin-top:14px"">News (shown on the in-game notice &amp; portal pages)</h2><textarea id=""smotd""></textarea>
<h2 style=""margin-top:14px"">Maintenance</h2><div class=""row""><label><input type=""checkbox"" id=""sm""> Maintenance mode (players get the message below)</label></div><textarea id=""smm""></textarea>
<div class=""row"" style=""margin-top:10px""><button class=""act"" id=""ssave"">Save settings</button><button class=""ghost"" id=""sflush"">Save all accounts to DB now</button></div></div></section>

<section id=""t-logs"" class=""hidden""><div class=""card""><h2>Recent server log</h2><pre id=""logs""></pre></div></section>
</main><div id=""toast""></div>
<script>
var T=sessionStorage.getItem('pt')||'';document.getElementById('token').value=T;
function $(i){return document.getElementById(i);}
function esc(s){return String(s==null?'':s).replace(/[&<>""']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c];});}
function toast(m){var t=$('toast');t.textContent=m;t.style.display='block';clearTimeout(t._h);t._h=setTimeout(function(){t.style.display='none';},2500);}
function api(m,u,b){return fetch(u,{method:m,headers:{'X-Admin-Token':T,'Content-Type':'application/json'},body:b?JSON.stringify(b):undefined})
 .then(function(r){return r.json().then(function(j){if(!r.ok)throw new Error(j.error||r.status);return j;});});}
function fail(e){toast('Error: '+e.message);}
function parseItems(s){return (s||'').split(',').map(function(x){x=x.trim();if(!x)return null;var p=x.split(':');return {id:+p[0],count:+(p[1]||1)};}).filter(Boolean);}
function parseIds(s){return (s||'').split(',').map(function(x){return +x.trim();}).filter(function(x){return x;});}
var tabs=document.querySelectorAll('#nav button');tabs.forEach(function(b){b.onclick=function(){tabs.forEach(function(x){x.classList.remove('on');});b.classList.add('on');
 document.querySelectorAll('main section').forEach(function(s){s.classList.add('hidden');});$('t-'+b.dataset.t).classList.remove('hidden');load(b.dataset.t);};});
$('login').onclick=function(){T=$('token').value;sessionStorage.setItem('pt',T);load('overview');};
function tile(k,v){return '<div class=""tile""><div class=""v"">'+esc(v)+'</div><div class=""k"">'+esc(k)+'</div></div>';}
var STATE=null,EV=null,CUR=null;
function load(t){
 if(t==='overview'||t==='settings'||t==='gifts')api('GET','/admin/api/overview').then(function(o){STATE=o.state;$('srv').textContent=o.server+' - '+o.game+' '+o.version;
  var m=o.metrics,up=Math.floor(m.uptimeSec/3600)+'h '+Math.floor(m.uptimeSec%3600/60)+'m';
  $('tiles').innerHTML=tile('Players online (15 min)',m.activePlayers15m)+tile('Accounts',o.db.accounts)+tile('Devices',o.db.devices)+tile('Cached / unsaved',o.db.cachedAccounts+' / '+o.db.dirtyAccounts)+
   tile('Uptime',up)+tile('Memory',m.memoryMb+' MB')+tile('Game tables',o.tables)+tile('Events (switchable)',o.events.total+' ('+o.events.switchable+')')+
   tile('Translation',o.translation.language?o.translation.language+': '+o.translation.translated+' texts':'off')+tile('Maintenance',o.state.maintenanceMode?'ON':'off');
  $('endpoints').innerHTML='<tr><th>Path</th><th>Requests</th><th>Errors</th><th>Avg ms</th></tr>'+m.endpoints.map(function(e){return '<tr><td>'+esc(e.path)+'</td><td>'+e.count+'</td><td>'+e.errors+'</td><td>'+e.avgMs+'</td></tr>';}).join('');
  if(t==='settings'){$('sy').value=STATE.ymoneyMultiplier;$('sx').value=STATE.expMultiplier;$('si').checked=STATE.infiniteSpirits;$('smotd').value=STATE.motd;$('sm').checked=STATE.maintenanceMode;$('smm').value=STATE.maintenanceMessage;}
  if(t==='gifts')$('flist').innerHTML='<tr><th>Created</th><th>Message</th><th>Contents</th><th></th></tr>'+STATE.gifts.map(function(g){return '<tr><td>'+esc(g.created)+'</td><td>'+esc(g.message)+'</td><td>'+
   esc((g.ymoney?g.ymoney+' Y-Money ':'')+(g.spirits?g.spirits+' spirits ':'')+g.items.map(function(i){return i.id+'x'+i.count;}).join(' ')+(g.youkai.length?' Yo-kai '+g.youkai.join(','):''))+
   '</td><td><button class=""ghost"" data-del=""'+esc(g.id)+'"">Delete</button></td></tr>';}).join('');
  document.querySelectorAll('[data-del]').forEach(function(b){b.onclick=function(){api('POST','/admin/api/gifts/'+b.dataset.del+'/delete').then(function(){load('gifts');}).catch(fail);};});
 }).catch(fail);
 if(t==='events')api('GET','/admin/api/events').then(function(e){EV=e;var s=e.settings;document.querySelectorAll('[name=emode]').forEach(function(r){r.checked=r.value===s.mode;});
  $('eplayer').checked=s.allowPlayerChoice;$('ehide').checked=s.hideInactiveEventMaps;$('edays').value=s.eventDurationDays;$('rdays').value=s.rotationDays;$('rcount').value=s.rotationCount;
  loadHistory();$('enow').textContent=e.runningNow?'Running now: '+e.runningNow.join(', '):'Running now: original data';renderEvents();}).catch(fail);
 if(t==='logs')api('GET','/admin/api/logs').then(function(l){$('logs').textContent=l.slice().reverse().join('\n');}).catch(fail);
}
function renderEvents(){var f=$('efilter').value.toLowerCase(),on={};EV.settings.activeEventIds.forEach(function(i){on[i]=1;});
 $('elist').innerHTML='<tr><th></th><th>ID</th><th>Name</th><th>Period</th><th>Maps</th><th>Source</th><th>Tables</th></tr>'+EV.catalog.filter(function(e){return (e.name+' '+e.eventId).toLowerCase().indexOf(f)>=0;}).map(function(e){
  return '<tr><td>'+(e.selectable?'<input type=""checkbox"" data-e=""'+e.eventId+'""'+(on[e.eventId]?' checked':'')+'>':'<span class=""pill"" title=""always on (shop offers etc.)"">fixed</span>')+'</td><td>'+e.eventId+'</td><td>'+esc(e.name)+
  (e.playable?'':' <span class=""pill"">no maps</span>')+'</td><td>'+esc(e.period)+'</td><td>'+e.mapIds.length+'</td><td>'+esc(e.sources.join(', '))+'</td><td class=""muted"">'+esc(e.tables.join(', '))+'</td></tr>';}).join('');
 document.querySelectorAll('[data-e]').forEach(function(c){c.onchange=function(){var id=+c.dataset.e,a=EV.settings.activeEventIds,k=a.indexOf(id);if(c.checked&&k<0)a.push(id);if(!c.checked&&k>=0)a.splice(k,1);};});}
$('efilter').oninput=renderEvents;
var HIST=[];
function loadHistory(){api('GET','/admin/api/events/history').then(function(h){HIST=h;renderHistory();}).catch(fail);
 api('GET','/admin/api/events/history/suggestions').then(function(s){$('hsug').innerHTML=s.length?'Suggestions (by shared Yo-kai): '+s.map(function(x){return esc(x.name)+' &larr; #'+x.eventId+' ('+x.overlap+')';}).join(', '):'No link suggestions.';}).catch(fail);}
$('hauto').onclick=function(){api('POST','/admin/api/events/history/autolink').then(function(r){toast('Linked '+r.linked+' event(s)');load('events');}).catch(fail);};
function renderHistory(){var f=$('hfilter').value.toLowerCase(),rows='<tr><th>Series</th><th>Event</th><th>Event ids</th><th></th></tr>';
 HIST.forEach(function(s,si){s.events.forEach(function(e,ei){if((s.series+' '+e.name).toLowerCase().indexOf(f)<0)return;
  rows+='<tr><td class=""muted"">'+esc(s.series)+'</td><td>'+esc(e.name)+(e.available?' <span class=""pill ok"">in data</span>':'')+'</td><td><input data-h=""'+si+'_'+ei+'"" value=""'+esc(e.eventIds.join(', '))+'"" size=""16""></td><td><button class=""ghost"" data-hs=""'+si+'_'+ei+'"">Link</button></td></tr>';});});
 $('hlist').innerHTML=rows;
 document.querySelectorAll('[data-hs]').forEach(function(b){b.onclick=function(){var k=b.dataset.hs.split('_'),s=HIST[+k[0]],e=s.events[+k[1]];
  api('POST','/admin/api/events/history',{series:s.series,name:e.name,eventIds:parseIds(document.querySelector('[data-h=""'+b.dataset.hs+'""]').value)}).then(function(){toast('Linked');load('events');}).catch(fail);};});}
$('hfilter').oninput=renderHistory;
$('eall').onclick=function(){EV.settings.activeEventIds=EV.catalog.filter(function(e){return e.selectable;}).map(function(e){return e.eventId;});renderEvents();};
$('enone').onclick=function(){EV.settings.activeEventIds=[];renderEvents();};
$('ereload').onclick=function(){api('POST','/admin/api/events/reload').then(function(){toast('Rescanned');load('events');}).catch(fail);};
$('esave').onclick=function(){var mode='default';document.querySelectorAll('[name=emode]').forEach(function(r){if(r.checked)mode=r.value;});
 api('POST','/admin/api/events',{mode:mode,activeEventIds:EV.settings.activeEventIds,allowPlayerChoice:$('eplayer').checked,hideInactiveEventMaps:$('ehide').checked,eventDurationDays:+$('edays').value,rotationDays:+$('rdays').value,rotationCount:+$('rcount').value}).then(function(){toast('Event selection saved');load('events');}).catch(fail);};
$('psearch').onclick=function(){api('GET','/admin/api/players?q='+encodeURIComponent($('pq').value)).then(function(l){
 $('plist').innerHTML='<tr><th>Name</th><th>Friend code</th><th>User id</th><th>Last login</th><th></th></tr>'+l.map(function(p){return '<tr><td>'+esc(p.PlayerName||'(no name)')+' '+(p.Online?'<span class=""pill ok"">online</span>':'')+(p.banned?' <span class=""pill bad"">banned</span>':'')+
  '</td><td>'+esc(p.CharacterId)+'</td><td>'+esc(p.UserId)+'</td><td>'+esc(p.LastLoginTime)+'</td><td><button class=""ghost"" data-p=""'+esc(p.Gdkey)+'"">Open</button></td></tr>';}).join('');
 document.querySelectorAll('[data-p]').forEach(function(b){b.onclick=function(){openPlayer(b.dataset.p);};});}).catch(fail);};
$('pq').onkeydown=function(e){if(e.key==='Enter')$('psearch').onclick();};
function openPlayer(g){api('GET','/admin/api/player/'+encodeURIComponent(g)).then(function(p){CUR=g;$('pcard').classList.remove('hidden');$('ptitle').textContent=(p.playerName||'(no name)')+' - '+p.friendCode;
 $('pinfo').innerHTML='gdkey: '+esc(p.gdkey)+'<br>user id: '+esc(p.userId)+' &middot; last login: '+esc(p.lastLogin)+'<br>Y-Money: <b>'+esc(p.ymoney)+'</b> &middot; spirits: <b>'+esc(p.spirits)+'</b> (+'+esc(p.freeSpirits)+' free) &middot; Yo-kai: <b>'+p.yokaiCount+'</b> &middot; stage: '+esc(p.currentStage)+
  '<br>events: '+esc(p.eventChoice?p.eventChoice.mode+' '+p.eventChoice.ids.join(','):'server')+(p.ban?'<br><span class=""pill bad"">banned: '+esc(p.ban.reason)+'</span>':'');}).catch(fail);}
$('grant').onclick=function(){api('POST','/admin/api/player/'+encodeURIComponent(CUR)+'/grant',{ymoney:+$('gy').value,spirits:+$('gs').value,items:parseItems($('gi').value),youkai:parseIds($('gk').value)})
 .then(function(r){toast('Applied'+(r.Notes&&r.Notes.length?' ('+r.Notes.join('; ')+')':''));openPlayer(CUR);}).catch(fail);};
$('lbtn').onclick=function(){api('GET','/admin/api/lookup?type='+$('lt').value+'&q='+encodeURIComponent($('lq').value)).then(function(l){
 $('lres').innerHTML=l.length?l.map(function(x){return '<a href=""#"" data-add=""'+esc(x.id)+'"">'+esc(x.name)+' ('+esc(x.id)+')</a>';}).join(' &middot; '):'nothing found';
 document.querySelectorAll('[data-add]').forEach(function(a){a.onclick=function(ev){ev.preventDefault();var f=$('lt').value==='item'?$('gi'):$('gk');
  f.value=(f.value?f.value+', ':'')+a.dataset.add+($('lt').value==='item'?':1':'');};});}).catch(fail);};
$('rbtn').onclick=function(){api('POST','/admin/api/player/'+encodeURIComponent(CUR)+'/rename',{name:$('rname').value}).then(function(){toast('Renamed');openPlayer(CUR);}).catch(fail);};
$('xbtn').onclick=function(){api('GET','/admin/api/player/'+encodeURIComponent(CUR)+'/export').then(function(j){var a=document.createElement('a');
 a.href=URL.createObjectURL(new Blob([JSON.stringify(j)],{type:'application/json'}));a.download='save-'+(j.friendCode||'player')+'.json';a.click();}).catch(fail);};
$('ifile').onchange=function(){var f=this.files[0];if(!f||!confirm('Replace this save with '+f.name+'? This cannot be undone.'))return;var r=new FileReader();
 r.onload=function(){var j;try{j=JSON.parse(r.result);}catch(e){toast('Not a JSON file');return;}api('POST','/admin/api/player/'+encodeURIComponent(CUR)+'/import',j).then(function(){toast('Save imported');openPlayer(CUR);}).catch(fail);};r.readAsText(f);};
$('ban').onclick=function(){api('POST','/admin/api/player/'+encodeURIComponent(CUR)+'/ban',{reason:$('breason').value}).then(function(){toast('Banned');openPlayer(CUR);}).catch(fail);};
$('unban').onclick=function(){api('POST','/admin/api/player/'+encodeURIComponent(CUR)+'/unban').then(function(){toast('Unbanned');openPlayer(CUR);}).catch(fail);};
$('fsend').onclick=function(){api('POST','/admin/api/gifts',{message:$('fm').value,ymoney:+$('fy').value,spirits:+$('fs').value,items:parseItems($('fi').value),youkai:parseIds($('fk').value)}).then(function(){toast('Gift created');load('gifts');}).catch(fail);};
$('ssave').onclick=function(){api('POST','/admin/api/settings',{ymoneyMultiplier:+$('sy').value,expMultiplier:+$('sx').value,infiniteSpirits:$('si').checked,motd:$('smotd').value,maintenanceMode:$('sm').checked,maintenanceMessage:$('smm').value}).then(function(){toast('Settings saved');}).catch(fail);};
$('sflush').onclick=function(){api('POST','/admin/api/flush').then(function(){toast('All accounts saved');}).catch(fail);};
if(T)load('overview');
</script></body></html>";
    }
}
