# The program at the smallest size it allows itself to be.
#
# Every picture taken of this program so far has been of a window at its
# default size on one screen. The layout has minimums in several places and
# nothing has ever checked what happens when they are all reached at once.
#
# No Hebrew in this file: Windows PowerShell reads a BOM-less script in the
# system code page and would mangle it.

param([string]$OutDir = 'build/out/shots/small', [int]$W = 720, [int]$H = 520)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

public static class Small
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int t, bool repaint);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

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

    public static void Resize(IntPtr h, int w, int t)
    {
        RECT r; GetWindowRect(h, out r);
        MoveWindow(h, r.L, r.T, w, t, true);
    }

    public static void Key(IntPtr h, int vk)
    {
        PostMessage(h, 0x0100, (IntPtr)vk, IntPtr.Zero);
        PostMessage(h, 0x0101, (IntPtr)vk, IntPtr.Zero);
    }

    public static void Click(IntPtr h, int x, int y)
    {
        IntPtr at = (IntPtr)((y << 16) | (x & 0xFFFF));
        PostMessage(h, 0x0200, IntPtr.Zero, at);
        PostMessage(h, 0x0201, (IntPtr)1, at);
        PostMessage(h, 0x0202, IntPtr.Zero, at);
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

if (-not ('Small' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[void][Small]::SetProcessDPIAware()

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
    'screen = 2', 'phosphor = 0', 'speed = 0', 'visits = 3')

Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$exe = (Resolve-Path 'dist\ELIZA.exe').Path
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2400
$proc.Refresh()
$shell = $proc.MainWindowHandle
if ($shell -eq 0) { throw 'no shell window' }
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id

[Small]::Resize($shell, $W, $H)
Start-Sleep -Milliseconds 900
[Small]::Save($shell, (Join-Path $out 'home.png'))
Write-Output '  home.png'

# The nav at this size: the strip is 196 unscaled wide on the right.
#
# Measured from the window that actually exists, not from the size that was
# asked for -- MinimumSize is itself scaled, so a 720-wide request on a 125 per
# cent screen produces a 900-wide window, and clicking at 720's coordinates
# lands in the middle of the page.
$size = [Small]::Size($shell)
$real = $size[0]
$scale = [math]::Round($real / ($W / 1.0), 3)
if ($scale -lt 0.5 -or $scale -gt 4) { $scale = 1 }
Write-Output ("window " + $size[0] + "x" + $size[1])
$navX = [int]($real - 196 * $scale + 80 * $scale)
[Small]::Click($shell, $navX, [int](226 * $scale))
Start-Sleep -Milliseconds 800
[Small]::Save($shell, (Join-Path $out 'settings.png'))
Write-Output '  settings.png'

[Small]::Click($shell, $navX, [int](134 * $scale))
Start-Sleep -Milliseconds 800
[Small]::Save($shell, (Join-Path $out 'about.png'))
Write-Output '  about.png'

[Small]::Click($shell, $navX, [int](88 * $scale))
Start-Sleep -Milliseconds 600
[Small]::Key($shell, 0x32)
Start-Sleep -Milliseconds 2200
$terminal = [Small]::Windows($tid) | Where-Object { $_ -ne $shell } | Select-Object -Last 1
if ($terminal) {
    [Small]::Resize($terminal, $W, $H)
    Start-Sleep -Milliseconds 900
    [Small]::Save($terminal, (Join-Path $out 'museum.png'))
    Write-Output '  museum.png'
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
& $restore
