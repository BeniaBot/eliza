# Pull the DOCTOR script that the reference implementation actually runs out of
# eliza.cpp, so it can be compared with the elizagen transcription.

import io
import re
import sys

SRC = 'scripts/eliza_reference.cpp'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'scripts/doctor_reference.txt'
NAME = 'CACM_1966_01_DOCTOR_script'

BS = chr(92)
DQ = chr(34)

text = io.open(SRC, encoding='utf-8', errors='replace').read()

start = text.index(NAME)
start = text.index(DQ, start)

# Collect the run of adjacent string literals that follows.
PIECE = re.compile(r'"((?:[^"' + BS + BS + r']|' + BS + BS + r'.)*)"')
out = []
pos = start
while True:
    m = PIECE.match(text, pos)
    if not m:
        # skip whitespace and line comments between literals
        rest = text[pos:]
        skip = re.match(r'(?:\s|//[^\n]*)+', rest)
        if skip:
            pos += skip.end()
            continue
        break
    piece = m.group(1)
    piece = piece.replace(BS + 'n', '\n')
    piece = piece.replace(BS + DQ, DQ)
    piece = piece.replace(BS + "'", "'")
    piece = piece.replace(BS + BS, BS)
    out.append(piece)
    pos = m.end()

script = ''.join(out)
io.open(OUT, 'w', encoding='utf-8', newline='\n').write(script)
sys.stderr.write('%d characters, %d lines -> %s\n' %
                 (len(script), script.count('\n'), OUT))
