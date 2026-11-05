namespace Puniemu.Src.Server.Web.Wiki
{
    // Hand-written guide articles for the in-game wiki (original text, English).
    public static class WikiArticles
    {
        public record Article(string Id, string Icon, string Title, string Html);

        public static readonly List<Article> All = new()
        {
            new("start", "&#127881;", "Getting started", @"
<p>Welcome! Puni Puni is a puzzle game: you connect <b>punis</b> (round Yo-kai blobs) of the same kind to pop them and damage the Yo-kai you fight.</p>
<h3>Your first steps</h3>
<ol>
<li>Play the first stages of <b>Springdale / Sakura</b> - the first map teaches you the basics.</li>
<li>Befriend the Yo-kai you meet and put the strongest ones in your <b>team</b>.</li>
<li>Collect the free rewards: <b>login stamps</b>, <b>missions</b> and the <b>presents</b> box.</li>
<li>Use the <b>Crank-a-kai</b> with the coins you earn to get new Yo-kai.</li>
<li>Clear stages with all <b>3 stars</b> - stars unlock new maps and hidden stages.</li>
</ol>
<h3>Good habits</h3>
<ul><li>Level the Yo-kai you use the most instead of many at once.</li>
<li>A team with Yo-kai of the <b>same tribe</b> is often stronger than random strong Yo-kai.</li>
<li>Look at the enemy before a boss stage and pick a team that fits.</li></ul>"),

            new("battle", "&#9876;", "Battles, combos & Soultimates", @"
<h3>Popping punis</h3>
<p>Drag your finger over at least 3 touching punis of the same Yo-kai to pop them. Each pop attacks the enemy with that Yo-kai. Longer chains hit harder.</p>
<h3>Combos</h3>
<p>Popping chains quickly one after another builds a <b>combo</b>. High combos raise your score and fill the <b>Fever</b> gauge faster.</p>
<h3>Fever</h3>
<p>When the Fever gauge is full, Fever mode starts: punis pop more easily and you score far more. Many star conditions are about combos or Fever.</p>
<h3>Soultimates</h3>
<p>Popping a Yo-kai's punis charges its Soultimate. When a puni starts to glow, tap it to use the Soultimate. Every Yo-kai has its own one (see the Yo-kai pages in the encyclopedia).</p>
<h3>Big punis and blocks</h3>
<p>Long chains can create a <b>big puni</b> worth extra. Some enemies throw <b>obstacles</b> on the board - pop punis next to them to clear them.</p>"),

            new("befriend", "&#129309;", "Befriending Yo-kai", @"
<p>After you beat a Yo-kai it may want to become your friend. A roulette decides if it joins you.</p>
<ul>
<li><b>Food</b> raises the chance a lot. Every Yo-kai has a favourite food type - the right food works best.</li>
<li>Higher-rank Yo-kai are harder to befriend.</li>
<li>Bosses and event Yo-kai often only appear on certain stages: the encyclopedia shows <b>where to find</b> each Yo-kai.</li>
<li>When you already have a Yo-kai with a fully levelled Soultimate, food is not used up on it.</li>
</ul>
<p class=""muted"">Tip: some Yo-kai can only be obtained from the Crank-a-kai or events.</p>"),

            new("ranks", "&#11088;", "Ranks & tribes", @"
<h3>Ranks</h3>
<p>From weakest to strongest: <b>E, D, C, B, A, S, SS, SSS, Z, ZZ, ZZZ, UZ, UZ+</b>. Higher ranks have higher HP, attack and stronger Soultimates.</p>
<h3>Tribes</h3>
<p>Every Yo-kai belongs to a tribe: <b>Brave, Mysterious, Tough, Charming, Heartful, Shady, Eerie, Slippery, Enma</b> and <b>Wicked</b>. Many skills and bonus effects only work for one tribe, and some events or bosses are weak against certain tribes, so building a team around one tribe pays off.</p>
<p>The encyclopedia can filter Yo-kai by rank and tribe.</p>"),

            new("levels", "&#128200;", "Levels, evolution & Soultimate levels", @"
<h3>Levels</h3>
<p>Yo-kai in your team get EXP after every battle and become stronger. Every Yo-kai has a maximum level; items can raise it further for some Yo-kai.</p>
<h3>Evolution</h3>
<p>Some Yo-kai evolve when they reach a certain level - the encyclopedia shows which ones and at which level.</p>
<h3>Soultimate levels</h3>
<p>Befriending the same Yo-kai again, or using <b>Soul Secrets</b> items, levels up its Soultimate (up to level 7). Higher levels make the Soultimate stronger or faster to charge.</p>"),

            new("stars", "&#127775;", "Stars, maps & hidden stages", @"
<p>Each stage has <b>3 star conditions</b> (for example a score, a combo or using a certain Yo-kai). The map pages in the encyclopedia list them for every stage.</p>
<p>Stars open the way to new maps and to <b>hidden stages</b> - special stages that only appear after you meet a condition somewhere on the map. When this server's data knows the condition, the stage page shows it.</p>
<p>Many maps also have a <b>reverse (""ura"") side</b> with harder stages and rare Yo-kai.</p>"),

            new("items", "&#127873;", "Spirits, Y-Money & items", @"
<h3>Spirits</h3>
<p>Every stage costs a <b>spirit</b> (hitodama). Free spirits refill over time; you can also get extra spirits from rewards.</p>
<h3>Y-Money</h3>
<p>Earned in battles and from rewards. Used in the shop and for some Crank-a-kai.</p>
<h3>Items</h3>
<p>Food (befriending), Soul Secrets (Soultimate levels), EXP items, coins (Crank-a-kai) and more. The encyclopedia lists every item with its description.</p>"),

            new("gacha", "&#127920;", "Crank-a-kai & coins", @"
<p>The <b>Crank-a-kai</b> gives you a random Yo-kai (or an item). Different coins and machines have different Yo-kai and odds - the encyclopedia shows the contents and the chance for each rank on this server.</p>
<ul><li><b>Select-a-Coins</b> let you choose the Yo-kai from a list.</li>
<li>Getting a Yo-kai you already have levels up its Soultimate, or turns into an item.</li>
<li>In Wibble Wobble the <b>Oni Fever</b> gauge fills with every crank; when it is full you get a special effect.</li></ul>"),

            new("missions", "&#128203;", "Missions, login stamps & presents", @"
<ul><li><b>Missions</b> give rewards for goals like clearing stages, befriending Yo-kai or scoring points. Check the mission menu often - the encyclopedia lists all of them.</li>
<li><b>Login stamps</b> reward you for logging in every day.</li>
<li>The <b>presents</b> box collects rewards and gifts.</li>
<li>This server can also send <b>server-wide gifts</b> - you get them automatically when you log in.</li></ul>"),

            new("score", "&#127942;", "Score Attack", @"
<p>Score Attack is about the highest score in a limited time instead of beating a boss. Your best score is ranked against other players.</p>
<ul><li>Use Yo-kai with score-boosting Soultimates.</li><li>Keep combos going and trigger Fever as often as you can.</li><li>Some Yo-kai have a bonus effect against a specific Score Attack opponent (see the Yo-kai pages).</li></ul>"),

            new("watch", "&#8986;", "Watches", @"
<p>Watches are equipment for your player. Better watches give bonuses in battle or on the map and are made from materials and Y-Money. Equip the one that fits what you are doing.</p>"),

            new("events", "&#127882;", "Events", @"
<p>Events add special maps, bosses, Yo-kai, missions and bonus Yo-kai for a limited time. On this server you can choose which events you want to play.</p>
<p>Open the <b>Event Guide</b> to read about every event of the game, its characters and story, and switch events on with <b>Play this event</b>.</p>"),

            new("friends", "&#128101;", "Friends", @"
<p>Add other players as friends with their friend code. Friends appear in rankings and can help you; the friend list shows their favourite Yo-kai and last play time.</p>"),

            new("save", "&#128274;", "Keep your save safe", @"
<p>Link your save to an e-mail address so you can restore it on a new phone:</p>
<ol><li>Open the account option in the game's settings / help page and enter your e-mail.</li>
<li>You receive a 6-digit code (valid for 15 minutes).</li>
<li>Enter the code in <b>Confirm action</b> in the game settings.</li></ol>
<p>To restore on another device, request a code with the same e-mail there and confirm it. Ask the server owner if e-mails are not enabled.</p>"),

            new("glossary", "&#128483;", "Japanese → English glossary", @"
<p>For players on the Japanese Puni Puni app. Common words you will see:</p>
<table class=""gl"">
<tr><td>ぷに</td><td>Puni</td></tr><tr><td>妖怪</td><td>Yo-kai</td></tr><tr><td>ステージ</td><td>Stage</td></tr><tr><td>ボス</td><td>Boss</td></tr>
<tr><td>マップ</td><td>Map</td></tr><tr><td>裏マップ / 裏ステージ</td><td>Reverse (hidden) map / stage</td></tr><tr><td>かくしステージ</td><td>Hidden stage</td></tr>
<tr><td>強敵</td><td>Strong enemy (boss challenge)</td></tr><tr><td>ひとだま</td><td>Spirits</td></tr><tr><td>Yマネー</td><td>Y-Money</td></tr>
<tr><td>ガシャ</td><td>Crank-a-kai</td></tr><tr><td>コイン</td><td>Coin</td></tr><tr><td>エラベールコイン</td><td>Select-a-Coin</td></tr><tr><td>ニャンボ</td><td>Nyanbo (special crank)</td></tr>
<tr><td>わざ</td><td>Soultimate</td></tr><tr><td>秘伝書</td><td>Soul Secrets</td></tr><tr><td>好物</td><td>Favourite food</td></tr><tr><td>なかま / ともだち</td><td>Befriend / Friend</td></tr>
<tr><td>へんせい / デッキ</td><td>Team</td></tr><tr><td>レベル</td><td>Level</td></tr><tr><td>経験値</td><td>EXP</td></tr><tr><td>進化</td><td>Evolution</td></tr><tr><td>合成</td><td>Fusion</td></tr>
<tr><td>ランク</td><td>Rank</td></tr><tr><td>しゅぞく</td><td>Tribe</td></tr><tr><td>イサマシ / フシギ / ゴーケツ / プリチー</td><td>Brave / Mysterious / Tough / Charming</td></tr>
<tr><td>ポカポカ / ウスラカゲ / ブキミー / ニョロロン</td><td>Heartful / Shady / Eerie / Slippery</td></tr><tr><td>エンマ / 怪魔</td><td>Enma / Wicked</td></tr>
<tr><td>図鑑</td><td>Medallium (Yo-kai list)</td></tr><tr><td>レジェンド</td><td>Legendary</td></tr><tr><td>封印 / 解放</td><td>Seal / Unlock</td></tr>
<tr><td>フィーバー</td><td>Fever</td></tr><tr><td>でかぷに</td><td>Big puni</td></tr><tr><td>おじゃま</td><td>Obstacle</td></tr><tr><td>スコアアタック</td><td>Score Attack</td></tr>
<tr><td>イベント</td><td>Event</td></tr><tr><td>おたすけ</td><td>Bonus / assist</td></tr><tr><td>ミッション</td><td>Missions</td></tr><tr><td>ログインスタンプ</td><td>Login stamp</td></tr>
<tr><td>プレゼント</td><td>Presents</td></tr><tr><td>ほうしゅう</td><td>Reward</td></tr><tr><td>うけとる</td><td>Receive</td></tr><tr><td>ショップ</td><td>Shop</td></tr>
<tr><td>アイテム</td><td>Items</td></tr><tr><td>ウォッチ</td><td>Watch</td></tr><tr><td>せってい</td><td>Settings</td></tr><tr><td>メニュー</td><td>Menu</td></tr>
<tr><td>けってい / OK</td><td>Confirm</td></tr><tr><td>もどる</td><td>Back</td></tr><tr><td>はい / いいえ</td><td>Yes / No</td></tr><tr><td>つづける</td><td>Continue</td></tr>
</table>
<p class=""muted"">If the server owner enabled the English translation, most game texts are already shown in English.</p>"),
        };
    }
}
