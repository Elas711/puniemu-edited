namespace Puniemu.Src.Server.Web
{
    public static class GuidePage
    {
        public const string HTML = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1"">
<title>{{SERVER_NAME}} - Event Guide</title>
<style>
:root{--bg:#fff6e9;--card:#fff;--ink:#35261a;--muted:#8b7765;--accent:#ee5a12;--accent2:#ffb020;--ok:#2f9e44;--line:#f0e2cf}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
body{margin:0;font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--ink)}
header{background:linear-gradient(135deg,var(--accent),#ff7b2e);color:#fff;padding:12px 14px;position:sticky;top:0;z-index:5;display:flex;align-items:center;gap:10px;box-shadow:0 2px 8px rgba(0,0,0,.15)}
header h1{margin:0;font-size:17px;flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
header button{background:rgba(255,255,255,.22);border:0;color:#fff;font-size:14px;font-weight:700;border-radius:18px;padding:7px 12px}
main{padding:12px;max-width:760px;margin:0 auto 40px}
input[type=search]{width:100%;padding:11px 14px;border-radius:22px;border:2px solid var(--line);font-size:15px;background:#fff;margin-bottom:10px}
.tabs{display:flex;gap:6px;overflow-x:auto;padding-bottom:6px;margin-bottom:6px}
.tabs button{flex:none;border:0;border-radius:16px;padding:7px 12px;background:#fff;color:var(--muted);font-weight:700;font-size:13px}
.tabs button.on{background:var(--accent);color:#fff}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}
.card{background:var(--card);border-radius:16px;overflow:hidden;box-shadow:0 2px 6px rgba(80,40,0,.1);cursor:pointer;position:relative}
.card .img{height:110px;background:#ffe9cf center/contain no-repeat}
.card .t{padding:8px 10px 10px;font-weight:700;font-size:13px;line-height:1.3}
.card .s{font-size:11px;color:var(--muted);font-weight:400;margin-top:3px}
.badge{position:absolute;top:8px;left:8px;background:var(--ok);color:#fff;font-size:10px;font-weight:800;padding:3px 7px;border-radius:10px}
.badge.st{left:auto;right:8px;background:#7048e8}
.box{background:var(--card);border-radius:16px;padding:14px;margin-bottom:12px;box-shadow:0 2px 6px rgba(80,40,0,.08)}
.box h2{font-size:15px;margin:0 0 10px;color:var(--accent)}
.hero{background:linear-gradient(135deg,#ffe0bd,#fff3e2);border-radius:18px;padding:16px;margin-bottom:12px}
.hero .cat{font-size:12px;color:var(--muted);font-weight:700;text-transform:uppercase;letter-spacing:.04em}
.hero h2{margin:4px 0 8px;font-size:19px}
.chips{display:flex;gap:6px;flex-wrap:wrap}
.chip{background:#fff;border-radius:12px;padding:4px 9px;font-size:12px;font-weight:700}
.chip.ok{background:#d3f9d8;color:var(--ok)} .chip.no{background:#f1e6d6;color:var(--muted)}
.chars{display:grid;grid-template-columns:repeat(auto-fill,minmax(96px,1fr));gap:8px}
.char{text-align:center;font-size:11px;font-weight:600}
.char .img{height:96px;border-radius:12px;background:#fff7ec center/contain no-repeat;margin-bottom:4px}
ul.list{margin:0;padding-left:18px} ul.list li{margin:5px 0;font-size:14px}
.bonus{display:flex;gap:10px;padding:8px 0;border-bottom:1px solid var(--line);font-size:13px}.bonus:last-child{border:0}
.bonus b{display:block;margin-bottom:2px}.bonus .bi{flex:none;width:44px;height:44px;border-radius:10px;background:#fff7ec center/contain no-repeat}
a.btn,button.btn{display:block;width:100%;text-align:center;border:0;border-radius:14px;padding:13px;font-size:15px;font-weight:800;background:var(--accent);color:#fff;text-decoration:none;margin-top:8px}
button.btn.off{background:#f1e6d6;color:var(--ink)} a.btn.sec{background:#7048e8}
.muted{color:var(--muted);font-size:13px} .row{display:flex;gap:8px}
.help h3{font-size:14px;margin:14px 0 4px} .help p{margin:0;font-size:14px;line-height:1.5}
#toast{position:fixed;left:50%;bottom:20px;transform:translateX(-50%);background:var(--ink);color:#fff;padding:10px 16px;border-radius:20px;font-size:14px;display:none;z-index:9}
.lb{position:fixed;inset:0;background:rgba(0,0,0,.85);display:none;align-items:center;justify-content:center;z-index:10;flex-direction:column;color:#fff}
.lb img{max-width:94%;max-height:80%}
</style></head>
<body>
<header><button id=""back"" style=""display:none"">&#8592;</button><h1 id=""title"">Event Guide</h1><button id=""wikiBtn"" onclick=""location.href='/web/wiki.nhn?userId='+encodeURIComponent(USER_ID)"">&#128218; Wiki</button><button id=""helpBtn"">? Help</button></header>
<main id=""app"">Loading...</main>
<div id=""toast""></div>
<div class=""lb"" id=""lb""><img id=""lbimg"" alt=""""><p id=""lbname""></p></div>
<script>
var USER_ID={{USER_ID}};
var DATA=null, ACTIVE={}, MODE='server', CAT='All';
function $(i){return document.getElementById(i);}
function esc(s){return String(s==null?'':s).replace(/[&<>""']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c];});}
function img(id,w){return id?'https://drive.google.com/thumbnail?id='+encodeURIComponent(id)+'&sz=w'+(w||300):'';}
function bg(id,w){return id?' style=""background-image:url(\''+img(id,w)+'\')""':'';}
function req(m,u,b,cb){var x=new XMLHttpRequest();x.open(m,u,true);x.setRequestHeader('Content-Type','application/json');
 x.onreadystatechange=function(){if(x.readyState===4){var j=null;try{j=JSON.parse(x.responseText);}catch(e){}cb(x.status,j);}};x.send(b?JSON.stringify(b):null);}
function toast(t){var e=$('toast');e.textContent=t;e.style.display='block';clearTimeout(e._t);e._t=setTimeout(function(){e.style.display='none';},2600);}
function loadActive(cb){req('GET','/web/api/events?userId='+encodeURIComponent(USER_ID),null,function(st,j){ACTIVE={};
 if(j){MODE=j.choice&&j.choice.mode==='custom'?'custom':'server';(MODE==='custom'?j.choice.ids:j.serverActive).forEach(function(i){ACTIVE[i]=1;});}cb();});}
function nav(h){location.hash=h;}
window.onhashchange=route;
$('back').onclick=function(){history.length>1?history.back():nav('');};
$('helpBtn').onclick=function(){nav('#/help');};
function route(){var h=location.hash.replace(/^#\/?/,'').split('/');window.scrollTo(0,0);
 $('back').style.display=h[0]?'':'none';
 if(h[0]==='s')return series(+h[1]); if(h[0]==='e')return eventPage(+h[1],+h[2]); if(h[0]==='help')return help(); home();}
function home(){$('title').textContent='Event Guide';
 var cats=['All'];DATA.series.forEach(function(s){if(cats.indexOf(s.category)<0)cats.push(s.category);});
 var a='<input type=""search"" id=""q"" placeholder=""Search events, series or characters..."">';
 a+='<div class=""tabs"">'+cats.map(function(c){return '<button data-c=""'+esc(c)+'""'+(c===CAT?' class=""on""':'')+'>'+esc(c)+'</button>';}).join('')+'<button data-c=""Playable"" '+(CAT==='Playable'?'class=""on""':'')+'>&#9654; Playable here</button></div><div id=""res""></div>';
 $('app').innerHTML=a;
 var tabs=document.querySelectorAll('.tabs button');for(var i=0;i<tabs.length;i++)tabs[i].onclick=function(){CAT=this.getAttribute('data-c');home();};
 $('q').oninput=function(){list(this.value);};list('');}
function list(q){q=(q||'').toLowerCase();var h='';
 if(q||CAT==='Playable'){ // event results
  DATA.series.forEach(function(s,si){s.events.forEach(function(e,ei){
   if(CAT==='Playable'&&!e.available)return; if(q&&(s.series+' '+e.name+' '+(e.names||'')).toLowerCase().indexOf(q)<0)return;
   h+=evCard(s,e,si,ei);});});
  $('res').innerHTML=h?'<div class=""grid"">'+h+'</div>':'<p class=""muted"">Nothing found.</p>';
 }else{
  DATA.series.forEach(function(s,si){if(CAT!=='All'&&s.category!==CAT)return;var n=s.events.filter(function(e){return e.available;}).length;
   h+='<div class=""card"" data-go=""#/s/'+si+'"">'+(n?'<span class=""badge"">'+n+' playable</span>':'')+'<div class=""img""'+bg(s.cover)+'></div><div class=""t"">'+esc(s.series)+'<div class=""s"">'+esc(s.category)+' &middot; '+s.events.length+' event'+(s.events.length>1?'s':'')+'</div></div></div>';});
  $('res').innerHTML='<div class=""grid"">'+h+'</div>';
 }
 bindGo();}
function evCard(s,e,si,ei){return '<div class=""card"" data-go=""#/e/'+si+'/'+ei+'"">'+(e.available?'<span class=""badge"">PLAYABLE</span>':'')+(e.story?'<span class=""badge st"">STORY</span>':'')+
 '<div class=""img""'+bg(e.cover)+'></div><div class=""t"">'+esc(e.name)+'<div class=""s"">'+esc(s.series)+(e.characters?' &middot; '+e.characters+' characters':'')+'</div></div></div>';}
function bindGo(){var c=document.querySelectorAll('[data-go]');for(var i=0;i<c.length;i++)c[i].onclick=function(){nav(this.getAttribute('data-go'));};}
function series(si){var s=DATA.series[si];if(!s)return home();$('title').textContent=s.series;
 $('app').innerHTML='<div class=""hero""><div class=""cat"">'+esc(s.category)+'</div><h2>'+esc(s.series)+'</h2><div class=""chips""><span class=""chip"">'+s.events.length+' events</span><span class=""chip ok"">'+
  s.events.filter(function(e){return e.available;}).length+' playable here</span></div></div><div class=""grid"">'+s.events.map(function(e,ei){return evCard(s,e,si,ei);}).join('')+'</div>';bindGo();}
function eventPage(si,ei){$('app').innerHTML='Loading...';
 req('GET','/web/api/guide/event?s='+si+'&e='+ei,null,function(st,d){if(!d||d.error){$('app').innerHTML='<p class=""muted"">Event not found.</p>';return;}
  $('title').textContent=d.name;
  var on=d.eventIds.some(function(i){return ACTIVE[i];});
  var h='<div class=""hero""><div class=""cat"">'+esc(d.category)+' &middot; '+esc(d.series)+'</div><h2>'+esc(d.name)+'</h2><div class=""chips"">'+
   (d.available?'<span class=""chip ok"">Playable on this server</span>':'<span class=""chip no"">Not in this server\'s data</span>')+
   (d.period?'<span class=""chip"">Originally: '+esc(d.period)+'</span>':'')+(d.maps.length?'<span class=""chip"">'+d.maps.length+' map(s)</span>':'')+'</div>';
  if(d.available){h+=DATA.allowPlayerChoice?'<button class=""btn'+(on?' off':'')+'"" id=""play"">'+(on?'&#10003; Event is ON for you - tap to turn off':'&#9654; Play this event')+'</button>':'<p class=""muted"">The server owner chooses which events are on.</p>';}
  h+='</div>';
  if(d.notes)h+='<div class=""box""><h2>Notes</h2><p>'+esc(d.notes)+'</p></div>';
  if(d.characters.length)h+='<div class=""box""><h2>Featured Yo-kai &amp; characters ('+d.characters.length+')</h2><div class=""chars"">'+d.characters.map(function(c){
   return '<div class=""char"" data-img=""'+esc(c.image)+'"" data-name=""'+esc(c.name)+'""><div class=""img""'+bg(c.image,200)+'></div>'+esc(c.name)+'</div>';}).join('')+'</div></div>';
  if(d.bonuses.length)h+='<div class=""box""><h2>Bonus Yo-kai</h2><p class=""muted"">Put these Yo-kai in your team to get the event bonuses.</p>'+d.bonuses.map(function(b){return '<div class=""bonus"">'+(b.image?'<span class=""bi""'+bg(b.image,120)+'></span>':'')+'<div><b>'+esc(b.youkai)+'</b>'+esc(b.effect).replace(/\n/g,' ')+'</div></div>';}).join('')+'</div>';
  if(d.missions.length)h+='<div class=""box""><h2>Event missions</h2><ul class=""list"">'+d.missions.map(function(m){return '<li>'+esc(m).replace(/\n/g,' ')+'</li>';}).join('')+'</ul></div>';
  if(d.maps.length)h+='<div class=""box""><h2>Maps</h2><ul class=""list"">'+d.maps.map(function(m){return '<li>'+esc(m)+'</li>';}).join('')+'</ul></div>';
  if(d.story||d.driveFolder){h+='<div class=""box""><h2>Story &amp; pictures</h2>'+(d.story?'<p>Fan translation: <b>'+esc(d.story.title)+'</b></p><a class=""btn sec"" target=""_blank"" href=""'+esc(d.story.url)+'"">&#128214; Read the story</a>':'')+
   (d.driveFolder?'<a class=""btn"" target=""_blank"" href=""'+esc(d.driveFolder)+'"">&#128444; All pictures of this event</a>':'')+'</div>';}
  h+='<div class=""box""><h2>Tips</h2><ul class=""list""><li>Use the bonus Yo-kai above (if any) - they boost points and damage on the event maps.</li><li>Event bosses can often be befriended: bring your best food and use Soultimates to fill the befriend gauge.</li><li>Missions give rewards - check them in the Mission menu during the event.</li><li>Need more spirits or Y-Money? Ask the server owner about gifts.</li></ul><p class=""muted"" style=""margin-top:8px""><a href=""#/help"">How events work on this server &rarr;</a></p></div>';
  $('app').innerHTML=h;
  var ch=document.querySelectorAll('[data-img]');for(var i=0;i<ch.length;i++)ch[i].onclick=function(){$('lbimg').src=img(this.getAttribute('data-img'),1000);$('lbname').textContent=this.getAttribute('data-name');$('lb').style.display='flex';};
  if($('play'))$('play').onclick=function(){req('POST','/web/api/guide/play',{userId:USER_ID,s:si,e:ei,remove:on},function(st,j){
   if(st===200){toast(on?'Event turned off':'Event turned on! Go back to the title screen to load it.');loadActive(function(){eventPage(si,ei);});}else toast(j&&j.error||'Error');});};
 });}
$('lb').onclick=function(){this.style.display='none';};
function help(){$('title').textContent='How events work';
 $('app').innerHTML='<div class=""box help""><h2>Events on {{SERVER_NAME}}</h2>'+
 '<h3>Which events can I play?</h3><p>The guide lists every event Puni Puni ever had. Events marked <b>PLAYABLE</b> have their game data on this server and can be switched on.</p>'+
 '<h3>Turning an event on</h3><p>Open an event and tap <b>Play this event</b>, or pick several at once on the <a href=""/web/portal.nhn?userId='+encodeURIComponent(USER_ID)+'"">Events &amp; Server</a> page. Then go back to the title screen (or restart the game) - the event data is loaded when you log in.</p>'+
 '<h3>Where is the event?</h3><p>Event maps appear in the map list. Event bosses, missions and bonus Yo-kai are shown on the event\'s page in this guide.</p>'+
 '<h3>Event bonuses</h3><p>Some Yo-kai boost your score, points or damage during an event. Put them in your team - the guide lists them under <b>Bonus Yo-kai</b>.</p>'+
 '<h3>Befriending event Yo-kai</h3><p>Defeat the event\'s enemies and bosses. Feeding them their favourite food and finishing with Soultimates raises the chance that they join you.</p>'+
 '<h3>Stories and pictures</h3><p>Many events have a fan-translated story and pictures of their characters, linked from the community archive. They open in your browser.</p>'+
 '<h3>Something missing?</h3><p>If an event you want is not playable, ask the server owner - old events can be imported from data dumps.</p></div>'+
 (DATA.otherStories&&DATA.otherStories.length?'<div class=""box""><h2>More translated stories</h2>'+DATA.otherStories.map(function(s){return '<a class=""btn sec"" target=""_blank"" href=""'+esc(s.url)+'"">'+esc(s.title)+'</a>';}).join('')+'</div>':'')+
 (DATA.source?'<p class=""muted"">Character art and stories: community archive (<a target=""_blank"" href=""'+esc(DATA.source)+'"">Google Drive</a>).</p>':'');}
req('GET','/web/api/guide',null,function(st,j){if(!j){$('app').innerHTML='Could not load the guide.';return;}DATA=j;loadActive(route);});
</script></body></html>";
    }
}
