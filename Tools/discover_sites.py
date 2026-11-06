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
  2. A polite crawler starts from what was found (and --seed URLs): it reads robots.txt, waits
     between requests to the same host, reads HTML only, and keeps a page when its title / text is
     about Puni Puni or Wibble Wobble. Links of relevant pages are followed up to --depth.
  3. Results are merged with earlier runs: <out>/sites.json (everything, with where it was found)
     and <out>/SITES.md (by category, domain by domain).

Left out on purpose
  * Sites behind bot checks or logins (DuckDuckGo/Google result pages, Reddit, X, Discord...):
    the tool never tries to get past a bot check.
  * File hosts (MediaFire, Mega, Google Drive files, Dropbox, torrent sites...): links found there
    are not followed or listed. Downloads go through Tools/mediafire_download.py with links you
    choose yourself.
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
TIMEOUT = 25
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
    ("Official", ["yw-p.com", "level5.co.jp", "level5.jp", "youkai-watch.jp", "yo-kai.com", "level5-ia.com"]),
    ("Wikis", ["fandom.com", "wikia", "miraheze.org", "wiki", "wikiwiki.jp", "seesaawiki.jp", "atwiki.jp"]),
    ("Code", ["github.com", "gitlab.com", "codeberg.org", "bitbucket.org", "sourceforge.net"]),
    ("Archives", ["archive.org"]),
    ("Videos", ["youtube.com", "youtu.be", "nicovideo.jp", "twitch.tv", "bilibili.com"]),
    ("Communities", ["reddit.com", "discord", "forum", "gamefaqs", "5ch", "2ch", "board", "community", "news.ycombinator.com", "stackexchange", "stackoverflow"]),
    ("Guides / news", ["game8", "gamewith", "appmedia", "altema", "famitsu", "4gamer", "gamerch", "kamigame", "news", "blog", "note.com", "hatenablog"]),
]


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


def relevance(text):
    t = (text or "").lower()
    strong = sum(t.count(k) for k in STRONG)
    if strong == 0:
        return 0
    return strong * 2 + min(5, sum(t.count(k) for k in YOKAI))


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
                    body = r.read(MAX_BYTES)
                    if r.status == 202:  # bot check pages ("please wait"): leave them
                        return None
                    charset = re.search(r"charset=([\w-]+)", ctype)
                    return body.decode(charset.group(1) if charset else "utf-8", errors="replace"), ctype, r.geturl()
            except urllib.error.HTTPError as e:
                if e.code in (429, 503) and attempt < retries:
                    time.sleep(int(e.headers.get("Retry-After") or 0) or 10 * (attempt + 1))
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

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "a" and a.get("href"):
            self.links.append(urllib.parse.urljoin(self.base, a["href"]))
        elif tag == "title":
            self._t = True
        elif tag == "meta" and (a.get("name") or a.get("property") or "").lower() in ("description", "og:description"):
            self.desc = self.desc or (a.get("content") or "")

    def handle_endtag(self, tag):
        if tag == "title":
            self._t = False

    def handle_data(self, data):
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
                cat.add(r["html_url"], f"{r['full_name']} - {r.get('description') or ''}", "github", s + 2,
                        f"{r.get('stargazers_count', 0)} stars, updated {str(r.get('pushed_at', ''))[:10]}")
                if r.get("homepage"):
                    cat.add(r["homepage"], r["full_name"] + " (homepage)", "github", s)
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


SOURCES = [src_archive, src_github, src_gitlab_codeberg, src_wikipedia, src_fandom, src_hn_stackexchange]


# ---- crawler --------------------------------------------------------------------------------

def crawl(f, cat, seeds, depth, max_pages):
    queue = collections.deque((u, 0) for u in seeds)
    seen, pages = set(), 0
    while queue and pages < max_pages:
        url, d = queue.popleft()
        url = norm_url(url)
        if not url or url in seen:
            continue
        seen.add(url)
        host = host_of(url)
        if in_set(host, FILE_HOSTS) or in_set(host, NO_CRAWL) or re.search(r"\.(zip|rar|7z|apk|xapk|apks|ipa|exe|pdf|png|jpe?g|gif|webp|mp4|mp3)$", url, re.I):
            continue
        r = f.get(url, quiet=True)
        pages += 1
        if not r or "html" not in r[1].lower():
            continue
        p = Page(r[2])
        try:
            p.feed(r[0])
        except Exception:  # noqa: BLE001
            continue
        text = f"{p.title} {p.desc} {' '.join(p.text)}"
        score = relevance(text)
        if score:
            cat.add(r[2], p.title, "crawl", min(10, score), p.desc)
            if d < depth:
                # same-site pages first (guides have many sub pages), then other sites
                for link in sorted(set(p.links), key=lambda x: host_of(x) != host)[:200]:
                    queue.append((link, d + 1))
        if pages % 25 == 0:
            print(f"  crawled {pages} pages, {len(cat.items)} sites known, {len(queue)} queued", flush=True)
            cat.save()


# ---- report ---------------------------------------------------------------------------------

def category(url):
    host, path = host_of(url), urllib.parse.urlsplit(url).path.lower()
    for part in (host, path):  # the site decides first, then the page path
        for name, keys in CATEGORIES:
            if any(k in part for k in keys):
                return name
    return "Other sites"


def write_md(cat, path):
    by_cat = collections.defaultdict(lambda: collections.defaultdict(list))
    for it in cat.items.values():
        by_cat[category(it["url"])][host_of(it["url"])].append(it)
    lines = ["# Puni Puni / Wibble Wobble on the web", "",
             f"{len(cat.items)} pages on {len({host_of(u) for u in cat.items})} sites, found by "
             "`Tools/discover_sites.py` (search APIs + crawler). File hosts are left out on purpose.", ""]
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
    open(path, "w", encoding="utf-8").write("\n".join(lines))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default="sites_found", help="folder for sites.json / SITES.md (merged with earlier runs)")
    ap.add_argument("--no-crawl", action="store_true", help="only the search APIs")
    ap.add_argument("--seed", action="append", default=[], help="extra start page for the crawler (repeatable)")
    ap.add_argument("--depth", type=int, default=2, help="link depth from relevant pages (default 2)")
    ap.add_argument("--max-pages", type=int, default=600, help="crawler page budget (default 600)")
    ap.add_argument("--delay", type=float, default=1.5, help="seconds between requests to one host")
    ap.add_argument("--only", help="comma list of sources: archive,github,gitlab,wikipedia,fandom,hn")
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

    if not a.no_crawl:
        seeds = a.seed + [u for u in cat.items if not in_set(host_of(u), NO_CRAWL)]
        print(f"[crawl] {len(seeds)} start pages, depth {a.depth}, up to {a.max_pages} pages", flush=True)
        crawl(f, cat, seeds, a.depth, a.max_pages)

    # links that were only candidates (score 0) and never turned out to be about the game
    cat.items = {u: it for u, it in cat.items.items() if it["score"] > 0}
    cat.save()
    write_md(cat, os.path.join(a.out, "SITES.md"))
    print(f"{len(cat.items)} pages ({len(cat.items) - before} new) on {len({host_of(u) for u in cat.items})} sites "
          f"-> {a.out}/SITES.md, {a.out}/sites.json")


if __name__ == "__main__":
    main()
