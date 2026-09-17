// Program.cs -- entry point, and where the scripts come from.
//
// Everything the program needs is compiled into the executable. On first run
// the scripts are also written out as plain text so they can be read and
// edited: Weizenbaum's whole point was that the script is data.
//
// If a "scripts" folder sits next to the .exe it wins, so the program can be
// carried on a stick and edited in place.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ElizaApp
{
    class ScriptEntry
    {
        public string Name = "";
        public string Text = "";
        public bool RightToLeft;
        public string Path = "";

        // From the script's own ABOUT, SCENE and FAMILY directives.
        public string About = "";
        public string Scene = "";
        public string Family = "";

        // Whether this is ELIZA herself or one of the models built on her.
        public bool IsModel
        {
            get { return string.Equals(Family, "MODEL", StringComparison.OrdinalIgnoreCase); }
        }

        /*  The same script written for a woman. A file called X.f.txt is the
            feminine counterpart of X.txt; it is not offered as a choice of its
            own, because it is not a different conversation, only a different
            person being spoken to. */
        public ScriptEntry Feminine;
        public string Designation = "";
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        /*  One list, in Settings, because "erase everything" has to know the
            same names and two copies of a list like this fall out of step the
            first time a script is added. */
        static string[] Embedded { get { return Settings.Shipped; } }
        static string[] Retired { get { return Settings.Retired; } }

        /*  A conversation named on the command line, waiting for a window
            to show it in. The shell picks it up once it is on its feet. */
        public static string Opening;

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }

            /*  One argument, and it has to be a file that is there. This is
                what makes "open with" work without writing a file type into
                somebody’s registry, which a program that runs off a stick
                should not be doing. Anything else is ignored in silence:
                a program started with an argument it does not understand
                should still be the program. */
            try
            {
                if (args != null && args.Length == 1 &&
                    args[0].EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(args[0]))
                    Opening = args[0];
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            /*  Nothing here should ever be able to take the window away.

                The scripts are text files, and the settings page has a button
                that opens the folder they are in and invites a person to edit
                them. That invitation is the point -- the script being data a
                person can read is the whole idea of the program -- but it
                means the program is handed input it did not write, by someone
                who is allowed to get it wrong. A stray bracket must produce a
                sentence, not the .NET crash dialog.

                The parser is forgiving and the paths that load a script are
                already guarded one by one; this is the net under all of them,
                because the guard that matters is the one for the mistake
                nobody thought of. */
            Application.ThreadException += (s, e) => Complain(e.Exception);
            AppDomain.CurrentDomain.UnhandledException +=
                (s, e) => Complain(e.ExceptionObject as Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            /*  The settings before the scripts, which is the other way
                round from how it was.

                These three messages are the only thing a person sees when
                the program cannot start, and they were the only three in the
                program that existed in English alone -- in a program whose
                default language is Hebrew. Loading the settings has no
                dependency on the scripts; it only has to happen before
                anything speaks. */
            var settings = Settings.Load();
            Say.Hebrew = settings.Hebrew;

            List<ScriptEntry> scripts;
            try { scripts = LoadScripts(); }
            catch (Exception ex)
            {
                MessageBox.Show(
                    (Say.Hebrew ? "התסריטים של אלייזה לא נטענו:"
                                : "Could not load the ELIZA scripts:") +
                    Environment.NewLine + Environment.NewLine + ex.Message,
                    "ELIZA", MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1, ShellForm.Reading(Say.Hebrew));
                return;
            }

            if (scripts.Count == 0)
            {
                MessageBox.Show(
                    Say.Hebrew ? "לא נמצא שום תסריט של אלייזה."
                               : "No ELIZA script was found.",
                    "ELIZA", MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1, ShellForm.Reading(Say.Hebrew));
                return;
            }

            // The last script chosen is the one the shell opens on.
            int start = scripts.FindIndex(x => x.Name == settings.Script);
            if (start < 0) start = 0;
            settings.Script = scripts[start].Name;

            Application.Run(new ShellForm(scripts, settings));
        }

        /*  What went wrong, in a window a person can read, and then carry on.

            Not a stack trace: the people this program is for did not ask for
            one and could not use it. The message says which file, because on
            the one occasion this fires the file is almost always a script
            somebody has just edited.

            The stack does go to a file, though, and the message says which.
            One sentence of .NET's own wording -- "the parameter is not
            valid" -- names neither the window it came from nor the line, and
            without the stack behind it a person who meets this can report
            only that something went wrong somewhere. */
        static void Complain(Exception ex)
        {
            if (ex == null) return;

            string note = null;
            try
            {
                note = Path.Combine(Settings.Folder, "..", "error.txt");
                note = Path.GetFullPath(note);
                File.AppendAllText(note, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    Environment.NewLine + ex + Environment.NewLine +
                    "----" + Environment.NewLine);
            }
            catch { note = null; }

            try
            {
                string where = Settings.Folder;
                string said = Say.Hebrew
                    ? "אלייזה נתקלה במשהו לא צפוי:" +
                      Environment.NewLine + Environment.NewLine + ex.Message +
                      Environment.NewLine + Environment.NewLine +
                      "אם ערכתם תסריט, זה המקום הראשון לבדוק. התסריטים נמצאים ב:" +
                      Environment.NewLine + where +
                      Environment.NewLine + Environment.NewLine +
                      "מחיקת תסריט שנערך מחזירה את זה שהגיע עם התוכנה."
                    : "ELIZA ran into something it did not expect:" +
                      Environment.NewLine + Environment.NewLine + ex.Message +
                      Environment.NewLine + Environment.NewLine +
                      "If you have edited a script, that is the first place to " +
                      "look. The scripts are in:" + Environment.NewLine + where +
                      Environment.NewLine + Environment.NewLine +
                      "Deleting an edited script brings back the one that came " +
                      "with the program.";
                if (note != null)
                    said += Environment.NewLine + Environment.NewLine +
                        (Say.Hebrew ? "הפרטים המלאים נשמרו ב:" : "The full detail was saved in:") +
                        Environment.NewLine + note;
                MessageBox.Show(said, "ELIZA", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                    ShellForm.Reading(Say.Hebrew));
            }
            catch { }
        }

        static string ScriptFolder()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string portable = Path.Combine(exeDir, "scripts");
            if (Directory.Exists(portable) && Directory.GetFiles(portable, "*.txt").Length > 0)
                return portable;

            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eliza", "scripts");
            Directory.CreateDirectory(appData);

            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in Retired) Retire(appData, name);
            foreach (var name in Embedded) Unpack(asm, appData, name);
            return appData;
        }

        /*  Write a script out, and remember what was written.

            Unpacking only when the file is missing would be simpler, and wrong:
            after the first run the copy on disk would be frozen for good, and a
            later version of the program would keep answering from the old
            script. So the text as unpacked is stamped, and on the next run the
            file is refreshed only if it still matches its stamp -- meaning
            nobody has edited it. An edited script is never overwritten. */
        static void Unpack(Assembly asm, string folder, string name)
        {
            string text = ReadResource(asm, name);
            if (text == null) return;

            string target = Path.Combine(folder, name);
            string stampFile = Path.Combine(folder, "." + name + ".stamp");
            string stamp = Stamp(text);

            if (File.Exists(target))
            {
                string onDisk;
                try { onDisk = File.ReadAllText(target, Encoding.UTF8); }
                catch { return; }

                if (Stamp(onDisk) == stamp) return;          // already current

                string previous = null;
                try { if (File.Exists(stampFile)) previous = File.ReadAllText(stampFile).Trim(); }
                catch { }

                // Stamped, and the stamp does not match: someone has been
                // editing. Leave it alone.
                if (previous != null && previous != Stamp(onDisk)) return;

                // Unstamped, so written by a version that kept no record. It is
                // almost certainly ours, but keep a copy rather than assume.
                if (previous == null)
                {
                    string backup = target + ".bak";
                    try { if (!File.Exists(backup)) File.Copy(target, backup); }
                    catch { return; }
                }
            }

            try
            {
                File.WriteAllText(target, text, new UTF8Encoding(true));

                /*  Take the hidden bit off before writing over it.

                    WriteAllText throws on a file that is already hidden, and
                    the throw was swallowed -- so the stamp was written once,
                    on the first run, and never again. After that every update
                    found a stamp that did not match the file it had just
                    replaced, decided somebody had been editing the script, and
                    left it alone. The scripts stopped refreshing altogether,
                    permanently and silently, and the only sign was a script
                    that would not change. */
                try
                {
                    if (File.Exists(stampFile))
                        File.SetAttributes(stampFile, FileAttributes.Normal);
                }
                catch { }

                File.WriteAllText(stampFile, stamp, Encoding.ASCII);
                try
                {
                    var hidden = new FileInfo(stampFile);
                    hidden.Attributes |= FileAttributes.Hidden;
                }
                catch { }
            }
            catch { }
        }

        /*  A script that used to ship and no longer does.

            The scripts live in a folder beside the settings and are written
            out of the executable on the first run. Rename one and the old
            copy stays where it is, so the program offers the same character
            twice under two names -- and the one nobody maintains any more is
            the one somebody talks to.

            Only a file this program wrote and nobody has touched since is
            removed: the stamp beside it says what was written, and if the
            file no longer matches it, somebody has been editing and it stays
            where it is. */
        static void Retire(string folder, string name)
        {
            string target = Path.Combine(folder, name);
            string stampFile = Path.Combine(folder, "." + name + ".stamp");
            if (!File.Exists(target)) return;
            try
            {
                if (!File.Exists(stampFile)) return;
                string previous = File.ReadAllText(stampFile).Trim();
                string onDisk = File.ReadAllText(target, Encoding.UTF8);
                if (previous != Stamp(onDisk)) return;       // edited: leave it

                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
                File.SetAttributes(stampFile, FileAttributes.Normal);
                File.Delete(stampFile);
            }
            catch { }
        }

        static string Stamp(string text)
        {
            // Line endings differ between what we write and what an editor saves,
            // and are not a change to the script.
            string normalised = text.Replace("\r\n", "\n").TrimStart('﻿');
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalised));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static string ReadResource(Assembly asm, string name)
        {
            using (var stream = asm.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }
        }

        static List<ScriptEntry> LoadScripts()
        {
            var folder = ScriptFolder();
            var result = new List<ScriptEntry>();
            var asm = Assembly.GetExecutingAssembly();

            // Keep the shipped order: the 1966 original first.
            var files = new List<string>();
            foreach (var name in Embedded)
            {
                string p = Path.Combine(folder, name);
                if (File.Exists(p)) files.Add(p);
            }
            foreach (var p in Directory.GetFiles(folder, "*.txt").OrderBy(x => x))
                if (!files.Contains(p)) files.Add(p);

            foreach (var path in files)
            {
                string text;
                try { text = File.ReadAllText(path, Encoding.UTF8); }
                catch { continue; }
                result.Add(Describe(text, path));
            }

            // Fold every X.f.txt into the X.txt it belongs to.
            var variants = result
                .Where(e => e.Path.EndsWith(".f.txt", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var variant in variants)
            {
                string stem = variant.Path.Substring(0, variant.Path.Length - 6) + ".txt";
                var owner = result.FirstOrDefault(e =>
                    string.Equals(e.Path, stem, StringComparison.OrdinalIgnoreCase));
                if (owner != null) owner.Feminine = variant;
                result.Remove(variant);
            }

            // Last resort: run straight from the embedded copies.
            if (result.Count == 0)
                foreach (var name in Embedded)
                {
                    string text = ReadResource(asm, name);
                    if (text != null) result.Add(Describe(text, name));
                }

            return result;
        }

        static ScriptEntry Describe(string text, string path)
        {
            var parsed = ElizaScript.Parse(text);
            return new ScriptEntry
            {
                Text = text,
                Path = path,
                RightToLeft = parsed.RightToLeft,

                /*  Weizenbaum's own file is a verbatim transcription of the
                    1966 paper and nothing is added to it, not even a name. So
                    it has none, and the shell offers it under the same
                    invitation as the Hebrew one, with DOCTOR kept underneath
                    as what it actually is. */
                Name = parsed.Name,
                About = parsed.About,
                Scene = parsed.Scene,
                Family = parsed.Family,
                Designation = Path.GetFileNameWithoutExtension(path).ToUpperInvariant()
            };
        }
    }
}
