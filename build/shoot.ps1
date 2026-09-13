# Drives ELIZA.exe and photographs it, so the look and the Hebrew layout can be
# checked rather than assumed.
#
# Keystrokes are posted to the program's own window and the picture is taken
# with PrintWindow, so nothing is stolen from whatever the user is doing.
#
#   shoot.ps1 -Out shot.png -Send "hello|{F2}|שלום"

param(
    [string]$Out = 'build/out/shot.png',
    [string[]]$Send = @(),
    [int]$Settle = 1500,
    [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

public static class Shoot
{
    // ELIZA declares itself DPI aware. If this process does not, Windows hands
    // us virtualised window rectangles and the picture comes out cropped.
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    // Unicode, so this binds PostMessageW: PostMessageA would mangle Hebrew.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr h, StringBuilder s, int max);

    delegate bool EnumProc(IntPtr h, IntPtr p);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_CHAR = 0x0102;

    // WinForms registers its own window classes, so the text box reports
    // something like "WindowsForms10.EDIT.app.0.34f5582_r8_ad1", not "Edit".
    public static IntPtr FindEdit(IntPtr parent)
    {
        IntPtr found = IntPtr.Zero;
        EnumProc cb = delegate(IntPtr h, IntPtr p) {
            var sb = new StringBuilder(128);
            GetClassName(h, sb, sb.Capacity);
            string name = sb.ToString();
            if (name == "Edit" || name.IndexOf(".EDIT.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found = h;
                return false;
            }
            return true;
        };
        EnumChildWindows(parent, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return found;
    }

    public static void Type(IntPtr h, string text)
    {
        foreach (char c in text)
            PostMessage(h, WM_CHAR, (IntPtr)c, IntPtr.Zero);
    }

    public static void Key(IntPtr h, int vk)
    {
        PostMessage(h, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        PostMessage(h, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
    }

    public static void Capture(IntPtr h, string path)
    {
        RECT r;
        GetWindowRect(h, out r);
        int w = r.Right - r.Left, ht = r.Bottom - r.Top;
        if (w <= 0 || ht <= 0) throw new Exception("window has no size");

        using (var bmp = new Bitmap(w, ht))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr dc = g.GetHdc();
                try { PrintWindow(h, dc, 2); }   // PW_RENDERFULLCONTENT
                finally { g.ReleaseHdc(dc); }
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
'@

if (-not ('Shoot' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing, System.Windows.Forms
}

$VK = @{
    'ENTER' = 0x0D; 'ESC' = 0x1B; 'TAB' = 0x09; 'BACK' = 0x08
    'F1' = 0x70; 'F2' = 0x71; 'F3' = 0x72; 'F4' = 0x73
    'F5' = 0x74; 'F6' = 0x75; 'F7' = 0x76; 'F8' = 0x77
}

[void][Shoot]::SetProcessDPIAware()

Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$exe = (Resolve-Path (Join-Path $PSScriptRoot '..\dist\ELIZA.exe')).Path
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2500
$proc.Refresh()

$win = $proc.MainWindowHandle
if ($win -eq 0) { throw 'ELIZA has no main window' }
# Switching script flips the window's reading direction, and WinForms rebuilds
# the text box when that happens: the old handle dies. Look it up every time.
function Get-Input {
    $h = [Shoot]::FindEdit($win)
    if ($h -eq 0) { throw 'could not find the input box' }
    return $h
}

[void](Get-Input)

foreach ($step in $Send) {
    $edit = Get-Input
    if ($step -match '^\{(\w+)\}$') {
        $name = $Matches[1].ToUpper()
        if (-not $VK.ContainsKey($name)) { throw "unknown key $name" }
        [Shoot]::Key($edit, $VK[$name])
    } else {
        [Shoot]::Type($edit, $step)
        [Shoot]::Key($edit, $VK['ENTER'])
    }
    Start-Sleep -Milliseconds 900
}

Start-Sleep -Milliseconds $Settle

$outPath = Join-Path (Get-Location) $Out
$dir = Split-Path $outPath -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
[Shoot]::Capture($win, $outPath)

if (-not $KeepOpen) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}
Write-Output "saved $outPath"
