# Writes the rules for "I did something" in Hebrew.
#
# English cannot drop the subject: "I murdered someone" always carries the
# word I, so the DOCTOR script's I rule catches it and answers by quoting it
# back -- YOU SAY YOU MURDERED SOMEONE -- which is what makes ELIZA sound as
# though she followed. Hebrew puts the subject inside the verb: רצחתי is one
# word and there is no אני anywhere in the sentence. A whole class of what
# people actually say therefore reaches no keyword at all and falls out to a
# stock line.
#
# The cure is Weizenbaum's own: substitute the word as it is scanned and link
# to a shared rule, exactly as (DREAMED = DREAMT 4 (=DREAMT)) does.
#
# Both forms are the same for a man and a woman in unvocalised Hebrew --
# רצחתי is רצחתי, and רצחת serves both -- so this family needs no feminine
# variant at all.

import io
import sys

# what the speaker types  ->  the same act, said back to them
VERBS = [
    # Read back out of scripts/doctor.he.txt, which is where
    # the family actually lives: the block below was generated
    # once and then punctuated and extended by hand, so the
    # script is the original and this list follows it.
    (u'אמרתי', u'אמרת'), (u'סיפרתי', u'סיפרת'), (u'שאלתי', u'שאלת'),
    (u'עניתי', u'ענית'), (u'דיברתי', u'דיברת'), (u'צעקתי', u'צעקת'),
    (u'שתקתי', u'שתקת'), (u'שיקרתי', u'שיקרת'), (u'הודיתי', u'הודית'),
    (u'חשבתי', u'חשבת'), (u'הבנתי', u'הבנת'), (u'ידעתי', u'ידעת'),
    (u'האמנתי', u'האמנת'), (u'קיוויתי', u'קיווית'), (u'החלטתי', u'החלטת'),
    (u'התכוונתי', u'התכוונת'), (u'גיליתי', u'גילית'), (u'הרגשתי', u'הרגשת'),
    (u'פחדתי', u'פחדת'), (u'בכיתי', u'בכית'), (u'כעסתי', u'כעסת'),
    (u'שנאתי', u'שנאת'), (u'אהבתי', u'אהבת'), (u'רציתי', u'רצית'),
    (u'נבהלתי', u'נבהלת'), (u'התביישתי', u'התביישת'), (u'נעלבתי', u'נעלבת'),
    (u'סבלתי', u'סבלת'), (u'נהניתי', u'נהנית'), (u'עשיתי', u'עשית'),
    (u'ניסיתי', u'ניסית'), (u'הצלחתי', u'הצלחת'), (u'נכשלתי', u'נכשלת'),
    (u'ויתרתי', u'ויתרת'), (u'החמצתי', u'החמצת'), (u'איבדתי', u'איבדת'),
    (u'הפסדתי', u'הפסדת'), (u'ניצחתי', u'ניצחת'), (u'שברתי', u'שברת'),
    (u'הרסתי', u'הרסת'), (u'פגעתי', u'פגעת'), (u'עזרתי', u'עזרת'),
    (u'גנבתי', u'גנבת'), (u'בגדתי', u'בגדת'), (u'רצחתי', u'רצחת'),
    (u'הרגתי', u'הרגת'), (u'הכיתי', u'הכית'), (u'הלכתי', u'הלכת'),
    (u'באתי', u'באת'), (u'חזרתי', u'חזרת'), (u'הגעתי', u'הגעת'),
    (u'יצאתי', u'יצאת'), (u'נכנסתי', u'נכנסת'), (u'ברחתי', u'ברחת'),
    (u'עזבתי', u'עזבת'), (u'נסעתי', u'נסעת'), (u'נפלתי', u'נפלת'),
    (u'קמתי', u'קמת'), (u'ישבתי', u'ישבת'), (u'ראיתי', u'ראית'),
    (u'שמעתי', u'שמעת'), (u'למדתי', u'למדת'), (u'עבדתי', u'עבדת'),
    (u'ישנתי', u'ישנת'), (u'אכלתי', u'אכלת'), (u'שתיתי', u'שתית'),
    (u'קניתי', u'קנית'), (u'מכרתי', u'מכרת'), (u'כתבתי', u'כתבת'),
    (u'קראתי', u'קראת'), (u'התחלתי', u'התחלת'), (u'סיימתי', u'סיימת'),
    (u'נולדתי', u'נולדת'), (u'גדלתי', u'גדלת'), (u'הספקתי', u'הספקת'),
    (u'התעוררתי', u'התעוררת'), (u'נרדמתי', u'נרדמת'), (u'פגשתי', u'פגשת'),
    (u'איחרתי', u'איחרת'), (u'חיכיתי', u'חיכית'), (u'קיבלתי', u'קיבלת'),
    (u'נתתי', u'נתת'), (u'לקחתי', u'לקחת'), (u'שילמתי', u'שילמת'),
    (u'הבטחתי', u'הבטחת'), (u'בחרתי', u'בחרת'), (u'נשארתי', u'נשארת'),
    (u'עברתי', u'עברת'), (u'השתניתי', u'השתנית'), (u'התאהבתי', u'התאהבת'),
    (u'נפרדתי', u'נפרדת'), (u'התגרשתי', u'התגרשת'), (u'התחתנתי', u'התחתנת'),
    (u'גרתי', u'גרת'), (u'התפטרתי', u'התפטרת'), (u'פוטרתי', u'פוטרת'),
    (u'הצטערתי', u'הצטערת'), (u'התגעגעתי', u'התגעגעת'), (u'הסתרתי', u'הסתרת'),
    (u'סלחתי', u'סלחת'), (u'צחקתי', u'צחקת'), (u'התחרטתי', u'התחרטת'),
    (u'התקדמתי', u'התקדמת'), (u'הסתדרתי', u'הסתדרת'), (u'השתדלתי', u'השתדלת'),
    (u'התאמצתי', u'התאמצת'), (u'התלבטתי', u'התלבטת'), (u'הסכמתי', u'הסכמת'),
    (u'סירבתי', u'סירבת'), (u'התנגדתי', u'התנגדת'), (u'התווכחתי', u'התווכחת'),
    (u'שכנעתי', u'שכנעת'), (u'הסברתי', u'הסברת'), (u'נזכרתי', u'נזכרת'),
    (u'הזכרתי', u'הזכרת'), (u'טעיתי', u'טעית'), (u'התבלבלתי', u'התבלבלת'),
    (u'התאכזבתי', u'התאכזבת'), (u'נפגעתי', u'נפגעת'), (u'התרגשתי', u'התרגשת'),
    (u'נלחצתי', u'נלחצת'), (u'נרגעתי', u'נרגעת'), (u'התעצבנתי', u'התעצבנת'),
    (u'התייאשתי', u'התייאשת'), (u'השתעממתי', u'השתעממת'), (u'התעייפתי', u'התעייפת'),
    (u'דאגתי', u'דאגת'), (u'התלוננתי', u'התלוננת'), (u'התפללתי', u'התפללת'),
    (u'הפסקתי', u'הפסקת'), (u'המשכתי', u'המשכת'), (u'רצתי', u'רצת'),
    (u'עמדתי', u'עמדת'), (u'שכבתי', u'שכבת'), (u'התארגנתי', u'התארגנת'),
    (u'הכנתי', u'הכנת'), (u'תכננתי', u'תכננת'), (u'בישלתי', u'בישלת'),
    (u'ניקיתי', u'ניקית'), (u'סידרתי', u'סידרת'), (u'תיקנתי', u'תיקנת'),
    (u'בניתי', u'בנית'), (u'שיחקתי', u'שיחקת'), (u'טיילתי', u'טיילת'),
    (u'נחתי', u'נחת'), (u'התקשרתי', u'התקשרת'), (u'שלחתי', u'שלחת'),
    (u'הזמנתי', u'הזמנת'), (u'ביקרתי', u'ביקרת'), (u'נפגשתי', u'נפגשת'),
    (u'הכרתי', u'הכרת'), (u'הודעתי', u'הודעת'), (u'הצעתי', u'הצעת'),
    (u'סיכמתי', u'סיכמת'), (u'נשבעתי', u'נשבעת'), (u'חיפשתי', u'חיפשת'),
    (u'מצאתי', u'מצאת'), (u'הסתכלתי', u'הסתכלת'), (u'שמתי', u'שמת'),
    (u'הנחתי', u'הנחת'), (u'הרמתי', u'הרמת'), (u'זרקתי', u'זרקת'),
    (u'החזרתי', u'החזרת'), (u'הוצאתי', u'הוצאת'), (u'הכנסתי', u'הכנסת'),
    (u'פתחתי', u'פתחת'), (u'סגרתי', u'סגרת'), (u'שמרתי', u'שמרת'),
    (u'אספתי', u'אספת'), (u'חילקתי', u'חילקת'), (u'הרווחתי', u'הרווחת'),
    (u'חסכתי', u'חסכת'), (u'בזבזתי', u'בזבזת'), (u'לימדתי', u'לימדת'),
    (u'נבחנתי', u'נבחנת'), (u'התקבלתי', u'התקבלת'), (u'הצטרפתי', u'הצטרפת'),
    (u'חליתי', u'חלית'), (u'הצטננתי', u'הצטננת'), (u'נפצעתי', u'נפצעת'),
    (u'החלמתי', u'החלמת'),
]

PRECEDENCE = 3

out = []
out.append('; ---- מה שעשיתי --------------------------------------------------------')
out.append('; נוצר בידי build/make_past.py.')
out.append(';')
out.append('; באנגלית אי אפשר להשמיט את הנושא: "I murdered someone" נושאת תמיד את')
out.append('; המילה I, והכלל שלה תופס אותה ומחזיר את דבריו של הדובר. בעברית הגוף')
out.append('; יושב בתוך הפועל -- "רצחתי" היא מילה אחת ואין בה "אני" -- ולכן מחלקה')
out.append('; שלמה של מה שאנשים באמת אומרים אינה מגיעה לשום מילת מפתח.')
out.append(';')
out.append('; התרופה היא של וייצנבאום עצמו: החלפת המילה בסריקה וקישור לכלל משותף,')
out.append('; בדיוק כמו (DREAMED = DREAMT 4 (=DREAMT)) שלו.')
out.append(';')
out.append('; שתי הצורות זהות לגבר ולאישה בכתיב לא מנוקד, ולכן המשפחה הזאת אינה')
out.append('; צריכה גרסה נקבית כלל.')
out.append('')
SECOND = ' '.join(b for _, b in VERBS)

out.append('(PAST')
# Negation needs its own pattern or it falls into the opening wildcard, never
# reaches a reassembly rule, and the meaning quietly inverts: "I did not
# manage to say goodbye" would come back as "why did you say goodbye".
out.append('    ((0 לא (* ' + SECOND + ') 0)')
out.append('        (למה לא 3)')
out.append('        (ומה מנע ממך)')
out.append('        (מתי כן 3)')
out.append('        (ספר לי עוד על כך)')
out.append('        (ומה היה קורה אילו כן 3))')
out.append('')
out.append('    ((0 (* ' + SECOND + ') 0)')
# A component number must stand alone. Glue a full stop to it and "3." stops
# being a number at all and is printed as it is.
out.append('        (למה 2)')
out.append('        (מתי 2)')
out.append('        (אז 2 3)')
out.append('        (ואיך הרגשת כשזה קרה)')
out.append('        (מה הביא אותך לכך)')
out.append('        (ספר לי עוד על כך)')
out.append('        (ומה קרה אחר כך)')
out.append('        (מי עוד יודע על כך))')
out.append('    ((0)')
out.append('        (NEWKEY)))')
out.append('')

for first, second in VERBS:
    out.append('(%s = %s %d (=PAST))' % (first, second, PRECEDENCE))

out.append('')

io.open('scripts/past.he.txt', 'w', encoding='utf-8').write('\n'.join(out))
sys.stderr.write('%d verbs -> scripts/past.he.txt\n' % len(VERBS))
