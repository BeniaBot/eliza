# How long is an ELIZA answer, really?
#
# Weizenbaum's DOCTOR does not reply in two words. It speaks in full clauses
# and usually plants the user's own words inside them. This measures that, so
# the Hebrew script can be held to the same register instead of drifting into
# terse interrogatives.

import io
import re
import sys


def tokenize(text):
    out = []
    cur = []
    i = 0
    while i < len(text):
        c = text[i]
        if c == ';':
            if cur:
                out.append(''.join(cur))
                cur = []
            while i < len(text) and text[i] != '\n':
                i += 1
        elif c in '()=':
            if cur:
                out.append(''.join(cur))
                cur = []
            out.append(c)
        elif c.isspace():
            if cur:
                out.append(''.join(cur))
                cur = []
        else:
            cur.append(c)
        i += 1
    if cur:
        out.append(''.join(cur))
    return out


def reassemblies(path):
    """Every reassembly rule, paired with the size of its decomposition.

    A reassembly is a list nested three deep: rule -> transformation ->
    reassembly, and it is not the first child of its transformation (that is
    the decomposition pattern).

    The decomposition matters for the second measurement. A rule whose pattern
    is the bare wildcard (0) has only one part -- the whole sentence -- and
    Weizenbaum never quotes that back. Counting those against the script would
    punish it for having a rich set of fallback lines, which is the opposite of
    what we are trying to encourage.
    """
    tokens = tokenize(io.open(path, encoding='utf-8').read())

    found = []
    depth = 0
    child = {}          # index of the child being read, per depth
    start = {}
    decomp = 0          # parts in the decomposition of the current transformation

    for n, t in enumerate(tokens):
        if t == '(':
            child[depth] = child.get(depth, -1) + 1
            depth += 1
            child[depth] = -1
            start[depth] = n + 1
        elif t == ')':
            if depth == 3 and child[2] == 0:
                decomp = len([w for w in tokens[start[depth]:n] if w not in '()='])
            elif depth == 3 and child[2] > 0:
                raw = [w for w in tokens[start[depth]:n] if w not in '()']
                # (=DIT) is a jump to another rule, not an answer at all.
                if raw and raw[0] != '=':
                    words = [w for w in raw if w != '=']
                    if words:
                        found.append((decomp, words))
            depth -= 1

    return found


def report(path, label):
    rules = [(d, w) for d, w in reassemblies(path) if w != ['NEWKEY']]
    if not rules:
        print('%-22s no reassembly rules found' % label)
        return

    lengths = sorted(len(w) for _, w in rules)
    average = sum(lengths) / float(len(lengths))
    terse = 100 * sum(1 for n in lengths if n <= 4) // len(lengths)

    # Only rules that have something to quote are asked whether they quote.
    can_quote = [(d, w) for d, w in rules if d > 1]
    quoting = [w for d, w in can_quote if any(x.isdigit() for x in w)]
    ratio = 100 * len(quoting) // len(can_quote) if can_quote else 0

    print('%-22s %3d answers, %.1f words on average, longest %d'
          % (label, len(rules), average, lengths[-1]))
    print('%-22s %2d%% are four words or fewer'
          % ('', terse))
    print('%-22s %2d%% of the %d answers that could quote the speaker do'
          % ('', ratio, len(can_quote)))

    if '--silent' in sys.argv:
        silent = [' '.join(w) for d, w in can_quote
                  if not any(x.isdigit() for x in w)]
        print('%-22s answers that could quote and do not:' % '')
        for line in silent:
            print('%-22s   %s' % ('', line))
    print()


for path in [a for a in sys.argv[1:] if not a.startswith('--')]:
    report(path, path.replace('\\', '/').split('/')[-1])
