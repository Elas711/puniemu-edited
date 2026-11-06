using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Logic;
using Puniemu.Src.Server.Infrastructure;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using UDM = Puniemu.Src.UserDataManager.Logic.UserDataManager;

namespace Puniemu.Src.Server.Web
{
    // Secret words ("ひみつのワード", as on the official site yokai-punipuni.jp/scword/): the server owner
    // creates words in the admin panel (Gifts tab); a player enters their 8-character character code
    // and the word on /web/scword.nhn (or /scword/), and the rewards arrive in the in-game present box.
    // Each word works once per player, optionally limited in uses and time.
    //   GET  /web/scword.nhn            the page (userId=... fills in the player's code)
    //   POST /web/api/scword            {code | userId, word}
    //   admin: GET/POST /admin/api/secretwords, POST /admin/api/secretwords/{word}/delete
    public static class SecretWords
    {
        private const string REDEEMED = "puniemu_secret_words";
        private const int MAX_FAILS_PER_HOUR = 20;
        private static readonly ConcurrentDictionary<string, (int fails, DateTime since)> _fails = new();
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static string Normalize(string? word) =>
            new string((word ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        // 16 characters like the official words, without look-alikes (0/O, 1/I)
        public static string NewWord()
        {
            const string ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var b = RandomNumberGenerator.GetBytes(16);
            return new string(b.Select(x => ALPHABET[x % ALPHABET.Length]).ToArray());
        }

        private static bool Active(SecretWord w) =>
            (w.MaxUses <= 0 || w.Uses < w.MaxUses) &&
            (string.IsNullOrEmpty(w.Until) || !DateTime.TryParse(w.Until, out var until) || DateTime.Now.Date <= until.Date);

        private static bool TooManyFails(string ip)
        {
            if (!_fails.TryGetValue(ip, out var f)) return false;
            if (DateTime.UtcNow - f.since > TimeSpan.FromHours(1)) { _fails.TryRemove(ip, out _); return false; }
            return f.fails >= MAX_FAILS_PER_HOUR;
        }

        private static void Fail(string ip) =>
            _fails.AddOrUpdate(ip, (1, DateTime.UtcNow), (_, f) => DateTime.UtcNow - f.since > TimeSpan.FromHours(1) ? (1, DateTime.UtcNow) : (f.fails + 1, f.since));

        private static async Task Json(HttpContext ctx, object o, int status = 200)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(o));
        }

        public static void Map(WebApplication app)
        {
            app.MapGet("/web/scword.nhn", Page);
            app.MapGet("/scword/", Page);
            app.MapGet("/scword", Page);
            app.MapPost("/web/api/scword", Redeem);
        }

        private static async Task Page(HttpContext ctx)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            var campaigns = ServerStateStore.Current.SecretWords.Where(Active).Select(w => w.Campaign).Where(c => c.Length > 0).Distinct().ToList();
            await ctx.Response.WriteAsync(PAGE
                .Replace("{{SERVER_NAME}}", WebUtility.HtmlEncode(DataManager.Logic.DataManager.ServerName ?? "Puniemu"))
                .Replace("{{CAMPAIGNS}}", campaigns.Count == 0 ? "" :
                    "<p class=\"muted\">Running now: " + string.Join(", ", campaigns.Select(WebUtility.HtmlEncode)) + "</p>")
                .Replace("{{USER_ID}}", Puniemu.Src.Utils.GeneralUtils.GeneralUtils.JsString(ctx.Request.Query["userId"].ToString())));
        }

        private static async Task Redeem(HttpContext ctx)
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
            if (TooManyFails(ip)) { await Json(ctx, new { error = "Too many wrong words. Please try again in an hour." }, 429); return; }
            JObject b;
            try { using var r = new StreamReader(ctx.Request.Body); b = JObject.Parse(await r.ReadToEndAsync()); }
            catch { await Json(ctx, new { error = "bad request" }, 400); return; }

            // the player: character code (as on the official page) or the in-game userId
            string gdkey = "";
            var code = (b.Value<string>("code") ?? "").Trim().ToLowerInvariant();
            var userId = b.Value<string>("userId") ?? "";
            if (code.Length is > 0 and <= 16) gdkey = await UDM.GetGdkeyFromCharacterId(code);
            if (gdkey.Length == 0 && userId.Length is > 0 and <= 64) gdkey = await UDM.GetGdkeyFromUserId(userId);
            if (gdkey.Length == 0) { Fail(ip); await Json(ctx, new { error = "No player has this character code." }, 404); return; }

            var word = Normalize(b.Value<string>("word"));
            await _lock.WaitAsync();
            try
            {
                var sw = ServerStateStore.Current.SecretWords.FirstOrDefault(w => Normalize(w.Word) == word);
                if (word.Length == 0 || sw == null) { Fail(ip); await Json(ctx, new { error = "This secret word is not valid." }, 404); return; }
                if (!Active(sw)) { await Json(ctx, new { error = "This secret word can't be used any more." }, 410); return; }

                var result = await GameRequestMiddleware.WithSaveLockAsync(gdkey, async () =>
                {
                    var used = await UDM.GetYwpUserAsync<List<string>>(gdkey, REDEEMED) ?? new();
                    if (used.Contains(word)) return "used";
                    var userData = await UDM.GetYwpUserAsync<YwpUserData>(gdkey, "ywp_user_data");
                    if (userData == null) return "nosave";
                    var box = await UDM.GetYwpUserAsync<List<PresentBoxList>>(gdkey, "ywp_user_present_box_list") ?? new();
                    var text = sw.Message.Length > 0 ? sw.Message : "Secret word" + (sw.Campaign.Length > 0 ? ": " + sw.Campaign : "");
                    void Add(int id, RewardType type, int count) =>
                        box.Insert(0, PresentBoxManager.CreatePresent(null, true, false, userData, id, type, count, text));
                    if (sw.YMoney > 0) Add(0, RewardType.YMoney, sw.YMoney);
                    if (sw.Spirits > 0) Add(0, RewardType.Hitodama, sw.Spirits);
                    foreach (var it in sw.Items.Where(i => i.Count > 0)) Add((int)it.Id, RewardType.Item, it.Count);
                    foreach (var y in sw.Youkai) Add((int)y, RewardType.Yokai, 1);
                    await UDM.SetYwpUserAsync(gdkey, "ywp_user_present_box_list", box);
                    used.Add(word);
                    await UDM.SetYwpUserAsync(gdkey, REDEEMED, used);
                    return "ok";
                });
                if (result == "used") { await Json(ctx, new { error = "You already used this secret word." }, 409); return; }
                if (result == "nosave") { await Json(ctx, new { error = "This player has not started the game yet." }, 404); return; }
                ServerStateStore.Update(s => { var w = s.SecretWords.FirstOrDefault(x => Normalize(x.Word) == word); if (w != null) w.Uses++; });
                Log.Info("scword", $"{gdkey} used the secret word {word} ({sw.Campaign})");
                await Json(ctx, new { ok = true, message = "Done! Your reward is waiting in the in-game present box." });
            }
            finally { _lock.Release(); }
        }

        // ---- admin ----

        public static SecretWord Create(JObject b)
        {
            var word = Normalize(b.Value<string>("word"));
            if (word.Length == 0) word = NewWord();
            if (word.Length is < 4 or > 32) throw new InvalidOperationException("A word has 4 to 32 letters / digits.");
            if (ServerStateStore.Current.SecretWords.Any(w => Normalize(w.Word) == word)) throw new InvalidOperationException("This word exists already.");
            var sw = new SecretWord
            {
                Word = word,
                Campaign = (b.Value<string>("campaign") ?? "").Trim(),
                Message = (b.Value<string>("message") ?? "").Trim(),
                YMoney = Math.Max(0, b.Value<int?>("ymoney") ?? 0),
                Spirits = Math.Max(0, b.Value<int?>("spirits") ?? 0),
                Items = b["items"]?.ToObject<List<ItemGrant>>() ?? new(),
                Youkai = b["youkai"]?.ToObject<List<long>>() ?? new(),
                MaxUses = Math.Max(0, b.Value<int?>("maxUses") ?? 0),
                Until = (b.Value<string>("until") ?? "").Trim(),
            };
            if (sw.YMoney + sw.Spirits == 0 && sw.Items.Count == 0 && sw.Youkai.Count == 0)
                throw new InvalidOperationException("Give the word at least one reward.");
            ServerStateStore.Update(s => s.SecretWords.Add(sw));
            Log.Info("admin", $"secret word created: {JsonConvert.SerializeObject(sw)}");
            return sw;
        }

        public static void Delete(string word)
        {
            var n = Normalize(word);
            ServerStateStore.Update(s => s.SecretWords.RemoveAll(w => Normalize(w.Word) == n));
        }

        private const string PAGE = @"<!DOCTYPE html><html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Secret words - {{SERVER_NAME}}</title><style>
:root{--bg:#fff7e6;--card:#fff;--ink:#3a2a16;--muted:#8a7150;--accent:#e8892b;--line:#f0dcc0}
@media (prefers-color-scheme:dark){:root{--bg:#1d1812;--card:#29221a;--ink:#f3e6d2;--muted:#b9a383;--accent:#f0a050;--line:#41352a}}
body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.5 system-ui,sans-serif}main{max-width:520px;margin:0 auto;padding:16px}
.card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:16px;margin:12px 0}h1{font-size:22px;margin:8px 0}
label{display:block;font-weight:600;margin-top:12px}input{box-sizing:border-box;width:100%;font-size:18px;padding:10px;border:1px solid var(--line);border-radius:10px;background:var(--bg);color:var(--ink);letter-spacing:1px}
button{margin-top:16px;width:100%;font-size:18px;padding:12px;border:0;border-radius:10px;background:var(--accent);color:#fff;font-weight:700}
.muted{color:var(--muted);font-size:14px}#msg{margin-top:12px;font-weight:600}</style></head><body><main>
<h1>&#128273; Secret words</h1><div class=""card""><p class=""muted"">Enter your character code and a secret word. The reward is sent to your in-game present box.</p>{{CAMPAIGNS}}
<label for=""code"">Character code</label><input id=""code"" maxlength=""8"" autocomplete=""off"" placeholder=""8 letters / digits"">
<p class=""muted"">Shown on your profile screen in the game.</p>
<label for=""word"">Secret word</label><input id=""word"" maxlength=""40"" autocomplete=""off"" placeholder=""16 letters / digits"">
<p class=""muted"">Without hyphens or spaces.</p><button id=""go"">Send</button><div id=""msg"" role=""status""></div></div></main><script>
var USER_ID={{USER_ID}};
document.getElementById('go').onclick=function(){var m=document.getElementById('msg');m.textContent='...';
var x=new XMLHttpRequest();x.open('POST','/web/api/scword');x.setRequestHeader('Content-Type','application/json');
x.onload=function(){var j={};try{j=JSON.parse(x.responseText);}catch(e){}m.textContent=j.message||j.error||('Error '+x.status);};
x.onerror=function(){m.textContent='Could not reach the server.';};
x.send(JSON.stringify({code:document.getElementById('code').value,word:document.getElementById('word').value,userId:USER_ID}));};
</script></body></html>";
    }
}
