# One picture of one page, for looking at a change without running the whole
# set. Pass -Page home|about|transcripts|reading|settings, or -Talk 1|2|3 for
# the conversation window in one of the three screen styles.
#
# No Hebrew in this file: Windows PowerShell reads a BOM-less script in the
# system code page and would mangle it.

param([string]$Page = 'about', [string]$Out = 'build/out/shots/one.png',
      [string]$Language = 'he', [string]$Mode = 'dark', [int]$Screen = -1,
      [int]$W = 0, [int]$H = 0, [int]$Script = 2, [int]$Tab = 0)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

public static class One
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int t, bool repaint);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr RealChildWindowFromPoint(IntPtr h, POINT p);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    public delegate bool EnumProc(IntPtr h, IntPtr p);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

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

    public static void Resize(IntPtr h, int w, int t)
    {
        RECT r; GetWindowRect(h, out r);
        MoveWindow(h, r.L, r.T, w, t, true);
    }

    public static void Click(IntPtr h, int x, int y)
    {
        IntPtr at = (IntPtr)((y << 16) | (x & 0xFFFF));
        PostMessage(h, 0x0200, IntPtr.Zero, at);
        PostMessage(h, 0x0201, (IntPtr)1, at);
        PostMessage(h, 0x0202, IntPtr.Zero, at);
    }

    // Down to whichever real control is under the point, then post there.
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

    /*  Which control has the keyboard, in the other process. A key posted to
        the top-level window is processed by the FORM, not by whatever has the
        focus -- so Enter on a focused list row went to the form's dialog-key
        handling and never reached the row at all. */
    public static IntPtr Focused(IntPtr fallback)
    {
        uint theirs = GetWindowThreadProcessId(fallback, IntPtr.Zero);
        uint mine = GetCurrentThreadId();
        AttachThreadInput(mine, theirs, true);
        IntPtr who = GetFocus();
        AttachThreadInput(mine, theirs, false);
        return who == IntPtr.Zero ? fallback : who;
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
        RECT r; GetWindowRect(h, out r);
        int w = r.R - r.L, t = r.B - r.T;
        if (w < 1 || t < 1) throw new Exception("no window");
        using (var bmp = new Bitmap(w, t))
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

if (-not ('One' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[void][One]::SetProcessDPIAware()

$full = Join-Path (Get-Location) $Out
$dir = Split-Path $full
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

$file = Join-Path $env:LOCALAPPDATA 'Eliza\settings.txt'
$parent = Split-Path $file
if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force $parent | Out-Null }
$screenSetting = if ($Screen -ge 0) { $Screen } else { 1 }
Set-Content -Path $file -Encoding utf8 -Value @(
    "language = $Language", "mode = $Mode", 'accent = 0', "tab = $Tab",
    "screen = $screenSetting", 'phosphor = 0', 'speed = 0', 'visits = 3')

Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$exe = (Resolve-Path 'dist\ELIZA.exe').Path
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2400
$proc.Refresh()
$shell = $proc.MainWindowHandle
if ($shell -eq 0) { throw 'no shell window' }
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id

if ($W -gt 0 -and $H -gt 0) { [One]::Resize($shell, $W, $H); Start-Sleep -Milliseconds 700 }

$size = [One]::Size($shell)
$scale = [math]::Round($size[0] / 980.0, 3)
if ($scale -lt 0.5 -or $scale -gt 4) { $scale = 1 }
function Px([int]$v) { [int][math]::Round($v * $scale) }
$navX = if ($Language -eq 'he') { $size[0] - (Px 196) + (Px 80) } else { Px 80 }
# The nav gained a row: home, models, about, transcripts, settings,
# 46 apart starting at 88.
$rows = @{ home = (Px 88); models = (Px 134); about = (Px 180);
           transcripts = (Px 226); settings = (Px 272) }

if ($Page -eq 'talk') {
    [One]::Key($shell, 0x30 + $Script)
    Start-Sleep -Milliseconds 2200
    $win = [One]::Windows($tid) | Where-Object { $_ -ne $shell } | Select-Object -Last 1
    if (-not $win) { throw 'the conversation did not open' }
    if ($W -gt 0 -and $H -gt 0) { [One]::Resize($win, $W, $H); Start-Sleep -Milliseconds 800 }
    Start-Sleep -Milliseconds 600
    [One]::Save($win, $full)
} else {
    if ($rows.ContainsKey($Page)) {
        [One]::Click($shell, $navX, $rows[$Page])
        Start-Sleep -Milliseconds 900
    }
    if ($Page -eq 'reading') {
        [One]::Click($shell, $navX, $rows['transcripts'])
        Start-Sleep -Milliseconds 900
        # A posted click does not raise Click in the other process while the
        # real pointer is somewhere else, so the row is opened from the
        # keyboard: Tab onto the first one, then Enter.
        [One]::Key($shell, 0x09)
        Start-Sleep -Milliseconds 500
        [One]::Key([One]::Focused($shell), 0x0D)
        Start-Sleep -Milliseconds 1400
    }
    [One]::Save($shell, $full)
}

Write-Output ("window " + $size[0] + "x" + $size[1] + "  scale " + $scale)
Write-Output $full
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
