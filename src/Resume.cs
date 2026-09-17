// Resume.cs -- reading a conversation back off the disk so it can be carried on.
//
// The engine keeps no history. Every answer is worked out from the sentence
// just typed and four small counters: the 1..4 limit, a pointer into each
// rule's phrasings, the queue of things she has kept to bring up later, and
// where she is in the answers to silence. Nothing else survives a turn, and
// the transcript is never read by her.
//
// So carrying a conversation on is not a matter of giving her the text back.
// It is a matter of putting those four counters where they were -- and the
// only thing that moves them is what the PERSON typed. Hand the same lines to
// a fresh engine in the same order and it arrives in the same state, because
// there is nothing else for it to depend on.
//
// Which has a consequence worth stating plainly: the file does not have to be
// true. She answers from where the counters are, and the counters come from
// your lines alone, so a conversation written by hand -- or one cut off in the
// middle, or one whose answers somebody edited -- carries on exactly as well
// as a real one. The screen shows the file as it is, and she continues from
// it. That is the point of the feature rather than a hole in it: a person can
// send a conversation to a friend, and the friend presses continue.

using System;
using System.Collections.Generic;
using System.Text;

namespace ElizaApp
{
    class Resumed
    {
        /*  The four styles the screen paints in, named. */
        public const int Her = 0, Person = 1, Quiet = 2, Opening = 3;

        public class Line
        {
            public string Text = "";
            public int Style = Her;
        }

        // Everything in the file, to be painted exactly as it is.
        public readonly List<Line> Lines = new List<Line>();
        // What the person typed, in the last conversation in the file only.
        public readonly List<string> Typed = new List<string>();
        // The name written in the banner, if there is one.
        public string ScriptName = "";
        // And the file it was read out of, when it came from one.
        public string Path = "";

        public bool IsEmpty { get { return Lines.Count == 0; } }

        /*  The marker.

            The program writes a MIDDLE DOT and a space in front of everything
            the person typed, and that is what this looks for. But the only way
            anybody learns the shape of these files is by opening one, and the
            second thing they will do is write one themselves -- so a file with
            no middle dot anywhere is read more generously, and a greater-than
            or a hyphen will do. A file that uses the real marker even once is
            read strictly, so a line of dialogue that happens to begin with a
            hyphen can never be mistaken for something somebody typed. */
        const char Dot = '·';

        static bool Marked(string line, bool loose, out string said)
        {
            said = null;
            string t = line.TrimStart(' ', '\t', '﻿');
            if (t.Length == 0) return false;
            if (t[0] == Dot) { said = t.Substring(1).Trim(); return true; }
            if (!loose) return false;
            if (t[0] == '•' || t[0] == '>') { said = t.Substring(1).Trim(); return true; }
            if (t[0] == '-' && t.Length > 1 && t[1] == ' ')
            { said = t.Substring(2).Trim(); return true; }
            return false;
        }

        /*  A run of hyphens is what the program puts between two conversations
            kept in one file -- pressing F5 or F2 starts a new one and does not
            start a new file. Eight is well below the forty it writes and well
            above anything that turns up in a sentence. */
        static bool IsSeparator(string line)
        {
            string t = line.Trim();
            if (t.Length < 8) return false;
            foreach (char c in t)
                if (c != '-' && c != '–' && c != '—' && c != '‒') return false;
            return true;
        }

        public static Resumed Read(string text)
        {
            var it = new Resumed();
            if (text == null) return it;

            var all = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            // A file that ends in a newline leaves one empty string behind it.
            int count = all.Length;
            if (count > 0 && all[count - 1].Length == 0) count--;

            bool loose = true;
            for (int i = 0; i < count; i++)
                if (all[i].TrimStart(' ', '\t', '﻿').StartsWith(Dot.ToString(), StringComparison.Ordinal))
                { loose = false; break; }

            /*  Where the last conversation in the file begins. Only its lines
                are replayed: the ones before it were answered by engines that
                were thrown away when the separator was written, and feeding
                them to this one would put the counters somewhere they never
                were. They are still painted -- what is on the screen is the
                file, all of it. */
            int live = 0;
            for (int i = 0; i < count; i++)
                if (IsSeparator(all[i])) live = i + 1;

            /*  The heading: the run of lines at the top of a conversation that
                nobody typed. In a file the program wrote it is three lines and
                a blank one. In a file somebody wrote by hand there is often
                none at all, which is why this stops at the first thing that
                looks like a turn rather than at the first blank line. */
            int headEnd = live;
            while (headEnd < count && headEnd - live < 6)
            {
                string said;
                if (all[headEnd].Trim().Length == 0) break;
                if (Marked(all[headEnd], loose, out said)) break;
                headEnd++;
            }

            bool openingDone = false;
            for (int i = 0; i < count; i++)
            {
                string line = all[i];
                string said;

                if (IsSeparator(line))
                { it.Lines.Add(new Line { Text = line, Style = Quiet }); continue; }

                if (line.Trim().Length == 0)
                { it.Lines.Add(new Line { Text = "", Style = Her }); continue; }

                if (Marked(line, loose, out said))
                {
                    /*  Painted with the marker on it, because that is how a
                        line somebody typed is held on the screen already.

                        With one correction: a file written by hand, with a
                        greater-than or a hyphen for a marker, is written back
                        with the real one. Otherwise the first save mixes the
                        two -- and the next time it is opened the middle dot in
                        the new turns makes the file strict, and every line the
                        person wrote by hand stops counting as theirs. A
                        conversation that survives being carried on once and
                        not twice would be worse than one that never was. */
                    it.Lines.Add(new Line
                    {
                        Text = loose ? "· " + said : line,
                        Style = Person
                    });
                    if (i >= live && said.Length > 0) it.Typed.Add(said);
                    openingDone = true;
                    continue;
                }

                int style = Her;
                if (i >= live && i < headEnd) style = Quiet;
                else if (i >= live && !openingDone) { style = Opening; openingDone = true; }
                it.Lines.Add(new Line { Text = line, Style = style });

                // The script's own name, out of the banner it was written into.
                string name = NameIn(line);
                if (name.Length > 0) it.ScriptName = name;
            }

            return it;
        }

        /*  "אלייזה · תסריט: לדבר עם אלייזה" and its English twin. Taken from
            wherever it appears, and the last one wins, because the last banner
            in the file belongs to the conversation being carried on. */
        static string NameIn(string line)
        {
            foreach (string mark in new[] { "תסריט:", "SCRIPT:" })
            {
                int at = line.IndexOf(mark, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                return line.Substring(at + mark.Length).Trim();
            }
            return "";
        }
    }
}
