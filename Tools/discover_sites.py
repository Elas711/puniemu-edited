#!/usr/bin/env python3
"""
Finds websites about Yo-kai Watch Puni Puni / Wibble Wobble: wikis, fan sites, code, archives,
guides, news, videos, communities.

  python -I Tools/discover_sites.py --out sites_found              (search + crawl, ~10-20 min)
  python -I Tools/discover_sites.py --out sites_found --no-crawl   (search APIs only, ~2 min)
  python -I Tools/discover_sites.py --out sites_found --seed https://example.com/punipuni

How it works
  1. Public search APIs that need no key: archive.org, GitHub / GitLab / Codeberg repositories,
     Wikipedia (several languages) and Wikimedia Commons, Fandom wikis, Hacker News, StackExchange.
     Set GITHUB_TOKEN for more GitHub results (unauthenticated: 10 searches per minute).
  2. Official APIs of sites that block crawlers, used only when you set your own keys (in a cloud
     session whose proxy adds a saved key, set the variable to any placeholder, e.g. "proxy"):
       Reddit        REDDIT_CLIENT_ID + REDDIT_CLIENT_SECRET  (a "script" app, reddit.com/prefs/apps)
       YouTube       YOUTUBE_API_KEY                           (YouTube Data API v3)
       Web search    BRAVE_API_KEY, or GOOGLE_API_KEY + GOOGLE_CSE_ID (Programmable Search)
  3. A polite crawler starts from what was found (and --seed URLs): it reads robots.txt, waits
     between requests to the same host, reads HTML only, and keeps a page when it is about Puni Puni
     or Wibble Wobble. Links of relevant pages are followed up to --depth. Links still waiting are
     saved (frontier.json), so the next run goes on from there; --hours limits a long run.
  4. Links to file hosts (MediaFire, Mega, Drive...) and downloads (.zip/.rar/.apk...) found on
     relevant pages are recorded with the page they are on (files.json + a section in SITES.md).
     They are never downloaded or opened; MediaFire names / sizes come from MediaFire's own API.
     Get what you want with Tools/mediafire_download.py.
  5. Results are merged with earlier runs: <out>/sites.json, <out>/files.json, <out>/SITES.md.

Not done
  * Getting past bot checks, logins or robots.txt "Disallow" (search engine result pages, Reddit
    pages, Discord, X...): those sites are only reached through their official APIs above.
Everything fetched is parsed as text only; nothing is executed or saved besides the report.
"""
import argparse
import collections
import html
import html.parser
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import urllib.robotparser

UA = "puniemu-discover/1.0 (fan site index for a Puni Puni server emulator; polite crawler)"
TIMEOUT = 25           # per socket operation
DEADLINE = 60          # whole request (a slow server trickling data must not stall the run)
MAX_BYTES = 2_000_000

# what the game is called around the world
STRONG = [
    "puni puni", "punipuni", "puni-puni", "ぷにぷに", "噗噗", "wibble wobble", "wibblewobble",
    "yo-kai watch puni", "yokai watch puni", "youkai watch puni", "妖怪ウォッチぷにぷに", "ywpuni", "puniemu",
]
YOKAI = ["yo-kai", "yokai", "youkai", "妖怪", "ジバニャン", "jibanyan", "level-5", "level5"]
QUERIES = [
    "Yo-kai Watch Puni Puni", "Yokai Watch Puni Puni", "Yo-kai Watch Wibble Wobble",
    "妖怪ウォッチ ぷにぷに", "ぷにぷに 攻略", "punipuni yokai", "wibble wobble yokai", "puniemu",
]

# start pages that search APIs don't find (official site, the big Japanese guide sites, news)
SEEDS = [
    "https://yokai-punipuni.jp/", "https://www.youkai-watch.jp/", "https://www.level5.co.jp/",
    "https://gamewith.jp/punipuni/", "https://game8.jp/punipuni", "https://punipuni.gamerch.com/",
    "https://www.4gamer.net/games/297/G029757/",
]

# never followed or listed (see "Left out on purpose")
FILE_HOSTS = {
    "mediafire.com", "mega.nz", "mega.io", "dropbox.com", "dropboxusercontent.com", "drive.google.com",
    "docs.google.com", "drive.usercontent.google.com", "1fichier.com", "uptobox.com", "zippyshare.com",
    "anonfiles.com", "gofile.io", "pixeldrain.com", "workupload.com", "sendspace.com", "rapidgator.net",
    "uploaded.net", "nitroflare.com", "krakenfiles.com", "files.catbox.moe", "thepiratebay.org",
    "1337x.to", "nyaa.si", "rutracker.org", "apkpure.com", "apkmirror.com", "apkcombo.com", "uptodown.com",
}
NO_CRAWL = {  # logins, bot checks, search engines, huge link farms
    "google.com", "bing.com", "duckduckgo.com", "yahoo.co.jp", "yandex.ru", "baidu.com", "reddit.com",
    "twitter.com", "x.com", "facebook.com", "instagram.com", "tiktok.com", "discord.com", "discord.gg",
    "linkedin.com", "pinterest.com", "amazon.com", "amazon.co.jp", "web.archive.org", "t.co", "bit.ly",
    "youtube.com", "youtu.be", "nicovideo.jp", "apps.apple.com", "play.google.com", "accounts.google.com",
    "wikipedia.org", "wikimedia.org", "wikidata.org", "github.com", "gitlab.com", "codeberg.org",
}
CATEGORIES = [
    ("Official", ["yw-p.com", "level5.co.jp", "level5.jp", "youkai-watch.jp", "yo-kai.com", "level5-ia.com",
                  "yokai-punipuni", "nhn-playart.com", "kadokawa.co.jp"]),
    ("App stores", ["apps.apple.com", "play.google.com", "xbox.com", "sensortower.com", "appannie"]),
    ("Wikis", ["fandom.com", "wikia", "miraheze.org", "wiki", "wikiwiki.jp", "seesaawiki.jp", "atwiki.jp"]),
    ("Code", ["github.com", "gitlab.com", "codeberg.org", "bitbucket.org", "sourceforge.net"]),
    ("Archives", ["archive.org"]),
    ("Videos", ["youtube.com", "youtu.be", "nicovideo.jp", "twitch.tv", "bilibili.com"]),
    ("Communities", ["reddit.com", "discord", "forum", "gamefaqs", "5ch", "2ch", "board", "community", "news.ycombinator.com",
                     "stackexchange", "stackoverflow", "resetera.com", "neogaf"]),
    ("Guides / news", ["game8", "gamewith", "appmedia", "altema", "famitsu", "4gamer", "gamerch", "kamigame", "news", "blog",
                       "note.com", "hatenablog", "siliconera", "polygon.com", "gamerant", "screenrant", "pocketgamer",
                       "macworld", "gamezebo", "metacritic", "ign.com", "kotaku", "toucharcade", "nintendolife", "inside-games"]),
    ("Fan sites / tools", ["github.io", "vercel.app", "netlify.app", "pages.dev", ".dev"]),
]


DOWNLOAD_EXT = re.compile(r"\.(zip|rar|7z|tar|gz|apk|xapk|apks|ipa|obb|cpk|ez|ayd)(\?|$)", re.I)


def is_file_link(url):
    host = host_of(url)
    if in_set(host, {"archive.org"}):  # archive.org items are listed as pages already
        return False
    return in_set(host, FILE_HOSTS) or bool(DOWNLOAD_EXT.search(urllib.parse.urlsplit(url).path))


def host_of(url):
    return (urllib.parse.urlsplit(url).hostname or "").lower().removeprefix("www.")


def in_set(host, domains):
    return any(host == d or host.endswith("." + d) for d in domains)


def norm_url(url):
    u = urllib.parse.urlsplit(url.strip())
    if u.scheme not in ("http", "https") or not u.hostname:
        return None
    q = urllib.parse.urlencode([(k, v) for k, v in urllib.parse.parse_qsl(u.query) if not k.lower().startswith(("utm_", "fbclid", "gclid"))])
    path = u.path or "/"
    return urllib.parse.urlunsplit(("https" if u.scheme == "https" else "http", u.netloc.lower(), path, q, ""))


# names that can only mean the game; the others ("puni puni", "wibble wobble") also need a Yo-kai
# word nearby ("Puni Puni Poemy" is an unrelated anime)
SPECIFIC = ["yo-kai watch puni", "yokai watch puni", "youkai watch puni", "妖怪ウォッチぷにぷに", "妖怪ウォッチ ぷにぷに",
            "yo-kai watch wibble", "yokai watch wibble", "puniemu", "ywpuni", "ywp-"]


def relevance(text):
    t = (text or "").lower()
    strong = sum(t.count(k) for k in STRONG)
    if strong == 0:
        return 0
    yokai = sum(t.count(k) for k in YOKAI)
    if yokai == 0 and not any(k in t for k in SPECIFIC):
        return 0
    return strong * 2 + min(5, yokai)


class Fetcher:
    def __init__(self, delay):
        self.delay, self.last, self.robots = delay, {}, {}
        self.github_token = os.environ.get("GITHUB_TOKEN")

    def _wait(self, host):
        dt = time.time() - self.last.get(host, 0)
        if dt < self.delay:
            time.sleep(self.delay - dt)
        self.last[host] = time.time()

    def allowed(self, url):
        host = host_of(url)
        if host not in self.robots:
            rp = urllib.robotparser.RobotFileParser()
            try:
                raw = self.get(urllib.parse.urljoin(url, "/robots.txt"), check_robots=False, quiet=True)
                rp.parse((raw[0] if raw else "").splitlines())
            except Exception:  # noqa: BLE001
                rp.parse([])
            self.robots[host] = rp
        return self.robots[host].can_fetch(UA, url)

    def get(self, url, check_robots=True, quiet=False, headers=None, retries=2):
        """(text, content_type, final_url) or None."""
        if check_robots and not self.allowed(url):
            return None
        h = {"User-Agent": UA, "Accept-Language": "en,ja;q=0.8"}
        if headers:
            h.update(headers)
        for attempt in range(retries + 1):
            self._wait(host_of(url))
            try:
                req = urllib.request.Request(url, headers=h)
                with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
                    ctype = r.headers.get("Content-Type", "")
                    start, chunks, size = time.time(), [], 0
                    while size < MAX_BYTES:
                        if time.time() - start > DEADLINE:
                            raise TimeoutError("too slow")
                        c = r.read(65536)
                        if not c:
                            break
                        chunks.append(c)
                        size += len(c)
                    body = b"".join(chunks)[:MAX_BYTES]
                    if r.status == 202:  # bot check pages ("please wait"): leave them
                        return None
                    charset = re.search(r"charset=([\w-]+)", ctype)
                    return body.decode(charset.group(1) if charset else "utf-8", errors="replace"), ctype, r.geturl()
            except urllib.error.HTTPError as e:
                if e.code in (429, 503) and attempt < retries:
                    ra = e.headers.get("Retry-After") or ""
                    time.sleep(min(60, int(ra) if ra.isdigit() else 10 * (attempt + 1)))
                    continue
                if not quiet:
                    print(f"  {e.code} {url[:100]}", file=sys.stderr)
                return None
            except Exception as e:  # noqa: BLE001
                if attempt < retries:
                    time.sleep(3)
                    continue
                if not quiet:
                    print(f"  error {url[:100]}: {e}", file=sys.stderr)
                return None

    def json(self, url, headers=None, quiet=False):
        r = self.get(url, check_robots=False, headers=headers, quiet=quiet)
        if not r:
            return None
        try:
            return json.loads(r[0])
        except ValueError:
            return None


class Page(html.parser.HTMLParser):
    def __init__(self, base):
        super().__init__(convert_charrefs=True)
        self.base, self.links, self.title, self.desc, self._t, self.text = base, [], "", "", False, []
        self.anchor = {}   # link -> its text
        self._a = None

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "a" and a.get("href"):
            self._a = urllib.parse.urljoin(self.base, a["href"])
            self.links.append(self._a)
        elif tag == "title":
            self._t = True
        elif tag == "meta" and (a.get("name") or a.get("property") or "").lower() in ("description", "og:description"):
            self.desc = self.desc or (a.get("content") or "")

    def handle_endtag(self, tag):
        if tag == "title":
            self._t = False
        elif tag == "a":
            self._a = None

    def handle_data(self, data):
        if self._a and len(self.anchor.get(self._a, "")) < 120:
            self.anchor[self._a] = (self.anchor.get(self._a, "") + " " + data.strip()).strip()
        if self._t:
            self.title += data
        elif len(self.text) < 4000:
            self.text.append(data)


class Catalog:
    def __init__(self, path):
        self.path = path
        self.items = {}
        if os.path.exists(path):
            for it in json.load(open(path, encoding="utf-8")):
                self.items[it["url"]] = it
        # links to file hosts / downloads found on relevant pages: listed, never downloaded
        self.files_path = os.path.join(os.path.dirname(path), "files.json")
        self.files = {}
        if os.path.exists(self.files_path):
            for it in json.load(open(self.files_path, encoding="utf-8")):
                self.files[it["url"]] = it

    def add_file(self, url, text, page, page_title):
        url = url.strip().split("#")[0]
        if not url.startswith(("http://", "https://")):
            return False
        it = self.files.get(url)
        new = it is None
        if new:
            it = self.files[url] = {"url": url, "host": host_of(url), "text": "", "pages": [], "found": time.strftime("%Y-%m-%d")}
        text = re.sub(r"\s+", " ", html.unescape(text or "")).strip()[:160]
        if text and not it["text"]:
            it["text"] = text
        if page and page not in [x["url"] for x in it["pages"]] and len(it["pages"]) < 10:
            it["pages"].append({"url": page, "title": (page_title or "")[:120]})
        return new

    def add(self, url, title, source, score=1, note=""):
        url = norm_url(url)
        if not url or in_set(host_of(url), FILE_HOSTS):
            return False
        it = self.items.get(url)
        new = it is None
        if new:
            it = self.items[url] = {"url": url, "title": "", "sources": [], "score": 0, "note": "",
                                    "found": time.strftime("%Y-%m-%d")}
        title = re.sub(r"\s+", " ", html.unescape(title or "")).strip()[:160]
        if title and not it["title"]:
            it["title"] = title
        if source not in it["sources"]:
            it["sources"].append(source)
        it["score"] = max(it["score"], score)
        if note and not it["note"]:
            it["note"] = note[:200]
        return new

    def save(self):
        with open(self.path, "w", encoding="utf-8") as f:
            json.dump(sorted(self.items.values(), key=lambda x: (-x["score"], x["url"])), f, ensure_ascii=False, indent=1)
        with open(self.files_path, "w", encoding="utf-8") as f:
            json.dump(sorted(self.files.values(), key=lambda x: (x["host"], x["url"])), f, ensure_ascii=False, indent=1)


# ---- search sources -------------------------------------------------------------------------

def q(s):
    return urllib.parse.quote(s)


def src_archive(f, cat):
    for term in ['"puni puni"', "punipuni", '"wibble wobble"', "ぷにぷに"]:
        j = f.json(f"https://archive.org/advancedsearch.php?q={q(term)}&fl[]=identifier&fl[]=title&fl[]=mediatype&rows=200&output=json")
        for d in (j or {}).get("response", {}).get("docs", []):
            t = d.get("title") if isinstance(d.get("title"), str) else " ".join(d.get("title") or [])
            if relevance(t + " " + d["identifier"]) or "yokai" in (t or "").lower() or "妖怪" in (t or ""):
                cat.add(f"https://archive.org/details/{d['identifier']}", t, "archive.org", 3, d.get("mediatype", ""))


def src_github(f, cat):
    h = {"Accept": "application/vnd.github+json"}
    if f.github_token:
        h["Authorization"] = "Bearer " + f.github_token
    for term in ["punipuni", "puni puni yokai", "wibble wobble", "yokai watch puni", "ぷにぷに", "puniemu", "ywp yokai"]:
        j = f.json(f"https://api.github.com/search/repositories?q={q(term)}&per_page=100", headers=h)
        for r in (j or {}).get("items", []):
            text = f"{r['full_name']} {r.get('description') or ''} {' '.join(r.get('topics') or [])}"
            s = relevance(text) or (2 if ("puni" in text.lower() and ("yokai" in text.lower() or "yo-kai" in text.lower())) else 0)
            if s:
                # a project homepage is only listed if the crawl finds it is about the game
                cat.add(r["html_url"], f"{r['full_name']} - {r.get('description') or ''}", "github", s + 2,
                        f"{r.get('stargazers_count', 0)} stars, updated {str(r.get('pushed_at', ''))[:10]}")
                if r.get("homepage"):
                    cat.add(r["homepage"], r["full_name"] + " (homepage)", "github", 0)
        if not f.github_token:
            time.sleep(6.5)  # 10 searches / minute without a token


def src_gitlab_codeberg(f, cat):
    for term in ["punipuni", "puni puni", "wibble wobble", "puniemu"]:
        for r in f.json(f"https://gitlab.com/api/v4/projects?search={q(term)}&per_page=100") or []:
            text = f"{r.get('path_with_namespace')} {r.get('description') or ''}"
            if relevance(text):
                cat.add(r["web_url"], text, "gitlab", 3)
        j = f.json(f"https://codeberg.org/api/v1/repos/search?q={q(term)}&limit=50")
        for r in (j or {}).get("data", []):
            text = f"{r.get('full_name')} {r.get('description') or ''}"
            if relevance(text):
                cat.add(r["html_url"], text, "codeberg", 3)


def src_wikipedia(f, cat):
    for lang, terms in {"en": ["Yo-kai Watch Puni Puni", "Wibble Wobble Yo-kai"], "ja": ["妖怪ウォッチ ぷにぷに"],
                        "fr": ["Yo-kai Watch Puni Puni"], "de": ["Yo-kai Watch Puni Puni"], "es": ["Yo-kai Watch Puni Puni"],
                        "zh": ["妖怪手錶 噗噗"], "ko": ["요괴워치 뿌니뿌니"]}.items():
        for term in terms:
            j = f.json(f"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={q(term)}&format=json&srlimit=20")
            for r in (j or {}).get("query", {}).get("search", []):
                snippet = re.sub(r"<[^>]+>", "", r.get("snippet", ""))
                if relevance(r["title"] + " " + snippet):
                    title = r["title"]
                    cat.add(f"https://{lang}.wikipedia.org/wiki/{q(title.replace(' ', '_'))}", title, f"wikipedia-{lang}", 3)
                    # the article's external links (official sites, references)
                    ext = f.json(f"https://{lang}.wikipedia.org/w/api.php?action=query&prop=extlinks&titles={q(title)}&ellimit=500&format=json")
                    for p in (ext or {}).get("query", {}).get("pages", {}).values():
                        for e in p.get("extlinks", []):
                            link = e.get("*") or e.get("url") or ""
                            if link.startswith("//"):
                                link = "https:" + link
                            cat.add(link, "", f"wikipedia-{lang}-link", 0)  # listed only if the crawl finds it relevant
    j = f.json(f"https://commons.wikimedia.org/w/api.php?action=query&list=search&srsearch={q('Puni Puni Yo-kai')}&format=json&srlimit=50")
    for r in (j or {}).get("query", {}).get("search", []):
        if relevance(r["title"]):
            cat.add(f"https://commons.wikimedia.org/wiki/{q(r['title'].replace(' ', '_'))}", r["title"], "commons", 2)


FANDOMS = ["yokaiwatch", "yo-kai-watch-puni-puni", "yokaiwatchpunipuni", "yokai-watch-wibble-wobble",
           "yokaiwatchwibblewobble", "yokai-watch", "youkaiwatch", "yokaiwatchfanon"]


def src_fandom(f, cat):
    for wiki in FANDOMS:
        base = f"https://{wiki}.fandom.com"
        j = f.json(f"{base}/api.php?action=query&list=search&srsearch={q('Puni Puni')}&format=json&srlimit=50", quiet=True)
        if not j:  # no such wiki
            continue
        hits = j.get("query", {}).get("search", [])
        if hits:
            cat.add(base + "/", wiki + " (Fandom)", "fandom", 4)
        for r in hits:
            cat.add(f"{base}/wiki/{q(r['title'].replace(' ', '_'))}", r["title"] + f" ({wiki})", "fandom", 2)
        j = f.json(f"{base}/api.php?action=query&list=search&srsearch={q('Wibble Wobble')}&format=json&srlimit=50")
        for r in (j or {}).get("query", {}).get("search", []):
            cat.add(f"{base}/wiki/{q(r['title'].replace(' ', '_'))}", r["title"] + f" ({wiki})", "fandom", 2)


def src_hn_stackexchange(f, cat):
    for term in ["puni puni", "wibble wobble yokai"]:
        j = f.json(f"https://hn.algolia.com/api/v1/search?query={q(term)}&hitsPerPage=50")
        for h in (j or {}).get("hits", []):
            text = f"{h.get('title') or ''} {h.get('url') or ''}"
            if relevance(text):
                cat.add(f"https://news.ycombinator.com/item?id={h['objectID']}", h.get("title"), "hackernews", 2)
                if h.get("url"):
                    cat.add(h["url"], h.get("title"), "hackernews", 2)
    for site in ["stackoverflow", "gaming", "reverseengineering"]:
        j = f.json(f"https://api.stackexchange.com/2.3/search/advanced?q={q('puni puni')}&site={site}&pagesize=50")
        for it in (j or {}).get("items", []):
            if relevance(it.get("title", "")):
                cat.add(it["link"], it.get("title"), f"stackexchange-{site}", 2)


# ---- official APIs of sites that block crawlers / need an account (used only with your keys) ----

URL_RE = re.compile(r"https?://[^\s<>\"')\]]+")


def links_in_text(cat, text, page, title):
    """Links in a post / video description: file links are recorded, others become crawl candidates."""
    for u in URL_RE.findall(text or ""):
        u = u.rstrip(".,;")
        if is_file_link(u):
            cat.add_file(u, "", page, title)
        else:
            cat.add(u, "", "linked", 0)


def src_reddit(f, cat):
    """Reddit's API (its robots.txt disallows crawling). REDDIT_CLIENT_ID / REDDIT_CLIENT_SECRET of a
    'script' app from https://www.reddit.com/prefs/apps."""
    cid, secret = os.environ.get("REDDIT_CLIENT_ID"), os.environ.get("REDDIT_CLIENT_SECRET")
    if not (cid and secret):
        print("  skipped (set REDDIT_CLIENT_ID / REDDIT_CLIENT_SECRET)")
        return
    import base64
    req = urllib.request.Request("https://www.reddit.com/api/v1/access_token", data=b"grant_type=client_credentials",
                                 headers={"User-Agent": UA, "Authorization": "Basic " + base64.b64encode(f"{cid}:{secret}".encode()).decode()})
    with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
        token = json.loads(r.read())["access_token"]
    h = {"Authorization": "Bearer " + token}
    for term in ["puni puni", "punipuni", "wibble wobble", "ぷにぷに", "puniemu"]:
        after = ""
        for _ in range(5):
            j = f.json(f"https://oauth.reddit.com/search?q={q(term)}&limit=100&sort=relevance&raw_json=1{after}", headers=h)
            posts = (j or {}).get("data", {}).get("children", [])
            for c in posts:
                d = c["data"]
                text = f"{d.get('title', '')} {d.get('selftext', '')}"
                if relevance(text + " " + d.get("subreddit", "")):
                    page = "https://www.reddit.com" + d["permalink"]
                    cat.add(page, d.get("title"), "reddit", 3, "r/" + d.get("subreddit", ""))
                    links_in_text(cat, d.get("selftext", "") + " " + (d.get("url") or ""), page, d.get("title"))
            a = (j or {}).get("data", {}).get("after")
            if not a:
                break
            after = "&after=" + a


def src_youtube(f, cat):
    """YouTube Data API v3 (YOUTUBE_API_KEY). Video descriptions often link guides and downloads."""
    key = os.environ.get("YOUTUBE_API_KEY")
    if not key:
        print("  skipped (set YOUTUBE_API_KEY)")
        return
    gh = {"X-goog-api-key": key}  # a header, not ?key=: keys stay out of URLs and logs
    for term in ["妖怪ウォッチぷにぷに", "Yo-kai Watch Puni Puni", "Yokai Watch Wibble Wobble", "puni puni private server"]:
        token = ""
        for _ in range(3):
            j = f.json(f"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&maxResults=50&q={q(term)}{token}", headers=gh)
            ids = [it["id"]["videoId"] for it in (j or {}).get("items", []) if it.get("id", {}).get("videoId")]
            if ids:
                v = f.json(f"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={','.join(ids)}", headers=gh)
                for it in (v or {}).get("items", []):
                    sn = it["snippet"]
                    if relevance(f"{sn.get('title', '')} {sn.get('description', '')} {' '.join(sn.get('tags') or [])}"):
                        page = "https://www.youtube.com/watch?v=" + it["id"]
                        cat.add(page, sn.get("title"), "youtube", 2, sn.get("channelTitle", ""))
                        links_in_text(cat, sn.get("description", ""), page, sn.get("title"))
            nxt = (j or {}).get("nextPageToken")
            if not nxt:
                break
            token = "&pageToken=" + nxt


def src_websearch(f, cat):
    """General web search through official APIs: Brave Search (BRAVE_API_KEY) and Google
    Programmable Search (GOOGLE_API_KEY + GOOGLE_CSE_ID). Result pages of search engines are never scraped."""
    brave, gkey, gcx = os.environ.get("BRAVE_API_KEY"), os.environ.get("GOOGLE_API_KEY"), os.environ.get("GOOGLE_CSE_ID")
    if not (brave or (gkey and gcx)):
        print("  skipped (set BRAVE_API_KEY, or GOOGLE_API_KEY + GOOGLE_CSE_ID)")
        return
    terms = QUERIES + ["ぷにぷに 攻略 wiki", "Yo-kai Watch Puni Puni guide", "Wibble Wobble private server", "ぷにぷに イベント 一覧"]
    for term in terms:
        if brave:
            for off in range(0, 5):
                j = f.json(f"https://api.search.brave.com/res/v1/web/search?q={q(term)}&count=20&offset={off}",
                           headers={"X-Subscription-Token": brave, "Accept": "application/json"})
                res = (j or {}).get("web", {}).get("results", [])
                for r in res:
                    if is_file_link(r["url"]):
                        cat.add_file(r["url"], r.get("title"), "", "web search")
                    else:
                        cat.add(r["url"], r.get("title"), "brave", 2 if relevance(r.get("title", "") + " " + r.get("description", "")) else 0)
                if len(res) < 20:
                    break
        if gkey and gcx:
            for start in range(1, 92, 10):
                j = f.json(f"https://www.googleapis.com/customsearch/v1?cx={gcx}&q={q(term)}&num=10&start={start}", headers={"X-goog-api-key": gkey})
                items = (j or {}).get("items", [])
                for r in items:
                    if is_file_link(r["link"]):
                        cat.add_file(r["link"], r.get("title"), "", "web search")
                    else:
                        cat.add(r["link"], r.get("title"), "google", 2 if relevance(r.get("title", "") + " " + r.get("snippet", "")) else 0)
                if len(items) < 10:
                    break


def mediafire_info(f, cat):
    """Name and size of MediaFire links from MediaFire's own API (no page visit, no download)."""
    for it in cat.files.values():
        if it["host"] != "mediafire.com" or it.get("name") or it.get("checked"):
            continue
        it["checked"] = True
        m = re.search(r"/(?:file|file_premium|view|download)/([a-z0-9]+)", it["url"]) or re.search(r"[?&]([a-z0-9]{11,15})$", it["url"])
        fm = re.search(r"/folder/([a-z0-9]+)", it["url"])
        if m:
            j = f.json(f"https://www.mediafire.com/api/1.5/file/get_info.php?quick_key={m.group(1)}&response_format=json", quiet=True)
            info = (j or {}).get("response", {}).get("file_info") or {}
            if info:
                it["name"], it["size"] = info.get("filename"), int(info.get("size") or 0)
            else:
                it["status"] = "gone or private"
        elif fm:
            j = f.json(f"https://www.mediafire.com/api/1.5/folder/get_info.php?folder_key={fm.group(1)}&response_format=json", quiet=True)
            info = (j or {}).get("response", {}).get("folder_info") or {}
            if info:
                it["name"] = (info.get("name") or "") + f" (folder, {info.get('file_count', '?')} files)"


SOURCES = [src_archive, src_github, src_gitlab_codeberg, src_wikipedia, src_fandom, src_hn_stackexchange,
           src_reddit, src_youtube, src_websearch]


# ---- crawler --------------------------------------------------------------------------------

def crawl(f, cat, seeds, depth, max_pages, per_host, deadline=None, frontier_path=None, report=None):
    # hot: pages known to be relevant and links found on relevant pages; cold: unconfirmed
    # candidates (e.g. a Wikipedia article's external links) - only when nothing hot is left
    hot = collections.deque((u, 0) for u in seeds if cat.items.get(norm_url(u) or "", {}).get("score", 1) > 0)
    cold = collections.deque((u, 0) for u in seeds if cat.items.get(norm_url(u) or "", {}).get("score", 1) == 0)
    seen, pages, per = set(), 0, collections.Counter()
    while (hot or cold) and pages < max_pages and (deadline is None or time.time() < deadline):
        url, d = (hot or cold).popleft()
        url = norm_url(url)
        if not url or url in seen:
            continue
        seen.add(url)
        host = host_of(url)
        if in_set(host, FILE_HOSTS) or in_set(host, NO_CRAWL) or re.search(r"\.(zip|rar|7z|apk|xapk|apks|ipa|exe|pdf|png|jpe?g|gif|webp|mp4|mp3)$", url, re.I):
            continue
        if per[host] >= per_host:  # spread the budget over many sites
            continue
        per[host] += 1
        r = f.get(url, quiet=True)
        pages += 1
        if not r or "html" not in r[1].lower():
            continue
        final = host_of(r[2])  # where a redirect ended (login pages, file hosts)
        if in_set(final, FILE_HOSTS) or in_set(final, NO_CRAWL) or final.startswith(("accounts.", "access.", "login.", "auth.")):
            continue
        p = Page(r[2])
        try:
            p.feed(r[0])
        except Exception:  # noqa: BLE001
            continue
        # about the game: named in the title / description, or several times in the text
        # (a single mention in a sidebar or "related articles" box is not enough)
        head = f"{p.title} {p.desc}"
        full = relevance(head + " " + " ".join(p.text))  # Yo-kai context may be anywhere on the page
        score = full if full and any(k in head.lower() for k in STRONG) else (full if full >= 8 else 0)
        if score:
            cat.add(r[2], p.title, "crawl", min(10, score), p.desc)
            for link in set(p.links):
                if is_file_link(link):
                    cat.add_file(link, p.anchor.get(link, ""), r[2], p.title)
            if d < depth:
                # other sites first (new sites are the point), then a few pages of this one
                for link in sorted(set(p.links), key=lambda x: host_of(x) == host)[:200]:
                    hot.append((link, d + 1))
        if pages % 25 == 0:
            print(f"  crawled {pages} pages, {len(cat.items)} pages known, {len(cat.files)} file links, "
                  f"{len(hot) + len(cold)} queued", flush=True)
            cat.save()
        if pages % 200 == 0:
            if report:
                report()
            if frontier_path:
                save_frontier(frontier_path, hot, seen)
    if frontier_path:
        save_frontier(frontier_path, hot, seen)


def save_frontier(path, hot, seen):
    """Links still waiting (from relevant pages): the next run starts with them."""
    left = [(u, d) for u, d in hot if norm_url(u) not in seen][:20000]
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(left, fh)


# ---- report ---------------------------------------------------------------------------------

def category(url):
    host = host_of(url)  # by site, so one site is never split over two sections
    for name, keys in CATEGORIES:
        if any(k in host for k in keys):
            return name
    return "Other sites"


def write_md(cat, path):
    by_cat = collections.defaultdict(lambda: collections.defaultdict(list))
    for it in cat.items.values():
        by_cat[category(it["url"])][host_of(it["url"])].append(it)
    lines = ["# Puni Puni / Wibble Wobble on the web", "",
             f"{len(cat.items)} pages on {len({host_of(u) for u in cat.items})} sites, found by "
             f"`Tools/discover_sites.py` (search APIs + crawler); {len(cat.files)} file host / download links at the end.", ""]
    for name in [c for c, _ in CATEGORIES] + ["Other sites"]:
        hosts = by_cat.get(name)
        if not hosts:
            continue
        lines += [f"## {name}", ""]
        for host, items in sorted(hosts.items(), key=lambda kv: (-max(i["score"] for i in kv[1]), kv[0])):
            items.sort(key=lambda i: (-i["score"], i["url"]))
            lines.append(f"* **{host}** ({len(items)})")
            for it in items[:15]:
                t = (it["title"] or it["url"]).replace("[", "(").replace("]", ")").replace("|", "/")
                lines.append(f"  * [{t}]({it['url']})" + (f" - {it['note']}" if it["note"] and name == "Code" else ""))
            if len(items) > 15:
                lines.append(f"  * ... {len(items) - 15} more in sites.json")
        lines.append("")
    if cat.files:
        lines += ["## File host / download links (not downloaded)", "",
                  "Links found on the pages above. Nothing was downloaded; fetch what you want yourself "
                  "(MediaFire: `Tools/mediafire_download.py`). Check what a file is before opening it.", ""]
        by_host = collections.defaultdict(list)
        for it in cat.files.values():
            by_host[it["host"]].append(it)
        for host, items in sorted(by_host.items(), key=lambda kv: (-len(kv[1]), kv[0])):
            lines.append(f"* **{host}** ({len(items)})")
            for it in sorted(items, key=lambda i: i["url"])[:40]:
                t = (it.get("name") or it["text"] or it["url"]).replace("[", "(").replace("]", ")").replace("|", "/")
                size = f", {it['size'] / 1e6:.0f} MB" if it.get("size") else ""
                pg = it["pages"][0] if it["pages"] else None
                lines.append(f"  * [{t}]({it['url']})" + size + (f" - on [{(pg['title'] or pg['url'])[:60]}]({pg['url']})" if pg else ""))
            if len(items) > 40:
                lines.append(f"  * ... {len(items) - 40} more in files.json")
        lines.append("")
    open(path, "w", encoding="utf-8").write("\n".join(lines))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default="sites_found", help="folder for sites.json / SITES.md (merged with earlier runs)")
    ap.add_argument("--no-crawl", action="store_true", help="only the search APIs")
    ap.add_argument("--seed", action="append", default=[], help="extra start page for the crawler (repeatable)")
    ap.add_argument("--depth", type=int, default=2, help="link depth from relevant pages (default 2)")
    ap.add_argument("--max-pages", type=int, default=600, help="crawler page budget (default 600)")
    ap.add_argument("--per-host", type=int, default=12, help="pages per site and run (default 12)")
    ap.add_argument("--delay", type=float, default=1.5, help="seconds between requests to one host")
    ap.add_argument("--only", help="comma list of sources: archive,github,gitlab,wikipedia,fandom,hn,reddit,youtube,websearch (none = crawl only)")
    ap.add_argument("--hours", type=float, help="stop crawling after this long (long runs: also raise --max-pages)")
    ap.add_argument("--fresh", action="store_true", help="ignore the links left over from the last run")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    cat = Catalog(os.path.join(a.out, "sites.json"))
    before = len(cat.items)
    f = Fetcher(a.delay)

    for src in SOURCES:
        name = src.__name__[4:]
        if a.only and not any(name.startswith(x) for x in a.only.split(",")):
            continue
        n = len(cat.items)
        print(f"[{name}]", flush=True)
        try:
            src(f, cat)
        except Exception as e:  # noqa: BLE001 - one broken source must not stop the run
            print(f"  {name} failed: {e}", file=sys.stderr)
        print(f"  +{len(cat.items) - n}", flush=True)
        cat.save()

    def report():
        mediafire_info(f, cat)
        cat.save()
        write_md(cat, os.path.join(a.out, "SITES.md"))

    if not a.no_crawl:
        frontier = os.path.join(a.out, "frontier.json")
        left = []
        if os.path.exists(frontier) and not a.fresh:
            left = [u for u, _ in json.load(open(frontier, encoding="utf-8"))]
        # pages already known to be relevant first, unconfirmed links (score 0) after them,
        # links left over from the last run in between
        known = sorted((u for u in cat.items if not in_set(host_of(u), NO_CRAWL)), key=lambda u: -cat.items[u]["score"])
        seeds = a.seed + SEEDS + [u for u in known if cat.items[u]["score"] > 0] + left + [u for u in known if cat.items[u]["score"] == 0]
        deadline = time.time() + a.hours * 3600 if a.hours else None
        print(f"[crawl] {len(seeds)} start pages ({len(left)} from the last run), depth {a.depth}, up to {a.max_pages} pages"
              + (f", {a.hours} h" if a.hours else ""), flush=True)
        crawl(f, cat, seeds, a.depth, a.max_pages, a.per_host, deadline, frontier, report)

    # links that were only candidates (score 0) and never turned out to be about the game
    cat.items = {u: it for u, it in cat.items.items() if it["score"] > 0}
    report()
    print(f"{len(cat.items)} pages ({len(cat.items) - before} new) on {len({host_of(u) for u in cat.items})} sites "
          f", {len(cat.files)} file links -> {a.out}/SITES.md, {a.out}/sites.json, {a.out}/files.json")


if __name__ == "__main__":
    main()
