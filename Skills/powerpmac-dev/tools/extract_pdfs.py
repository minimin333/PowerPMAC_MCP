# -*- coding: utf-8 -*-
"""Extract PowerPMAC manual PDFs into a grep-able UTF-8 corpus.
Mechanical only: no model tokens. Chunks ~20 pages/file with page-range names.
"""
import os, re, sys, glob
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
from pypdf import PdfReader

# Paths are derived from this script's location so it works from any clone:
#   <repo>/Skills/powerpmac-dev/tools/extract_pdfs.py
HERE = os.path.dirname(os.path.abspath(__file__))
SKILL = os.path.dirname(HERE)                          # <repo>/Skills/powerpmac-dev
REPO = os.path.dirname(os.path.dirname(SKILL))         # <repo>
SRC = os.environ.get("POWERPMAC_MANUAL_DIR", os.path.join(REPO, "Power PMAC Manual"))
OUT = os.path.join(SKILL, "reference", "raw")
CHUNK = 20

MANUALS = {
    "O014-E-02_Power PMAC User Manual.pdf": "user-manual",
    "Power PMAC Software Reference Manual.pdf": "software-ref",
    "Power PMAC 5-Day Training - December 2016.pdf": "training",
    "Power PMAC C Programing_260402.pdf": "c-programming",
}

def clean(t):
    if not t:
        return ""
    t = t.replace("’", "'").replace("‘", "'")
    t = t.replace("“", '"').replace("”", '"')
    t = re.sub(r"[ \t]+\n", "\n", t)
    t = re.sub(r"\n{4,}", "\n\n\n", t)
    return t

os.makedirs(OUT, exist_ok=True)
index_lines = ["# Raw corpus index (page-range chunks, grep these)\n"]

for fname, slug in MANUALS.items():
    path = os.path.join(SRC, fname)
    if not os.path.exists(path):
        print("MISSING:", path); continue
    r = PdfReader(path)
    n = len(r.pages)
    d = os.path.join(OUT, slug)
    os.makedirs(d, exist_ok=True)
    pages = []
    for i in range(n):
        try:
            pages.append(clean(r.pages[i].extract_text()))
        except Exception as e:
            pages.append(f"[extract error p{i+1}: {e}]")
    # TOC = first 18 pages, often holds the table of contents
    with open(os.path.join(d, "_toc.txt"), "w", encoding="utf-8") as f:
        for i in range(min(18, n)):
            f.write(f"\n===== PAGE {i+1} =====\n{pages[i]}\n")
    # chunked body
    chunks = []
    for start in range(0, n, CHUNK):
        end = min(start + CHUNK, n)
        cname = f"p{start+1:04d}-{end:04d}.txt"
        with open(os.path.join(d, cname), "w", encoding="utf-8") as f:
            for i in range(start, end):
                f.write(f"\n===== PAGE {i+1} =====\n{pages[i]}\n")
        chunks.append(cname)
    index_lines.append(f"## {slug}  ({n} pages, {fname})")
    index_lines.append(f"path: reference/raw/{slug}/  | chunks: {len(chunks)} | _toc.txt has TOC")
    index_lines.append("")
    print(f"{slug}: {n} pages -> {len(chunks)} chunks")

with open(os.path.join(OUT, "_INDEX.md"), "w", encoding="utf-8") as f:
    f.write("\n".join(index_lines))
print("DONE ->", OUT)
