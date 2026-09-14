# Does the window show a wrong picture while it changes page?
#
# The report was "switching between tabs shows a garbled screen for a second
# or two, and sometimes it stays garbled". A page is rebuilt by disposing
# every control on it and making new ones, inside a panel that has
# WS_EX_COMPOSITED -- which defers painting. So this takes a picture very soon
# after the page changes and another once everything has settled, and counts
# how far apart they are. A correct rebuild shows the new page immediately:
# the two pictures should be identical.
#
# Two ways of taking the picture, and only one of them can see the fault.
# PrintWindow asks the window to draw itself again, so it shows what the
# window WOULD paint, not what is on the screen -- a half-built page shows up,
# a stale one does not. -FromScreen copies the desktop instead, which sees
# both, and needs the window in front: it puts it there, which takes the
# focus for the length of the run.
#
# No Hebrew in this file: Windows PowerShell reads a BOM-less script in the
# system code page and would mangle it.

param([string]$OutDir = 'build/out/shots/flick', [int]$Early = 120, [int]$Late = 1600,
      [switch]$FromScreen)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;

public static class Flick
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int t, uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    public static int[] Size(IntPtr h)
    {
        RECT r; GetWindowRect(h, out r);
        return new int[] { r.R - r.L, r.B - r.T };
    }

    public static void Click(IntPtr h, int x, int y)
    {
        IntPtr at = (IntPtr)((y << 16) | (x & 0xFFFF));
        PostMessage(h, 0x0200, IntPtr.Zero, at);
        PostMessage(h, 0x0201, (IntPtr)1, at);
        PostMessage(h, 0x0202, IntPtr.Zero, at);
    }

    // In front, and staying there, so a screen copy sees this window.
    public static void Front(IntPtr h)
    {
        SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002);   // HWND_TOPMOST
        SetForegroundWindow(h);
    }

    /*  What is actually on the screen, rather than what the window would
        paint if it were asked again. The difference is the whole point: a
        window that has not repainted shows the old picture to the eye and the
        new one to PrintWindow. */
    public static void Grab(IntPtr h, string path)
    {
        RECT r; GetWindowRect(h, out r);
        int w = r.R - r.L, t = r.B - r.T;
        if (w < 1 || t < 1) throw new Exception("no window");
        using (var bmp = new Bitmap(w, t))
        {
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.L, r.T, 0, 0, new Size(w, t));
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
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

    // How many sampled pixels differ, and where the first difference is.
    public static int[] Apart(string a, string b)
    {
        using (var ia = (Bitmap)Bitmap.FromFile(a))
        using (var ib = (Bitmap)Bitmap.FromFile(b))
        {
            if (ia.Width != ib.Width || ia.Height != ib.Height)
                return new int[] { -1, 0, 0, 0 };
            int n = 0, total = 0, fx = -1, fy = -1;
            for (int y = 0; y < ia.Height; y += 2)
                for (int x = 0; x < ia.Width; x += 2)
                {
                    total++;
                    if (ia.GetPixel(x, y).ToArgb() == ib.GetPixel(x, y).ToArgb()) continue;
                    if (fx < 0) { fx = x; fy = y; }
                    n++;
                }
            return new int[] { n, total, fx, fy };
        }
    }
}
'@

if (-not ('Flick' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[void][Flick]::SetProcessDPIAware()

$out = Join-Path (Get-Location) $OutDir
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Force $out | Out-Null }

# The settings file belongs to whoever uses the program. These scripts put it
# into a known state and MUST put it back, including if something throws.
$file = Join-Path $env:LOCALAPPDATA 'Eliza\settings.txt'
$theirs = if (Test-Path $file) { [System.IO.File]::ReadAllBytes($file) } else { $null }
$restore = {
    if ($null -ne $theirs) { [System.IO.File]::WriteAllBytes($file, $theirs) }
    elseif (Test-Path $file) { Remove-Item $file -Force -ErrorAction SilentlyContinue }
}
trap { & $restore; break }
$parent = Split-Path $file
if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force $parent | Out-Null }
Set-Content -Path $file -Encoding utf8 -Value @(
    'language = he', 'mode = dark', 'accent = 0', 'tab = 0',
    'screen = 0', 'phosphor = 0', 'speed = 0', 'visits = 3')

Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$proc = Start-Process -FilePath (Resolve-Path 'dist\ELIZA.exe').Path -PassThru
Start-Sleep -Milliseconds 2500
$proc.Refresh()
$shell = $proc.MainWindowHandle
if ($shell -eq 0) { throw 'no shell window' }

$size = [Flick]::Size($shell)
$scale = [math]::Round($size[0] / 980.0, 3)
if ($scale -lt 0.5 -or $scale -gt 4) { $scale = 1 }
function Px([int]$v) { [int][math]::Round($v * $scale) }
$navX = $size[0] - (Px 196) + (Px 80)
# The nav gained a row: home, models, about, transcripts, settings,
# 46 apart starting at 88.
$rows = @{ home = (Px 88); models = (Px 134); about = (Px 180);
           transcripts = (Px 226); settings = (Px 272) }

Write-Output ("window " + $size[0] + "x" + $size[1] + "  scale " + $scale +
              "  capture " + $(if ($FromScreen) { 'the screen' } else { 'PrintWindow' }))
if ($FromScreen) { [Flick]::Front($shell); Start-Sleep -Milliseconds 600 }

function Shot([string]$path) {
    if ($FromScreen) { [Flick]::Grab($shell, $path) } else { [Flick]::Save($shell, $path) }
}

<#  A frame taken during a page change is fine if it is the page you were on
    -- the change has not happened yet -- and fine if it is the page you asked
    for. It is garbled only if it is neither: a header from one page over a
    body from the other. So three pictures are taken, not two, and the early
    one is compared with both.  #>
foreach ($page in 'settings', 'about', 'transcripts', 'models', 'home') {
    # Not $early / $late: PowerShell is case-insensitive, so those are the
    # [int] parameters and assigning a path to one is a type error.
    $shotWas = Join-Path $out ($page + '-before.png')
    Shot $shotWas

    [Flick]::Click($shell, $navX, $rows[$page])
    Start-Sleep -Milliseconds $Early
    $shotA = Join-Path $out ($page + '-early.png')
    Shot $shotA
    Start-Sleep -Milliseconds $Late
    $shotB = Join-Path $out ($page + '-late.png')
    Shot $shotB

    $fromWas = [Flick]::Apart($shotA, $shotWas)
    $fromNow = [Flick]::Apart($shotA, $shotB)
    $verdict =
        if ($fromWas[0] -eq 0) { 'the page it was on, unchanged so far' }
        elseif ($fromNow[0] -eq 0) { 'the new page, already whole' }
        else { 'NEITHER PAGE -- ' + $fromWas[0] + ' pixels from the old, ' +
               $fromNow[0] + ' from the new, first at ' + $fromNow[2] + ',' + $fromNow[3] }
    Write-Output ("  " + $page.PadRight(12) + " at +" + $Early + "ms: " + $verdict)
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
& $restore
