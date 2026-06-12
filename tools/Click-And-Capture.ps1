param(
    [Parameter(Mandatory)][int]$X,       # click offset from window left
    [Parameter(Mandatory)][int]$Y,       # click offset from window top
    [Parameter(Mandatory)][string]$OutFile,
    [string]$ProcessName = 'WinUpdateChecker',
    [int]$SettleMs = 1200
)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Win32Click {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$proc = Get-Process $ProcessName -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $proc.MainWindowHandle
$TOPMOST = [IntPtr](-1); $NOTOPMOST = [IntPtr](-2)
[Win32Click]::SetWindowPos($h, $TOPMOST, 0, 0, 0, 0, 0x0003) | Out-Null   # SWP_NOMOVE|NOSIZE
Start-Sleep -Milliseconds 300
$r = New-Object Win32Click+RECT
[Win32Click]::GetWindowRect($h, [ref]$r) | Out-Null
[Win32Click]::SetCursorPos($r.Left + $X, $r.Top + $Y) | Out-Null
Start-Sleep -Milliseconds 150
[Win32Click]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)  # LEFTDOWN
[Win32Click]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)  # LEFTUP
Start-Sleep -Milliseconds $SettleMs
$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win32Click]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
[Win32Click]::SetWindowPos($h, $NOTOPMOST, 0, 0, 0, 0, 0x0003) | Out-Null
Write-Output "clicked ($X,$Y), saved $OutFile"
