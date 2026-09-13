# Generates the feminine Hebrew script from the masculine one.
#
# Two files, not a switch in the engine. The moment the engine learns "if the
# speaker is a woman then", it stops being Weizenbaum's engine and the checks
# that hold it to the 1966 conversations stop meaning anything. A script is
# data; a second script is still data.
#
# Generated, not written by hand, or the two drift apart within a week.
#
# What is NOT touched, and why:
#
#   * the reflection layer. Unvocalised Hebrew writes the second-person suffix
#     the same for a man and a woman -- לך, אותך, שלך, ממך -- so it already
#     serves both and has no variant to make.
#
#   * the past-tense family. רצחתי is רצחתי for either, and רצחת serves both.
#
#   * anything she says about herself. She is feminine in both files, and the
#     table only maps masculine to feminine, so her own words are already past
#     it.
#
#   * five pronoun keywords. Renaming them would collide: the word that
#     addresses her in the feminine, את, is a rule in its own right.
#
#   * a handful of phrases where a masculine form is not about the speaker at
#     all -- "מה נשמע" is a greeting, and "זה היה אומר" has no person in it.

import io
import re
import sys

SOURCE = 'scripts/doctor.he.txt'
TARGET = 'scripts/doctor.he.f.txt'

# masculine -> feminine
PAIRS = [
    ('אתה', 'את'), ('שאתה', 'שאת'), ('כשאתה', 'כשאת'),

    # what she asks them to do
    ('ספר', 'ספרי'), ('תוכל', 'תוכלי'), ('קח', 'קחי'), ('המשך', 'המשיכי'),
    ('תקבל', 'תקבלי'), ('תרצה', 'תרצי'), ('תדע', 'תדעי'),
    ('תתנצל', 'תתנצלי'), ('נסה', 'נסי'),

    # what she says they are doing
    ('אומר', 'אומרת'), ('שואל', 'שואלת'), ('חושב', 'חושבת'),
    ('מרגיש', 'מרגישה'), ('זוכר', 'זוכרת'), ('יודע', 'יודעת'),
    ('מבין', 'מבינה'), ('מדבר', 'מדברת'), ('מסרב', 'מסרבת'),
    ('מתנצל', 'מתנצלת'), ('שוכח', 'שוכחת'), ('חולם', 'חולמת'),
    ('מאמין', 'מאמינה'), ('מוצא', 'מוצאת'), ('נשמע', 'נשמעת'),
    ('אוהב', 'אוהבת'), ('נזכר', 'נזכרת'), ('חוזר', 'חוזרת'),
    ('מזכיר', 'מזכירה'), ('מספר', 'מספרת'), ('מתכוון', 'מתכוונת'),

    # what she listens for: how they describe themselves
    ('עצוב', 'עצובה'), ('אומלל', 'אומללה'), ('מדוכא', 'מדוכאת'),
    ('מתוסכל', 'מתוסכלת'), ('בודד', 'בודדה'), ('אבוד', 'אבודה'),
    ('מפוחד', 'מפוחדת'), ('מבולבל', 'מבולבלת'), ('אשם', 'אשמה'),
    ('שמח', 'שמחה'), ('מאושר', 'מאושרת'), ('נהנה', 'נהנית'),
    ('רגוע', 'רגועה'), ('יכול', 'יכולה'), ('מסוגל', 'מסוגלת'),
    ('מצליח', 'מצליחה'), ('צריך', 'צריכה'), ('חייב', 'חייבת'),
    ('מעוניין', 'מעוניינת'), ('בטוח', 'בטוחה'), ('משוכנע', 'משוכנעת'),
    ('החלטי', 'החלטית'), ('שלילי', 'שלילית'), ('עייף', 'עייפה'),

    # the rest of the present-tense family. It was half converted, so a
    # woman who dropped the subject pronoun -- which Hebrew does constantly
    # -- fell through to the escape message: "כועסת עליו כל הזמן",
    # "מפחדת מהעתיד", "בורחת מהבית" all reached nothing at all.
    ('שונא', 'שונאת'), ('מפחד', 'מפחדת'), ('דואג', 'דואגת'),
    ('כועס', 'כועסת'), ('צוחק', 'צוחקת'), ('סובל', 'סובלת'),
    ('מתגעגע', 'מתגעגעת'), ('מוותר', 'מוותרת'), ('בורח', 'בורחת'),
    ('נשאר', 'נשארת'), ('ממשיך', 'ממשיכה'), ('שבור', 'שבורה'),
    ('תקוע', 'תקועה'), ('חי', 'חיה'), ('גר', 'גרה'),
    ('עובד', 'עובדת'), ('לומד', 'לומדת'), ('ישן', 'ישנה'),
    ('נחנק', 'נחנקת'), ('מתבייש', 'מתביישת'), ('מתחרט', 'מתחרטת'),
    ('מאשים', 'מאשימה'), ('נלחם', 'נלחמת'), ('מרוצה', 'מרוצה'),
]

# A rule keyword that must keep its name.
KEEP_KEYWORD = set(['אני', 'שאני', 'אתה', 'שאתה', 'את', 'נשמע', 'שלי', 'שלך'])

# Phrases where a masculine form is not about the speaker.
KEEP_PHRASE = ['מה נשמע', 'היה אומר', 'זה אומר', 'מה זה אומר']

# Entry points a woman will type that the masculine file has no word for.
EXTRA = """

; ---- שערי כניסה נוספים ------------------------------------------------
; נוצרו בידי build/make_feminine.py. הפנייה אליה בגרסה הזאת היא "את",
; שכבר יש לה כלל משלה, ולכן היא אינה צריכה שער נוסף. "שאת" כן צריכה.

(שאת = שאני (=SUBJ))

; אישה שמציגה את עצמה. פעם היה כאן כלל על המילה "בת", והוא הגדיר מחדש
; מילת מפתch שכבר קיימת -- והמנתח שומר את ההגדרה האחרונה, כך שהתג
; (/משפחה) של "בת" ושל "אישה" אבד דווקא בגרסה הנשית. הכלל נושא עכשיו שם
; משלו, ושתי המילים האחרות מקושרות אליו.

(גברת 30
    ((0)
        (ובכן, ספרי לי מה מטריד אותך.)
        (זה לא משנה לי. מה הביא אותך לכאן?)
        (NEWKEY)))

(נערה = גברת 30 (=גברת))
"""

HEBREW = 'א-ת'
MARK = '\x01'


def whole(w):
    return re.compile('(?<![' + HEBREW + '])' + re.escape(w) + '(?![' + HEBREW + '])')


REPLACERS = [(whole(m), f) for m, f in PAIRS]
PHRASES = [(whole(p.split()[0]).pattern, p) for p in KEEP_PHRASE]


def convert(line):
    stripped = line.lstrip()
    if stripped.startswith(';') or not stripped:
        return line

    # Hide the phrases that must survive untouched.
    hidden = []
    for phrase in KEEP_PHRASE:
        while phrase in line:
            line = line.replace(phrase, MARK + str(len(hidden)) + MARK, 1)
            hidden.append(phrase)

    # Hide a protected keyword: the first word after a bracket at column zero.
    guard = None
    opening = re.match(r'^(\()([^\s()=]+)', line)
    if opening and opening.group(2) in KEEP_KEYWORD:
        guard = opening.group(2)
        line = line[:1] + MARK + 'K' + MARK + line[opening.end(2):]

    for pattern, feminine in REPLACERS:
        line = pattern.sub(feminine, line)

    if guard is not None:
        line = line.replace(MARK + 'K' + MARK, guard, 1)
    for i, phrase in enumerate(hidden):
        line = line.replace(MARK + str(i) + MARK, phrase, 1)
    return line


def first_group(flat):
    """The decomposition off the front of a transformation.

    Counted rather than split on the first bracket: a pattern with a tag
    in it -- (0 YOU (/BELIEF) THAT 0) -- has brackets inside it, and
    cutting at the first close bracket makes two different patterns look
    the same. It did: one of them was thrown away.
    """
    start = flat.find('(')
    if start < 0:
        return flat
    start = flat.find('(', start + 1)
    if start < 0:
        return flat
    depth = 0
    for i in range(start, len(flat)):
        if flat[i] == '(':
            depth += 1
        elif flat[i] == ')':
            depth -= 1
            if depth == 0:
                return flat[start:i + 1]
    return flat


def drop_repeated_patterns(text):
    """Two patterns that were different can come out the same.

    The masculine script looks for both מזכיר and מזכירה, because ELIZA is
    a she and a person may write either -- and the table turns the first
    into the second, so the rule ends up asking the same question twice.
    The engine takes the first decomposition that matches, so the second
    one never fires again: it is dead text in a file people are invited to
    read. Only whole transformations are dropped, and only when the pattern
    above them is the same word for word.
    """
    lines = text.split('\n')
    out = []
    seen = set()
    depth = 0
    inside_rule = False
    block = []
    block_depth = 0
    for line in lines:
        stripped = line.lstrip()
        comment = stripped.startswith(';')

        if not inside_rule:
            out.append(line)
            if not comment and stripped.startswith('('):
                depth += line.count('(') - line.count(')')
                if depth > 0:
                    inside_rule = True
                    seen = set()
                else:
                    depth = 0
            continue

        # inside a rule: gather one transformation at a time
        if not block and not stripped.startswith('(('):
            out.append(line)
            if not comment:
                depth += line.count('(') - line.count(')')
                if depth <= 0:
                    inside_rule = False
                    depth = 0
            continue

        block.append(line)
        if not comment:
            block_depth += line.count('(') - line.count(')')
        if block_depth > 0:
            continue

        flat = ' '.join(l.strip() for l in block if not l.lstrip().startswith(';'))
        pattern = ' '.join(first_group(flat).split())
        if pattern in seen:
            out.append('; ' + pattern + '  -- כפול, נוצר בהחלפה, והוסר')
        else:
            seen.add(pattern)
            out.extend(block)
        depth += sum(l.count('(') - l.count(')')
                     for l in block if not l.lstrip().startswith(';'))
        if depth <= 0:
            inside_rule = False
            depth = 0
        block = []
        block_depth = 0
    return '\n'.join(out)


def main():
    source = io.open(SOURCE, encoding='utf-8').read()

    # A keyword whose feminine form the script already has is left alone.
    #
    # חוזר and חוזרת are both keywords in the masculine file, each with its
    # own rules, and turning the first into the second declares the keyword
    # twice -- which the engine answers by keeping one and throwing the
    # other's rules away, in silence. There is nothing to convert here: the
    # feminine file wants both words exactly as the masculine one has them.
    have = set(re.findall(r'^\(([^\s()=]+)', source, re.M))
    for masculine, feminine in PAIRS:
        if masculine in have and feminine in have and masculine != feminine:
            KEEP_KEYWORD.add(masculine)

    lines = source.split('\n')
    out = [convert(l) for l in lines]

    # Substituting a keyword can land it on one that already exists.
    seen = set()
    cleaned = []
    for line in out:
        opening = re.match(r'^\(([^\s()=]+)', line)
        if opening and line.rstrip().endswith(')') and '(' in line[1:]:
            key = opening.group(1)
            if key in seen:
                cleaned.append('; ' + line + '   ; כפול, נוצר בהחלפה')
                continue
            seen.add(key)
        cleaned.append(line)

    text = drop_repeated_patterns('\n'.join(cleaned))
    text = text.replace('(NAME לדבר עם אלייזה)', '(NAME לדבר עם אלייזה, נקבה)', 1)

    header = ('; ' + '-' * 69 + '\n'
              '; נוצר אוטומטית מתוך doctor.he.txt בידי build/make_feminine.py.\n'
              '; אין לערוך אותו. כל שינוי נכנס לקובץ המקור ונוצר מחדש.\n'
              '; ' + '-' * 69 + '\n')

    io.open(TARGET, 'w', encoding='utf-8').write(header + text + EXTRA)

    # The words that give a speaker away are exactly the ones this table
    # turns, so the lists and the script can never fall out of step. A word
    # that reads the same either way -- רוצה, חולה -- gives nothing away and
    # belongs to neither list.
    masculine_forms = set(m for m, _ in PAIRS)
    feminine_forms = set(f for _, f in PAIRS)

    markers = set(f for m, f in PAIRS if f != m and f not in masculine_forms)
    markers.discard('את')
    markers |= set(['בת', 'אישה', 'נערה', 'גברת'])
    markers = sorted(markers)

    theirs = set(m for m, f in PAIRS if f != m and m not in feminine_forms)
    theirs.discard('אתה')
    theirs |= set(['בן', 'גבר', 'בחור', 'אדון'])
    theirs = sorted(theirs)

    block = ('\n; ---- מה מסגיר מי מדבר -------------------------------------------------\n'
             '; המילים שהמחולל יודע להפוך, ולכן הן והתסריט לעולם לא ייצאו מסנכרון.\n'
             '; התוכנה מחפשת אותן מיד אחרי "אני", ולכן "אשתי עייפה" אינו מסגיר דבר\n'
             '; על מי שמקליד. מילה שנראית אותו דבר בשני המינים אינה מסגירה כלום\n'
             '; ואינה נמצאת באף אחת מהרשימות.\n'
             '(FEMININE-MARKERS ' + ' '.join(markers) + ')\n'
             '(MASCULINE-MARKERS ' + ' '.join(theirs) + ')\n')

    # The block must land before the opening line. A directive that comes after
    # it is no longer a directive at all -- the reader has moved on and takes it
    # for an ordinary rule, in silence. The source marks the spot.
    for path in (SOURCE, TARGET):
        current = io.open(path, encoding='utf-8').read()
        # Put the placeholder back as the block comes out, so running this
        # twice is the same as running it once.
        current = re.sub(
            r'; ---- מה מסגיר.*?\n\(FEMININE-MARKERS[^\n]*\)'
            r'(\n\(MASCULINE-MARKERS[^\n]*\))?',
            '; @markers', current, flags=re.S)
        if '; @markers' not in current:
            raise SystemExit('no "; @markers" line in ' + path)
        current = current.replace('; @markers', block.strip(), 1)
        io.open(path, 'w', encoding='utf-8').write(current)

    # A generator can introduce a keyword that already exists as easily as a
    # person can, and the parser keeps the later definition without a word --
    # which is how "בת" lost its family tag in the feminine script alone.
    final = io.open(TARGET, encoding='utf-8').read()
    seen = {}
    for number, line in enumerate(final.split('\n'), 1):
        opening = re.match(r'^\(([^\s()=]+)', line)
        if not opening:
            continue
        word = opening.group(1)
        if word in seen:
            raise SystemExit(
                'make_feminine: %s is defined twice in %s, at lines %d and %d. '
                'The parser keeps the second and drops the first.'
                % (word, TARGET, seen[word], number))
        seen[word] = number

    changed = sum(1 for a, b in zip(lines, out) if a != b)
    sys.stderr.write('%d lines rewritten, %d markers -> %s\n'
                     % (changed, len(markers), TARGET))


main()
