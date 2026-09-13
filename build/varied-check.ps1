# Does "מתחלף בין שיחות" actually do anything in the program?
#
# The engine's own tests drive StartAt directly, so they passed while the
# feature did nothing at all in the running program -- StartConversation called
# Reset immediately afterwards and zeroed every offset. This check runs the
# real executable, types the same word into a fresh conversation several times,
# and reads what she answered out of the transcripts she saved.
#
# No Hebrew in this file: Windows PowerShell reads a BOM-less script in the
# system code page and would mangle it.

param([int]$Runs = 5)

$ErrorActionPreference = 'Stop'

$source = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Poke
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr h, StringBuilder s, int max);

    public delegate bool EnumProc(IntPtr h, IntPtr p);

    public static List<IntPtr> Windows(uint tid)
    {
        var found = new List<IntPtr>();
        EnumThreadWindows(tid, delegate(IntPtr h, IntPtr p) {
            if (IsWindowVisible(h)) found.Add(h);
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindEdit(IntPtr parent)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, delegate(IntPtr h, IntPtr p) {
            var name = new StringBuilder(256);
            GetClassName(h, name, name.Capacity);
            if (name.ToString().IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0)
            { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
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
}
'@

if (-not ('Poke' -as [type])) { Add-Type -TypeDefinition $source }

$home2 = Join-Path $env:LOCALAPPDATA 'Eliza'
$store = Join-Path $home2 'transcripts'
if (Test-Path $store) { Remove-Item (Join-Path $store '*.txt') -Force -ErrorAction SilentlyContinue }

$word = [string]([char]0x05D4 + [char]0x05DB + [char]0x05DC)   # "hakol"
$exe = (Resolve-Path 'dist\ELIZA.exe').Path
$answers = @()

for ($run = 1; $run -le $Runs; $run++) {
    Get-Process -Name 'ELIZA' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400

    $proc = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Milliseconds 2400
    $proc.Refresh()
    $shell = $proc.MainWindowHandle
    if ($shell -eq 0) { throw 'no shell window' }
    $tid = [uint32](Get-Process -Id $proc.Id).Threads[0].Id

    # Give the window the keyboard before typing at it, and allow the key a
    # second attempt: the shell is still laying itself out for a moment after
    # it appears, and a key that arrives during that goes nowhere.
    $terminal = $null
    foreach ($try in 1..3) {
        [Poke]::Key($shell, 0x32)      # '2' -- the Hebrew script
        Start-Sleep -Milliseconds 1600
        $terminal = [Poke]::Windows($tid) | Where-Object { $_ -ne $shell } | Select-Object -Last 1
        if ($terminal) { break }
    }
    if (-not $terminal) { throw 'the conversation did not open' }
    $edit = [Poke]::FindEdit($terminal)
    if ($edit -eq 0) { throw 'no input box' }

    [Poke]::Type($edit, $word)
    [Poke]::Key($edit, 0x0D)
    Start-Sleep -Milliseconds 1400
    [Poke]::Key($edit, 0x1B)           # Esc, out of the conversation
    Start-Sleep -Milliseconds 900
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    $newest = Get-ChildItem (Join-Path $store '*.txt') -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $newest) { Write-Output "run ${run}: no transcript"; continue }

    # Her reply is the line after the one that starts with the echo marker.
    $lines = Get-Content $newest.FullName -Encoding UTF8
    $reply = ''
    for ($i = 0; $i -lt $lines.Count - 1; $i++) {
        if ($lines[$i].StartsWith([string][char]0x00B7)) { $reply = $lines[$i + 1].Trim(); break }
    }
    $answers += $reply
    Write-Output ("run " + $run + ": " + $reply)
}

$distinct = ($answers | Where-Object { $_ -ne '' } | Sort-Object -Unique).Count
Write-Output ''
Write-Output ("distinct answers to the same word over " + $Runs + " fresh conversations: " + $distinct)
if ($distinct -le 1) { Write-Output 'STUCK -- the varied order is not reaching the program' }
else { Write-Output 'varied' }
