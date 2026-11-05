namespace Puniemu.Src.Server.Web.Wiki
{
    public static class WikiPage
    {
        public const string HTML = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1"">
<title>{{SERVER_NAME}} - Wiki</title>
<style>
:root{--bg:#fff6e9;--card:#fff;--ink:#35261a;--muted:#8b7765;--accent:#ee5a12;--ok:#2f9e44;--line:#f0e2cf;--blue:#1c7ed6;--purple:#7048e8}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
body{margin:0;font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--ink)}
header{background:linear-gradient(135deg,#1c7ed6,#4dabf7);color:#fff;padding:12px 14px;position:sticky;top:0;z-index:5;display:flex;align-items:center;gap:10px;box-shadow:0 2px 8px rgba(0,0,0,.15)}
header h1{margin:0;font-size:17px;flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
header button{background:rgba(255,255,255,.22);border:0;color:#fff;font-size:15px;font-weight:700;border-radius:18px;padding:7px 12px}
main{padding:12px;max-width:760px;margin:0 auto 40px}
input[type=search],select{width:100%;padding:11px 14px;border-radius:22px;border:2px solid var(--line);font-size:15px;background:#fff;margin-bottom:10px;color:var(--ink)}
.filters{display:flex;gap:8px}.filters select{flex:1}
.tiles{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}
.tile{background:var(--card);border-radius:16px;padding:14px;box-shadow:0 2px 6px rgba(80,40,0,.1);cursor:pointer}
.tile .ic{font-size:28px}.tile b{display:block;margin-top:6px;font-size:15px}.tile small{color:var(--muted)}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(92px,1fr));gap:8px}
.yk{background:var(--card);border-radius:14px;padding:8px 4px;text-align:center;font-size:11px;font-weight:700;box-shadow:0 1px 4px rgba(80,40,0,.08);cursor:pointer}
.yk .img{height:64px;background:center/contain no-repeat;margin-bottom:4px;display:flex;align-items:center;justify-content:center}
.ph{width:54px;height:54px;border-radius:50%;color:#fff;font-size:24px;font-weight:800;display:flex;align-items:center;justify-content:center}
.rk{display:inline-block;font-size:10px;font-weight:800;padding:1px 6px;border-radius:8px;background:#ffe8cc;color:#a0410b;margin-top:3px}
.box{background:var(--card);border-radius:16px;padding:14px;margin-bottom:12px;box-shadow:0 2px 6px rgba(80,40,0,.08)}
.box h2{font-size:15px;margin:0 0 10px;color:var(--blue)} .box h3{font-size:14px;margin:14px 0 4px}
.box p,.box li{font-size:14px;line-height:1.5}
.hero{display:flex;gap:14px;align-items:center;background:linear-gradient(135deg,#dbeafe,#fff3e2);border-radius:18px;padding:14px;margin-bottom:12px}
.hero .img{width:96px;height:96px;flex:none;background:#fff center/contain no-repeat;border-radius:18px;display:flex;align-items:center;justify-content:center}
.hero h2{margin:0 0 6px;font-size:20px}
.chips{display:flex;gap:6px;flex-wrap:wrap}.chip{background:#fff;border-radius:12px;padding:4px 9px;font-size:12px;font-weight:700}
.stats{display:grid;grid-template-columns:1fr 1fr;gap:8px}.stat{background:#f8f3ec;border-radius:12px;padding:8px 10px}.stat small{color:var(--muted);display:block;font-size:11px}.stat b{font-size:16px}
.row{display:flex;gap:10px;padding:9px 0;border-bottom:1px solid var(--line);align-items:flex-start;cursor:pointer}.row:last-child{border:0}
.row .t{flex:1;font-size:14px}.row .t small{display:block;color:var(--muted);font-size:12px;margin-top:2px}
.tag{display:inline-block;font-size:10px;font-weight:800;padding:2px 6px;border-radius:8px;color:#fff;background:var(--accent);margin-left:4px;vertical-align:middle}
.tag.h{background:var(--purple)}.tag.b{background:#c92a2a}
.mini{display:flex;gap:6px;flex-wrap:wrap;margin-top:6px}.mini span{display:flex;align-items:center;gap:4px;background:#f8f3ec;border-radius:10px;padding:2px 8px 2px 2px;font-size:12px}
.mini i{width:26px;height:26px;background:center/contain no-repeat;display:inline-block}
.muted{color:var(--muted);font-size:13px}
table.gl{width:100%;border-collapse:collapse}table.gl td{padding:6px;border-bottom:1px solid var(--line);font-size:14px}table.gl td:first-child{font-weight:700;width:45%}
a{color:var(--blue)} .pill{display:inline-block;background:#e7f5ff;color:var(--blue);border-radius:10px;padding:2px 8px;font-size:12px;font-weight:700;margin:2px}
details summary{cursor:pointer;font-weight:700;padding:6px 0}
</style></head><body>
<header><button id=""back"" style=""display:none"">&#8592;</button><h1 id=""title"">Wiki</h1><button id=""home"">&#8962;</button></header>
<main id=""app"">Loading...</main>
<script>
var USER_ID={{USER_ID}};
var RANKS=['','E','D','C','B','A','S','SS','SSS','Z','ZZ','ZZZ','UZ','UZ+'];
var TRIBES=['None','Brave','Mysterious','Tough','Charming','Heartful','Shady','Eerie','Slippery','Enma','Wicked'];
var FOODS=['','Rice balls','Sandwiches','Sweets','Chocobars','Milk','Juice','Burgers','Ramen','Chinese food','Vegetables','Meat','Seafood','Sushi','Curry','Dessert','Oden','Soba','Chips','Ice cream'];
var IDX=null, CACHE={}, F={q:'',rank:'',tribe:''};
function $(i){return document.getElementById(i);}
function esc(s){return String(s==null?'':s).replace(/[&<>""']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c];});}
function nl(s){return esc(s).replace(/\n/g,'<br>');}
var TC=['#adb5bd','#e8590c','#7048e8','#a0522d','#e64980','#2f9e44','#495057','#5f3dc4','#1c7ed6','#c92a2a','#212529'];
function ph(name,tribe){return '<span class=""ph"" style=""background:'+(TC[tribe]||TC[0])+'"">'+esc((name||'?').charAt(0))+'</span>';}
function im(n){return n?' style=""background-image:url(\'/web/wiki/img/'+encodeURIComponent(n)+'\')""':'';}
function get(u,cb){if(CACHE[u])return cb(CACHE[u]);var x=new XMLHttpRequest();x.open('GET',u,true);x.onreadystatechange=function(){if(x.readyState===4){var j=null;try{j=JSON.parse(x.responseText);}catch(e){}if(j)CACHE[u]=j;cb(j);}};x.send();}
function nav(h){location.hash=h;}
window.onhashchange=route;
$('back').onclick=function(){history.back();};$('home').onclick=function(){nav('');};
function bind(){var c=document.querySelectorAll('[data-go]');for(var i=0;i<c.length;i++)c[i].onclick=function(e){e.preventDefault();nav(this.getAttribute('data-go'));};}
function page(t,h){$('title').textContent=t;$('app').innerHTML=h;bind();window.scrollTo(0,0);}
function route(){var h=location.hash.replace(/^#\/?/,'').split('/');$('back').style.display=h[0]?'':'none';
 var r={yokai:yokaiList,y:yokai,maps:maps,m:map,items:items,missions:missions,gachas:gachas,guides:guides,a:article,server:server,links:links};
 (r[h[0]]||home)(decodeURIComponent(h[1]||''));}
function home(){var c=IDX.counts;
 var t=[['#/yokai','&#128123;','Yo-kai',c.yokai+' Yo-kai'],['#/maps','&#128506;','Maps & stages',c.maps+' maps, '+c.stages+' stages'],['#/items','&#127873;','Items',c.items+' items'],
  ['#/missions','&#128203;','Missions',c.missions+' missions'],['#/gachas','&#127920;','Crank-a-kai',c.gachas+' machines'],['#/guides','&#128218;','Guides',IDX.articles.length+' articles'],
  ['EVENTS','&#127882;','Event Guide','every event'],['#/server','&#9881;','This server','rules & features'],['#/links','&#128279;','More help','community sites']];
 page('Wiki','<input type=""search"" id=""gq"" placeholder=""Search Yo-kai, items, maps...""><div id=""gres""></div><div class=""tiles"">'+t.map(function(x){
  return '<div class=""tile"" '+(x[0]==='EVENTS'?'id=""evt""':'data-go=""'+x[0]+'""')+'><span class=""ic"">'+x[1]+'</span><b>'+x[2]+'</b><small>'+x[3]+'</small></div>';}).join('')+'</div>'+
  (IDX.settings.motd?'<div class=""box"" style=""margin-top:12px""><h2>News</h2><p>'+nl(IDX.settings.motd)+'</p></div>':''));
 $('evt').onclick=function(){location.href='/web/guide.nhn?userId='+encodeURIComponent(USER_ID);};
 $('gq').oninput=function(){var q=this.value.toLowerCase();if(q.length<2){$('gres').innerHTML='';return;}
  get('/web/api/wiki/yokai',function(Y){get('/web/api/wiki/items',function(I){get('/web/api/wiki/maps',function(M){var h='';
   Y.filter(function(y){return y.name.toLowerCase().indexOf(q)>=0;}).slice(0,12).forEach(function(y){h+='<div class=""row"" data-go=""#/y/'+y.id+'""><span class=""mini""><span><i'+im(y.img)+'></i>Yo-kai</span></span><div class=""t"">'+esc(y.name)+' <span class=""rk"">'+RANKS[y.rank]+'</span></div></div>';});
   M.filter(function(m){return m.name.toLowerCase().indexOf(q)>=0;}).slice(0,6).forEach(function(m){h+='<div class=""row"" data-go=""#/m/'+m.id+'""><div class=""t"">&#128506; '+esc(m.name)+'<small>Map &middot; '+m.stages+' stages</small></div></div>';});
   I.filter(function(i){return i.name.toLowerCase().indexOf(q)>=0;}).slice(0,8).forEach(function(i){h+='<div class=""row"" data-go=""#/items/'+encodeURIComponent(i.name)+'""><div class=""t"">&#127873; '+esc(i.name)+'<small>'+esc(i.text).slice(0,80)+'</small></div></div>';});
   $('gres').innerHTML=h?'<div class=""box"">'+h+'</div>':'<p class=""muted"">Nothing found.</p>';bind();});});});};}
function yokaiList(){get('/web/api/wiki/yokai',function(Y){
 page('Yo-kai','<input type=""search"" id=""q"" placeholder=""Search Yo-kai..."" value=""'+esc(F.q)+'""><div class=""filters""><select id=""fr""><option value="""">All ranks</option>'+RANKS.slice(1).map(function(r,i){return '<option value=""'+(i+1)+'""'+(F.rank==String(i+1)?' selected':'')+'>Rank '+r+'</option>';}).join('')+
  '</select><select id=""ft""><option value="""">All tribes</option>'+TRIBES.slice(1).map(function(t,i){return '<option value=""'+(i+1)+'""'+(F.tribe==String(i+1)?' selected':'')+'>'+t+'</option>';}).join('')+'</select></div><p class=""muted"" id=""cnt""></p><div class=""grid"" id=""yl""></div>');
 function draw(){var q=F.q.toLowerCase();var L=Y.filter(function(y){return (!q||y.name.toLowerCase().indexOf(q)>=0)&&(!F.rank||y.rank==F.rank)&&(!F.tribe||y.tribe==F.tribe);});
  $('cnt').textContent=L.length+' Yo-kai';
  $('yl').innerHTML=L.slice(0,400).map(function(y){return '<div class=""yk"" data-go=""#/y/'+y.id+'""><div class=""img""'+im(y.img)+'>'+(y.img?'':ph(y.name,y.tribe))+'</div>'+esc(y.name)+'<br><span class=""rk"">'+RANKS[y.rank]+'</span></div>';}).join('');bind();}
 $('q').oninput=function(){F.q=this.value;draw();};$('fr').onchange=function(){F.rank=this.value;draw();};$('ft').onchange=function(){F.tribe=this.value;draw();};draw();});}
function yokai(id){get('/web/api/wiki/yokai/'+encodeURIComponent(id),function(y){if(!y)return page('Yo-kai','<p class=""muted"">Not found.</p>');
 var h='<div class=""hero""><div class=""img""'+im(y.img)+'>'+(y.img?'':ph(y.name,y.tribe))+'</div><div><h2>'+esc(y.name)+'</h2><div class=""chips""><span class=""chip"">Rank '+RANKS[y.rank]+'</span><span class=""chip"">'+TRIBES[y.tribe]+'</span>'+(FOODS[y.food]?'<span class=""chip"">Likes: '+FOODS[y.food]+'</span>':'')+'</div></div></div>';
 if(y.description)h+='<div class=""box""><p>'+nl(y.description)+'</p></div>';
 h+='<div class=""box""><h2>Stats</h2><div class=""stats""><div class=""stat""><small>HP (Lv1 &rarr; max)</small><b>'+y.baseHp+' &rarr; '+y.maxHp+'</b></div><div class=""stat""><small>Attack (Lv1 &rarr; max)</small><b>'+y.baseAtk+' &rarr; '+y.maxAtk+'</b></div><div class=""stat""><small>Max level</small><b>'+y.maxLevel+'</b></div><div class=""stat""><small>Yo-kai id</small><b>'+esc(y.id)+'</b></div></div></div>';
 if(y.skillName||y.skillText)h+='<div class=""box""><h2>Soultimate: '+esc(y.skillName)+'</h2><p>'+nl(y.skillText)+'</p></div>';
 if(y.bonus)h+='<div class=""box""><h2>Bonus effect</h2><p>'+nl(y.bonus)+'</p></div>';
 if(y.evolvesTo||y.evolvesFrom.length){h+='<div class=""box""><h2>Evolution</h2>';
  y.evolvesFrom.forEach(function(e){h+='<div class=""row"" data-go=""#/y/'+e.id+'""><div class=""t"">&#11013; Evolves from <b>'+esc(e.name)+'</b><small>at level '+e.level+'</small></div></div>';});
  if(y.evolvesTo)h+='<div class=""row"" data-go=""#/y/'+y.evolvesTo.id+'""><div class=""t"">&#10145; Evolves into <b>'+esc(y.evolvesTo.name)+'</b><small>at level '+y.evolvesTo.level+'</small></div></div>';h+='</div>';}
 h+='<div class=""box""><h2>Where to find</h2>'+(y.where.length?(function(){var g={},o=[];y.where.forEach(function(w){if(!g[w.mapId]){g[w.mapId]={map:w.map,id:w.mapId,s:[]};o.push(g[w.mapId]);}g[w.mapId].s.push(esc(w.stage)+(w.boss?' (boss)':'')+(w.hidden?' (hidden)':''));});
  return o.map(function(m){return '<div class=""row"" data-go=""#/m/'+m.id+'""><div class=""t"">'+esc(m.map)+'<small>'+m.s.join(', ')+'</small></div></div>';}).join('');})():
  '<p class=""muted"">Not found on any stage in this server\'s data - it may come from the Crank-a-kai, an event or a reward.</p>')+'</div>';
 page(y.name,h);});}
function maps(){get('/web/api/wiki/maps',function(M){var types={1:'Main maps',2:'Main maps',3:'Event maps',4:'Special rooms',5:'Event rooms',7:'Training'};var groups={};
 M.forEach(function(m){var g=types[m.type]||'Other maps';(groups[g]=groups[g]||[]).push(m);});
 var h='';for(var g in groups){h+='<div class=""box""><h2>'+g+'</h2>'+groups[g].map(function(m){return '<div class=""row"" data-go=""#/m/'+m.id+'""><div class=""t"">'+esc(m.name||('Map '+m.id))+'<small>'+m.stages+' stages'+(m.bosses?' &middot; '+m.bosses+' bosses':'')+(m.hidden?' &middot; '+m.hidden+' hidden':'')+'</small></div></div>';}).join('')+'</div>';}
 page('Maps & stages',h);});}
function map(id){get('/web/api/wiki/map/'+encodeURIComponent(id),function(m){if(!m)return page('Map','Not found');
 var h=(m.description?'<div class=""box""><p>'+nl(m.description)+'</p></div>':'')+'<div class=""box""><h2>Stages</h2>'+(m.stages.length?m.stages.map(function(s){
  return '<details><summary>'+(s.no?s.no+'. ':'')+esc(s.name||'Stage')+(s.boss?'<span class=""tag b"">BOSS</span>':'')+(s.hidden?'<span class=""tag h"">HIDDEN</span>':'')+'</summary>'+
  (s.unlock?'<p><b>How to unlock:</b> '+esc(s.unlock)+'</p>':'')+(s.level?'<p class=""muted"">Recommended level: '+s.level+'</p>':'')+
  (s.stars.length?'<p><b>Stars:</b></p><ol>'+s.stars.map(function(t){return '<li>'+esc(t)+'</li>';}).join('')+'</ol>':'')+
  (s.enemies.length?'<p><b>Enemies:</b></p><div class=""mini"">'+s.enemies.map(function(e){return '<span data-go=""#/y/'+e.id+'""><i'+im(e.img)+'></i>'+esc(e.name)+'</span>';}).join('')+'</div>':'')+'</details>';}).join(''):'<p class=""muted"">No stage data.</p>')+'</div>';
 page(m.name,h);});}
function filterList(title,url,render,init){get(url,function(L){page(title,'<input type=""search"" id=""q"" placeholder=""Search..."" value=""'+esc(init||'')+'""><p class=""muted"" id=""cnt""></p><div class=""box"" id=""l""></div>');
 function draw(){var q=$('q').value.toLowerCase();var R=L.filter(function(x){return JSON.stringify(x).toLowerCase().indexOf(q)>=0;});$('cnt').textContent=R.length+' results';$('l').innerHTML=R.slice(0,300).map(render).join('')||'<p class=""muted"">Nothing found.</p>';bind();}
 $('q').oninput=draw;draw();});}
function items(q){filterList('Items','/web/api/wiki/items',function(i){return '<div class=""row""><div class=""t""><b>'+esc(i.name)+'</b><small>'+nl(i.text)+'</small></div></div>';},q);}
function missions(){filterList('Missions','/web/api/wiki/missions',function(m){return '<div class=""row""><div class=""t""><b>'+esc(m.title)+'</b><small>'+esc(m.text)+(m.reward?' &middot; Reward: '+esc(m.reward):'')+'</small></div></div>';});}
function gachas(){get('/web/api/wiki/gachas',function(G){page('Crank-a-kai','<p class=""muted"">Contents and odds of the Crank-a-kai machines on this server.</p>'+(G.length?G.map(function(g){
 return '<div class=""box""><h2>Crank-a-kai #'+esc(g.id)+'</h2>'+(g.price?'<p class=""muted"">Price: '+esc(g.price)+'</p>':'')+g.tiers.map(function(t){
  return '<details><summary>Rank '+esc(t.rank)+(t.chance!=null?' &middot; '+t.chance+'%':'')+' ('+t.yokai.length+')</summary><div class=""mini"">'+t.yokai.map(function(y){return '<span data-go=""#/y/'+y.id+'""><i'+im(y.image)+'></i>'+esc(y.name)+'</span>';}).join('')+'</div></details>';}).join('')+'</div>';}).join(''):'<p class=""muted"">No crank-a-kai data.</p>'));});}
function guides(){page('Guides','<div class=""box"">'+IDX.articles.map(function(a){return '<div class=""row"" data-go=""#/a/'+a.id+'""><div class=""t"">'+a.icon+' '+esc(a.title)+'</div></div>';}).join('')+'</div>');}
function article(id){get('/web/api/wiki/article/'+encodeURIComponent(id),function(a){if(!a)return page('Guide','Not found');page(a.title.replace(/&[^;]+;/g,''),'<div class=""box""><h2>'+a.icon+' '+esc(a.title)+'</h2>'+a.html+'</div>');});}
function server(){var s=IDX.settings;var x=function(v){return v===1?'normal':'x'+v;};
 page('This server','<div class=""box""><h2>'+esc(IDX.server)+'</h2><p>'+esc(IDX.game)+' private server.</p><div class=""stats"">'+
 '<div class=""stat""><small>Y-Money from battles</small><b>'+x(s.ymoney)+'</b></div><div class=""stat""><small>EXP from battles</small><b>'+x(s.exp)+'</b></div>'+
 '<div class=""stat""><small>Spirits</small><b>'+(s.infiniteSpirits?'Infinite':'Normal')+'</b></div><div class=""stat""><small>Events</small><b>'+(s.eventMode==='custom'?'Chosen by the server':s.eventMode==='rotation'?'Rotating':'Original')+'</b></div>'+
 '<div class=""stat""><small>Choose your own events</small><b>'+(s.playerEvents?'Yes':'No')+'</b></div><div class=""stat""><small>Game text</small><b>'+(s.english?'English':'Original')+'</b></div></div>'+
 '<h3>Good to know</h3><ul><li>In-app purchases are disabled - everything is free.</li><li>Gifts from the server owner arrive automatically when you log in'+(s.gifts?' ('+s.gifts+' gift'+(s.gifts>1?'s':'')+' active)':'')+'.</li>'+
 '<li>Open <b>Help</b> in the game menu for the Event Guide, this wiki and the event selection.</li><li>Link your save to an e-mail (see the <a href=""#/a/save"">guide</a>).</li></ul></div>');}
function links(){page('More help','<div class=""box""><p class=""muted"">Community sites with more guides. They open in your browser.</p>'+IDX.links.map(function(l){return '<div class=""row""><div class=""t""><a target=""_blank"" href=""'+esc(l.url)+'"">'+esc(l.title)+'</a><small>'+esc(l.text)+'</small></div></div>';}).join('')+'</div>');}
get('/web/api/wiki/index',function(j){if(!j){$('app').innerHTML='Could not load the wiki.';return;}IDX=j;route();});
</script></body></html>";
    }
}
