# Writes the rules for a sentence that starts with its verb.
#
# Hebrew drops the subject pronoun in the present tense too, not only in the
# past: people say רוצה לבכות and עייף מהכול, with no אני anywhere. The verb
# itself is the whole clause. English cannot do that -- "want to cry" is not a
# sentence -- so the DOCTOR script has no rule shaped like this and there is
# nothing to translate.
#
# The pattern has no leading wildcard on purpose. ((* רוצה צריך) 0) matches
# only when the sentence begins with one of these words, so "היא רוצה" is left
# alone: somebody else wanting something says nothing about the speaker.
#
# Gender-free throughout: these forms serve a man and a woman alike, except
# the few the feminine table already knows how to turn.

import io
import sys

VERBS = [
    'רוצה', 'צריך', 'חייב', 'מנסה', 'מצליח', 'מקווה', 'מתכוון',
    'מרגיש', 'חושב', 'יודע', 'מבין', 'זוכר', 'שוכח', 'מאמין',
    'אוהב', 'שונא', 'מפחד', 'דואג', 'כועס', 'בוכה', 'צוחק',
    'סובל', 'מתגעגע', 'מחכה', 'מוותר', 'בורח', 'נשאר', 'ממשיך',
    'עייף', 'שבור', 'עצוב', 'שמח', 'לבד', 'אבוד', 'תקוע', 'מבולבל',
    'חי', 'גר', 'עובד', 'לומד', 'ישן', 'חולם', 'נחנק', 'מדבר',
    'עוזר', 'מרוצה', 'מתבייש', 'מתחרט', 'מאשים', 'מוותר', 'נלחם',
]

OPENERS = ['פשוט', 'ממש', 'סתם', 'קצת', 'רק', 'באמת', 'כבר', 'תמיד', 'אולי']

# Verbs that already have a rule of their own in doctor.he.txt. A link
# generated for one of them is written at column zero later in the file, and
# the parser keeps the last definition of a keyword -- so the generated
# one-liner silently replaced a twelve-phrasing rule, or replaced a rule that
# carried a tag with one that carried nothing.
HAS_ITS_OWN = set(['זוכר', 'חולם', 'חושב', 'מרגיש', 'מאמין'])

verbs = ' '.join(VERBS)
openers = ' '.join(OPENERS)

out = []
out.append('; ---- משפט שמתחיל בפועל -------------------------------------------------')
out.append('; נוצר בידי build/make_present.py.')
out.append(';')
out.append('; עברית משמיטה את כינוי הגוף גם בהווה, לא רק בעבר: אומרים "רוצה לבכות"')
out.append('; ו"עייף מהכול", בלי "אני" בשום מקום. באנגלית זה אינו משפט כלל, ולכן')
out.append('; לתסריט המקורי אין כלל בצורה הזאת ואין מה לתרגם.')
out.append(';')
out.append('; לתבנית אין כוכבית פותחת, וזה מכוון: היא נתפסת רק כשהמשפט מתחיל')
out.append('; באחת המילים האלה. "היא רוצה" נשאר בחוץ -- מישהו אחר שרוצה משהו')
out.append('; אינו אומר דבר על מי שמדבר.')
out.append('')
out.append('(PRESENT')
out.append('    ((לא (* ' + verbs + ') 0)')
out.append('        (למה אתה לא 2 3)')
out.append('        (מה מונע ממך)')
out.append('        (תמיד זה ככה)')
out.append('        (ספר לי עוד על כך))')
out.append('')
out.append('    ((0 (* ' + openers + ') (* ' + verbs + ') 0)')
out.append('        (למה אתה 3 4)')
out.append('        (כמה זמן אתה 3 4)')
out.append('        (ספר לי עוד על כך)')
out.append('        (מה גרם לזה))')
out.append('')
out.append('    (((* ' + verbs + ') 0)')
out.append('        (למה אתה 1 2)')
out.append('        (כמה זמן אתה 1 2)')
out.append('        (ומה עוד)')
out.append('        (האם תמיד אתה 1 2)')
out.append('        (ספר לי עוד על כך)')
out.append('        (מה זה אומר עליך))')
out.append('')
out.append('    ((0)')
out.append('        (NEWKEY)))')
out.append('')

# Precedence 1, so that the verb beats לא and the negation pattern is the one
# that answers. It costs nothing elsewhere: when the sentence does carry אני
# the verb is no longer first, none of these patterns match, and NEWKEY hands
# the turn back to the rule for אני.
seen = set()
for verb in VERBS:
    if verb in HAS_ITS_OWN or verb in seen:
        continue          # מוותר was in the list twice, as it happens
    seen.add(verb)
    out.append('(%s 1 (=PRESENT))' % verb)

out.append('')

io.open('build/out/present.he.txt', 'w', encoding='utf-8').write('\n'.join(out))
sys.stderr.write('%d verbs -> build/out/present.he.txt\n' % len(VERBS))
