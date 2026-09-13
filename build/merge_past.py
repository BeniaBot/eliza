# Replaces the generated past-tense section inside the Hebrew script.
#
# The section is delimited by its own heading and the one that follows it, so
# regenerating never disturbs anything a person wrote by hand.

import io
import sys

SCRIPT = 'scripts/doctor.he.txt'
BLOCK = 'scripts/past.he.txt'
HEAD = '; ---- מה שעשיתי -'
NEXT = '; ---- חלומות -'

script = io.open(SCRIPT, encoding='utf-8').read()
block = io.open(BLOCK, encoding='utf-8').read()

start = script.find(HEAD)
end = script.find(NEXT)
if end < 0:
    sys.exit('cannot find the section that follows')

if start < 0:
    # not there yet: put it in front of the next section
    script = script[:end] + block + '\n' + script[end:]
else:
    script = script[:start] + block + '\n' + script[end:]

io.open(SCRIPT, 'w', encoding='utf-8').write(script)
sys.stderr.write('past-tense section replaced in %s\n' % SCRIPT)
