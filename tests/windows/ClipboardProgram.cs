using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class ClipboardProgram
{
    [STAThread]
    private static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BearDL-clipboard-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "ملف عربي with spaces.mp4");
        File.WriteAllText(path, "clipboard interoperability check");
        var owner = CreateOwner();
        try
        {
            FileClipboard.CopyFileAsync(owner, path).GetAwaiter().GetResult();
            VerifyFile(owner, path);
            Console.WriteLine("PASS: Unicode file paths and COPY drop effect round-trip through the Windows Shell clipboard.");
            try
            {
                FileClipboard.CopyFileAsync(owner, Path.Combine(directory, "missing.mp4")).GetAwaiter().GetResult();
                throw new InvalidOperationException("A missing file must fail before changing the clipboard.");
            }
            catch (FileNotFoundException)
            {
                VerifyFile(owner, path);
                Console.WriteLine("PASS: A missing file is rejected without replacing the existing clipboard.");
            }
            DestroyWindow(owner);
            owner = CreateOwner();
            VerifyFile(owner, path);
            Console.WriteLine("PASS: File data remains available after destroying the source window.");
        }
        finally
        {
            if (OpenClipboard(owner))
            {
                EmptyClipboard();
                CloseClipboard();
            }
            DestroyWindow(owner);
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static nint CreateOwner()
    {
        var owner = CreateWindowEx(0, "STATIC", "BearDL clipboard checks", 0, 0, 0, 0, 0, (nint)(-3), 0, 0, 0);
        return owner != 0 ? owner : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static void VerifyFile(nint owner, string expected)
    {
        if (!OpenClipboard(owner))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        try
        {
            var drop = GetClipboardData(15);
            if (drop == 0 || DragQueryFile(drop, uint.MaxValue, null, 0) != 1)
            {
                throw new InvalidOperationException("The clipboard must contain exactly one Shell file item.");
            }
            var length = DragQueryFile(drop, 0, null, 0);
            var buffer = new StringBuilder((int)length + 1);
            DragQueryFile(drop, 0, buffer, (uint)buffer.Capacity);
            if (buffer.ToString() != expected)
            {
                throw new InvalidOperationException("The copied file path changed during clipboard transfer.");
            }
            var effect = GetClipboardData(RegisterClipboardFormat("Preferred DropEffect"));
            var data = GlobalLock(effect);
            if (data == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            try
            {
                if (Marshal.ReadInt32(data) != 1)
                {
                    throw new InvalidOperationException("The clipboard must request COPY, not MOVE.");
                }
            }
            finally
            {
                GlobalUnlock(effect);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern nint GetClipboardData(uint format);

    [DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(nint drop, uint index, StringBuilder? path, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(nint memory);
}
