// Test.cs -- proof that the engine behaves like the original.
//
// The conversations in Conversations.cs are lifted from the reference
// implementation's test suite: the exchange Weizenbaum printed in the 1966
// CACM paper, a second version found in his MIT archive, the one in his 1965
// paper, and a run that exercises every single rule in the DOCTOR script.
//
// Anything less than every character matching is a difference from the
// original, so the runner prints the first mismatch and fails.
//
//   ElizaTest              run every check against the 1966 script
//   ElizaTest --hebrew     drive the Hebrew script and report rule coverage

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ElizaApp
{
    static class Test
    {
        static int failures;
        static int checks;

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length > 0 && args[0] == "--hebrew")
                return HebrewReport();

            // Talk to any script from the command line, one line in, one out.
            /*  Read a script and say what is in it and what is wrong with
                it. Adding a script to this program means writing a thousand
                lines of s-expressions by hand, and the two faults that cost
                the most are both invisible in the file: a reassembly with no
                full stop at the end of it, and a keyword at a high precedence
                whose only decomposition is a bare wildcard -- which takes
                every sentence the word appears in and answers all of them the
                same way. Both were found in the shipped Hebrew script by
                reading it, one sentence at a time, which is not a method. */
            if (args.Length > 1 && args[0] == "--check")
            {
                var read = ElizaScript.Parse(
                    File.ReadAllText(FindScript(args[1]), Encoding.UTF8));
                int patterns = 0, answers = 0, unpunctuated = 0, greedy = 0;
                int quoting = 0;
                foreach (var rule in read.Rules.Values)
                {
                    /*  Once per rule, not once per answer: a keyword at a
                        high precedence whose only pattern is a bare wildcard
                        answers every sentence the word appears in. Sometimes
                        that is meant -- COMPUTER is like that in Weizenbaum's
                        own script -- so this is something to look at rather
                        than something that is wrong. */
                    if (rule.Precedence >= 10 && rule.Transformations.Count == 1 &&
                        rule.Transformations[0].Decomposition.Count == 1 &&
                        rule.Transformations[0].Decomposition[0] == "0" &&
                        rule.Transformations[0].Reassemblies.Count > 0 &&
                        rule.Transformations[0].Reassemblies[0].What == Reassembly.Kind.Normal)
                    {
                        greedy++;
                        Console.WriteLine("CATCHES ALL  (" + rule.Keyword + " " +
                            rule.Precedence + ") answers every sentence the word " +
                            "appears in, wherever it appears");
                    }

                    foreach (var t in rule.Transformations)
                    {
                        patterns++;
                        foreach (var r in t.Reassemblies)
                        {
                            if (r.What != Reassembly.Kind.Normal) continue;
                            answers++;
                            /*  Does this answer put the speaker's own words
                                back in front of them? That is the move the
                                1966 script is built on, and the one thing a
                                script can have plenty of rules and still not
                                do. It is harder in Hebrew -- a bare wildcard
                                before a quoted part swallows the clause --
                                so it is the first thing a writer quietly
                                gives up on, and the number says so. */
                            foreach (var token in r.Tokens)
                            {
                                int part;
                                if (int.TryParse(token.TrimEnd(
                                        '.', ',', '?', '!', ':', ';', '-'),
                                        out part) && part > 0)
                                { quoting++; break; }
                            }
                            string last = r.Tokens.Count == 0 ? "" :
                                r.Tokens[r.Tokens.Count - 1];
                            if (last.Length > 0 && ".?!…".IndexOf(last[last.Length - 1]) < 0)
                            {
                                unpunctuated++;
                                Console.WriteLine("NO STOP  (" + rule.Keyword + ") " +
                                    string.Join(" ", r.Tokens));
                            }
                        }
                    }
                }
                Console.WriteLine();
                Console.WriteLine("name        " + read.Name);
                Console.WriteLine("scene       " + read.Scene + "   family " + read.Family);
                Console.WriteLine("keywords    " + read.Rules.Count);
                Console.WriteLine("patterns    " + patterns);
                Console.WriteLine("answers     " + answers);
                Console.WriteLine("quoting     " + quoting + "   (" +
                    (answers == 0 ? 0 : quoting * 100 / answers) +
                    "% of the answers hand the sentence back)");
                foreach (var w in read.Warnings) Console.WriteLine("WARNING  " + w);
                int bad = read.Warnings.Count + unpunctuated + greedy;
                Console.WriteLine(bad == 0 ? "nothing to report"
                    : bad + " things to look at");
                return bad == 0 ? 0 : 1;
            }

            if (args.Length > 1 && args[0] == "--talk")
            {
                var talker = new ElizaEngine(ElizaScript.Parse(
                    File.ReadAllText(FindScript(args[1]), Encoding.UTF8)));
                Console.WriteLine(talker.Greeting);
                // Read the raw stream as UTF-8. Console.ReadLine would decode
                // through the console code page and Hebrew would arrive ruined.
                var stdin = new StreamReader(Console.OpenStandardInput(),
                                             new UTF8Encoding(false));
                string line;
                while ((line = stdin.ReadLine()) != null)
                {
                    Console.WriteLine("> " + line);
                    Console.WriteLine(talker.Respond(line));
                }
                return 0;
            }

            /*  Run a file of whole conversations, each from a fresh start.
                A script can look right for two turns and fall apart on the
                fifth, when the rotation has moved on and the memory is due,
                and nothing but reading whole conversations will show it. */
            if (args.Length > 1 && args[0] == "--talks")
            {
                // --random: the way the web versions choose, to compare against.
                bool atRandom = args.Contains("--random");
                // --visit N: enter the rotation where visit N would.
                int visit = 0;
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "--visit") int.TryParse(args[i + 1], out visit);
                string scriptText = File.ReadAllText(FindScript(args[1]), Encoding.UTF8);
                string corpus = args.Length > 2 ? args[2] : "build/talks.he.txt";
                var all = File.ReadAllLines(corpus, Encoding.UTF8);

                ElizaEngine talker = null;
                var said = new List<string>();
                int turns = 0, repeats = 0, stock = 0;
                Action close = () =>
                {
                    if (turns == 0) return;
                    Console.WriteLine("  -- " + turns + " תורות, " + repeats +
                                      " חזרות, " + stock + " תשובות מדף");
                };

                foreach (var raw in all)
                {
                    string line = raw.Trim();
                    if (line.StartsWith("==="))
                    {
                        close();
                        Console.WriteLine();
                        Console.WriteLine(line);
                        talker = new ElizaEngine(ElizaScript.Parse(scriptText));
                        talker.ChooseAtRandom = atRandom;
                        if (visit > 0) talker.StartAt(visit);
                        said.Clear();
                        turns = repeats = stock = 0;
                        Console.WriteLine("  " + talker.Greeting);
                        continue;
                    }
                    if (line.Length == 0 || line[0] == '#' || talker == null) continue;

                    string reply = talker.Respond(line);
                    Console.WriteLine("> " + line);
                    Console.WriteLine("  " + reply);

                    turns++;
                    if (said.Contains(reply)) repeats++;
                    said.Add(reply);
                    // A stock answer is one she would have given to anything:
                    // it quotes nothing the speaker said.
                    if (talker.Trace.Contains("using NONE") ||
                        talker.Trace.Any(t => t.StartsWith("built-in message"))) stock++;
                }
                close();
                return 0;
            }

            if (args.Length > 1 && args[0] == "--rules")
            {
                var dumped = ElizaScript.Parse(
                    File.ReadAllText(FindScript(args[1]), Encoding.UTF8));
                foreach (var k in dumped.Rules.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    Console.WriteLine(k);
                return 0;
            }

            string path = FindScript("doctor.txt");
            string text = File.ReadAllText(path, Encoding.UTF8);
            var script = ElizaScript.Parse(text);

            Section("script structure");
            Equal("rule count", script.Rules.Count, 67);
            Equal("tag count", script.Tags.Count, 3);
            Equal("tag BELIEF", Tag(script, "BELIEF"), "BELIEVE FEEL THINK WISH");
            Equal("tag FAMILY", Tag(script, "FAMILY"),
                  "BROTHER CHILDREN DAD FATHER MOM MOTHER SISTER WIFE");
            Equal("tag NOUN", Tag(script, "NOUN"), "FATHER MOTHER");
            Equal("memory keyword", script.Memory.Keyword, "MY");
            Equal("memory rules", script.Memory.Transformations.Count, 4);
            Equal("greeting", script.Greeting,
                  "HOW DO YOU DO. PLEASE TELL ME YOUR PROBLEM");
            Equal("script warnings", string.Join("; ", script.Warnings), "");

            Section("the IBM 7094 arithmetic");
            Equal("bcd of nothing", Slip.LastChunkAsBcd(""), Oct("606060606060"));
            Equal("bcd of X", Slip.LastChunkAsBcd("X"), Oct("676060606060"));
            Equal("bcd of HERE", Slip.LastChunkAsBcd("HERE"), Oct("302551256060"));
            Equal("bcd of ALWAYS", Slip.LastChunkAsBcd("ALWAYS"), Oct("214366217062"));
            Equal("bcd of INVENTED", Slip.LastChunkAsBcd("INVENTED"), Oct("252460606060"));
            Equal("hash ALWAYS", Slip.Hash(Oct("214366217062"), 7), 14);
            Equal("hash HERE", Slip.Hash(Oct("302551256060"), 2), 3);
            Equal("hash 423124626060", Slip.Hash(Oct("423124626060"), 2), 1);
            Equal("hash 633144256060", Slip.Hash(Oct("633144256060"), 2), 0);

            // The reference runs these two on one continuous session, so the
            // reassembly rotation and the LIMIT counter carry over between them.
            Section("1966 CACM conversation, and an imagined continuation");
            var engine = new ElizaEngine(ElizaScript.Parse(text));
            Replay(engine, Conversations.Cacm1966, "cacm");
            Replay(engine, Conversations.HayContinued, "continued");

            Section("straying from the psychiatric context");
            Replay(new ElizaEngine(ElizaScript.Parse(text)), Conversations.Turingish, "turing");

            Section("the typescript from Box 8 of the MIT archive");
            Replay(new ElizaEngine(ElizaScript.Parse(text)), Conversations.AltMenAlike, "box8");

            Section("the conversation in the August 1965 paper");
            Replay(new ElizaEngine(ElizaScript.Parse(text)), Conversations.VeryUnhappy, "1965");

            /*  The only independent record of what ELIZA actually said.

                In 1973 David Avidan sat at an IBM terminal in Tel Aviv, in
                front of DOCTOR on punched cards shipped from the States, and
                held eight conversations with her over some fifteen hours. He
                published them the next year as "My Electronic Psychiatrist",
                translating his own English into Hebrew. The book is not
                online and no English edition was ever printed, but Israeli
                journalism and one Van Leer essay quote fragments, and those
                fragments are every line of ELIZA's that survives outside
                Weizenbaum's own papers.

                They are worth more than any test I could invent, because
                nobody was trying to demonstrate the program when they were
                recorded. So: the surviving quotes, translated back, against
                this engine. All nine come out verbatim.

                One of them settles an old argument. Zvi Yanai, who arranged
                the machine, said in 2001 that he doubted the transcripts,
                because he could not believe ELIZA would answer a question
                about having relations with "WHAT ARE YOUR FEELINGS NOW". It
                is the fourth reassembly of that rule, and it comes up here
                exactly where a rotation puts it. Avidan did not invent it.

                That rotation is itself the finding. Three separately
                published fragments show these four phrasings in the order
                the script lists them -- which is only possible if she took
                them in turn. The ELIZA that a poet found worth fifteen hours
                was this one, going round in order, and that is why order is
                what this program does unless someone asks for otherwise. */
            Section("the fragments David Avidan published in 1974");
            var avidan = new ElizaEngine(ElizaScript.Parse(text));
            Replay(avidan, new string[,] {
                { "Hello.",
                  "HOW DO YOU DO. PLEASE STATE YOUR PROBLEM" },
                { "Won't you introduce yourself?",
                  "WE WERE DISCUSSING YOU - NOT ME" },
                { "I talk about you",
                  "PERHAPS IN YOUR FANTASY WE TALK ABOUT EACH OTHER" },
                { "Won't you introduce yourself?",
                  "OH, I INTRODUCE MYSELF" },
                { "Won't you introduce yourself?",
                  "YOU'RE NOT REALLY TALKING ABOUT ME - ARE YOU" },
                { "Won't you introduce yourself?",
                  "WHAT ARE YOUR FEELINGS NOW" },
                { "I don't have enough money",
                  "DON'T YOU REALLY HAVE ENOUGH MONEY" },
                { "I don't have enough money",
                  "WHY DON'T YOU HAVE ENOUGH MONEY" },
                { "My work is basically non-commercial",
                  "YOUR WORK IS BASICALLY NON-COMMERCIAL" },
                { "My work is basically non-commercial",
                  "WHY DO YOU SAY YOUR WORK IS BASICALLY NON-COMMERCIAL" },
                { "I am interested in painting",
                  "IS IT BECAUSE YOU ARE INTERESTED IN PAINTING THAT YOU CAME TO ME" },
                { "I am interested in painting",
                  "HOW LONG HAVE YOU BEEN INTERESTED IN PAINTING" },
                { "I am interested in painting",
                  "DO YOU BELIEVE IT NORMAL TO BE INTERESTED IN PAINTING" },
                { "I am interested in painting",
                  "DO YOU ENJOY BEING INTERESTED IN PAINTING" },
            }, "avidan");

            Section("every rule in the script");
            Replay(new ElizaEngine(ElizaScript.Parse(text)), Conversations.EveryRule, "every");

            /*  These lock in behaviour that no DOCTOR conversation can reach,
                because the script is data and someone will write another one.
                Each of them was a divergence from the reference until it was
                found by reading the two implementations side by side. */
            Section("behaviour the DOCTOR script never exercises");

            // 'LATIN CAPITAL LETTER C WITH CEDILLA' as UTF-8, from the
            // reference's own test: the chunking counts bytes, not characters.
            Equal("bcd of a non-ASCII word", Slip.LastChunkAsBcd("Ç"),
                  Oct("030760606060"));

            // .NET leaves Greek final sigma alone; the reference's table folds it.
            Equal("greek final sigma", ElizaEngine.ElizaUppercase("ς"), "Σ");
            Equal("dotless i", ElizaEngine.ElizaUppercase("ı"), "I");
            Equal("a question mark becomes a full stop",
                  ElizaEngine.ElizaUppercase("why?"), "WHY.");

            // A script written in lower case must work: the tokenizer folds it.
            var lower = ElizaScript.Parse(
                "(hello there)\nstart\n(sorry ((0) (please do not apologise)))\n" +
                "(none ((0) (go on)))\n");
            Equal("lowercase greeting", lower.Greeting, "HELLO THERE");
            Equal("lowercase keyword found", lower.Rules.ContainsKey("SORRY"), true);
            Equal("lowercase reply", new ElizaEngine(lower).Respond("Sorry about that"),
                  "PLEASE DO NOT APOLOGISE");

            // The opening line is whatever list comes first, whatever its shape.
            // Not the word GREETINGS: that is a directive's name now.
            Equal("a one-word greeting", ElizaScript.Parse(
                      "(WELCOME) START (NONE ((0) (GO ON)))").Greeting, "WELCOME");

            // The substitution, precedence and DLIST may come in any order.
            var anyOrder = ElizaScript.Parse(
                "(HI)\nSTART\n(MOM DLIST(/FAMILY) 7 = MOTHER)\n(NONE ((0) (GO ON)))\n");
            Equal("precedence after DLIST", anyOrder.Rules["MOM"].Precedence, 7);
            Equal("substitution last", anyOrder.Rules["MOM"].Substitute, "MOTHER");
            Equal("tag still collected", Tag(anyOrder, "FAMILY"), "MOM");

            // A script author's typo must not become an unhandled exception.
            var broken = ElizaScript.Parse(
                "(HI)\nSTART\n(YOU ((0 YOU 0) (PRE (1) (=))))\n(NONE ((0) (GO ON)))\n");
            checks++;
            try
            {
                new ElizaEngine(broken).Respond("you are here");
                Console.WriteLine("   ok   a malformed PRE does not throw");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("   FAIL a malformed PRE threw " + ex.GetType().Name);
            }

            /*  Saying nothing. The 1966 script has no answer for it and must
                stay quiet; a script that carries answers uses them in order,
                because an escalation is only an escalation if it is a sequence. */
            Section("saying nothing");
            var quiet = new ElizaEngine(ElizaScript.Parse(text));
            Equal("the 1966 script has nothing to say to silence",
                  quiet.AnswersSilence, false);
            Equal("and says nothing", quiet.RespondToSilence(), null);

            var speaks = new ElizaEngine(ElizaScript.Parse(
                "(SILENCE (FIRST) (SECOND) (THIRD))\n(HI)\nSTART\n" +
                "(NONE ((0) (GO ON)))\n"));
            Equal("a script may answer silence", speaks.AnswersSilence, true);
            Equal("in order, first", speaks.RespondToSilence(), "FIRST");
            Equal("in order, second", speaks.RespondToSilence(), "SECOND");
            Equal("in order, third", speaks.RespondToSilence(), "THIRD");
            Equal("then round again", speaks.RespondToSilence(), "FIRST");

            /*  More than one way to open, and telling the speaker apart.
                Neither is in the 1966 script, so both are asserted on a script
                written here rather than on that one -- which must go on having
                exactly one opening and no opinion about who is typing. */
            Section("beyond the original");
            Equal("the 1966 script has one opening", script.Greetings.Count, 0);
            Equal("and uses it", new ElizaEngine(script).GreetingFor(7),
                  "HOW DO YOU DO. PLEASE TELL ME YOUR PROBLEM");

            /*  All on one line: the reader treats every run of whitespace the
                same, and there are no comments here to need line ends.

                Every directive comes before the opening line, and that is not
                tidiness. Past the opening line the reader has moved on and
                takes a directive for an ordinary rule, in silence. Writing
                this test the wrong way round is how the rule was found. */
            var many = new ElizaEngine(ElizaScript.Parse(
                "(GREETINGS (FIRST) (SECOND) (THIRD)) " +
                "(FEMININE-MARKERS SAD) (MASCULINE-MARKERS GLAD) " +
                "(HI) START (NONE ((0) (GO ON)))"));
            Equal("visit one", many.GreetingFor(0), "FIRST");
            Equal("visit three", many.GreetingFor(2), "THIRD");
            Equal("visit four comes round again", many.GreetingFor(3), "FIRST");

            // Directives placed after the opening line are not directives, so
            // finding them loaded at all is half the check.
            Equal("markers loaded", many.Script.FeminineMarkers.Count, 1);
            Equal("hears a woman", many.SpeakerSounds("אני SAD"), 1);
            Equal("hears a man", many.SpeakerSounds("אני GLAD"), -1);
            Equal("hears nothing either way", many.SpeakerSounds("אני here"), 0);
            Equal("and not about somebody else",
                  many.SpeakerSounds("my wife is SAD"), 0);

            Section("standing up to nonsense");
            Punish(text, "the 1966 script");
            Punish(File.ReadAllText(FindScript("doctor.he.txt"), Encoding.UTF8),
                   "the Hebrew script");

            /*  And the feminine one, which nothing checked.

                It is generated from the Hebrew script, it ships inside the
                executable, and it is the script that runs for every woman who
                uses the program -- and it was the one file of the three that
                no check ever loaded. A generator can produce something that
                does not parse as easily as a person can. */
            Punish(File.ReadAllText(FindScript("doctor.he.f.txt"), Encoding.UTF8),
                   "the feminine Hebrew script");

            /*  Twenty-one sentences that used to come back wrong.

                They were found by reading the script one rule at a time,
                which is not a method, and every one of them was invisible
                from the outside: the program did not crash, did not warn, and
                answered in fluent Hebrew that did not follow from what had
                been typed. Nothing would have caught them coming back.

                The check is not what she says -- the wording may improve and
                the rotation moves -- but what she must not say, which is the
                fault itself. */
            Section("twenty-two Hebrew faults, so they stay fixed");
            string he = File.ReadAllText(FindScript("doctor.he.txt"), Encoding.UTF8);

            // The free wildcard that swallowed the clause before the word.
            Mended(he, "אני לא מסתדר עם השכן שלי", "השכן שלך", "אתה לא מסתדר");
            // Three keywords at a high precedence with no decomposition.
            Mended(he, "אני לא יודע מי אני יותר", "", "למה אתה שואל");
            Mended(he, "לא נעים לי לדבר על זה", "", "גם לי");
            Mended(he, "אין לנו שלום בית", "", "שלום. ספר");
            // Frames that stopped being Hebrew once the input was spliced in.
            Mended(he, "אני רוצה לישון", "", "מקבל לישון");
            Mended(he, "אני לא יכול יותר", "", "בינך לבין");
            Mended(he, "אני מרגיש שאני לא שווה כלום", "", "שאתה שאתה");
            Mended(he, "החיים שלי קשים", "", "החיים שלך עולה");
            Mended(he, "יש לי בעיה בעבודה", "", "עוד על בעיה");
            Mended(he, "חלמתי על אבא שלי", "", "דמיינת על");
            Mended(he, "אני זוכר ימים טובים יותר", "", "נזכר ימים");
            Mended(he, "הייתי ילד שקט", "", "מה שהיית ילד");
            Mended(he, "אני עצוב כי אשתי עזבה", "", "בגלל אשתך עזבה");
            // Negation dropped, which reverses the meaning.
            Mended(he, "לא פיטרו אותי", "לא", "פיטרו אותך.");
            Mended(he, "כבר לא בא לי לחיות", "", "ולמה בא לך לחיות");
            Mended(he, "לא הכל בסדר", "", "אם הכל בסדר");
            // A substitution that changed what was said.
            Mended(he, "אני צריך קצת זמן", "", "הרבה");
            // A rule named after a word its own list did not contain.
            Mended(he, "משפחתי לא תומכת בי", "המשפחה", "");
            // The second person that never came back to the first.
            Mended(he, "אני חושב עליך כל הזמן", "עליי", "עליך");
            // A specific line filed under a general pattern.
            Mended(he, "אף אחד לא מבין אותי", "", "אמור להיות שם");
            Mended(he, "זה מה שקורה לי כל הזמן", "", "מה בדיוק מה");
            // A word quoted back that the speaker never used.
            Mended(he, "זאת הבעיה שלי.", "", "אומר שלך");

            /*  And the punctuation, over the whole file rather than one
                sentence at a time. One reassembly in the script had no full
                stop, which is what the user noticed, and reading four hundred
                and thirty-three of them to find it is not a method either. */
            /*  And the models, which are the same engine with a different
                file -- so the same thing that could go wrong in the shipped
                script can go wrong in them, and they are four times the size.
                A model that no longer answers is a model nobody would
                notice. */
            Section("the models");
            foreach (var name in new[] { "rabati.he.txt", "chikaber.he.txt",
                                         "mashgiach.he.txt", "shadchan.he.txt",
                                         "dayan.he.txt", "tzul.he.txt" })
                Punish(File.ReadAllText(FindScript(name), Encoding.UTF8), name);

            Section("the scripts load without complaint");
            foreach (var name in Settings.Shipped)
            {
                var loaded = ElizaScript.Parse(
                    File.ReadAllText(FindScript(name), Encoding.UTF8));
                Equal(name + " loads with no warnings",
                      loaded.Warnings.Count == 0 ? "clean" :
                      loaded.Warnings.Count + ": " + loaded.Warnings[0], "clean");

                // Every answer in a Hebrew script ends in a stop. The 1966
                // script has no punctuation anywhere and is not asked.
                if (name.EndsWith(".he.txt", StringComparison.Ordinal))
                    Equal(name + ": every answer ends in a stop",
                          Unpunctuated(File.ReadAllText(FindScript(name), Encoding.UTF8)), 0);

                /*  And every pattern can fire. This one is asked of the 1966
                    script too: it passes, which is part of the answer to
                    whether the transcription is faithful. */
                string dead;
                int unreachable = Unreachable(
                    File.ReadAllText(FindScript(name), Encoding.UTF8), out dead);
                Equal(name + ": every pattern can fire" +
                      (unreachable > 0 ? "  " + dead : ""), unreachable, 0);
            }

            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? string.Format("all {0} checks passed", checks)
                : string.Format("{0} of {1} checks FAILED", failures, checks));
            return failures == 0 ? 0 : 1;
        }

        /*  ELIZA must answer anything. A person will type an empty line, a row
            of punctuation, a paragraph pasted from somewhere, two languages at
            once. None of that may throw, none of it may return nothing, and
            none of it may take long enough to notice: the decomposition matcher
            backtracks, and a pattern full of wildcards against a long sentence
            is exactly where a backtracking matcher goes quadratic. */
        static void Punish(string scriptText, string label)
        {
            var script = ElizaScript.Parse(scriptText);
            var engine = new ElizaEngine(script);

            // Build a vocabulary out of the script's own words, so the awkward
            // inputs are ones its rules will actually engage with.
            var vocabulary = script.Rules.Keys
                .Where(k => k.Length > 1 && k[0] != '(')
                .ToList();
            vocabulary.AddRange(new[] { ",", ".", "?", "!", "-", "'", "\"", "(", ")",
                                        "123", "%%%", "\t", "ß", "Ω", "мама", "שלום" });

            var inputs = new List<string> { "", " ", "   ", ".", ",", "...", "?!?",
                                            "'", "\"\"", "-", "0", "0 0 0" };

            // Deterministic pseudo-random sentences: same run every time.
            uint seed = 20260911;
            Func<int, int> next = n =>
            {
                seed = seed * 1664525 + 1013904223;
                return (int)(seed >> 8) % n;
            };

            for (int i = 0; i < 400; i++)
            {
                int words = 1 + next(14);
                var sb = new StringBuilder();
                for (int w = 0; w < words; w++)
                {
                    if (w > 0) sb.Append(' ');
                    sb.Append(vocabulary[next(vocabulary.Count)]);
                }
                inputs.Add(sb.ToString());
            }

            // And one deliberately awful sentence: long, and made only of words
            // the patterns want to match, which is the worst case for the matcher.
            var long_ = new StringBuilder();
            for (int i = 0; i < 300; i++)
            {
                if (i > 0) long_.Append(' ');
                long_.Append(vocabulary[i % Math.Min(8, vocabulary.Count)]);
            }
            inputs.Add(long_.ToString());
            inputs.Add(new string('x', 5000));

            var clock = System.Diagnostics.Stopwatch.StartNew();
            string worstInput = "";
            long worstMs = 0;

            foreach (var input in inputs)
            {
                var one = System.Diagnostics.Stopwatch.StartNew();
                string reply;
                try
                {
                    reply = engine.Respond(input);
                }
                catch (Exception ex)
                {
                    checks++;
                    failures++;
                    Console.WriteLine("   FAIL " + label + " threw on: " +
                                      Trim(input) + "\n     " + ex.GetType().Name +
                                      ": " + ex.Message);
                    return;
                }
                one.Stop();

                if (one.ElapsedMilliseconds > worstMs)
                {
                    worstMs = one.ElapsedMilliseconds;
                    worstInput = input;
                }

                if (string.IsNullOrWhiteSpace(reply))
                {
                    checks++;
                    failures++;
                    Console.WriteLine("   FAIL " + label + " answered nothing to: " +
                                      Trim(input));
                    return;
                }
            }
            clock.Stop();

            checks++;
            Console.WriteLine("   ok   " + label + ": " + inputs.Count +
                              " awkward inputs, none threw, all answered");
            checks++;
            if (worstMs < 250)
                Console.WriteLine("   ok   " + label + ": slowest reply " + worstMs +
                                  " ms, whole run " + clock.ElapsedMilliseconds + " ms");
            else
            {
                failures++;
                Console.WriteLine("   FAIL " + label + ": one reply took " + worstMs +
                                  " ms, for: " + Trim(worstInput));
            }
        }

        static string Trim(string s)
        {
            s = s.Replace("\t", " ");
            return s.Length <= 60 ? "\"" + s + "\"" : "\"" + s.Substring(0, 60) + "...\"";
        }

        // ---- the Hebrew script ------------------------------------------

        static int HebrewReport()
        {
            string path = FindScript("doctor.he.txt");
            string text = File.ReadAllText(path, Encoding.UTF8);
            var script = ElizaScript.Parse(text);
            var engine = new ElizaEngine(script);

            /*  A directive placed after the opening line is not a directive
                any more: the reader has moved on and takes it for a rule, in
                silence. Every one of them is worth asserting for that reason. */
            Console.WriteLine("# הוראות: " +
                "פתיחה=" + (script.Greeting.Length > 0) +
                "  שתיקה=" + script.Silence.Count +
                "  סימני-נקבה=" + script.FeminineMarkers.Count +
                "  סימני-זכר=" + script.MasculineMarkers.Count +
                "  מילוט=" + script.NoMatchMessages.Count +
                "  מפרידים=" + script.Delimiters.Count +
                "  כיוון=" + (script.RightToLeft ? "RTL" : "LTR"));
            if (script.FeminineMarkers.Count == 0)
                Console.WriteLine("! אין סימני נקבה. ההוראה כנראה יושבת אחרי שורת הפתיחה.");
            Console.WriteLine("# תסריט: " + script.Name);
            Console.WriteLine("# כללים: " + script.Rules.Count +
                              "   תגיות: " + script.Tags.Count +
                              "   פתיחה: " + script.Greeting);
            foreach (var tag in script.Tags.OrderBy(t => t.Key, StringComparer.Ordinal))
                Console.WriteLine("# תגית " + tag.Key + ": " + string.Join(" ", tag.Value));
            Console.WriteLine();

            foreach (var w in script.Warnings)
                Console.WriteLine("! " + w);
            foreach (var w in DanglingPrefixes(script))
                Console.WriteLine("! " + w);
            if (script.Warnings.Count > 0) Console.WriteLine();

            var fired = new HashSet<string>(StringComparer.Ordinal);
            // Kept out of scripts/, which the app treats as its script folder.
            string probesPath = Path.Combine(
                Path.GetDirectoryName(Path.GetDirectoryName(path)), "build", "probes.he.txt");
            var probes = File.Exists(probesPath)
                ? File.ReadAllLines(probesPath, Encoding.UTF8)
                    .Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#"))
                    .ToArray()
                : new string[0];

            foreach (var probe in probes)
            {
                string reply = engine.Respond(probe);
                foreach (var line in engine.Trace)
                {
                    if (line == "using NONE") fired.Add(ElizaScript.NoneKeyword);
                    if (!line.StartsWith("rule (")) continue;
                    int a = line.IndexOf('(') + 1;
                    int b = line.LastIndexOf(") ->");
                    if (b > a) fired.Add(line.Substring(a, b - a));
                }
                Console.WriteLine("> " + probe);
                Console.WriteLine("  " + reply);
                Console.WriteLine();
            }

            /*  Every keyword, on its own, with nothing after it.
                This is where a reply comes out cut in half: a rule that ends
                its sentence with a part the speaker never filled in says
                "how long have you been" and stops. It is the shape of the
                rule that causes it, so only running it finds it. */
            Console.WriteLine();
            Console.WriteLine("# כל מילת מפתח לבדה");
            foreach (var rule in script.Rules.Values
                         .Where(r => r.HasTransformation && r.Keyword[0] != '(')
                         .OrderBy(r => r.Keyword, StringComparer.Ordinal))
            {
                string reply = engine.Respond(rule.Keyword);
                foreach (var line in engine.Trace)
                {
                    if (line == "using NONE") fired.Add(ElizaScript.NoneKeyword);
                    if (!line.StartsWith("rule (")) continue;
                    int a = line.IndexOf('(') + 1;
                    int b = line.LastIndexOf(") ->");
                    if (b > a) fired.Add(line.Substring(a, b - a));
                }
                string mark = reply.Length < 14 ? "  <-- קצר" : "";
                Console.WriteLine("> " + rule.Keyword);
                Console.WriteLine("  " + reply + mark);
            }
            Console.WriteLine();

            var never = script.Rules.Values
                .Where(r => r.HasTransformation && !fired.Contains(r.Keyword))
                .Select(r => r.Keyword)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            Console.WriteLine("# כללים שנורו: " + fired.Count);
            Console.WriteLine(never.Count == 0
                ? "# כל הכללים נוסו"
                : "# כללים שלא נוסו (" + never.Count + "): " + string.Join(" ", never));
            return 0;
        }

        /*  Hebrew attaches its conjunctions and prepositions to the following
            word: "that you" is one word, שאתה. Because a reassembly rule joins
            its tokens with spaces, a lone ש or ב in a rule always comes out
            detached -- "ש אתה" -- which is not Hebrew. A single Hebrew letter
            standing on its own in a reassembly is therefore always a mistake. */
        static IEnumerable<string> DanglingPrefixes(ElizaScript script)
        {
            foreach (var rule in script.Rules.Values)
                foreach (var t in rule.Transformations)
                    foreach (var r in t.Reassemblies)
                        foreach (var token in r.Tokens)
                            if (token.Length == 1 && token[0] >= 'א' && token[0] <= 'ת')
                                yield return rule.Keyword + ": a prefix letter left on its own, " +
                                             token + ", in (" + string.Join(" ", r.Tokens) + ")";

            foreach (var t in script.Memory.Transformations)
                foreach (var token in t.Reassemblies[0].Tokens)
                    if (token.Length == 1 && token[0] >= 'א' && token[0] <= 'ת')
                        yield return "MEMORY: a prefix letter left on its own, " + token +
                                     ", in (" + string.Join(" ", t.Reassemblies[0].Tokens) + ")";
        }

        // ---- plumbing ---------------------------------------------------

        static string FindScript(string name)
        {
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 5 && dir != null; i++)
            {
                string p = Path.Combine(dir, "scripts", name);
                if (File.Exists(p)) return p;
                var parent = Directory.GetParent(dir);
                dir = parent == null ? null : parent.FullName;
            }
            throw new FileNotFoundException("cannot find scripts/" + name);
        }

        static ulong Oct(string octal) { return Convert.ToUInt64(octal, 8); }

        static string Tag(ElizaScript s, string name)
        {
            List<string> words;
            if (!s.Tags.TryGetValue(name, out words)) return "<missing>";
            var sorted = new List<string>(words);
            sorted.Sort(StringComparer.Ordinal);
            return string.Join(" ", sorted);
        }

        /*  A sentence that used to come back wrong, into a conversation
            that has only just started: a fresh engine each time, so the
            rotation is at its first answer and the result does not depend on
            what was asked before. */
        static void Mended(string text, string input, string must, string mustNot)
        {
            string said = new ElizaEngine(ElizaScript.Parse(text)).Respond(input);
            bool ok =
                (must.Length == 0 || said.IndexOf(must, StringComparison.Ordinal) >= 0) &&
                (mustNot.Length == 0 || said.IndexOf(mustNot, StringComparison.Ordinal) < 0);
            checks++;
            if (ok) { Console.WriteLine("   ok   " + input); return; }
            failures++;
            Console.WriteLine("   FAIL " + input);
            Console.WriteLine("     said     " + said);
            if (must.Length > 0) Console.WriteLine("     needs    " + must);
            if (mustNot.Length > 0) Console.WriteLine("     must not " + mustNot);
        }

        /*  How many patterns in a script can never fire.

            The engine takes the first decomposition that matches, so a
            pattern written after a wider one in the same rule is dead: it
            can be read, it can be checked by eye, and it will never once
            answer anybody. Writing one is easy and noticing one is not,
            because the pronoun swap runs before the match and the words in
            the pattern are not the word in the rule above it -- so the rule
            says SIKAMNU and the pattern says SIKAMTEM, and the eye does not
            join them up.

            Only the plain case is counted, and it is the one that happens:
            an earlier (0 w1..wk 0) whose words sit in the later pattern as a
            run with nothing between them. Words that are not adjacent in the
            later pattern are not matched by the earlier one at all -- the
            pattern (0 YOU SAD 0) does not match YOU ARE NOT SAD, because
            the two words have to be next to each other -- so a gap means the
            later pattern still lives. A pattern with a bracketed choice in
            it is left alone: what it matches cannot be read off the line. */
        static int Unreachable(string text, out string first)
        {
            first = "";
            int dead = 0;
            foreach (var rule in ElizaScript.Parse(text).Rules.Values)
            {
                var patterns = rule.Transformations
                    .Select(t => t.Decomposition).ToList();
                for (int j = 0; j < patterns.Count; j++)
                    for (int i = 0; i < j; i++)
                    {
                        if (!Swallows(patterns[i], patterns[j])) continue;
                        dead++;
                        if (first.Length == 0)
                            first = "(" + rule.Keyword + ") (" +
                                string.Join(" ", patterns[j].ToArray()) +
                                ") never fires, (" +
                                string.Join(" ", patterns[i].ToArray()) +
                                ") above it matches it first";
                        break;
                    }
            }
            return dead;
        }

        static bool Plain(List<string> pattern)
        {
            foreach (var t in pattern)
                if (t.Length > 0 && (t[0] == '(' || t[0] == '/')) return false;
            return true;
        }

        static bool Swallows(List<string> first, List<string> second)
        {
            if (!Plain(first) || !Plain(second)) return false;
            if (first.Count < 3 || first[0] != "0" ||
                first[first.Count - 1] != "0") return false;

            var run = first.GetRange(1, first.Count - 2);
            foreach (var t in run)
            {
                int n;
                if (int.TryParse(t, out n)) return false;   // a wildcard inside
            }

            for (int i = 0; i + run.Count <= second.Count; i++)
            {
                bool same = true;
                for (int k = 0; k < run.Count; k++)
                    if (second[i + k] != run[k]) { same = false; break; }
                if (same) return true;
            }
            return false;
        }

        // How many answers in a script end in no full stop, question mark or
        // exclamation. Weizenbaum's own script has no punctuation at all and
        // is not asked this question.
        static int Unpunctuated(string text)
        {
            int loose = 0;
            foreach (var rule in ElizaScript.Parse(text).Rules.Values)
                foreach (var t in rule.Transformations)
                    foreach (var r in t.Reassemblies)
                    {
                        if (r.What != Reassembly.Kind.Normal || r.Tokens.Count == 0) continue;
                        string last = r.Tokens[r.Tokens.Count - 1];
                        if (last.Length == 0) continue;
                        if (".?!\u2026".IndexOf(last[last.Length - 1]) < 0) loose++;
                    }
            return loose;
        }

        static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine("-- " + title);
        }

        static void Replay(ElizaEngine engine, string[,] conversation, string label)
        {
            int n = conversation.GetLength(0);
            int bad = 0;
            for (int i = 0; i < n; i++)
            {
                string prompt = conversation[i, 0];
                string expected = conversation[i, 1];
                string actual = engine.Respond(prompt);
                checks++;
                if (actual == expected) continue;

                failures++;
                bad++;
                if (bad <= 3)
                {
                    Console.WriteLine("   FAIL " + label + " #" + (i + 1));
                    Console.WriteLine("     input    " + prompt);
                    Console.WriteLine("     expected " + expected);
                    Console.WriteLine("     actual   " + actual);
                }
            }
            Console.WriteLine("   " + label + ": " + (n - bad) + "/" + n +
                              (bad == 0 ? " ok" : " FAILED"));
        }

        static void Equal<T>(string what, T actual, T expected)
        {
            checks++;
            if (EqualityComparer<T>.Default.Equals(actual, expected))
            {
                Console.WriteLine("   ok   " + what);
                return;
            }
            failures++;
            Console.WriteLine("   FAIL " + what);
            Console.WriteLine("     expected " + expected);
            Console.WriteLine("     actual   " + actual);
        }
    }
}
