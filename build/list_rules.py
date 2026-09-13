# List the keyword of every top-level form in a script file, so the parser's
# view can be compared against the file's.

import io
import sys

path = sys.argv[1] if len(sys.argv) > 1 else 'scripts/doctor.txt'
text = io.open(path, encoding='utf-8').read()

tokens = []
cur = []
i = 0
while i < len(text):
    c = text[i]
    if c == ';':
        if cur:
            tokens.append(''.join(cur))
            cur = []
        while i < len(text) and text[i] != '\n':
            i += 1
    elif c in '()=':
        if cur:
            tokens.append(''.join(cur))
            cur = []
        tokens.append(c)
    elif c.isspace():
        if cur:
            tokens.append(''.join(cur))
            cur = []
    else:
        cur.append(c)
    i += 1
if cur:
    tokens.append(''.join(cur))

depth = 0
keywords = []
for n, t in enumerate(tokens):
    if t == '(':
        if depth == 0 and n + 1 < len(tokens):
            keywords.append(tokens[n + 1])
        depth += 1
    elif t == ')':
        depth -= 1

print('%d top-level forms' % len(keywords))
for k in keywords:
    print(k)
