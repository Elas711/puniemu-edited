using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Events;
using Puniemu.Src.Server.Infrastructure;
using System.Net;

namespace Puniemu.Src.Server.Web
{
    // The "Events & Server" page players open from the in-game Help page. Lets every player pick the
    // events they want to play, independent of other players.
    public static class PlayerPortal
    {
        private static async Task<string?> ResolveGdkeyAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || userId.Length > 64) return null;
            try
            {
                var gdkey = await UserDataManager.Logic.UserDataManager.GetGdkeyFromUserId(userId);
                return string.IsNullOrEmpty(gdkey) ? null : gdkey;
            }
            catch
            {
                return null;
            }
        }

        public static async Task HandlePageAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            var html = PAGE
                .Replace("{{SERVER_NAME}}", WebUtility.HtmlEncode(DataManager.Logic.DataManager.ServerName ?? "Puniemu"))
                .Replace("{{MOTD}}", WebUtility.HtmlEncode(ServerStateStore.Current.Motd).Replace("\n", "<br>"))
                .Replace("{{USER_ID}}", JsonConvert.SerializeObject(ctx.Request.Query["userId"].ToString()));
            await ctx.Response.WriteAsync(html);
        }

        public static async Task HandleGetEventsAsync(HttpContext ctx)
        {
            var gdkey = await ResolveGdkeyAsync(ctx.Request.Query["userId"]);
            var s = ServerStateStore.Current.Events;
            PlayerEventChoice? choice = gdkey != null ? await EventManager.GetPlayerChoiceAsync(gdkey) : null;
            var serverIds = s.Mode == "custom" ? s.ActiveEventIds : EventManager.Catalog.Where(x => x.Selectable && x.Sources.Contains("master")).Select(x => x.EventId).ToList();
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                known = gdkey != null,
                allowPlayerChoice = s.AllowPlayerChoice,
                serverMode = s.Mode,
                serverActive = serverIds,
                choice = choice ?? new PlayerEventChoice(),
                events = EventManager.Catalog.Where(x => x.Selectable).Select(x => new
                {
                    eventId = x.EventId,
                    name = x.Name,
                    description = x.Description,
                    period = x.Period,
                    playable = x.Playable,
                    maps = x.MapIds.Count
                })
            }));
        }

        public static async Task HandleSetEventsAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            if (!ServerStateStore.Current.Events.AllowPlayerChoice)
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsync("{\"error\":\"The server owner disabled personal event selection.\"}");
                return;
            }
            JObject body;
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var text = await reader.ReadToEndAsync();
                if (text.Length > 100_000) throw new Exception();
                body = JObject.Parse(text);
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("{\"error\":\"bad request\"}");
                return;
            }
            var gdkey = await ResolveGdkeyAsync(body.Value<string>("userId"));
            if (gdkey == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.WriteAsync("{\"error\":\"Unknown player. Open this page from the in-game Help menu.\"}");
                return;
            }
            var known = EventManager.Catalog.Where(x => x.Selectable).Select(x => x.EventId).ToHashSet();
            var choice = new PlayerEventChoice
            {
                Mode = body.Value<string>("mode") == "custom" ? "custom" : "server",
                Ids = (body["ids"] as JArray)?.Select(x => x.Value<long>()).Where(known.Contains).Distinct().Take(500).ToList() ?? new()
            };
            await EventManager.SetPlayerChoiceAsync(gdkey, choice);
            Metrics.Incr("player_event_choice_saved");
            await ctx.Response.WriteAsync("{\"ok\":true}");
        }

        private const string PAGE = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1"">
<title>{{SERVER_NAME}} - Events</title>
<style>
:root{--bg:#fff7e6;--card:#fff;--ink:#3b2a1a;--muted:#8a7664;--accent:#e8590c;--ok:#2f9e44}
*{box-sizing:border-box}
body{margin:0;font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--ink)}
header{background:var(--accent);color:#fff;padding:14px 16px;position:sticky;top:0;z-index:2}
header h1{margin:0;font-size:18px} header p{margin:2px 0 0;font-size:12px;opacity:.9}
main{padding:12px 12px 90px;max-width:720px;margin:0 auto}
.card{background:var(--card);border-radius:14px;padding:14px;margin-bottom:12px;box-shadow:0 1px 4px rgba(0,0,0,.1)}
h2{font-size:15px;margin:0 0 8px;color:var(--accent)}
.mode label{display:block;padding:10px;border:2px solid #eee;border-radius:10px;margin-bottom:8px;font-weight:600}
.mode input{margin-right:8px}
.ev{display:flex;align-items:flex-start;gap:10px;padding:10px 4px;border-bottom:1px solid #f1e6d6}
.ev:last-child{border-bottom:0}
.ev input{width:22px;height:22px;flex:none;margin-top:2px}
.ev b{display:block;font-size:14px} .ev small{color:var(--muted);font-size:12px}
.tag{display:inline-block;font-size:11px;padding:1px 6px;border-radius:6px;background:#ffe8cc;color:#a0410b;margin-left:4px}
.bar{position:fixed;left:0;right:0;bottom:0;background:#fff;padding:10px 12px;box-shadow:0 -2px 8px rgba(0,0,0,.12);display:flex;gap:8px}
button{flex:1;border:0;border-radius:12px;padding:13px;font-size:15px;font-weight:700;background:var(--accent);color:#fff}
button.sec{background:#f1e6d6;color:var(--ink)}
#msg{font-size:13px;margin-top:6px;min-height:18px} input[type=search]{width:100%;padding:10px;border-radius:10px;border:1px solid #ddd;font-size:14px;margin-bottom:6px}
.muted{color:var(--muted);font-size:13px}
</style></head>
<body>
<header><h1>{{SERVER_NAME}}</h1><p>Events &amp; server info</p></header>
<main>
<div class=""card""><h2>News</h2><div>{{MOTD}}</div></div>
<div class=""card"" id=""choiceCard""><h2>Which events do you want to play?</h2>
<div class=""mode"">
<label><input type=""radio"" name=""mode"" value=""server"" checked>Same as the server <span class=""muted"" id=""serverInfo""></span></label>
<label><input type=""radio"" name=""mode"" value=""custom"">Choose my own events</label>
</div>
<p class=""muted"">After saving, go back to the title screen (or restart the game) so the new event data is loaded.</p>
<div id=""msg""></div></div>
<div class=""card""><h2>Event list</h2>
<input type=""search"" id=""filter"" placeholder=""Search events..."">
<div id=""list"" class=""muted"">Loading...</div></div>
</main>
<div class=""bar""><button class=""sec"" id=""none"">Clear</button><button class=""sec"" id=""all"">All</button><button id=""save"">Save</button></div>
<script>
var USER_ID = {{USER_ID}};
var data = null;
function $(id){return document.getElementById(id);}
function esc(s){return String(s==null?'':s).replace(/[&<>""']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c];});}
function req(method,url,body,cb){var x=new XMLHttpRequest();x.open(method,url,true);x.setRequestHeader('Content-Type','application/json');
x.onreadystatechange=function(){if(x.readyState===4){var j=null;try{j=JSON.parse(x.responseText);}catch(e){} cb(x.status,j);}};x.send(body?JSON.stringify(body):null);}
function mode(){var r=document.getElementsByName('mode');for(var i=0;i<r.length;i++)if(r[i].checked)return r[i].value;return 'server';}
function render(){
  var f=$('filter').value.toLowerCase(); var custom=mode()==='custom'; var h='';
  var chosen={}; (custom?data.choice.ids:data.serverActive).forEach(function(i){chosen[i]=true;});
  data.events.forEach(function(e){
    var text=(e.name+' '+e.eventId+' '+(e.description||'')).toLowerCase(); if(f&&text.indexOf(f)<0)return;
    h+='<label class=""ev""><input type=""checkbox"" data-id=""'+e.eventId+'""'+(chosen[e.eventId]?' checked':'')+(custom?'':' disabled')+'>'+
      '<span><b>'+esc(e.name)+'<span class=""tag"">#'+e.eventId+'</span>'+(e.playable?'':'<span class=""tag"">no maps</span>')+'</b>'+
      '<small>'+esc(e.period||'')+(e.maps?' &middot; '+e.maps+' map(s)':'')+'</small>'+(e.description?'<small>'+esc(e.description)+'</small>':'')+'</span></label>';
  });
  $('list').innerHTML=h||'<p class=""muted"">No events found in the server data.</p>';
  var boxes=$('list').querySelectorAll('input[type=checkbox]');
  for(var i=0;i<boxes.length;i++){boxes[i].onchange=function(){var id=+this.getAttribute('data-id');var ids=data.choice.ids;var k=ids.indexOf(id);
    if(this.checked&&k<0)ids.push(id);if(!this.checked&&k>=0)ids.splice(k,1);};}
}
function load(){req('GET','/web/api/events?userId='+encodeURIComponent(USER_ID),null,function(st,j){
  if(!j){$('list').innerHTML='Could not load events.';return;} data=j; if(!data.choice.ids)data.choice.ids=[];
  $('serverInfo').innerHTML='('+(j.serverMode==='custom'?j.serverActive.length+' selected by the server':'original event data')+')';
  if(!j.allowPlayerChoice){$('choiceCard').innerHTML='<h2>Events</h2><p class=""muted"">The server owner chooses the events on this server.</p>';$('save').disabled=true;}
  else if(!j.known){$('msg').innerHTML='Open this page from the in-game <b>Help</b> menu to save your choice.';}
  var r=document.getElementsByName('mode');for(var i=0;i<r.length;i++){r[i].checked=(r[i].value===(j.choice.mode||'server'));r[i].onchange=render;}
  render();});}
$('filter').oninput=render;
$('all').onclick=function(){if(mode()!=='custom')return;data.choice.ids=data.events.map(function(e){return e.eventId;});render();};
$('none').onclick=function(){if(mode()!=='custom')return;data.choice.ids=[];render();};
$('save').onclick=function(){req('POST','/web/api/events',{userId:USER_ID,mode:mode(),ids:data.choice.ids},function(st,j){
  $('msg').innerHTML=st===200?'<b style=""color:#2f9e44"">Saved!</b> Return to the title screen to load the events.':'<b style=""color:#c92a2a"">'+esc(j&&j.error||'Error')+'</b>';});};
load();
</script></body></html>";
    }
}
