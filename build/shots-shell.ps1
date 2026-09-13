# Photographs the shell's settings page and the conversation in each of its
# three screen styles.
#
# Clicks are posted to the window rather than performed with the real mouse, so
# nothing is taken away from whatever the user is doing at the time. Hover
# highlights therefore do not appear in these pictures: WM_MOUSEMOVE is posted,
# but Control.MousePosition still reports where the real pointer is.
#
# No Hebrew in this file on purpose: Windows PowerShell reads a BOM-less script
# in the system code page and would mangle it.

param(
    [string]$OutDir = 'build/out/shots',
    [string]$Mode = 'system',
    [int]$Accent = 0,
    [int]$ShowMore = 1,
    [string]$Language = 'he'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

public static class Shot2
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);

    public delegate bool EnumProc(IntPtr h, IntPtr p);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int L, T, R, B; }

    public static List<IntPtr> Windows(uint tid)
    {
        var found = new List<IntPtr>();
        EnumThreadWindows(tid, delegate(IntPtr h, IntPtr p) {
            if (IsWindowVisible(h)) found.Add(h);
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static int[] Size(IntPtr h)
    {
        RECT r; GetWindowRect(h, out r);
        return new int[] { r.R - r.L, r.B - r.T };
    }

    public static IntPtr FindEdit(IntPtr parent)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, delegate(IntPtr h, IntPtr p) {
            var name = new StringBuilder(256);
            GetClassName(h, name, name.Capacity);
            // WinForms renames the class: WindowsForms10.EDIT.app.0.xxxx
            if (name.ToString().IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0)
            { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Click(IntPtr h, int x, int y)
    {
        IntPtr at = (IntPtr)((y << 16) | (x & 0xFFFF));
        PostMessage(h, 0x0200, IntPtr.Zero, at);          // WM_MOUSEMOVE
        PostMessage(h, 0x0201, (IntPtr)1, at);            // WM_LBUTTONDOWN
        PostMessage(h, 0x0202, IntPtr.Zero, at);          // WM_LBUTTONUP
    }

    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    /*  Which control has the keyboard, in the other process. GetFocus only
        answers for the calling thread's own queue, so the two have to be
        joined for the length of the question. */
    public static IntPtr Focused()
    {
        IntPtr top = GetForegroundWindow();
        uint theirs = GetWindowThreadProcessId(top, IntPtr.Zero);
        uint mine = GetCurrentThreadId();
        AttachThreadInput(mine, theirs, true);
        IntPtr who = GetFocus();
        AttachThreadInput(mine, theirs, false);
        return who == IntPtr.Zero ? top : who;
    }

    [DllImport("user32.dll")] static extern IntPtr RealChildWindowFromPoint(IntPtr h, POINT p);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    /*  Click at a point in the window, whichever control is actually there.

        The pages are real child controls now, each with its own window, so a
        message posted to the top-level window never reaches them -- which is
        why clicking a settings tab appeared to do nothing at all. Walk down to
        the control under the point and post to that. */
    public static void ClickDeep(IntPtr top, int x, int y)
    {
        var p = new POINT { X = x, Y = y };
        IntPtr target = top;
        for (int depth = 0; depth < 8; depth++)
        {
            IntPtr child = RealChildWindowFromPoint(target, p);
            if (child == IntPtr.Zero || child == target) break;
            var screen = p;
            ClientToScreen(target, ref screen);
            var local = screen;
            ScreenToClient(child, ref local);
            target = child;
            p = local;
        }
        Click(target, p.X, p.Y);
    }

    public static void Key(IntPtr h, int vk)
    {
        PostMessage(h, 0x0100, (IntPtr)vk, IntPtr.Zero);
        PostMessage(h, 0x0101, (IntPtr)vk, IntPtr.Zero);
    }

    public static void Type(IntPtr h, string text)
    {
        foreach (char c in text) PostMessageW(h, 0x0102, (IntPtr)c, IntPtr.Zero);
    }

    public static void Save(IntPtr h, string path)
    {
        RECT r;
        GetWindowRect(h, out r);
        int w = r.R - r.L, ht = r.B - r.T;
        if (w < 1 || ht < 1) throw new Exception("no window");
        using (var bmp = new Bitmap(w, ht))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr dc = g.GetHdc();
                try { PrintWindow(h, dc, 2); } finally { g.ReleaseHdc(dc); }
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
'@

if (-not ('Shot2' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[void][Shot2]::SetProcessDPIAware()

$out = Join-Path (Get-Location) $OutDir
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Force $out | Out-Null }

# Start from a known state.
$file = Join-Path $env:LOCALAPPDATA 'Eliza\settings.txt'
$parent = Split-Path $file
if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force $parent | Out-Null }
$lines = @(
    "language = $Language",
    "mode = $Mode",
    "accent = $Accent",
    "showmore = $ShowMore",
    "screen = 1",
    "phosphor = 0",
    "speed = 0",
    "visits = 3"
)
Set-Content -Path $file -Value $lines -Encoding utf8

Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$exe = (Resolve-Path 'dist\ELIZA.exe').Path
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2600
$proc.Refresh()
$shell = $proc.MainWindowHandle
if ($shell -eq 0) { throw 'the shell has no window' }
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id

$size = [Shot2]::Size($shell)
$w = $size[0]
Write-Output ("window " + $size[0] + "x" + $size[1])

# The nav strip is 196 wide at 96 DPI, on the right in Hebrew, with rows 68
# down and 46 apart. Everything in the window is scaled by the screen's DPI,
# and the window itself is 980 wide before scaling -- so the width it actually
# came out at is the scale factor, and clicking without working that out lands
# one row off, which is exactly what happened the first time.
$scale = [math]::Round($w / 980.0, 3)
if ($scale -lt 0.5 -or $scale -gt 4) { $scale = 1 }
Write-Output ("scale " + $scale)
# "Sc" would be the built-in alias for Set-Content; this one has to be its
# own word or the call is parsed as a cmdlet with no -Value.
function Px([int]$v) { [int][math]::Round($v * $scale) }
$navX = if ($Language -eq 'he') { $w - (Px 196) + (Px 80) } else { Px 80 }
# The nav gained a row: home, models, about, transcripts, settings,
# 46 apart starting at 88.
$rows = @{ home = (Px 88); models = (Px 134); about = (Px 180);
           transcripts = (Px 226); settings = (Px 272) }

[Shot2]::Save($shell, (Join-Path $out 'a-home.png'))
Write-Output '  a-home.png'

[Shot2]::Click($shell, $navX, $rows.settings)
Start-Sleep -Milliseconds 700
[Shot2]::Save($shell, (Join-Path $out 'b-settings.png'))
Write-Output '  b-settings.png'

# The other two settings tabs. The tab strip sits under the page title; these
# coordinates were measured off the picture and divided back to unscaled units.
# The tabs are real child controls in another process now. A posted click
# reaches them, but WinForms will not raise Click while the real cursor is
# somewhere else, and a posted key goes to whatever the other thread thinks has
# focus. Rather than fight that, the page remembers which tab was last open --
# which it should do anyway -- so each one can be photographed by starting the
# program on it.
$shots = @('b2-display.png', 'b3-talking.png')
for ($t = 1; $t -le 2; $t++) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Set-Content -Path $file -Value ($lines + ("tab = " + $t)) -Encoding utf8
    $proc = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Milliseconds 2400
    $proc.Refresh()
    $shell = $proc.MainWindowHandle
    $tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id
    [Shot2]::Click($shell, $navX, $rows.settings)
    Start-Sleep -Milliseconds 800
    [Shot2]::Save($shell, (Join-Path $out $shots[$t - 1]))
    Write-Output ('  ' + $shots[$t - 1])
}

# Back to the first tab for the rest of the run.
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
Set-Content -Path $file -Value ($lines + "tab = 0") -Encoding utf8
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2400
$proc.Refresh()
$shell = $proc.MainWindowHandle
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id



# The bottom of the program tab, where the destructive actions live.
[Shot2]::ClickDeep($shell, (Px 700), (Px 140))
Start-Sleep -Milliseconds 400
[Shot2]::Key($shell, 0x23)          # VK_END
Start-Sleep -Milliseconds 500
[Shot2]::Save($shell, (Join-Path $out 'c-settings-more.png'))
Write-Output '  c-settings-more.png'

# And once in the light theme, which nothing else in this run shows.
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
Set-Content -Path $file -Value (($lines -replace '^mode = .*$', 'mode = light') + "tab = 1") -Encoding utf8
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2400
$proc.Refresh()
$shell = $proc.MainWindowHandle
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id
[Shot2]::Click($shell, $navX, $rows.settings)
Start-Sleep -Milliseconds 800
[Shot2]::Save($shell, (Join-Path $out 'b4-light.png'))
Write-Output '  b4-light.png'

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
Set-Content -Path $file -Value ($lines + "tab = 0") -Encoding utf8
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2400
$proc.Refresh()
$shell = $proc.MainWindowHandle
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id

[Shot2]::Click($shell, $navX, $rows.transcripts)
Start-Sleep -Milliseconds 700
[Shot2]::Save($shell, (Join-Path $out 'c2-transcripts.png'))
Write-Output '  c2-transcripts.png'

# And one open, which is where the old grey scrollbar was worst.
[Shot2]::ClickDeep($shell, (Px 400), (Px 180))
Start-Sleep -Milliseconds 800
[Shot2]::Save($shell, (Join-Path $out 'c3-reading.png'))
Write-Output '  c3-reading.png'

[Shot2]::Click($shell, $navX, $rows.about)
Start-Sleep -Milliseconds 700
[Shot2]::Save($shell, (Join-Path $out 'd-about.png'))
Write-Output '  d-about.png'

# Scrolled, to prove the reading does not paint over the chapter row. This is
# the check that matters: the shapes were always clipped correctly and only the
# words came through, so nothing but a picture shows it.
for ($i = 0; $i -lt 4; $i++) {
    [Shot2]::Key($shell, 0x22)
    Start-Sleep -Milliseconds 150
}
Start-Sleep -Milliseconds 400
[Shot2]::Save($shell, (Join-Path $out 'd1-about-scrolled.png'))
Write-Output '  d1-about-scrolled.png'

# The chapter chips, measured off the picture above and divided back down to
# unscaled units so this works on any screen.
[Shot2]::ClickDeep($shell, (Px 273), (Px 86))
Start-Sleep -Milliseconds 600
[Shot2]::Save($shell, (Join-Path $out 'd2-avidan.png'))
Write-Output '  d2-avidan.png'

[Shot2]::ClickDeep($shell, (Px 707), (Px 124))
Start-Sleep -Milliseconds 600
[Shot2]::Save($shell, (Join-Path $out 'd3-reading.png'))
Write-Output '  d3-reading.png'

# Into a conversation, and round the three screen styles.
[Shot2]::Click($shell, $navX, $rows.home)
Start-Sleep -Milliseconds 600
[Shot2]::Key($shell, 0x32)          # '2', the Hebrew script
Start-Sleep -Milliseconds 2400

$terminal = [Shot2]::Windows($tid) | Where-Object { $_ -ne $shell } | Select-Object -Last 1
if (-not $terminal) { throw 'the conversation did not open' }
$edit = [Shot2]::FindEdit($terminal)
if ($edit -eq 0) { throw 'no input box in the conversation' }

foreach ($line in @([char]0x05D4 + [char]0x05DB + [char]0x05DC)) {
    [Shot2]::Type($edit, $line)
    [Shot2]::Key($edit, 0x0D)
    Start-Sleep -Milliseconds 900
}

$names = @('e-machine.png', 'f-museum.png', 'g-terminal.png')
foreach ($name in $names) {
    Start-Sleep -Milliseconds 500
    [Shot2]::Save($terminal, (Join-Path $out $name))
    Write-Output ('  ' + $name)
    [Shot2]::Key($edit, 0x73)       # F4, next style
    Start-Sleep -Milliseconds 500
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
