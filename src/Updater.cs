// Updater.cs -- the one thing in this program that touches the network.
//
// It is off until somebody turns it on, and when it is on it does exactly
// one thing: asks GitHub what the newest release is called. It downloads
// nothing, installs nothing, and sends nothing about the machine it is
// running on. If a newer version exists the program says so and offers to
// open the page in a browser, and the person does the rest.
//
// That is a deliberate shape. This program is meant to be carried on a
// stick to a machine with no network at all, and a startup that quietly
// reaches out would break that promise without anyone noticing.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace ElizaApp
{
    static class Updater
    {
        /*  What GitHub answered, once it has answered. Written on a worker
            thread and read on the interface thread, which is why they are
            volatile: without it the interface thread is entitled to go on
            reading a cached null for as long as it likes. */
        public static volatile string Newest;     // the tag, e.g. "v1.1"
        public static volatile string Where;      // the page to open
        public static volatile string Trouble;    // why it could not be asked

        public const string Version = "1.0";

        /*  Ask, on a thread of its own.

            Nothing here blocks the window: a machine with no network takes
            the full timeout to find that out, and eight seconds of a frozen
            program to learn something nobody asked about out loud is not a
            trade worth making. */
        public static void Ask(string repository, Action done)
        {
            if (repository == null || repository.Length == 0) return;
            var thread = new Thread(delegate ()
            {
                try { Look(repository); }
                catch (Exception ex) { Trouble = ex.Message; }
                if (done != null) done();
            });
            thread.IsBackground = true;
            thread.Start();
        }

        static void Look(string repository)
        {
            /*  .NET 4 asks for TLS 1.0 by default and GitHub has not accepted
                that since 2018, so without this line the answer is always the
                same handshake failure. The value is written as a number
                because the name TLS12 does not exist in this framework. */
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
            catch { }

            var request = (HttpWebRequest)WebRequest.Create(
                "https://api.github.com/repos/" + repository + "/releases/latest");
            // GitHub refuses a request with no user agent, and says so plainly.
            request.UserAgent = "ELIZA/" + Version;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 8000;
            request.ReadWriteTimeout = 8000;

            string json;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                json = reader.ReadToEnd();

            string tag = Field(json, "tag_name");
            string page = Field(json, "html_url");
            if (tag.Length == 0) return;

            Newest = tag;
            Where = page.Length > 0 ? page
                                    : "https://github.com/" + repository + "/releases/latest";
            Asset = Attached(json);
        }

        /*  The file to fetch, out of the release's list of attachments.

            Only a .exe, and only one: this program is a single executable and
            a release that carries anything else is a release this version was
            not built to install. If there is no such attachment the program
            says there is a new version and offers the page, which is what it
            did before it could update itself. */
        public static volatile string Asset;

        static string Attached(string json)
        {
            int at = json.IndexOf("\"assets\"", StringComparison.Ordinal);
            if (at < 0) return null;
            string rest = json.Substring(at);
            int found = 0;
            while (true)
            {
                found = rest.IndexOf("\"browser_download_url\"", found, StringComparison.Ordinal);
                if (found < 0) return null;
                string url = Field(rest.Substring(found), "browser_download_url");
                if (url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return url;
                found += 22;
            }
        }

        /*  Fetch it, on a thread, telling the caller how far along it is.

            The file lands beside the running program rather than in the
            temporary folder: the swap that follows is a rename, and a rename
            across two volumes is a copy, which is exactly the thing that
            cannot be done while the file is in use. */
        public static void Fetch(string url, string into, Action<int> along,
                                 Action<string> done)
        {
            var thread = new Thread(delegate ()
            {
                string trouble = null;
                try { Pull(url, into, along); }
                catch (Exception ex) { trouble = ex.Message; }
                if (done != null) done(trouble);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        static void Pull(string url, string into, Action<int> along)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
            catch { }

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "ELIZA/" + Version;
            request.Timeout = 20000;
            request.ReadWriteTimeout = 60000;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var from = response.GetResponseStream())
            using (var file = new FileStream(into, FileMode.Create, FileAccess.Write))
            {
                long total = response.ContentLength, had = 0;
                var buffer = new byte[64 * 1024];
                int got;
                while ((got = from.Read(buffer, 0, buffer.Length)) > 0)
                {
                    file.Write(buffer, 0, got);
                    had += got;
                    if (along != null && total > 0)
                        along((int)(had * 100 / total));
                }
            }

            /*  And it has to be a program. A proxy that answers every request
                with a sign-in page would otherwise be installed over the top
                of this one, and the next thing anybody would see is a window
                that does not open. */
            var head = new byte[2];
            using (var check = File.OpenRead(into))
                if (check.Read(head, 0, 2) != 2 || head[0] != (byte)'M' || head[1] != (byte)'Z')
                    throw new InvalidDataException("what came back is not a program");
            if (new FileInfo(into).Length < 200 * 1024)
                throw new InvalidDataException("what came back is too small to be this program");
        }

        /*  Put the new one in the old one's place, and start it.

            Windows will not let a running program be overwritten, and it will
            let it be renamed -- so the old one is moved aside and the new one
            takes the name. The batch waits for this process to be gone before
            it touches anything, because a rename while the file is mapped
            fails and the failure looks exactly like success from here.

            Written as a batch rather than done here for the obvious reason:
            the program cannot replace itself while it is the thing running. */
        /*  A line in a log beside the program.

            A swap that fails looks exactly like a swap that worked: the
            program closes, and it opens again in the version it was. Without
            somewhere to write down which of the two happened there is nothing
            to look at afterwards. */
        public static void Note(string what)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "eliza-update.log"),
                    DateTime.Now.ToString("HH:mm:ss") + "  " + what + Environment.NewLine);
            }
            catch { }
        }

        public static string Swap(string fetched)
        {
            try
            {
                /*  Every path in short form, wherever Windows has one.

                    A batch file is read by cmd in the console's code page,
                    and this machine's own home folder is spelled in Hebrew.
                    Written in one code page and read in another, every path
                    in the script came out as something that does not exist,
                    and the swap failed in silence -- on the machine of the
                    person it was written for. An 8.3 name is ASCII whatever
                    the folder is called. The file is written as UTF-8 with no
                    mark and chcp 65001 in front of it as well, for the
                    volumes where short names are turned off. */
                string mine = Short(System.Reflection.Assembly.GetExecutingAssembly().Location);
                fetched = Short(fetched);
                string old = mine + ".old";
                int who = System.Diagnostics.Process.GetCurrentProcess().Id;
                string bat = Path.Combine(Path.GetTempPath(), "eliza-update.cmd");

                var script = new StringBuilder();
                script.AppendLine("@echo off");
                script.AppendLine("chcp 65001 >nul");
                script.AppendLine("powershell -NoProfile -ExecutionPolicy Bypass -Command " +
                    "\"$ErrorActionPreference='SilentlyContinue';" +
                    "for($i=0;$i -lt 60 -and (Get-Process -Id " + who + ");$i++)" +
                    "{Start-Sleep -Milliseconds 250}\"");
                script.AppendLine("ping -n 2 127.0.0.1 >nul");
                string log = Short(Path.GetTempPath()) + "eliza-update.log";
                script.AppendLine("echo the batch woke up >> \"" + log + "\"");
                script.AppendLine("del \"" + old + "\" >nul 2>&1");
                script.AppendLine("move /y \"" + mine + "\" \"" + old + "\" >nul");
                script.AppendLine("if errorlevel 1 (echo could not move the old one aside >> \"" + log + "\" & goto sorry)");
                script.AppendLine("move /y \"" + fetched + "\" \"" + mine + "\" >nul");
                script.AppendLine("if errorlevel 1 (echo could not move the new one in >> \"" + log + "\" & goto putback)");
                script.AppendLine("echo swapped >> \"" + log + "\"");
                script.AppendLine("start \"\" \"" + mine + "\"");
                script.AppendLine("ping -n 3 127.0.0.1 >nul");
                script.AppendLine("del \"" + old + "\" >nul 2>&1");
                script.AppendLine("del \"%~f0\"");
                script.AppendLine("exit /b 0");
                script.AppendLine(":putback");
                script.AppendLine("move /y \"" + old + "\" \"" + mine + "\" >nul");
                script.AppendLine(":sorry");
                script.AppendLine("start \"\" \"" + mine + "\"");
                script.AppendLine("del \"%~f0\"");

                File.WriteAllText(bat, script.ToString(), new UTF8Encoding(false));
                bat = Short(bat);

                var go = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                go.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                go.CreateNoWindow = true;
                go.UseShellExecute = false;
                System.Diagnostics.Process.Start(go);
                Note("batch started: " + bat);
                return null;
            }
            catch (Exception ex) { Note("swap failed: " + ex.Message); return ex.Message; }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet =
            System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int GetShortPathNameW(string from, StringBuilder into, int size);

        /*  The 8.3 name, when the volume has one. Only for a path that
            exists: Windows has nothing to shorten otherwise. */
        static string Short(string path)
        {
            try
            {
                var got = new StringBuilder(600);
                int n = GetShortPathNameW(path, got, got.Capacity);
                if (n > 0 && n < got.Capacity) return got.ToString();
            }
            catch { }
            return path;
        }

        /*  One field out of the answer, without a JSON library.

            The release description is free text and can hold anything,
            quotation marks included, so this reads the value a character at a
            time and honours the backslash rather than looking for the next
            quotation mark and hoping. */
        static string Field(string json, string name)
        {
            string key = "\"" + name + "\"";
            int at = json.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return "";
            at = json.IndexOf('"', at + key.Length + 1);
            if (at < 0) return "";
            var value = new StringBuilder();
            for (int i = at + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length) { value.Append(json[++i]); continue; }
                if (c == '"') break;
                value.Append(c);
            }
            return value.ToString();
        }

        /*  Is what GitHub has newer than what is running?

            Tags are compared number by number, so that 1.10 is after 1.9 --
            which comparing the text would get backwards. A leading v is
            dropped, and anything that is not a number ends the comparison.
        */
        public static bool Newer(string tag, string running)
        {
            int[] there = Numbers(tag), here = Numbers(running);
            for (int i = 0; i < Math.Max(there.Length, here.Length); i++)
            {
                int a = i < there.Length ? there[i] : 0;
                int b = i < here.Length ? here[i] : 0;
                if (a != b) return a > b;
            }
            return false;
        }

        static int[] Numbers(string tag)
        {
            if (tag == null) return new int[0];
            var parts = new System.Collections.Generic.List<int>();
            var digits = new StringBuilder();
            foreach (char c in tag)
            {
                if (c >= '0' && c <= '9') { digits.Append(c); continue; }
                if (digits.Length > 0) { parts.Add(int.Parse(digits.ToString())); digits.Length = 0; }
                if (c != '.' && c != 'v' && c != 'V') break;
            }
            if (digits.Length > 0) parts.Add(int.Parse(digits.ToString()));
            return parts.ToArray();
        }
    }
}
