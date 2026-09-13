# Photographs the shell, then the terminal it opens, then the way back.
#
# Keystrokes are posted to the program's own windows and the pictures are taken
# with PrintWindow, so nothing is stolen from whatever the user is doing.
#
# No Hebrew in this file on purpose: Windows PowerShell reads a BOM-less script
# in the system code page and would mangle it. Pass the lines in with -Lines.

param(
    [string]$OutDir = 'build/out',
    [string[]]$Lines = @('hello', 'i feel unwell lately'),
    [int]$Script = 1
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

public static class Shots
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

    public delegate bool EnumProc(IntPtr h, IntPtr p);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_CHAR = 0x0102;

    // Every visible top-level window this thread owns.
    public static IntPtr[] Windows(uint threadId)
    {
        var found = new List<IntPtr>();
        EnumProc cb = delegate(IntPtr h, IntPtr p) {
            if (IsWindowVisible(h)) found.Add(h);
            return true;
        };
        EnumThreadWindows(threadId, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return found.ToArray();
    }

    // WinForms registers its own classes, so a text box reports
    // "WindowsForms10.EDIT.app...", not "Edit".
    public static IntPtr FindEdit(IntPtr parent)
    {
        IntPtr found = IntPtr.Zero;
        EnumProc cb = delegate(IntPtr h, IntPtr p) {
            var sb = new StringBuilder(128);
            GetClassName(h, sb, sb.Capacity);
            string name = sb.ToString();
            if (name == "Edit" || name.IndexOf(".EDIT.", StringComparison.OrdinalIgnoreCase) >= 0)
            { found = h; return false; }
            return true;
        };
        EnumChildWindows(parent, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return found;
    }

    // Unicode, so this binds PostMessageW and Hebrew survives.
    public static void Type(IntPtr h, string text)
    {
        foreach (char c in text) PostMessage(h, WM_CHAR, (IntPtr)c, IntPtr.Zero);
    }

    public static void Key(IntPtr h, int vk)
    {
        PostMessage(h, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        PostMessage(h, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
    }

    public static void Shot(IntPtr h, string path)
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
                try { PrintWindow(h, dc, 2); } finally { g.ReleaseHdc(dc); }
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
'@

if (-not ('Shots' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[void][Shots]::SetProcessDPIAware()

$VK = @{ ENTER = 0x0D; ESC = 0x1B; ONE = 0x31 }

$out = Join-Path (Get-Location) $OutDir
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Force $out | Out-Null }

Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$exe = (Resolve-Path (Join-Path $PSScriptRoot '..\dist\ELIZA.exe')).Path
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2600
$proc.Refresh()
$shell = $proc.MainWindowHandle
if ($shell -eq 0) { throw 'the shell has no window' }
$tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id

[Shots]::Shot($shell, (Join-Path $out '10-shell-home.png'))
Write-Output '  10-shell-home.png'

# Go in. The shell takes 1..9 for the script cards.
[Shots]::Key($shell, $VK.ONE + $Script - 1)
Start-Sleep -Milliseconds 2600

$terminal = [Shots]::Windows($tid) | Where-Object { $_ -ne $shell } | Select-Object -Last 1
if (-not $terminal) { throw 'the conversation did not open' }

$edit = [Shots]::FindEdit($terminal)
if ($edit -eq 0) { throw 'no input box in the conversation' }

foreach ($line in $Lines) {
    [Shots]::Type($edit, $line)
    [Shots]::Key($edit, $VK.ENTER)
    Start-Sleep -Milliseconds 900
}
Start-Sleep -Milliseconds 900
[Shots]::Shot($terminal, (Join-Path $out '11-shell-terminal.png'))
Write-Output '  11-shell-terminal.png'

# Get up and leave the room.
[Shots]::Key($edit, $VK.ESC)
Start-Sleep -Milliseconds 1800
[Shots]::Shot($shell, (Join-Path $out '12-shell-back.png'))
Write-Output '  12-shell-back.png'

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
