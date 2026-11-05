#!/usr/bin/env python3
"""
Machine-translates every Japanese text the server could not translate, so the whole game is in
English. Runs offline on the CPU with a CTranslate2 Japanese -> English model.

1. Export what is left: admin panel -> Translation -> "Download untranslated texts"
   (or GET /admin/api/translation/untranslated with the X-Admin-Token header) -> untranslated.json
2. Get a model (any CTranslate2 ja->en model with SentencePiece files works), for example
     * Sugoi v4 (best for game dialogue; NTT licence: non-commercial / research use only)
         https://huggingface.co/entai2965/sugoi-v4-ja-en-ctranslate2
     * FuguMT ja-en (CC BY-SA 4.0)   https://huggingface.co/shift-ykurami/fugumt-ja-en-ct2-f32
     * Argos Translate ja_en (MIT)   https://argos-net.com/v1/translate-ja_en-1_1.argosmodel (unzip it)
3. pip install ctranslate2 sentencepiece
   python Tools/machine_translate.py --input untranslated.json --model path/to/model \
          --translations Translations/en --out Translations/en/machine.generated.json
4. Restart the server. machine*.json files have the lowest priority: every real translation wins.

What the tool does around the model:
  * names and terms that are already in the dictionaries (Yo-kai, characters, tribes, items,
    Tools/translation_glossary.json) are put in as English before translating, so they stay right,
  * dialogue pages (split by ▽) are translated one by one and the line breaks are put back so the
    text fits the game's text boxes; <R>..</R> highlights are kept where the highlighted words can
    be found again,
  * it can be stopped at any time and continues where it stopped (--out is saved every batch).

Output values are machine translations: good enough to play, not perfect. Fix any entry by putting
the corrected text in another dictionary file - it overrides the machine file.
"""
import argparse
import json
import os
import re
import sys
import time
import unicodedata

JAPANESE = re.compile(r"[぀-ヿ㐀-鿿ｦ-ﾟ]")
HIRAGANA = re.compile(r"[ぁ-ゟ]")
KANA_KANJI_RUN = re.compile(r"[぀-ヿ㐀-鿿ー]+")
TAG = re.compile(r"<([A-Z])>(.*?)</\1>", re.S)
ANY_TAG = re.compile(r"</?[A-Z]>")
# game placeholders such as <USERNAME>: replaced by a plain name the model keeps, then put back
PLACEHOLDER = re.compile(r"<[A-Z][A-Z_0-9]+>")
STAND_INS = ["Alex", "Sam", "Max", "Kim"]
# dialogue / speech: these markers keep "I", without them leading "I'll" is removed
SPEECH = re.compile(r"[！？!?…♪～〜「」『』▽]|ニャ|だよ|です|ます|ぞ$|ね$|よ$|わ$|俺|オレ|私|わたし|僕|ボク|おいら|オイラ|あたし|わし|ワシ|拙者|我")
LEADING_I = re.compile(r"^(?:I'll|I will|I'm going to|I am going to|I'm gonna|We'll|I)\s+(?=[a-z])")


def load_model(path, threads):
    import ctranslate2
    import sentencepiece as spm

    def find(*names):
        for n in names:
            p = os.path.join(path, n)
            if os.path.exists(p):
                return p
        return None

    model_dir = path if os.path.exists(os.path.join(path, "model.bin")) else os.path.join(path, "model")
    src = find("spm/spm.ja.nopretok.model", "source.spm", "sentencepiece.model", "spm.ja.nopretok.model")
    tgt = find("spm/spm.en.nopretok.model", "target.spm", "sentencepiece.model", "spm.en.nopretok.model")
    if not src or not tgt or not os.path.exists(os.path.join(model_dir, "model.bin")):
        sys.exit(f"{path}: no CTranslate2 model with SentencePiece files found")
    # Marian-style models (FuguMT) need the end-of-sentence token
    eos = os.path.exists(os.path.join(path, "source.spm"))
    translator = ctranslate2.Translator(model_dir, device="cpu", compute_type="int8", intra_threads=threads)
    sp_src = spm.SentencePieceProcessor(model_file=src)
    sp_tgt = spm.SentencePieceProcessor(model_file=tgt)

    def translate(texts, beam):
        toks = [sp_src.encode(t, out_type=str) + (["</s>"] if eos else []) for t in texts]
        res = translator.translate_batch(toks, beam_size=beam, max_batch_size=32, repetition_penalty=1.15,
                                         max_decoding_length=256)
        return [sp_tgt.decode(r.hypotheses[0]) for r in res]

    return translate


def load_glossary(folder, extra):
    """Short names / terms (value of at most 4 words) from the dictionaries, longest key first."""
    terms = {}
    files = []
    if folder and os.path.isdir(folder):
        files += [os.path.join(folder, f) for f in sorted(os.listdir(folder))
                  if f.endswith(".json") and not f.startswith("machine") and not f.startswith("patterns")
                  and not f.startswith("phrases") and not f.startswith("untranslated")]
    if extra and os.path.exists(extra):
        files.append(extra)
    for f in files:
        try:
            with open(f, encoding="utf-8-sig") as fh:
                d = json.load(fh)
        except (OSError, ValueError) as e:
            print(f"skipping {f}: {e}")
            continue
        for k, v in d.items():
            if k.startswith("_") or not isinstance(v, str) or not v.strip():
                continue
            k = unicodedata.normalize("NFKC", k)
            if not JAPANESE.search(k) or JAPANESE.search(v) or "\n" in k:
                continue
            # one-character terms only from the hand-made glossary
            if len(v.split()) > 4 or re.search(r"[.!?]$", v) or (len(k) < 2 and f != extra):
                continue
            # short hiragana words ("たま", "かぜ") are inside too many other words
            if not re.search(r"[ァ-ヿ㐀-鿿]", k) and len(k) < 4 and f != extra:
                continue
            terms[k] = v.strip()
    return sorted(terms.items(), key=lambda kv: -len(kv[0]))


NUMBER_TERMS = [(re.compile(r"(\d+)周年"), lambda m: f" {m.group(1)}{ordinal(int(m.group(1)))} Anniversary "),
                (re.compile(r"(\d+)連ガシャ"), r" \1-pull Crank-a-kai "),
                (re.compile(r"(\d+)連"), r" \1-pull ")]


def ordinal(n):
    return "th" if 10 <= n % 100 <= 20 else {1: "st", 2: "nd", 3: "rd"}.get(n % 10, "th")


def protect(text, glossary):
    """Puts the English names in. Returns the new text and the names used."""
    used = []
    if not JAPANESE.search(text):
        return text, used
    for rx, en in NUMBER_TERMS:
        text = rx.sub(en, text)
    for k, v in glossary:
        if k in text:
            text = text.replace(k, f" {v} ")
            used.append(v)
            if not JAPANESE.search(text):
                break
    text = re.sub(r" {2,}", " ", text)
    text = re.sub(r" +([:、。,.!?！？)）」』])", r"\1", text)
    text = re.sub(r"([(（「『]) +", r"\1", text)
    return text.strip(), used


def restore_case(out, used):
    for v in sorted(set(used), key=len, reverse=True):
        out = re.sub(r"(?<![A-Za-z])" + re.escape(v) + r"(?![A-Za-z])", v.replace("\\", "\\\\"), out, flags=re.I)
    return out


def tidy(src, out, used):
    out = out.strip()
    # things the model likes to add
    if "♪" not in src:
        out = out.replace("♪", "").strip()
    if not re.search(r"[「」『』\"“”]", src):
        # the model quotes the English names it finds in the sentence
        out = re.sub(r"[\"“”]", "", out)
    out = re.sub(r" +([:,.!?])", r"\1", out).strip()
    out = restore_case(out, used)
    if len(out) > 3 and out.isupper() and not src.isascii():
        out = " ".join(w if len(w) <= 3 else w[:1] + w[1:].lower() for w in out.split())
    out = out.replace("’", "'")
    # the Japanese middle dot between name parts ("Y覚醒・剣豪紅丸")
    out = re.sub(r"\s*[・･]\s*", ": " if "・" in src and not HIRAGANA.search(src) else " ", out).strip()
    if "%" in src or "％" in src:
        out = re.sub(r"(\d) percent\b", r"\1%", out)
    out = re.sub(r"(?<=[A-Za-z]):(?=[0-9+\-])", ": ", out)
    if "わざゲージ" in src or "ひっさつわざ" in src:
        out = re.sub(r"\b(?:S[A-Za-z]*)?[Uu]ltimate [Gg]auge", "Soultimate gauge", out)
    if not SPEECH.search(src):
        # skill / item descriptions have no subject; the model likes to add "I'll"
        out = LEADING_I.sub("", out)
        if out[:1].islower():
            out = out[0].upper() + out[1:]
        # names / labels: "A Forever Coin" -> "Forever Coin"
        if len(src) <= 20 and not re.search(r"[。、]", src):
            out = re.sub(r"^(?:A|An|The) (?=[A-Z])", "", out)
        # labels without a sentence end in Japanese get none in English either
        if not re.search(r"[。.!?！？]$", src) and out.endswith(".") and not out.endswith("..."):
            out = out[:-1]
    elif out[:1].islower():
        out = out[0].upper() + out[1:]
    # a short name without hiragana is a proper noun: Title Case ("ikari shinji" -> "Ikari Shinji")
    if not HIRAGANA.search(src) and len(src) <= 16 and len(out.split()) <= 4 and out == out.lower():
        out = " ".join(w[:1].upper() + w[1:] for w in out.split())
    return out.replace("|", "/").replace("*", "＊")


def wrap(text, width, lines):
    """Re-breaks English text into lines of about `width` characters."""
    words, rows, cur = text.split(), [], ""
    for w in words:
        if cur and len(cur) + 1 + len(w) > width:
            rows.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}" if cur else w
    if cur:
        rows.append(cur)
    return "\n".join(rows)


def disp_width(s):
    return sum(2 if unicodedata.east_asian_width(c) in "WF" else 1 for c in s)


SENTENCE_END = re.compile(r"[。！？!?♪]$")
SEGMENT_START = re.compile(r"^[～〜【「『(（]")


def segments(lines):
    """Joins the lines of a page back into sentences (lines are broken for the text box only)."""
    segs, cur = [], ""
    for l in lines:
        l = l.strip()
        if cur and (SENTENCE_END.search(cur) or SEGMENT_START.match(l)):
            segs.append(cur)
            cur = ""
        cur += l
    if cur:
        segs.append(cur)
    return segs


def placeholders(page):
    """[(placeholder, stand-in name)] for every different placeholder of a page, in order."""
    seen = list(dict.fromkeys(PLACEHOLDER.findall(page)))
    return [(ph, STAND_INS[i % len(STAND_INS)]) for i, ph in enumerate(seen)]


def prepare(page, glossary):
    """One page of text -> (sentences for the model, names used, line layout, highlights)."""
    lines = page.split("\n")
    highlights = [m.group(2) for m in TAG.finditer(page)]
    srcs, used, joins = [], [], []
    for seg in segments(lines):
        flat = ANY_TAG.sub("", seg)
        for ph, stand_in in placeholders(page):
            flat = flat.replace(ph, stand_in)
        # short labels made of parts ("10周年・極妖怪パス"): each part on its own, joined with ": "
        # "HP・ATK" is a list, not name parts
        flat = re.sub(r"(?<=[A-Za-z0-9])・(?=[A-Za-z0-9])", "/", flat)
        if ("・" in flat and len(flat) <= 30 and not HIRAGANA.search(flat.replace("の", ""))
                and not re.search(r"[！？!?。\n]", flat)
                and all(len(x.strip()) >= 2 for x in flat.split("・"))):
            parts = [x for x in flat.split("・") if x.strip()]
            for k, part in enumerate(parts):
                src, u = protect(unicodedata.normalize("NFKC", part).strip(), glossary)
                srcs.append(src)
                used += u
                joins.append(": " if k < len(parts) - 1 else " ")
            continue
        flat = unicodedata.normalize("NFKC", flat).replace("　", " ").strip()
        src, u = protect(flat, glossary)
        srcs.append(src)
        used += u
        joins.append(" ")
    width = max(disp_width(ANY_TAG.sub("", l)) for l in lines) if lines else 0
    return srcs, used, (len(lines), width), highlights, joins


NUM = re.compile(r"\d+(?:\.\d+)?")


def template(text):
    return NUM.sub("#", unicodedata.normalize("NFKC", text))


def renumber(rep_src, rep_out, member):
    """Turns the translation of one text into that of a text that only differs in its numbers."""
    a = NUM.findall(unicodedata.normalize("NFKC", rep_src))
    b = NUM.findall(unicodedata.normalize("NFKC", member))
    if len(a) != len(b):
        return None
    used = set()
    pieces, last = [], 0
    for m in NUM.finditer(rep_out):
        idx = next((i for i, x in enumerate(a) if x == m.group(0) and i not in used), None)
        if idx is None:
            return None
        used.add(idx)
        pieces.append(rep_out[last:m.start()] + b[idx])
        last = m.end()
    if len(used) != len(a):
        return None  # a number got lost ("3" -> "three"): translate this text on its own
    return "".join(pieces) + rep_out[last:]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--input", required=True, help="untranslated.json exported by the server")
    ap.add_argument("--model", required=True, help="CTranslate2 ja->en model folder")
    ap.add_argument("--translations", default="Translations/en", help="dictionaries used as glossary")
    ap.add_argument("--glossary", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "translation_glossary.json"))
    ap.add_argument("--out", default="Translations/en/machine.generated.json")
    ap.add_argument("--beam", type=int, default=3)
    ap.add_argument("--threads", type=int, default=os.cpu_count() or 4)
    ap.add_argument("--batch", type=int, default=64, help="texts per batch (output is saved after each)")
    ap.add_argument("--limit", type=int, default=0, help="only translate this many texts (for a test)")
    args = ap.parse_args()

    with open(args.input, encoding="utf-8-sig") as f:
        todo = [k for k in json.load(f) if not k.startswith("_") and JAPANESE.search(k)]
    done = {}
    if os.path.exists(args.out):
        with open(args.out, encoding="utf-8") as f:
            done = {k: v for k, v in json.load(f).items() if not k.startswith("_")}
    todo = [k for k in todo if k not in done]
    if args.limit:
        todo = todo[:args.limit]
    glossary = load_glossary(args.translations, args.glossary)
    print(f"{len(todo)} texts to translate ({len(done)} already done), {len(glossary)} glossary terms", flush=True)
    if not todo:
        return
    translate = load_model(args.model, args.threads)

    def save():
        out = {"_info": "Machine translation (Tools/machine_translate.py). Lowest priority: any other dictionary overrides it."}
        out.update(dict(sorted(done.items())))
        tmp = args.out + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, indent=1)
        os.replace(tmp, args.out)

    # texts that only differ in their numbers ("効果度:64pt", "効果度:73pt") are translated once
    groups = {}
    for t in todo:
        groups.setdefault(template(t), []).append(t)
    followers = {g[0]: g[1:] for g in groups.values()}
    todo = list(followers)
    print(f"{len(todo)} distinct texts once the numbers are ignored", flush=True)
    # shorter texts first: similar lengths batch better, and the useful short labels are done first
    todo.sort(key=len)
    start = time.time()
    i = 0
    while i < len(todo):  # (todo can grow: numbered texts that need their own translation)
        batch = todo[i:i + args.batch]
        jobs = []  # (text index, page index, prepared)
        pages_of = []
        for ti, text in enumerate(batch):
            sep = "▽\n" if "▽\n" in text else "▽"
            pages = text.replace("\r", "").split(sep) if "▽" in text else [text.replace("\r", "")]
            pages_of.append((sep, pages))
            for pi, page in enumerate(pages):
                jobs.append((ti, pi, prepare(page, glossary)))
        need = [(j[0], j[1], si, sent) for j in jobs for si, sent in enumerate(j[2][0]) if JAPANESE.search(sent)]
        outs = translate([n[3] for n in need], args.beam) if need else []
        seg_out = {(n[0], n[1], n[2]): o for n, o in zip(need, outs)}
        result = {}
        for (tj, pi, prep) in jobs:
            result[(tj, pi)] = "".join(seg_out.get((tj, pi, si), sent).strip().rstrip(".") + prep[4][si]
                                       if prep[4][si] == ": " else seg_out.get((tj, pi, si), sent) + prep[4][si]
                                       for si, sent in enumerate(prep[0])).strip()
        for ti, text in enumerate(batch):
            sep, pages = pages_of[ti]
            new_pages = []
            for (tj, pi, (src, used, (nlines, width), highlights, _)) in [j for j in jobs if j[0] == ti]:
                out = tidy(pages[pi], result[(tj, pi)], used)
                # put the game's placeholders back
                for ph, stand_in in placeholders(pages[pi]):
                    out = re.sub(r"\b" + stand_in + r"\b", ph, out)
                # highlights: wrap the English name again if it is in the sentence
                for h in highlights:
                    hs, hu = protect(unicodedata.normalize("NFKC", h), glossary)
                    if JAPANESE.search(hs):
                        continue
                    m = re.search(re.escape(hs), out, re.I)
                    if m:
                        out = out[:m.start()] + "<R>" + out[m.start():m.end()] + "</R>" + out[m.end():]
                if nlines > 1:
                    # a full-width character is about two English letters wide
                    out = wrap(out, max(14, int(width * 1.15)), nlines)
                new_pages.append(out)
            done[text] = sep.join(new_pages)
            for other in followers.get(text, []):
                r = renumber(text, done[text], other)
                if r is not None:
                    done[other] = r
                else:
                    todo.append(other)  # translated on its own in a later batch
        save()
        i += len(batch)
        n = i
        rate = n / max(time.time() - start, 0.001)
        print(f"{n}/{len(todo)} ({rate:.1f}/s, about {int((len(todo) - n) / rate / 60)} min left)", flush=True)
    print(f"done: {len(done)} translations in {args.out}")


if __name__ == "__main__":
    main()
