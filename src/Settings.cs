// Settings.cs -- what the shell remembers between runs.
//
// The shell remembers. ELIZA does not, and must not: the DOCTOR script gives
// the keyword NAME a precedence of 15 and spends the whole rule refusing to
// take one. Weizenbaum did not forget to give her a memory of the person in
// front of her; he wrote the refusal in. So everything kept here belongs to
// the program around her, never to her.
//
// Plain key=value text, because a person should be able to open it.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ElizaApp
{
    class Settings
    {
        readonly Dictionary<string, string> values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Path { get; private set; }

        // ---- the settings themselves ------------------------------------

        public string Script
        {
            get { return Get("script", ""); }
            set { Set("script", value); }
        }

        /*  The file header invites editing, and the key lookup is already
            case-insensitive -- so a file that is case-insensitive to the left
            of the "=" and case-sensitive to the right is a trap nobody would
            guess. Every string value is folded before it is compared. */
        public bool Hebrew
        {
            get { return Word("language", "he") != "en"; }
            set { Set("language", value ? "he" : "en"); }
        }

        /*  Which of the three things the screen is set into: a plain
            terminal, the console of 1966, or a sheet of paper. Chosen inside
            the conversation window rather than out here, because it is not a
            preference about the program -- it is the room you sit in while
            you talk to her. */
        public int ScreenStyle
        {
            get { return Number("screen", 1, 0, 2); }
            set { Set("screen", value.ToString(CultureInfo.InvariantCulture)); }
        }

        // What the tube is made of: green, amber or white. The sheet of
        // paper has no tube, and ignores this.
        public int Phosphor
        {
            get { return Number("phosphor", 0, 0, 2); }
            set { Set("phosphor", value.ToString(CultureInfo.InvariantCulture)); }
        }

        /*  The shell's own look: "system", "dark" or "light", and one of six
            accent colours. This is the window around her and nothing to do
            with her -- which is why the museum is no longer offered here. A
            drawn room is a thing to sit in, not a colour scheme, and it
            belongs where the conversation happens. */
        public string Mode
        {
            get
            {
                string v = Word("mode", "system");
                return v == "dark" || v == "light" ? v : "system";
            }
            set { Set("mode", value); }
        }

        public int Accent
        {
            get { return Number("accent", 0, 0, 5); }
            set { Set("accent", value.ToString(CultureInfo.InvariantCulture)); }
        }

        public int Speed
        {
            get { return Number("speed", 1, 0, 2); }
            set { Set("speed", value.ToString(CultureInfo.InvariantCulture)); }
        }

        public int FontSize
        {
            get { return Number("fontsize", 12, 8, 22); }
            set { Set("fontsize", value.ToString(CultureInfo.InvariantCulture)); }
        }

        /*  How to address the speaker: "auto" notices and switches, "m" and
            "f" settle it. Kept here rather than in the script, because it is
            about the person at the keyboard and not about her. */
        /*  Validated like the others, and it matters more here than anywhere.
            An unrecognised value used to be returned as it stood: the settings
            page saw something that was not "m" or "f" and drew "as you speak",
            while the conversation tested for "auto", did not find it, and
            never listened at all. The page said one thing and the program did
            another. */
        public string Address
        {
            get
            {
                string v = Word("address", "auto");
                return v == "m" || v == "f" ? v : "auto";
            }
            set { Set("address", value); }
        }

        // How many conversations have been begun. Used only to vary the
        // opening line, and kept here because she keeps nothing.
        public int Visits
        {
            get { return Number("visits", 0, 0, 1000000); }
            set { Set("visits", value.ToString(CultureInfo.InvariantCulture)); }
        }

        /*  How she picks among the phrasings a rule offers.

              "original"  in turn from the first, exactly as in 1966. Say the
                          same thing next time and you get the same answer,
                          which is what Weizenbaum wrote and what Avidan saw.
              "varied"    in turn, but entering the rotation somewhere else
                          each visit. The mechanism is untouched; only the
                          starting point moves.
              "shuffled"  a shuffled deck: unpredictable order, and still
                          every phrasing before any of them comes round again.

            "varied" is the default because the fault people notice is not
            that she repeats herself inside a conversation -- she does not --
            but that she opens every conversation the same way. */
        public string Order
        {
            get
            {
                string v = Word("order", "varied");
                return v == "original" || v == "shuffled" ? v : "varied";
            }
            set { Set("order", value); }
        }

        // Which tab of the settings was last open. A settings page that
        // reopens where it was left is what every other one does.
        public int Tab
        {
            get { return Number("tab", 0, 0, 2); }
            set { Set("tab", value.ToString(CultureInfo.InvariantCulture)); }
        }

        public bool KeepTranscripts
        {
            get { return Get("keeptranscripts", "1") == "1"; }
            set { Set("keeptranscripts", value ? "1" : "0"); }
        }

        /*  Off unless asked for. The program is meant to run on a machine with
            no network at all, and a startup that quietly reaches out would
            break that promise without anyone noticing. */
        public bool CheckUpdates
        {
            get { return Get("checkupdates", "0") == "1"; }
            set { Set("checkupdates", value ? "1" : "0"); }
        }

        /*  Where the program lives now that it is published. It is still only
            asked when CheckUpdates is on, and it is kept as a setting rather
            than a constant so that somebody running their own fork can point
            it at their own. */
        public string UpdateRepository
        {
            get { return Get("repository", "BeniaBot/eliza"); }
            set { Set("repository", value); }
        }

        public string InstalledVersion
        {
            get { return Get("version", ""); }
            set { Set("version", value); }
        }

        // ---- where things live ------------------------------------------

        /*  Where everything lives, decided once.

            It used to be recomputed on every access, and the test was "is
            there a scripts folder beside the exe" -- which erasing the scripts
            makes false. So on a portable copy the answer changed in the middle
            of a run: settings went one way, transcripts another, and the next
            launch unpacked itself into AppData, which is exactly the machine
            the person carrying it on a stick was trying to leave alone.

            The test also has to agree with the one in Program.cs, which wants
            a scripts folder that actually holds a script. Requiring less here
            split an installation in half: the scripts were read from AppData
            while "open the script folder" opened an empty folder beside the
            exe and invited someone to edit files nothing would ever read. */
        static string decided;

        public static string Folder
        {
            get
            {
                if (decided != null) return decided;

                string exeDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string portable = System.IO.Path.Combine(exeDir, "scripts");
                bool carried = false;
                try
                {
                    carried = Directory.Exists(portable) &&
                              Directory.GetFiles(portable, "*.txt").Length > 0;
                }
                catch { }

                decided = carried
                    ? exeDir
                    : System.IO.Path.Combine(
                          Environment.GetFolderPath(
                              Environment.SpecialFolder.LocalApplicationData),
                          "Eliza");
                return decided;
            }
        }

        public static string TranscriptFolder
        {
            get { return System.IO.Path.Combine(Folder, "transcripts"); }
        }

        // ---- starting over ----------------------------------------------

        /*  Back to how the program arrived: every preference forgotten, the
            file left in place and empty. Transcripts are not preferences and
            are not touched -- somebody who wants a green screen back should
            not lose what they wrote.

            The count of visits goes too. It is what varies her opening line,
            so keeping it would mean a program reset to its defaults that did
            not greet you the way a new one does. */
        public void ResetToDefaults()
        {
            /*  And it clears Erased first.

                Erased is there so that closing the program does not write
                the settings file back seconds after somebody asked for it to
                be gone. It was stopping this too -- and a reset that is
                pressed, confirmed, and then silently does nothing is worse
                than no reset at all. Pressing a button is an instruction, not
                a side effect of closing a window. */
            Erased = false;
            values.Clear();
            Save();
        }

        /*  Everything, as though the program had never run here: preferences,
            transcripts, and the scripts unpacked beside them -- which come
            back on the next start, because they live inside the executable.

            Returns what could not be removed, and does not stop at the first
            failure: a transcript held open by a text editor should not leave
            the rest of it behind. */
        /*  The scripts that come out of the executable. Kept here rather
            than in Program because it is a list of names, not entry-point
            logic, and because this is where it has to be known. */
        /*  And the order they are offered in. ELIZA herself first -- the
            1966 script, then the Hebrew one -- and then the models, largest
            first, which is also nearest-to-her first. */
        public static readonly string[] Shipped =
        {
            "doctor.txt", "doctor.he.txt", "doctor.he.f.txt",
            "rabati.he.txt", "chikaber.he.txt", "mashgiach.he.txt",
            "shadchan.he.txt", "dayan.he.txt", "tzul.he.txt"
        };

        /*  And the names that used to be on that list.

            A script is written out beside the settings on the first run, so
            renaming one leaves the old copy where it is and the program then
            offers the same character twice under two names. Program.Retire
            removes one, and only if nobody has edited it. */
        public static readonly string[] Retired = { "chavrusa.he.txt" };

        public static List<string> EraseEverything()
        {
            /*  Only what this program put there.

                It used to sweep every file under Folder. On a copy carried on
                a stick, Folder IS the directory the executable sits in -- so
                "erase everything" would have taken the readme next to it, and
                anything else in the folder somebody had dropped the program
                into. The note promises the settings, the transcripts and the
                scripts. It should take those and stop. */
            var failed = new List<string>();
            string home = Folder;
            if (!Directory.Exists(home)) return failed;

            Kill(System.IO.Path.Combine(home, "settings.txt"), failed);
            Sweep(System.IO.Path.Combine(home, "transcripts"), failed);

            /*  Only the scripts this program put there.

                The note beside the button promises that the scripts come
                back on the next start, because they are kept inside the
                executable -- which is true of the ones that shipped and
                false of one somebody wrote themselves. And a script somebody
                wrote is exactly the thing this program's README and its About
                page spend their length inviting people to make. Sweeping the
                folder took it, and the note said it would return. */
            string scriptDir = System.IO.Path.Combine(home, "scripts");
            foreach (var name in Shipped)
            {
                Kill(System.IO.Path.Combine(scriptDir, name), failed);
                Kill(System.IO.Path.Combine(scriptDir, "." + name + ".stamp"), failed);
                Kill(System.IO.Path.Combine(scriptDir, name + ".bak"), failed);
            }
            // And the folder itself, if nothing of anybody's is left in it.
            try { Directory.Delete(scriptDir, false); } catch { }

            // The stamps the unpacker leaves beside the scripts, and the
            // backups it makes of a script somebody edited.
            foreach (var name in SafeList(home, failed))
            {
                string leaf = System.IO.Path.GetFileName(name);
                if (leaf.StartsWith(".", StringComparison.Ordinal) &&
                        leaf.EndsWith(".stamp", StringComparison.OrdinalIgnoreCase) ||
                    leaf.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    Kill(name, failed);
            }

            // And the folder itself, but only when it is ours to remove.
            if (!IsBesideTheProgram(home))
                try { Directory.Delete(home, false); } catch { }

            return failed;
        }

        static bool IsBesideTheProgram(string folder)
        {
            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                return string.Equals(
                    System.IO.Path.GetFullPath(folder).TrimEnd('\\'),
                    System.IO.Path.GetFullPath(exeDir).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }      // when in doubt, leave it alone
        }

        static void Kill(string file, List<string> failed)
        {
            try
            {
                if (!File.Exists(file)) return;
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch { failed.Add(file); }
        }

        /*  A folder this program owns, and everything under it. Deepest first,
            or a directory is never empty when its turn comes. */
        static void Sweep(string folder, List<string> failed)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in SafeList(folder, failed)) Kill(file, failed);
            foreach (var dir in SafeDirs(folder).OrderByDescending(d => d.Length))
                try { Directory.Delete(dir, false); } catch { failed.Add(dir); }
            try { Directory.Delete(folder, false); } catch { failed.Add(folder); }
        }

        static IEnumerable<string> SafeList(string folder, List<string> failed)
        {
            try { return Directory.GetFiles(folder, "*", SearchOption.AllDirectories); }
            catch { failed.Add(folder); return new string[0]; }
        }

        // Folded, so that a hand-edited file is read the way it looks.
        string Word(string key, string fallback)
        {
            return Get(key, fallback).Trim().ToLowerInvariant();
        }

        static IEnumerable<string> SafeDirs(string folder)
        {
            try { return Directory.GetDirectories(folder, "*", SearchOption.AllDirectories); }
            catch { return new string[0]; }
        }

        // ---- reading and writing ----------------------------------------

        /*  Set when the file existed but could not be read, and the reason
            the next Save refuses to run: a settings.txt held open for a moment
            by an editor or a scanner used to produce a program with default
            preferences that then wrote those defaults over the real file. The
            preferences were not ignored for one run, they were destroyed. */
        public bool Unread { get; private set; }
        // Assigned by the shell; the test runner compiles this file
        // without it, so the default is written out to say so.
        public bool Erased = false;
        public string LastError { get; private set; }

        public static Settings Load()
        {
            var s = new Settings();
            s.Path = System.IO.Path.Combine(Folder, "settings.txt");
            try
            {
                if (!File.Exists(s.Path)) return s;
                foreach (var line in File.ReadAllLines(s.Path, Encoding.UTF8))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    s.values[trimmed.Substring(0, eq).Trim()] =
                        trimmed.Substring(eq + 1).Trim();
                }
                s.Migrate();
            }
            catch (Exception ex)
            {
                s.Unread = true;
                s.LastError = ex.Message;
            }
            return s;
        }

        /*  Older versions kept two of these under keys nothing reads now.
            "phosphor" ran 0 to 3 with 3 meaning paper, and "look" was
            modern or museum; the museum is a screen style today, and the
            choice between taking her phrasings in turn or at random was
            "choose".

            Clamping is the wrong answer to a value from an older scheme --
            phosphor 3 clamped to 2 gives a 1966 console with a white tube,
            the one thing nobody asked for. Move the choice across once, then
            take the dead keys out: a key the program ignores should not sit
            in a file that invites editing. */
        void Migrate()
        {
            bool moved = false;
            string v;
            int old;

            if (!values.ContainsKey("screen"))
            {
                if (values.TryGetValue("phosphor", out v) &&
                    int.TryParse(v.Trim(), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out old) && old == 3)
                {
                    values["screen"] = "2";      // paper is a style now
                    values["phosphor"] = "0";    // and paper has no tube colour
                    moved = true;
                }
                else if (values.TryGetValue("look", out v) &&
                         v.Trim().ToLowerInvariant() == "museum")
                {
                    values["screen"] = "2";
                    moved = true;
                }
            }

            if (!values.ContainsKey("order") && values.TryGetValue("choose", out v))
            {
                values["order"] = v.Trim().ToLowerInvariant() == "random"
                    ? "shuffled" : "original";
                moved = true;
            }

            bool a = values.Remove("look"), b = values.Remove("choose");
            if (moved || a || b) Save();
        }

        /*  Returns whether it worked, because three things upstream were
            claiming success they had no evidence for: "reset settings" printed
            Done over a file it had not managed to write, and a read-only
            settings.txt forgot every preference on every run without ever
            saying so. */
        public bool Save()
        {
            // Erased means the folder is meant to be gone. Writing it back
            // seconds later, which is what used to happen on close, makes the
            // one destructive action in the program a no-op.
            if (Erased) return true;
            if (Unread) { LastError = "the existing settings could not be read"; return false; }

            try
            {
                // The directory of the file being written, not Folder. The two
                // used to be able to disagree, and then this created one empty
                // folder and wrote the file into another.
                string home = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(home)) Directory.CreateDirectory(home);

                var sb = new StringBuilder();
                sb.AppendLine("# ELIZA. Edit freely; unknown lines are ignored.");
                foreach (var pair in values)
                    sb.AppendLine(pair.Key + " = " + pair.Value);
                File.WriteAllText(Path, sb.ToString(), new UTF8Encoding(true));
                LastError = null;
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        string Get(string key, string fallback)
        {
            string v;
            return values.TryGetValue(key, out v) && v.Length > 0 ? v : fallback;
        }

        /*  Changing a preference after an erase means the program is in
            use again, so the file is allowed back. Without this, everything
            touched after the erase button was kept in memory until the window
            closed and then thrown away. */
        void Set(string key, string value) { Erased = false; values[key] = value ?? ""; }

        int Number(string key, int fallback, int low, int high)
        {
            int n;
            if (!int.TryParse(Get(key, ""), NumberStyles.Integer,
                              CultureInfo.InvariantCulture, out n)) return fallback;
            return n < low ? low : (n > high ? high : n);
        }
    }

    // ------------------------------------------------------------------

    /*  Every piece of text the shell shows, in both languages. ELIZA's own
        words are never in here -- those live in the scripts, which is the
        whole point of her design. */
    static class Say
    {
        public static bool Hebrew = true;

        static string Pick(string he, string en) { return Hebrew ? he : en; }

        public static string Converse { get { return Pick("שיחה", "Converse"); } }
        public static string About { get { return Pick("על אלייזה", "About"); } }
        public static string Transcripts { get { return Pick("תמלילים", "Transcripts"); } }
        // Windows calls it Settings, and so does the row on this page that
        // resets it and the message that says it could not be saved.
        public static string Preferences { get { return Pick("הגדרות", "Settings"); } }

        public static string Enter { get { return Pick("לצ'אט", "Enter"); } }

        /*  The button on a script's card, in that script's own language.

            Both cards carried the interface's word and put it against the
            interface's edge, so on a Hebrew interface the English card said
            "לצ'אט" and said it on the right: the wrong word, on the wrong
            side, on a card whose title and its "English" note were already
            laid out the other way. */
        public static string EnterFor(bool hebrewScript)
        {
            return hebrewScript ? "לצ'אט" : "Chat";
        }
        public static string Subtitle
        {
            get
            {
                return Pick("התוכנה ששוחחה עם בני אדם, 1966",
                            "The program that talked with people, 1966");
            }
        }

        public static string ChooseScript
        {
            get { return Pick("בחרו תסריט והיכנסו לשיחה", "Choose a script and go in"); }
        }

        public static string Herself { get { return Pick("אלייזה", "ELIZA"); } }

        /*  Not "models".

            A model is what the program calls them and it says nothing: the
            word a person reaches for is the one that says they are her,
            copied and changed. In Hebrew that is a pun a Hebrew speaker
            hears at once -- shibut is cloning and a bot is a bot -- and in
            English the plain word for the same thing is the clone. */
        public static string Models { get { return Pick("שי-בוטים", "Clones"); } }

        public static string OursModels
        {
            get { return Pick("המודלים שלנו", "The ones we built"); }
        }

        public static string AlsoModels
        {
            get
            {
                return Pick("יש גם שי-בוטים שנבנו על אותו מנוע, עם דמויות אחרות ←",
                            "There are also clones built on the same engine, with other characters →");
            }
        }

        public static string NoteModels
        {
            get
            {
                return Pick("אלייזה שהוחלף לה התסריט. אותו מנוע בדיוק, קובץ אחר.",
                            "ELIZA with her script swapped. The same engine, a different file.");
            }
        }

        public static string NoTranscripts
        {
            get { return Pick("עוד לא נשמרו תמלילים", "No transcripts saved yet"); }
        }

        // A verbal noun, like מחיקה and חזרה beside it -- not a masculine
        // singular imperative, in a program that carries a preference for
        // addressing the user in the right gender.
        public static string Open { get { return Pick("פתיחה", "Open"); } }
        public static string Delete { get { return Pick("מחיקה", "Delete"); } }

        /*  What a script that does not name itself is offered as.

            In the script's OWN language, not the interface's. The card for
            Weizenbaum's English script is an invitation to talk to her in
            English, and putting the Hebrew invitation on it said the opposite
            of what it meant. */
        public static string TalkTo(bool hebrewScript)
        {
            return hebrewScript ? "לדבר עם אלייזה" : "Talk to ELIZA";
        }

        public static string TalkWindow
        {
            get { return Pick("חלון השיחה", "The conversation window"); }
        }

        public static string Screen { get { return Pick("סגנון המסך", "Screen style"); } }
        public static string Tube { get { return Pick("צבע המסך", "Screen colour"); } }

        public static string[] Screens
        {
            get
            {
                return Hebrew
                    ? new[] { "מסוף", "מחשב 1966", "מותאם לדמות" }
                    : new[] { "Terminal", "1966 machine", "In character" };
            }
        }

        public static string[] Tubes
        {
            get
            {
                return Hebrew
                    ? new[] { "ירוק", "ענבר", "לבן" }
                    : new[] { "Green", "Amber", "White" };
            }
        }

        public static string NoteScreen
        {
            get
            {
                return Pick("איך נראה חלון השיחה: מסוף פשוט, מחשב 1966, או חדר שמתאים לדמות שמדברים איתה — אצל אלייזה בעברית זה חדר ההמתנה שהוצג במוזיאון המדע, ולכל שי-בוט יש חדר משלו. אפשר להחליף גם מתוך השיחה, במקש F4.",
                            "What the conversation window is: a plain terminal, the 1966 machine, or a room that belongs to whoever you are talking to — for the Hebrew ELIZA that is the waiting room the Science Museum showed, and every model has its own. F4 changes it from inside the conversation too.");
            }
        }

        public static string NoteTube
        {
            get
            {
                return Pick("צבע הזרחן של השפופרת. לחדר שמותאם לדמות אין שפופרת — הצבעים שלו הם שלו — ולכן השורה הזאת נעלמת כשבוחרים בו.",
                            "The colour of the phosphor. A room that belongs to a character has no tube, and its colours are its own, so this row disappears when it is chosen.");
            }
        }
        public static string Back { get { return Pick("חזרה", "Back"); } }
        public static string CopyAll { get { return Pick("העתקה", "Copy"); } }

        public static string DeleteTitle
        {
            get { return Pick("מחיקת תמליל", "Delete transcript"); }
        }

        public static string DeleteAsk
        {
            get
            {
                return Pick("למחוק את התמליל הזה? הוא יעבור לסל המיחזור.",
                            "Delete this transcript? It goes to the recycle bin.");
            }
        }

        public static string DeleteFailed
        {
            get
            {
                return Pick("לא ניתן היה למחוק את הקובץ.",
                            "The file could not be deleted.");
            }
        }

        public static string DeleteAll
        {
            get { return Pick("מחיקת כל התמלילים", "Delete every transcript"); }
        }

        public static string DeleteAllAsk
        {
            get
            {
                return Pick("למחוק את כל התמלילים השמורים? הם יעברו לסל המיחזור.",
                            "Delete every saved transcript? They go to the recycle bin.");
            }
        }

        public static string SaveFailed
        {
            get
            {
                return Pick("לא ניתן היה לשמור את ההגדרות.",
                            "The settings could not be saved.");
            }
        }
        public static string Folder2 { get { return Pick("תיקיית התסריטים", "Script folder"); } }

        public static string Language { get { return Pick("שפת הממשק", "Interface language"); } }
        public static string Typing { get { return Pick("מהירות הקלדה", "Typing speed"); } }
        public static string Size { get { return Pick("גודל הטקסט", "Text size"); } }
        public static string Keep { get { return Pick("שמירת תמלילים", "Keep transcripts"); } }
        public static string Address { get { return Pick("לשון הפנייה", "Address me as"); } }
        public static string Theme { get { return Pick("ערכת נושא", "Theme"); } }
        public static string Accent { get { return Pick("צבע הדגשה", "Colour"); } }
        public static string Order { get { return Pick("סדר התשובות", "Order of replies"); } }

        // ---- the headings the settings sit under ------------------------

        /*  Tab labels, under a page already headed "הגדרות". Repeating the
            word in every tab is how a settings page reads when nobody has
            looked at it: short names, and the heading carries the rest. */
        public static string General { get { return Pick("התוכנה", "Program"); } }
        public static string Appearance { get { return Pick("תצוגה", "Display"); } }
        public static string Talking { get { return Pick("השיחה", "Conversation"); } }
        public static string More { get { return Pick("מתקדם", "Advanced"); } }

        // ---- the ones that do something rather than hold a value --------

        public static string Reset { get { return Pick("איפוס ההגדרות", "Reset settings"); } }
        public static string Erase { get { return Pick("מחיקת הכול מהמחשב", "Erase everything"); } }
        /*  Two buttons on the same page, one of which resets the settings
            and one of which deletes everything the program ever wrote, both
            said "בצע" and were told apart only by the red. In greyscale, or
            to a red-green colour-blind reader, they were the same button. */
        public static string DoReset { get { return Pick("איפוס", "Reset"); } }
        public static string Sure { get { return Pick("בטוח? לחצו שוב", "Sure? Click again"); } }
        public static string Done { get { return Pick("בוצע", "Done"); } }

        public static string[] Themes
        {
            get
            {
                return Hebrew
                    ? new[] { "לפי המערכת", "כהה", "בהיר" }
                    : new[] { "Follow the system", "Dark", "Light" };
            }
        }

        public static string[] Accents
        {
            get
            {
                return Hebrew
                    ? new[] { "ירוק", "ענבר", "תכלת", "סגול", "אלמוג", "אפור" }
                    : new[] { "Green", "Amber", "Sky", "Violet", "Terracotta", "Slate" };
            }
        }

        public static string[] Addresses
        {
            get
            {
                return Hebrew
                    ? new[] { "לפי המילים", "זכר", "נקבה" }   // not "זיהוי אוטומטי"
                    : new[] { "From what you say", "Masculine", "Feminine" };
            }
        }

        public static string[] Orders
        {
            get
            {
                return Hebrew
                    ? new[] { "בדיוק כמו במקור", "מתחלף בין שיחות", "בערבוב" }
                    : new[] { "Exactly as in 1966", "A new start each visit", "Shuffled" };
            }
        }

        /*  A line under each setting saying what it does. Not decoration: a
            row that reads "סדר התשובות: בערבוב" and nothing else is a row
            nobody can make a decision about, which is the same as having no
            setting at all. */
        public static string[] OrderNotes
        {
            get
            {
                return Hebrew
                    ? new[] {
                        "לכל כלל יש כמה ניסוחים, והיא מתחילה תמיד מהראשון. אותו משפט יקבל את אותה תשובה גם מחר. כך כתב וייצנבאום, וכך ראה אותה אבידן ב-1973.",
                        "היא עוברת על הניסוחים לפי הסדר, אבל כל שיחה נכנסת לסבב בנקודה אחרת. בתוך השיחה היא לא חוזרת על עצמה, והתשובות אינן אותן תשובות שקיבלתם בפעם הקודמת.",
                        "היא בוחרת בסדר אקראי, כמו הגרסאות הנפוצות באינטרנט, אבל שלא כמותן עוברת על כל הניסוחים לפני שאחד מהם חוזר." }
                    : new[] {
                        "Each rule offers several phrasings and she always starts at the first. The same sentence gets the same answer tomorrow. This is what Weizenbaum wrote, and what Avidan met in 1973.",
                        "She still goes through the phrasings in order, but each conversation enters the rotation at a different point. No repetition within a conversation, and answers that are not the ones you got last time.",
                        "She chooses at random, as the common web versions do, but unlike them every phrasing is used before any of them comes round again." };
            }
        }

        public static string[] AddressNotes
        {
            get
            {
                return Hebrew
                    ? new[] {
                        "אלייזה מזהה מן המילים מי מדבר אליה, גבר או אישה, ומתקנת את לשון הפנייה תוך כדי השיחה, לשני הכיוונים. חל רק על תסריט שיש לו גרסה בלשון נקבה.",
                        "תמיד לשון זכר, בלי לנסות לזהות.",
                        "תמיד לשון נקבה, בלי לנסות לזהות." }
                    : new[] {
                        "ELIZA works out from your words whether a man or a woman is speaking, and corrects the way she addresses you as she goes, in either direction. Applies only to a script that has a feminine version.",
                        "Always masculine, with no attempt to tell who is speaking.",
                        "Always feminine, with no attempt to tell who is speaking." };
            }
        }

        public static string NoteLanguage
        {
            get
            {
                return Pick("שפת הכיתוב בחלון הזה. שפת השיחה נקבעת לפי התסריט, ואותו בוחרים במסך הפתיחה.",
                            "The language of this window. The conversation is in the language of its script, and the script is chosen on the opening screen.");
            }
        }

        public static string NoteTheme
        {
            get
            {
                return Pick("צבעי החלון הזה בלבד. סגנון המסך של השיחה נמצא כאן למטה.",
                            "The colours of this window only. The screen style of the conversation is below.");
            }
        }

        public static string NoteAccent
        {
            get
            {
                return Pick("צבע ההדגשות והקווים בחלון הזה.",
                            "The colour of the highlights and the lines in this window.");
            }
        }

        public static string NoteTyping
        {
            get
            {
                return Pick("מיידית, מהירה, או בקצב שבו טלפרינטר היה מדפיס את התשובה ב-1966.",
                            "Instant, fast, or the rate at which a teletype would have printed the answer in 1966.");
            }
        }

        public static string NoteSize
        {
            get
            {
                return Pick("גודל האותיות בחלון השיחה. אפשר לשנות גם משם, במקשי F7 ו-F8.",
                            "The size of the letters in the conversation window. F7 and F8 change it from there too.");
            }
        }

        public static string NoteKeep
        {
            get
            {
                return Pick("כשההגדרה פועלת, כל שיחה נשמרת כקובץ טקסט, ואפשר לקרוא אותה בעמוד \"תמלילים\".",
                            "When this is on, every conversation is saved as a text file, readable from the Transcripts page.");
            }
        }

        public static string NoteUpdates
        {
            get
            {
                return Pick("התוכנה אינה פונה לרשת בשום שלב אלא אם תבקשו זאת כאן.",
                            "The program never reaches the network unless you ask it to here.");
            }
        }

        public static string NoteFolder
        {
            get
            {
                return Pick("התסריטים הם קובצי טקסט רגילים. אפשר לפתוח אותם, לקרוא ולשנות. קובץ שנערך לא יידרס לעולם. לתסריט העברי יש גרסה בלשון נקבה בקובץ נפרד, ושינוי כאן אינו מגיע אליה.",
                            "The scripts are ordinary text files. Open, read and change them. An edited file is never overwritten. The Hebrew script has a feminine twin in a file of its own, and a change here does not reach it.");
            }
        }

        public static string NoteReset
        {
            get
            {
                return Pick("כל ההגדרות חוזרות לברירת המחדל. התמלילים והתסריטים נשארים.",
                            "Every setting goes back to its default. Transcripts and scripts are left alone.");
            }
        }

        /*  The note under the row is a statement; the dialog has to ask a
            question, and this one asked nothing at all -- it was the same
            sentence, with OK and Cancel under it. */
        public static string EraseAsk
        {
            get
            {
                return Pick("למחוק לצמיתות את ההגדרות, את התמלילים ואת התסריטים שהגיעו עם התוכנה? אין דרך חזרה, וזה לא עובר לסל המיחזור. התסריטים ייווצרו מחדש בהפעלה הבאה. תסריט שכתבתם בעצמכם יישאר.",
                            "Permanently delete the settings, the transcripts and the scripts that came with the program? There is no way back and nothing goes to the Recycle Bin. Those scripts come back on the next start. A script you wrote yourself is left alone.");
            }
        }

        public static string Erased
        {
            get
            {
                return Pick("נמחק. ההגדרות והתמלילים אינם עוד על המחשב הזה.",
                            "Erased. The settings and the transcripts are no longer on this computer.");
            }
        }

        public static string EraseLeft
        {
            get { return Pick("חלק מהקבצים לא נמחקו:", "Some files could not be deleted:"); }
        }

        public static string NoteErase
        {
            get
            {
                return Pick("מוחק לצמיתות, בלי סל מיחזור ובלי ביטול: ההגדרות, התמלילים והתסריטים שהגיעו עם התוכנה. אלה ייווצרו מחדש בהפעלה הבאה, כי הם שמורים בתוך קובץ התוכנה. תסריט שכתבתם בעצמכם אינו נמחק.",
                            "Deletes permanently, with no recycle bin and no undo: the settings, the transcripts and the scripts that came with the program. Those come back on the next start, because they live inside the executable itself. A script you wrote yourself is left alone.");
            }
        }

        public static string Updates { get { return Pick("בדיקת עדכונים", "Check for updates"); } }
        public static string Running { get { return Pick("הגרסה כאן:", "Running:"); } }
        public static string Newest { get { return Pick("הגרסה האחרונה", "The newest release"); } }
        public static string LookNow { get { return Pick("לבדוק עכשיו", "Look now"); } }
        public static string GetNow { get { return Pick("לעדכן עכשיו", "Update now"); } }

        public static string AskUpdate
        {
            get
            {
                return Pick("להוריד את הגרסה החדשה ולהחליף את התוכנה? היא תיסגר ותיפתח מחדש מעודכנת.",
                            "Download the new version and put it in place? The program closes and opens again updated.");
            }
        }

        public static string UpdateFailed
        {
            get
            {
                return Pick("העדכון לא הושלם, והתוכנה נשארה כפי שהייתה.",
                            "The update did not finish, and the program is as it was.");
            }
        }

        public static string NoteLooking
        {
            get
            {
                return Pick("עוד לא נבדק. אפשר לבדוק עכשיו, והתוכנה תשאל את גיטהאב מהי הגרסה האחרונה ולא תוריד דבר.",
                            "Not asked yet. Looking asks GitHub what the newest release is called, and downloads nothing.");
            }
        }

        public static string NoteNoAnswer
        {
            get
            {
                return Pick("לא התקבלה תשובה. ייתכן שאין חיבור לרשת, וזה בסדר גמור: התוכנה עובדת בלעדיו.",
                            "No answer. There may be no network, which is fine: the program does not need one.");
            }
        }

        public static string NoteCurrent
        {
            get
            {
                return Pick("זו הגרסה האחרונה.", "This is the newest release.");
            }
        }

        public static string NoteNewer
        {
            get
            {
                return Pick("יש גרסה חדשה יותר:", "There is a newer release:");
            }
        }

        public static string NewHere
        {
            get
            {
                return Pick("יש גרסה חדשה להורדה", "A newer version is ready to download");
            }
        }


        public static string[] Speeds
        {
            get
            {
                return Hebrew
                    ? new[] { "מיידית", "מהירה", "טלפרינטר" }
                    : new[] { "Instant", "Fast", "Teletype" };
            }
        }

        public static string On { get { return Pick("כן", "Yes"); } }
        public static string Off { get { return Pick("לא", "No"); } }

        public static string NoRepository
        {
            get
            {
                return Pick("לגרסה הזאת אין מקור עדכונים, והתוכנה אינה פונה לרשת.",
                            "This copy has nowhere to check, and nothing reaches the network.");
            }
        }
    }
}
