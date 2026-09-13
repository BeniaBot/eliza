# How much of the Hebrew script actually carries the user's gender?
#
# Less than it looks. Unvocalised Hebrew writes the second-person suffix the
# same way for a man and a woman -- לך, אותך, שלך, ממך -- so the whole
# reflection layer already serves both. What does carry gender is the pronoun
# itself, the verbs ELIZA supplies in her own sentences, and -- easy to miss --
# the words the patterns listen for, because a woman types עצובה, not עצוב.

import io
import re
import sys

PATH = sys.argv[1] if len(sys.argv) > 1 else 'scripts/doctor.he.txt'

text = io.open(PATH, encoding='utf-8').read()
lines = [l for l in text.split('\n') if not l.lstrip().startswith(';')]
body = '\n'.join(lines)

# Anything inside a (* ... ) group is a word the script listens for.
LISTENS = re.compile(r'\(\*([^()]*)\)', re.S)
listened = []
for group in LISTENS.findall(body):
    listened.extend(group.split())

# Everything else that is plain text in a rule is something she says.
SAYS = []
for line in lines:
    stripped = line.strip()
    if not stripped.startswith('(') or '(*' in stripped or '(/' in stripped:
        continue
    SAYS.append(stripped)
said = ' '.join(SAYS)

MASCULINE_PRONOUN = ['אתה', 'שאתה', 'אינך', 'בך', 'עליך']
IMPERATIVES = ['ספר', 'תוכל', 'קח', 'ניסית', 'המשך', 'חשבת', 'נזכרת', 'שכחת',
               'חלמת', 'דמיינת', 'שאלת', 'סיפרת', 'אמרת', 'היית', 'תקבל',
               'תרצה', 'תדע', 'מרבה', 'מסרב', 'נשמע', 'מדבר', 'אומר', 'שואל',
               'חושב', 'מרגיש', 'זוכר', 'יודע', 'רוצה', 'צריך', 'מבין']

def count(words, where):
    total = 0
    for w in words:
        total += len(re.findall(r'(?<![א-ת])' + re.escape(w) + r'(?![א-ת])', where))
    return total

pronouns = count(MASCULINE_PRONOUN, said)
verbs = count(IMPERATIVES, said)

# In the listened-for groups, which entries are masculine adjectives or verbs?
feminine_endings = ('ה', 'ת')
listened_masculine = [w for w in set(listened)
                      if len(w) > 2 and not w.endswith(feminine_endings)
                      and not w.startswith('*')]

print('%-46s %s' % ('what she says: masculine pronouns', pronouns))
print('%-46s %s' % ('what she says: verbs marked for gender', verbs))
print('%-46s %s' % ('what she listens for, masculine forms',
                    len(listened_masculine)))
print()
print('a sample of the words she listens for and would miss')
print('from a woman:')
for w in sorted(listened_masculine)[:16]:
    print('   ' + w)
print()
print('%-46s %s' % ('total words in (* ) groups', len(set(listened))))
