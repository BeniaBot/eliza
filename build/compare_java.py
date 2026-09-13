# Compares Charles Hayden's Java ELIZA against the conversation Weizenbaum
# printed in the 1966 CACM paper, ignoring case and the punctuation his version
# adds for prettiness. Run after ElizaApp has been driven over cacm.txt.

import io
import re
import sys

EXPECTED = [
    "IN WHAT WAY",
    "CAN YOU THINK OF A SPECIFIC EXAMPLE",
    "YOUR BOYFRIEND MADE YOU COME HERE",
    "I AM SORRY TO HEAR YOU ARE DEPRESSED",
    "DO YOU THINK COMING HERE WILL HELP YOU NOT TO BE UNHAPPY",
    "WHAT WOULD IT MEAN TO YOU IF YOU GOT SOME HELP",
    "TELL ME MORE ABOUT YOUR FAMILY",
    "WHO ELSE IN YOUR FAMILY TAKES CARE OF YOU",
    "YOUR FATHER",
    "WHAT RESEMBLANCE DO YOU SEE",
    "WHAT MAKES YOU THINK I AM NOT VERY AGGRESSIVE",
    "WHY DO YOU THINK I DON'T ARGUE WITH YOU",
    "DOES IT PLEASE YOU TO BELIEVE I AM AFRAID OF YOU",
    "WHAT ELSE COMES TO MIND WHEN YOU THINK OF YOUR FATHER",
    "DOES THAT HAVE ANYTHING TO DO WITH THE FACT THAT "
    "YOUR BOYFRIEND MADE YOU COME HERE",
]


def normalise(line):
    line = line.upper()
    line = re.sub(r'[.?!,]', ' ', line)
    return ' '.join(line.split())


path = sys.argv[1] if len(sys.argv) > 1 else '/tmp/chayden/java-out.txt'
lines = io.open(path, encoding='utf-8', errors='replace').read().split('\n')
actual = [l for l in lines if l.strip() and not l.startswith('>>')]

same = 0
for i, want in enumerate(EXPECTED):
    got = actual[i] if i < len(actual) else '<nothing>'
    ok = normalise(got) == normalise(want)
    if ok:
        same += 1
    else:
        print('%2d  published : %s' % (i + 1, want))
        print('    java      : %s' % got.strip())
        print()

print('%d of %d lines match the 1966 paper word for word' % (same, len(EXPECTED)))
