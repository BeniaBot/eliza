// Eliza.cs -- a faithful reimplementation of Joseph Weizenbaum's ELIZA (1966).
//
// The algorithm here follows Weizenbaum's CACM paper and the recovered
// MAD-SLIP source, including the details that are easy to miss:
//   * the LIMIT counter that cycles 1..4 and gates both the built-in
//     "no match" messages and when a memory may be recalled;
//   * the keystack, ordered by keyword precedence;
//   * clause splitting on , . and BUT, keeping the first clause with a keyword;
//   * reassembly rules cycled round-robin per decomposition rule;
//   * NEWKEY, (=KEYWORD) links and PRE pre-transformations;
//   * the MEMORY rule, whose transformation is chosen by the SLIP mid-square
//     HASH of the BCD encoding of the last word of the user's input.
//
// The script is data, not part of the program -- exactly as Weizenbaum intended.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ElizaApp
{
    // ------------------------------------------------------------------
    // S-expressions
    // ------------------------------------------------------------------

    class Sexp
    {
        public string Atom;          // non-null iff this is an atom
        public List<Sexp> Items;     // non-null iff this is a list

        public bool IsAtom { get { return Atom != null; } }
        public bool IsList { get { return Items != null; } }

        public static Sexp NewAtom(string a) { return new Sexp { Atom = a }; }
        public static Sexp NewList() { return new Sexp { Items = new List<Sexp>() }; }

        public string AtomAt(int i)
        {
            if (Items == null || i < 0 || i >= Items.Count) return null;
            return Items[i].Atom;
        }

        // Flatten to a space separated string, keeping nested parentheses.
        public string Flatten()
        {
            if (IsAtom) return Atom;
            var sb = new StringBuilder("(");
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Items[i].Flatten());
            }
            sb.Append(')');
            return sb.ToString();
        }
    }

    static class SexpReader
    {
        /*  '(' ')' and '=' are self-delimiting; ';' starts a comment.

            Every symbol is folded to upper case as it is read, exactly as the
            reference tokenizer does. It matters: user input is uppercased too,
            so without this a script written in lower case would be inert --
            its keywords could never match anything a person typed. */
        public static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var cur = new StringBuilder();
            Action flush = () =>
            {
                if (cur.Length == 0) return;
                tokens.Add(ElizaEngine.ElizaUppercase(cur.ToString(), false));
                cur.Length = 0;
            };

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ';')
                {
                    flush();
                    while (i < text.Length && text[i] != '\n') i++;
                }
                else if (c == '(' || c == ')' || c == '=')
                {
                    flush();
                    tokens.Add(c.ToString());
                }
                else if (char.IsWhiteSpace(c))
                {
                    flush();
                }
                else cur.Append(c);
            }
            flush();
            return tokens;
        }

        // Read every top level form. Bare atoms (such as START) are returned too.
        public static List<Sexp> ReadAll(string text)
        {
            var tokens = Tokenize(text);
            var result = new List<Sexp>();
            int pos = 0;
            while (pos < tokens.Count)
            {
                var form = Read(tokens, ref pos);
                if (form != null) result.Add(form);
            }
            return result;
        }

        static Sexp Read(List<string> tokens, ref int pos)
        {
            if (pos >= tokens.Count) return null;
            string t = tokens[pos++];
            if (t == ")") return null;            // stray close paren: ignore
            if (t != "(") return Sexp.NewAtom(t);

            var list = Sexp.NewList();
            while (pos < tokens.Count)
            {
                if (tokens[pos] == ")") { pos++; return list; }
                var item = Read(tokens, ref pos);
                if (item == null) return list;
                list.Items.Add(item);
            }
            return list;                           // unterminated: tolerate
        }
    }

    // ------------------------------------------------------------------
    // Script model
    // ------------------------------------------------------------------

    class Reassembly
    {
        public enum Kind { Normal, NewKey, Link, Pre }

        public Kind What;
        public List<string> Tokens = new List<string>();  // Normal, and PRE's own rule
        public string Link;                               // Link and PRE target
    }

    class Transformation
    {
        public List<string> Decomposition = new List<string>();
        public List<Reassembly> Reassemblies = new List<Reassembly>();
        public int NextReassembly;
        public List<int> Shuffled;      // only for the random choice
    }

    class KeywordRule
    {
        public string Keyword = "";
        public string Substitute = "";
        public string LinkKeyword = "";
        public int Precedence;
        public List<string> Tags = new List<string>();
        public List<Transformation> Transformations = new List<Transformation>();

        public bool HasTransformation
        {
            get { return Transformations.Count > 0 || LinkKeyword.Length > 0; }
        }

        public string WordSubstitute(string word)
        {
            return Substitute.Length > 0 ? Substitute : word;
        }
    }

    class MemoryRule
    {
        public string Keyword = "";
        public List<Transformation> Transformations = new List<Transformation>();
        public readonly Queue<string> Memories = new Queue<string>();

        public bool IsEmpty { get { return Keyword.Length == 0 || Transformations.Count == 0; } }
    }

    class ElizaScript
    {
        public string Greeting = "HELLO";

        /*  More than one way to open, which the original does not have: it
            carries a single line, read once and printed. The engine does not
            choose between these -- the program around it counts the visits and
            says which. She remembers nothing between conversations; the room
            does. */
        public List<string> Greetings = new List<string>();
        public string Name = "";

        /*  Three more things a script may say about itself, all optional and
            none of them in Weizenbaum's format -- his file says nothing at
            all, and parsing it gives exactly his behaviour.

            About    one line for the card on the opening screen.
            Scene    which drawing the "in character" screen style puts her
                     in. The Hebrew script asks for the museum's room; a
                     script with a character of its own asks for its own.
            Family   which shelf of the opening screen it belongs on:
                     ELIZA herself, or one of the models built on her. */
        public string About = "";
        public string Scene = "";
        public string Family = "";

        public bool RightToLeft;
        public readonly Dictionary<string, KeywordRule> Rules =
            new Dictionary<string, KeywordRule>(StringComparer.Ordinal);
        public MemoryRule Memory = new MemoryRule();
        public readonly Dictionary<string, List<string>> Tags =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Weizenbaum's built-in messages, selected by the LIMIT counter.
        public List<string> NoMatchMessages = new List<string>
        {
            "PLEASE CONTINUE", "HMMM", "GO ON , PLEASE", "I SEE"
        };

        public List<string> Delimiters = new List<string> { ",", ".", "BUT" };

        /*  What to say when the user sends nothing at all.

            This is an extension, not Weizenbaum. His ELIZA had no notion of
            an empty line: you were at a teletype, and saying nothing simply
            said nothing. The 1966 script carries no SILENCE directive, so it
            keeps that behaviour exactly.

            The lines are used in order and the order is the point. Silence
            answered the same way twice is a machine; silence answered with a
            question, then a check, then patience, is someone waiting. The
            engine already cycles reassembly rules in order, so this needs no
            new machinery -- only somewhere to put the lines. */
        public List<string> Silence = new List<string>();

        /*  Words that give away that a woman is speaking.

            Hebrew marks the speaker's gender on the verb, so "I am sad" is
            עצוב from a man and עצובה from a woman, and a script written for
            one addresses the other wrongly from the first sentence. This list
            is generated from the same table that makes the feminine script,
            so the two can never fall out of step.

            Only ever read right after the word אני, which is what keeps
            "my wife is tired" from saying anything about who is typing. */
        public List<string> FeminineMarkers = new List<string>();

        // And the words that say a man is. A speaker may correct the program,
        // or a second person may sit down at the same keyboard, so the reading
        // has to be able to change its mind in both directions.
        public List<string> MasculineMarkers = new List<string>();

        // Held under a name no user input can produce, so typing "NONE" is
        // just an ordinary unknown word.
        public const string NoneKeyword = "(NONE)";

        // Problems found while reading the script. Weizenbaum's own loader
        // checked reassembly indexes too: a rule that points at a part its
        // decomposition never produces is a silent nonsense generator.
        public readonly List<string> Warnings = new List<string>();

        public static ElizaScript Parse(string text)
        {
            var script = new ElizaScript();
            var forms = SexpReader.ReadAll(text);
            bool greetingTaken = false;
            bool started = false;

            foreach (var form in forms)
            {
                if (form.IsAtom)
                {
                    // Weizenbaum's format puts the opening line, then START,
                    // then the rules.
                    if (form.Atom == "START") started = true;
                    continue;
                }
                string head = form.AtomAt(0);

                /*  Our own directives are read only in the window before the
                    opening line, because from there on every name belongs to
                    the script author: the 1966 DOCTOR script really does have
                    a rule for the keyword NAME, and START is optional. */
                bool directive = !started && !greetingTaken && script.Rules.Count == 0 &&
                    (head == "NAME" || head == "NOMATCH" || head == "SILENCE" ||
                     head == "DELIMITERS" || head == "DIRECTION" ||
                     head == "ABOUT" || head == "SCENE" || head == "FAMILY" ||
                     head == "FEMININE-MARKERS" || head == "MASCULINE-MARKERS" ||
                     head == "GREETINGS");

                if (!greetingTaken && !directive)
                {
                    // The first list in the file is the opening line, whatever
                    // its shape. The reference reads it positionally too.
                    script.Greeting = string.Join(" ", form.Items.Select(x => x.Flatten()));
                    greetingTaken = true;
                    continue;
                }

                if (form.Items.Count == 0) continue;

                if (!directive && head != "MEMORY" && head != "NONE")
                {
                    script.ParseKeyword(form, null);
                    continue;
                }

                switch (head)
                {
                    // Optional directives, all of them ours. The 1966 script has
                    // none, so parsing it gives exactly Weizenbaum's behaviour.
                    case "NAME":
                        script.Name = string.Join(" ", form.Items.Skip(1).Select(x => x.Flatten()));
                        continue;
                    case "ABOUT":
                        script.About = string.Join(" ", form.Items.Skip(1).Select(x => x.Flatten()));
                        continue;
                    case "SCENE":
                        script.Scene = form.AtomAt(1);
                        continue;
                    case "FAMILY":
                        script.Family = form.AtomAt(1);
                        continue;
                    case "DIRECTION":
                        script.RightToLeft = string.Equals(form.AtomAt(1), "RTL",
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    case "NOMATCH":
                        script.NoMatchMessages = form.Items.Skip(1)
                            .Where(x => x.IsList)
                            .Select(x => string.Join(" ", x.Items.Select(y => y.Flatten())))
                            .ToList();
                        while (script.NoMatchMessages.Count < 4 && script.NoMatchMessages.Count > 0)
                            script.NoMatchMessages.Add(script.NoMatchMessages[0]);
                        continue;
                    case "GREETINGS":
                        script.Greetings = form.Items.Skip(1)
                            .Where(x => x.IsList)
                            .Select(x => string.Join(" ", x.Items.Select(y => y.Flatten())))
                            .ToList();
                        continue;
                    case "SILENCE":
                        script.Silence = form.Items.Skip(1)
                            .Where(x => x.IsList)
                            .Select(x => string.Join(" ", x.Items.Select(y => y.Flatten())))
                            .ToList();
                        continue;
                    case "FEMININE-MARKERS":
                        script.FeminineMarkers = form.Items.Skip(1)
                            .Where(x => x.IsAtom).Select(x => x.Atom).ToList();
                        continue;
                    case "MASCULINE-MARKERS":
                        script.MasculineMarkers = form.Items.Skip(1)
                            .Where(x => x.IsAtom).Select(x => x.Atom).ToList();
                        continue;
                    case "DELIMITERS":
                        script.Delimiters = form.Items.Skip(1).Select(x => x.Flatten()).ToList();
                        continue;
                    case "MEMORY":
                        script.ParseMemory(form);
                        continue;
                    case "NONE":
                        script.ParseKeyword(form, NoneKeyword);
                        continue;
                    default:
                        script.ParseKeyword(form, null);
                        continue;
                }
            }

            script.CollectTags();
            script.Check();
            return script;
        }

        // ---- script checking --------------------------------------------

        void Check()
        {
            foreach (var rule in Rules.Values)
            {
                string name = rule.Keyword == NoneKeyword ? "NONE" : rule.Keyword;

                if (rule.LinkKeyword.Length > 0 && !Rules.ContainsKey(rule.LinkKeyword))
                    Warnings.Add(name + ": links to a keyword that does not exist, " +
                                 rule.LinkKeyword);

                foreach (var t in rule.Transformations)
                {
                    int parts = t.Decomposition.Count;
                    foreach (var r in t.Reassemblies)
                    {
                        if ((r.What == Reassembly.Kind.Link ||
                             r.What == Reassembly.Kind.Pre) &&
                            !Rules.ContainsKey(r.Link ?? ""))
                            Warnings.Add(name + ": links to a keyword that does not exist, " +
                                         (r.Link ?? "<nothing>"));

                        if (r.What != Reassembly.Kind.Normal &&
                            r.What != Reassembly.Kind.Pre) continue;

                        /*  The same word twice.

                            A pattern like (0 בא לך 0) puts לך in part three
                            and the rest in part four, so a reassembly that
                            reads "בא לך 3" prints לך and then prints it again.
                            It is an easy slip -- the part you want is the last
                            one, not the one before it -- and reading the reply
                            is the only other way to notice. */
                        for (int i = 0; i + 1 < r.Tokens.Count; i++)
                        {
                            int n = Index(r.Tokens[i + 1]);
                            if (n <= 0 || n > parts) continue;
                            if (t.Decomposition[n - 1] != r.Tokens[i]) continue;
                            Warnings.Add(name + ": (" + string.Join(" ", r.Tokens) +
                                         ") says \"" + r.Tokens[i] + "\" and then " +
                                         "part " + n + ", which is the same word again");
                        }

                        foreach (var token in r.Tokens)
                        {
                            /*  A part number may now carry punctuation -- "3?"
                                is the third piece followed by a question mark,
                                which is the only way a script can ask about
                                the words it is quoting. What is still wrong is
                                a digit followed by anything else. */
                            if (token.Length > 1 && char.IsDigit(token[0]) &&
                                Index(token) < 0)
                            {
                                Warnings.Add(name + ": the reassembly (" +
                                             string.Join(" ", r.Tokens) + ") has \"" +
                                             token + "\" stuck to something that is not " +
                                             "punctuation, so it is printed rather than " +
                                             "filled in");
                                continue;
                            }

                            int n = Index(token);
                            if (n > 0 && n <= parts) continue;
                            if (n < 0) continue;
                            Warnings.Add(name + ": (" + string.Join(" ", t.Decomposition) +
                                         ") has " + parts + " parts, but a reassembly " +
                                         "asks for part " + token);
                        }
                    }
                }
            }

            /*  A rule reached by a synonym must be able to answer for it.

                (אה 1 (=SOSO)) sends the word אה to SOSO, and if every pattern
                there names some other word, nothing matches and the reply
                falls out to a stock line. Weizenbaum's own linked rules all
                end in a bare (0) for this reason: DIT, DREAM, WHAT. */
            /*  A rule that substitutes before it jumps is fine: by the time
                the target sees the words, the word it names is there.
                (חלמת = חלמתי 4 (=חלמתי)) is safe; (שום (=כלום)) is not. */
            var jumps = new List<KeyValuePair<string, string>>();   // from, to
            foreach (var rule in Rules.Values)
            {
                if (rule.Substitute.Length > 0) continue;
                if (rule.LinkKeyword.Length > 0)
                    jumps.Add(new KeyValuePair<string, string>(
                        rule.Keyword, rule.LinkKeyword));
                foreach (var t in rule.Transformations)
                    foreach (var r in t.Reassemblies)
                        if ((r.What == Reassembly.Kind.Link ||
                             r.What == Reassembly.Kind.Pre) &&
                            !string.IsNullOrEmpty(r.Link))
                            jumps.Add(new KeyValuePair<string, string>(
                                rule.Keyword, r.Link));
            }

            foreach (var jump in jumps)
            {
                KeywordRule target;
                if (!Rules.TryGetValue(jump.Value, out target)) continue;
                if (target.LinkKeyword.Length > 0) continue;
                if (target.Transformations.Any(
                        t => t.Decomposition.Count == 1 && t.Decomposition[0] == "0"))
                    continue;

                /*  Or the target may name the word itself. Weizenbaum's
                    EVERYBODY jumps to EVERYONE, whose pattern is the list
                    (* EVERYONE EVERYBODY NOBODY NOONE) -- so the word does
                    match when it arrives, and nothing is wrong. */
                if (target.Transformations.Any(t => t.Decomposition.Any(
                        part => part == jump.Key || Names(part, jump.Key))))
                    continue;

                Warnings.Add(jump.Value + ": " + jump.Key + " jumps here without " +
                             "substituting, no pattern names it and none is a bare " +
                             "(0), so it falls out to a stock line");
            }

            foreach (var t in Memory.Transformations)
            {
                int parts = t.Decomposition.Count;
                foreach (var token in t.Reassemblies[0].Tokens)
                {
                    int n = Index(token);
                    if (n > 0 && n <= parts) continue;
                    if (n < 0) continue;
                    Warnings.Add("MEMORY: (" + string.Join(" ", t.Decomposition) +
                                 ") has " + parts + " parts, but a reassembly " +
                                 "asks for part " + token);
                }
            }
        }

        // Does this pattern element -- a (*WORD WORD) group -- name the word?
        static bool Names(string part, string word)
        {
            if (part.Length < 2 || part[0] != '(') return false;
            string body = part.Substring(1).TrimEnd(')');
            if (body.Length == 0 || body[0] != '*') return false;
            foreach (var member in body.Substring(1).Split(
                         new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries))
                if (member == word) return true;
            return false;
        }

        /*  The same reading the engine uses, so that what the checker warns
            about and what the engine does cannot drift apart. A number may
            carry punctuation after it now, and both have to agree that it
            still counts as a number. */
        static int Index(string token)
        {
            if (token.Length == 0) return -1;
            int i = 0;
            while (i < token.Length && token[i] >= '0' && token[i] <= '9') i++;
            if (i == 0) return -1;
            for (int k = i; k < token.Length; k++)
                if (!ElizaEngine.IsMarkChar(token[k])) return -1;
            int n;
            return int.TryParse(token.Substring(0, i), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out n) ? n : -1;
        }

        void ParseMemory(Sexp form)
        {
            Memory = new MemoryRule { Keyword = form.AtomAt(1) ?? "" };
            foreach (var item in form.Items.Skip(2))
            {
                if (!item.IsList) continue;
                int eq = item.Items.FindIndex(x => x.Atom == "=");
                if (eq < 0) continue;

                var t = new Transformation();
                t.Decomposition = item.Items.Take(eq).Select(FlattenPatternItem).ToList();

                /*  A tag or word list here works in this engine, but the
                    reference reads a MEMORY decomposition straight off the
                    token stream and never assembles brackets into one term, so
                    the same pattern would not match there. Keeping the useful
                    behaviour and saying so is better than reproducing the
                    quirk in silence. */
                if (item.Items.Take(eq).Any(x => x.IsList))
                    Warnings.Add("MEMORY: (" + string.Join(" ", t.Decomposition) +
                                 ") uses a bracketed group, which this engine matches " +
                                 "but the reference implementation does not");

                var r = new Reassembly { What = Reassembly.Kind.Normal };
                r.Tokens = item.Items.Skip(eq + 1).Select(x => x.Flatten()).ToList();
                t.Reassemblies.Add(r);
                Memory.Transformations.Add(t);
            }
        }

        void ParseKeyword(Sexp form, string forcedKeyword)
        {
            var rule = new KeywordRule();
            int i = 0;
            if (forcedKeyword != null) { rule.Keyword = forcedKeyword; i = 1; }
            else { rule.Keyword = form.AtomAt(0) ?? ""; i = 1; }
            if (rule.Keyword.Length == 0) return;

            /*  The substitution, the precedence and the DLIST may appear in any
                order and anywhere in the body: the reference reads the whole
                rule with one order-free loop, and its own grammar documents
                DLIST before the precedence as readily as after it. */
            for (; i < form.Items.Count; i++)
            {
                var item = form.Items[i];

                if (!item.IsList)
                {
                    if (item.Atom == "=")
                    {
                        if (i + 1 < form.Items.Count) rule.Substitute = form.AtomAt(++i) ?? "";
                    }
                    else if (item.Atom == "DLIST")
                    {
                        if (i + 1 < form.Items.Count && form.Items[i + 1].IsList)
                            rule.Tags = TagNames(form.Items[++i]);
                    }
                    else if (Index(item.Atom) >= 0)
                    {
                        rule.Precedence = Index(item.Atom);
                    }
                    continue;
                }

                // R4: a bare link, (=WHAT)
                if (item.Items.Count >= 2 && item.AtomAt(0) == "=")
                {
                    rule.LinkKeyword = item.AtomAt(1) ?? "";
                    continue;
                }

                // A transformation: ((decomposition) (reassembly) (reassembly) ...)
                if (item.Items.Count == 0 || !item.Items[0].IsList) continue;

                var t = new Transformation();
                t.Decomposition = item.Items[0].Items.Select(FlattenPatternItem).ToList();
                foreach (var r in item.Items.Skip(1))
                    if (r.IsList) t.Reassemblies.Add(ParseReassembly(r));
                if (t.Reassemblies.Count > 0) rule.Transformations.Add(t);
            }

            /*  Defining a keyword twice keeps the later one and drops the
                earlier one without a word, and the earlier one is usually the
                real rule: a generated one-line link at the foot of a file
                replaced a twelve-phrasing rule at the top of it, and nothing
                said so. A rule may carry a precedence, a tag and a link at
                once, so there is never a reason to write two. */
            if (Rules.ContainsKey(rule.Keyword))
                Warnings.Add("the keyword " + rule.Keyword + " is defined twice, " +
                             "and only the second one survives; put the precedence, " +
                             "the DLIST and the link on one rule instead");

            Rules[rule.Keyword] = rule;
        }

        // In a decomposition pattern a nested list stays one token: "(*SAD HAPPY)".
        static string FlattenPatternItem(Sexp item)
        {
            return item.Flatten();
        }

        static Reassembly ParseReassembly(Sexp r)
        {
            var result = new Reassembly();

            if (r.Items.Count == 1 && r.AtomAt(0) == "NEWKEY")
            {
                result.What = Reassembly.Kind.NewKey;
                return result;
            }

            if (r.Items.Count >= 2 && r.AtomAt(0) == "=")
            {
                result.What = Reassembly.Kind.Link;
                result.Link = r.AtomAt(1) ?? "";
                return result;
            }

            if (r.Items.Count >= 3 && r.AtomAt(0) == "PRE" &&
                r.Items[1].IsList && r.Items[2].IsList)
            {
                result.What = Reassembly.Kind.Pre;
                result.Tokens = r.Items[1].Items.Select(x => x.Flatten()).ToList();
                var link = r.Items[2];
                // Never null: a malformed (PRE (1) (=)) would otherwise reach
                // the rule lookup as a null key and throw mid-conversation.
                // Empty takes the "links to a keyword that does not exist" path.
                result.Link = (link.AtomAt(0) == "=" ? link.AtomAt(1) : link.AtomAt(0)) ?? "";
                return result;
            }

            result.What = Reassembly.Kind.Normal;
            result.Tokens = r.Items.Select(x => x.Flatten()).ToList();
            return result;
        }

        static List<string> TagNames(Sexp list)
        {
            // (/NOUN FAMILY) or (/ FAMILY)
            var names = new List<string>();
            foreach (var item in list.Items)
            {
                string s = item.Flatten().TrimStart('/');
                if (s.Length > 0) names.Add(s);
            }
            return names;
        }

        void CollectTags()
        {
            Tags.Clear();
            foreach (var rule in Rules.Values)
            {
                foreach (var tag in rule.Tags)
                {
                    List<string> words;
                    if (!Tags.TryGetValue(tag, out words))
                        Tags[tag] = words = new List<string>();
                    // The keyword itself, not its substitution: a substituted
                    // word carries its own rule, and that rule carries its tags.
                    if (!words.Contains(rule.Keyword)) words.Add(rule.Keyword);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // The IBM 7094 bits: BCD encoding and the SLIP mid-square hash
    // ------------------------------------------------------------------

    static class Slip
    {
        static readonly byte[] ToBcd = new byte[256];

        static Slip()
        {
            const string bcd =
                "0123456789\0=\'\0\0\0" +
                "+ABCDEFGHI\0.)\0\0\0" +
                "-JKLMNOPQR\0$*\0\0\0" +
                " /STUVWXYZ\0,(\0\0\0";
            for (int i = 0; i < 256; i++) ToBcd[i] = 0xFF;
            for (int c = 0; c < 64 && c < bcd.Length; c++)
                if (bcd[c] != '\0') ToBcd[bcd[c]] = (byte)c;
        }

        // The 36-bit BCD encoding of the final six-character chunk of a word.
        public static ulong LastChunkAsBcd(string s)
        {
            ulong result = 0;
            int count = 0;

            Action<char> append = c =>
            {
                result <<= 6;
                byte b = c < 256 ? ToBcd[c] : (byte)0xFF;
                // Characters outside the Hollerith set (Hebrew, for instance) were
                // never going to appear on a 7094; any stable encoding will do.
                result |= b != 0xFF ? b : (ulong)(c & 0x3F);
            };

            /*  A SLIP cell held six characters, so a long word is chunked six
                at a time and only the last chunk is hashed. The reference
                counts bytes, not characters, and that is what decides where
                the chunk boundary falls -- so a Hebrew word must be measured
                the same way or it selects a different memory. ASCII is
                identical either way, which is why the documented conversations
                cannot tell the difference. */
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            if (bytes.Length > 0)
                for (int i = ((bytes.Length - 1) / 6) * 6; i < bytes.Length; i++, count++)
                    append((char)bytes[i]);

            while (count++ < 6) append(' ');
            return result;
        }

        // SLIP's HASH: the middle n bits of the low 35 bits of d, squared.
        public static int Hash(ulong d, int n)
        {
            d &= 0x7FFFFFFFFUL;
            unchecked { d *= d; }
            d >>= 35 - n / 2;
            return (int)(d & ((1UL << n) - 1));
        }
    }

    // ------------------------------------------------------------------
    // The engine
    // ------------------------------------------------------------------

    class ElizaEngine
    {
        readonly ElizaScript script;
        /*  How she picks between the phrasings of a rule.

            Weizenbaum takes them strictly in turn, and that is what the
            documented conversations reproduce, so it stays the default.

            But the ELIZA most people have actually met is elizabot.js, which
            chooses at random -- and that is why she is remembered as livelier
            than she was. Going round a list in order is predictable by the
            fourth time; chance hides the machine. Offered here as a choice,
            never as the default, because a whole test suite rests on the
            other one.

            Not dice, though. Measured over the sixteen conversations in
            build/talks.he.txt, throwing dice each turn gave six answers she
            had already given, where taking them in turn gave none -- because
            going round in order is precisely what guarantees she uses every
            phrasing before she reuses one. So: a shuffled deck. The order is
            unpredictable, and she still says all of them before she says any
            of them twice. Random where random helps, ordered where order
            helps. The deck is reshuffled when it runs out, and never in a way
            that repeats the card just played. */
        public bool ChooseAtRandom;
        readonly Random dice = new Random(20260911);
        List<int> silenceDeck;

        /*  A fresh deck of 0..count-1 in random order. If the last card of the
            previous deck would come up first in this one, it is swapped away,
            so no phrasing can follow itself across the join. */
        List<int> Shuffle(int count, List<int> previous)
        {
            var deck = new List<int>();
            for (int i = 0; i < count; i++) deck.Add(i);
            for (int i = count - 1; i > 0; i--)
            {
                int j = dice.Next(i + 1);
                int swap = deck[i]; deck[i] = deck[j]; deck[j] = swap;
            }
            if (count > 1 && previous != null && deck[0] == previous[count - 1])
            {
                int j = 1 + dice.Next(count - 1);
                int swap = deck[0]; deck[0] = deck[j]; deck[j] = swap;
            }
            return deck;
        }

        /*  Where in each rotation to begin.

            Weizenbaum starts at the first phrasing, and inside one
            conversation that is exactly right: she uses every phrasing of a
            rule before she uses any of them twice.

            But a program restarts, and the rotation restarts with it. Say
            the same thing on Tuesday that you said on Monday and you get
            Monday's answer -- which is how the mechanism designed to prevent
            repetition becomes the thing that causes it. Weizenbaum never had
            to think about this. Nobody ran DOCTOR twenty times in an evening;
            you got your session and you got up and left.

            So the rotation is entered somewhere else each visit. Inside the
            conversation nothing at all changes: still round the list in
            order, still every phrasing before any repeat. Only the door she
            comes in by moves. */
        public void StartAt(int visit)
        {
            int n = 0;
            foreach (var rule in script.Rules.Values)
                foreach (var t in rule.Transformations)
                {
                    n++;
                    if (t.Reassemblies.Count > 1)
                        t.NextReassembly =
                            Math.Abs(visit * 7 + n * 3) % t.Reassemblies.Count;
                }
            if (script.Silence.Count > 1)
                silenceAt = Math.Abs(visit * 5) % script.Silence.Count;
        }

        int limit = 1;                 // Weizenbaum's "certain counting mechanism"
        int silenceAt;                 // where we are in the answers to silence
        string punctuation = "";

        public List<string> Trace = new List<string>();

        public ElizaEngine(ElizaScript script)
        {
            this.script = script;
            const string bcdPunctuation = "='+.)-$*/,(";
            var sb = new StringBuilder();
            foreach (var d in script.Delimiters)
                if (d.Length == 1 && bcdPunctuation.IndexOf(d[0]) >= 0) sb.Append(d[0]);
            punctuation = sb.ToString();
        }

        public ElizaScript Script { get { return script; } }
        public string Greeting { get { return script.Greeting; } }

        // Which opening to use. The caller counts; she does not.
        public string GreetingFor(int visit)
        {
            var all = script.Greetings;
            if (all.Count == 0) return script.Greeting;
            return all[((visit % all.Count) + all.Count) % all.Count];
        }

        // Nothing typed at all. The 1966 script has no answer for this and
        // says nothing, which is what an empty Silence list means.
        public bool AnswersSilence { get { return script.Silence.Count > 0; } }

        public string RespondToSilence()
        {
            if (script.Silence.Count == 0) return null;
            if (ChooseAtRandom && script.Silence.Count > 1)
            {
                int at = silenceAt % script.Silence.Count;
                if (at == 0) silenceDeck = Shuffle(script.Silence.Count, silenceDeck);
                silenceAt++;
                return script.Silence[silenceDeck[at]];
            }
            string line = script.Silence[silenceAt % script.Silence.Count];
            silenceAt++;
            Log("silence " + silenceAt);
            return line;
        }

        /*  Does this sentence give away that a woman is speaking?

            Only the word right after אני counts, or the one after that when
            what stands between is plainly an intensifier. "אני עצובה" says
            something about who is typing; "אני חושב שאשתי עצובה" does not,
            and a looser rule would read it wrongly. */
        static readonly string[] Intensifiers =
            { "מאוד", "קצת", "ממש", "לגמרי", "פשוט", "באמת", "כל-כך", "נורא", "לא" };

        // 1 a woman, -1 a man, 0 nothing either way.
        public int SpeakerSounds(string input)
        {
            if (script.FeminineMarkers.Count == 0 &&
                script.MasculineMarkers.Count == 0) return 0;

            var words = SplitInput(input);
            for (int i = 0; i < words.Count - 1; i++)
            {
                if (words[i] != "אני") continue;

                string next = words[i + 1];
                string after = i + 2 < words.Count ? words[i + 2] : null;
                bool gap = Array.IndexOf(Intensifiers, next) >= 0;

                if (script.FeminineMarkers.Contains(next)) return 1;
                if (script.MasculineMarkers.Contains(next)) return -1;
                if (gap && after != null)
                {
                    if (script.FeminineMarkers.Contains(after)) return 1;
                    if (script.MasculineMarkers.Contains(after)) return -1;
                }
            }
            return 0;
        }

        /*  Take over from an engine that was mid-conversation.

            Hebrew marks gender on the verb, so noticing that a woman is
            speaking means handing the rest of the conversation to the script
            written for her -- a different script, on the same engine, which is
            the whole point of the script being data.

            But an engine holds more than its script. It holds Weizenbaum's
            counting mechanism, which is what decides when a memory surfaces,
            and it holds the memories themselves. Building a new one and
            throwing those away restarts the conversation invisibly. What does
            not come across is the rotation: the new script's rules are not the
            old script's rules, and an index into one means nothing in the
            other. */
        public void CarryOn(ElizaEngine other)
        {
            if (other == null) return;
            limit = other.limit;
            script.Memory.Memories.Clear();
            foreach (var held in other.script.Memory.Memories)
                script.Memory.Memories.Enqueue(held);
        }

        public void Reset()
        {
            limit = 1;
            silenceAt = 0;
            script.Memory.Memories.Clear();
            foreach (var rule in script.Rules.Values)
                foreach (var t in rule.Transformations)
                {
                    t.NextReassembly = 0;
                    t.Shuffled = null;
                }
        }

        bool IsDelimiter(string s)
        {
            return script.Delimiters.Contains(s);
        }

        /*  ELIZA was written for the BCD character set, so the question of what
            to do with a question mark never came up. People type them, though,
            and COMPUTER? is not the keyword COMPUTER. The reference
            implementation settles this by folding the awkward characters into
            the three ELIZA does understand, and we follow it exactly. */
        /*  forInput folds the marks ELIZA cannot read into the three it can,
            which is what the reference does and what keeps COMPUTER? from
            being a different word to COMPUTER.

            It is wrong for the script itself. Weizenbaum's own script has no
            question mark in it -- an uppercase teletype in 1966 had little use
            for one -- so folding the script cost nothing in English and was
            never noticed. In Hebrew it costs everything: every question she
            asks came out as a flat statement, and "וזו הסיבה היחידה" without
            its mark is not a question at all, it is a strange thing to assert.

            So the script keeps its punctuation and the input is folded exactly
            as before. The two are different texts and always were. */
        public static string ElizaUppercase(string text)
        {
            return ElizaUppercase(text, true);
        }

        public static string ElizaUppercase(string text, bool forInput)
        {
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '’': sb.Append('\''); break;   // right single quote

                    case '‘': case '`': case '"':
                    case '«': case '»': case '‚': case '‛':
                    case '“': case '”': case '„': case '‟':
                    case '‹': case '›':
                    case '¡': case '¿':
                        sb.Append(' '); break;

                    case '!': case '?':
                        sb.Append(forInput ? '.' : c); break;

                    case ':': case ';': case '–': case '—':
                        sb.Append(','); break;

                    case 'ß': sb.Append("SS"); break;
                    case 'ﬀ': sb.Append("FF"); break;
                    case 'ﬁ': sb.Append("FI"); break;
                    case 'ﬂ': sb.Append("FL"); break;
                    case 'ﬃ': sb.Append("FFI"); break;
                    case 'ﬄ': sb.Append("FFL"); break;
                    case 'ﬅ': case 'ﬆ': sb.Append("ST"); break;

                    default:
                        // Not char.ToUpperInvariant: .NET's table is not this
                        // one. Greek final sigma is the case that would bite --
                        // .NET leaves ς alone, this folds it to Σ, and a Greek
                        // keyword ending in sigma would otherwise never match.
                        if (char.IsHighSurrogate(c) && i + 1 < text.Length &&
                            char.IsLowSurrogate(text[i + 1]))
                        {
                            uint pair = (uint)char.ConvertToUtf32(c, text[i + 1]);
                            sb.Append(char.ConvertFromUtf32((int)CaseTable.Upper(pair)));
                            i++;
                        }
                        else sb.Append((char)CaseTable.Upper(c));
                        break;
                }
            }
            return sb.ToString();
        }

        List<string> SplitInput(string input)
        {
            // A word is any run of characters that is neither a space nor one
            // of the delimiters; each delimiter becomes a word of its own.
            var words = new List<string>();
            var cur = new StringBuilder();
            foreach (char c in ElizaUppercase(input))
            {
                if (c == ' ' || punctuation.IndexOf(c) >= 0)
                {
                    if (cur.Length > 0) { words.Add(cur.ToString()); cur.Length = 0; }
                    if (c != ' ') words.Add(c.ToString());
                }
                else cur.Append(c);
            }
            if (cur.Length > 0) words.Add(cur.ToString());
            return words;
        }

        // ---- the core algorithm ----------------------------------------

        public string Respond(string input)
        {
            Trace.Clear();
            var words = SplitInput(input);

            limit = limit % 4 + 1;
            Log("LIMIT = " + limit);

            // Scan for keywords, build the keystack, apply word substitutions.
            var keystack = new List<string>();
            int topRank = 0;

            for (int w = 0; w < words.Count; )
            {
                if (IsDelimiter(words[w]))
                {
                    if (keystack.Count == 0)
                    {
                        // No keyword yet: throw away this clause and keep scanning.
                        Log("discarded clause: " + string.Join(" ", words.Take(w + 1)));
                        words.RemoveRange(0, w + 1);
                        w = 0;
                        continue;
                    }
                    // A keyword was found: everything after the delimiter goes.
                    words.RemoveRange(w, words.Count - w);
                    break;
                }

                KeywordRule rule;
                if (script.Rules.TryGetValue(words[w], out rule))
                {
                    if (rule.HasTransformation)
                    {
                        if (rule.Precedence > topRank)
                        {
                            keystack.Insert(0, words[w]);
                            topRank = rule.Precedence;
                        }
                        else keystack.Add(words[w]);
                    }
                    string sub = rule.WordSubstitute(words[w]);
                    if (sub != words[w]) Log("substitute: " + words[w] + " -> " + sub);
                    words[w] = sub;
                }
                w++;
            }

            Log("keystack: " + (keystack.Count == 0 ? "<empty>" : string.Join(" ", keystack)));

            if (keystack.Count == 0)
            {
                // No keyword at all. Weizenbaum's code recalls a memory only when
                // the LIMIT counter happens to be 4.
                if (limit == 4 && script.Memory.Memories.Count > 0)
                {
                    Log("recalling a memory");
                    return script.Memory.Memories.Dequeue();
                }
            }

            while (keystack.Count > 0)
            {
                string top = keystack[0];
                keystack.RemoveAt(0);

                KeywordRule rule;
                if (!script.Rules.TryGetValue(top, out rule))
                {
                    Log("rule links to unknown keyword " + top);
                    return NoMatch();
                }

                CreateMemory(top, words);

                string link;
                var action = Apply(rule, ref words, out link);
                Log("rule (" + Describe(top) + ") -> " + action);

                if (action == Action.Complete) return string.Join(" ", words);

                if (action == Action.Inapplicable)
                {
                    // No decomposition rule matched: a fault in the script.
                    return NoMatch();
                }

                if (action == Action.LinkKey)
                {
                    keystack.Insert(0, link);
                    continue;
                }

                // NEWKEY asks for the next keyword down, but there is none.
                // The CACM paper says a NONE message is used here, and the
                // recovered code agrees, so we fall out of the loop.
                if (keystack.Count == 0)
                {
                    Log("NEWKEY with an empty keystack");
                    break;
                }
            }

            // Last resort: the NONE rule, which never fails.
            KeywordRule none;
            if (script.Rules.TryGetValue(ElizaScript.NoneKeyword, out none))
            {
                string discard;
                Apply(none, ref words, out discard);
                Log("using NONE");
                return string.Join(" ", words);
            }
            return NoMatch();
        }

        string Describe(string keyword)
        {
            return keyword == ElizaScript.NoneKeyword ? "NONE" : keyword;
        }

        string NoMatch()
        {
            var msgs = script.NoMatchMessages;
            if (msgs.Count == 0) return "HMMM";
            string m = msgs[(limit - 1) % msgs.Count];
            Log("built-in message #" + limit);
            return m;
        }

        void Log(string s) { Trace.Add(s); }

        enum Action { Complete, Inapplicable, LinkKey, NewKey }

        Action Apply(KeywordRule rule, ref List<string> words, out string linkKeyword)
        {
            linkKeyword = "";
            List<string> components = null;
            Transformation matched = null;

            foreach (var t in rule.Transformations)
            {
                if (Match(t.Decomposition, words, out components)) { matched = t; break; }
            }

            if (matched == null)
            {
                if (rule.LinkKeyword.Length > 0)
                {
                    linkKeyword = rule.LinkKeyword;
                    return Action.LinkKey;
                }
                return Action.Inapplicable;
            }

            Log("decomposition (" + string.Join(" ", matched.Decomposition) + ")");
            Log("parts: " + string.Join(" | ", components));

            int pick;
            if (ChooseAtRandom && matched.Reassemblies.Count > 1)
            {
                if (matched.Shuffled == null || matched.NextReassembly == 0)
                    matched.Shuffled = Shuffle(matched.Reassemblies.Count,
                                               matched.Shuffled);
                pick = matched.Shuffled[matched.NextReassembly];
                matched.NextReassembly =
                    (matched.NextReassembly + 1) % matched.Reassemblies.Count;
            }
            else
            {
                pick = matched.NextReassembly;
                matched.NextReassembly = (pick + 1) % matched.Reassemblies.Count;
            }
            var reassembly = matched.Reassemblies[pick];

            switch (reassembly.What)
            {
                case Reassembly.Kind.NewKey:
                    return Action.NewKey;

                case Reassembly.Kind.Link:
                    linkKeyword = reassembly.Link;
                    return Action.LinkKey;

                case Reassembly.Kind.Pre:
                    words = Reassemble(reassembly.Tokens, components);
                    linkKeyword = reassembly.Link;
                    return Action.LinkKey;

                default:
                    Log("reassembly (" + string.Join(" ", reassembly.Tokens) + ")");
                    words = Reassemble(reassembly.Tokens, components);
                    return Action.Complete;
            }
        }

        void CreateMemory(string keyword, List<string> words)
        {
            var mem = script.Memory;
            if (mem.IsEmpty || keyword != mem.Keyword || words.Count == 0) return;

            // Weizenbaum wrote that the rule is chosen at random; the code shows it
            // is chosen by hashing the last word of the input.
            int index = Slip.Hash(Slip.LastChunkAsBcd(words[words.Count - 1]), 2);
            if (index >= mem.Transformations.Count) index %= mem.Transformations.Count;
            var t = mem.Transformations[index];

            List<string> components;
            if (!Match(t.Decomposition, words, out components)) return;

            string memory = string.Join(" ", Reassemble(t.Reassemblies[0].Tokens, components));
            mem.Memories.Enqueue(memory);
            Log("stored a memory: " + memory);
        }

        // ---- decomposition matching -------------------------------------

        static int ToInt(string s)
        {
            string ignored;
            return ToInt(s, out ignored);
        }

        /*  The number of a part, and anything that was stuck to the end of it.

            "3" is the third piece of what the speaker said. "3?" is the same
            piece with a question mark after it, which is the only way a script
            can ask a question about the words it is quoting -- and until now it
            was not a number at all, so the token was printed literally and the
            speaker's own words were dropped on the floor.

            Only punctuation may follow. A letter after a digit is not a part
            and never was. */
        static int ToInt(string s, out string tail)
        {
            tail = "";
            if (s.Length == 0) return -1;

            int i = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            if (i == 0) return -1;

            for (int k = i; k < s.Length; k++)
                if (!IsMark(s[k])) return -1;

            tail = s.Substring(i);
            int n;
            return int.TryParse(s.Substring(0, i), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out n) ? n : -1;
        }

        public static bool IsMarkChar(char c) { return IsMark(c); }

        static bool IsMark(char c)
        {
            return c == '.' || c == ',' || c == '?' || c == '!' ||
                   c == ':' || c == ';' || c == '…' || c == '-';
        }

        class Wildcard
        {
            public int PatternIndex, WordsIndex, Length;
        }

        bool Match(List<string> pattern, List<string> words, out List<string> components)
        {
            components = new List<string>();
            var wildcards = new List<Wildcard>();

            int p = 0, w = 0;
            for (; ; )
            {
                bool backtrack = true;

                if (p < pattern.Count)
                {
                    int n = ToInt(pattern[p]);
                    if (n == 0)
                    {
                        // A wildcard of any length; start by assuming it eats nothing.
                        wildcards.Add(new Wildcard { PatternIndex = p, WordsIndex = w, Length = 0 });
                        p++;
                        continue;
                    }
                    if (w < words.Count)
                    {
                        if (n > 0)
                        {
                            p++;
                            w += n;
                            if (w <= words.Count) continue;
                            w -= n;
                            p--;
                        }
                        else if (pattern[p].Length > 0 && pattern[p][0] == '(')
                        {
                            if (InList(words[w], pattern[p])) { p++; w++; continue; }
                        }
                        else if (pattern[p] == words[w]) { p++; w++; continue; }
                    }
                }
                else if (w == words.Count) break;    // pattern and words both consumed

                if (backtrack)
                {
                    for (; ; )
                    {
                        if (wildcards.Count == 0) return false;
                        var last = wildcards[wildcards.Count - 1];
                        last.Length++;
                        if (last.WordsIndex + last.Length <= words.Count) break;
                        wildcards.RemoveAt(wildcards.Count - 1);
                    }
                    var top = wildcards[wildcards.Count - 1];
                    p = top.PatternIndex + 1;
                    w = top.WordsIndex + top.Length;
                }
            }

            // Every wildcard length is now known; slice the input into parts.
            int nextWildcard = 0;
            for (int pi = 0, wi = 0; pi < pattern.Count; pi++)
            {
                int partLength = 1;
                int n = ToInt(pattern[pi]);
                if (n == 0) partLength = wildcards[nextWildcard++].Length;
                else if (n > 1) partLength = n;

                var part = new List<string>();
                for (int i = 0; i < partLength && wi < words.Count; i++) part.Add(words[wi++]);
                components.Add(string.Join(" ", part));
            }
            return true;
        }

        bool InList(string word, string list)
        {
            string s = list;
            if (s.EndsWith(")")) s = s.Substring(0, s.Length - 1);
            if (s.StartsWith("(")) s = s.Substring(1);
            s = s.TrimStart();
            if (s.Length == 0) return false;

            char kind = s[0];
            var members = s.Substring(1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (kind == '*')                      // (*SAD UNHAPPY DEPRESSED)
                return members.Contains(word, StringComparer.Ordinal);

            if (kind == '/')                      // (/NOUN FAMILY)
            {
                foreach (var tag in members)
                {
                    List<string> tagged;
                    if (script.Tags.TryGetValue(tag, out tagged) && tagged.Contains(word))
                        return true;
                }
                return false;
            }
            return false;
        }

        static List<string> Reassemble(List<string> rule, List<string> components)
        {
            var result = new List<string>();
            foreach (var r in rule)
            {
                string tail;
                int n = ToInt(r, out tail);
                if (n < 0) { result.Add(r); continue; }

                if (n == 0 || n > components.Count) result.Add("HMMM");
                else result.AddRange(components[n - 1]
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                // Whatever was stuck to the number goes on the end of what the
                // number stood for, with no space between them.
                if (tail.Length == 0) continue;
                if (result.Count > 0) result[result.Count - 1] += tail;
                else result.Add(tail);
            }
            return result;
        }
    }
}
