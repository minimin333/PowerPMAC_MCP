# -*- coding: utf-8 -*-
"""Generate an authoritative data-structure element index from the controller's
firmware intellisense tables (pp_swtbl*.txt). Mechanical; no model tokens."""
import sys, collections, glob, os
sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# firmware dir relative to this script (<repo>/Skills/powerpmac-dev/tools/) so it works from any cwd
FW = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "reference", "firmware"))
struct = collections.defaultdict(set)
srccount = collections.Counter()
total = 0
for fn in sorted(glob.glob(os.path.join(FW, "pp_swtbl*.txt"))):
    for line in open(fn, encoding="utf-8", errors="replace"):
        line = line.strip()
        if not line:
            continue
        srccount[os.path.basename(fn)] += 1
        parts = line.split(",")
        if len(parts) >= 2:
            struct[parts[0]].add(parts[1])
            total += 1

L = []
L.append("# Power PMAC Data-Structure Element Index (authoritative)")
L.append("")
L.append("Generated from the controller firmware intellisense tables `pp_swtbl0-3.txt`")
L.append("(simulator opt/ppmac, fw 2.3.1.82) — the canonical `Structure,Element` lists the")
L.append("IDE/C intellisense uses. For the full element set under a structure, grep the tables")
L.append("in `reference/firmware/` (e.g. `Motor`, `Coord`, `Sys` in `pp_swtbl1.txt`).")
L.append("Live controller is fw 2.8.3.0 — a few elements may differ; confirm with `get_response`.")
L.append("")
L.append("Source tables: " + ", ".join("%s (%d rows)" % (k, v) for k, v in sorted(srccount.items())))
L.append("Total entries: %d  |  distinct top structures: %d" % (total, len(struct)))
L.append("")
L.append("## Top-level structures by element count")
L.append("| Structure | # elements |")
L.append("|---|---|")
for s in sorted(struct, key=lambda x: (-len(struct[x]), x)):
    L.append("| `%s` | %d |" % (s, len(struct[s])))
open(os.path.join(FW, "ELEMENTS_INDEX.md"), "w", encoding="utf-8").write("\n".join(L) + "\n")
print("wrote ELEMENTS_INDEX.md:", len(struct), "structures,", total, "entries")
