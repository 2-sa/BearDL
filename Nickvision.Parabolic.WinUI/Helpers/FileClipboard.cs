using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static partial class FileClipboard
{
    private const uint FileDropFormat = 15;
    private const uint MovableMemory = 0x0002;

    // CF_HDROP carries file paths in the native Shell format used by Explorer.
    // Once SetClipboardData succeeds, Windows owns the memory, including after exit.
    public static async Task CopyFileAsync(nint owner, string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(null, path);
        }
        if (owner == 0)
        {
            throw new ArgumentException("A clipboard owner window is required.", nameof(owner));
        }
        var paths = Encoding.Unicode.GetBytes(path + "\0\0");
        var fileDrop = new byte[20 + paths.Length];
        // DROPFILES: pFiles=20, point=(0,0), fNC=0, fWide=1.
        BitConverter.GetBytes(20).CopyTo(fileDrop, 0);
        BitConverter.GetBytes(1).CopyTo(fileDrop, 16);
        paths.CopyTo(fileDrop, 20);
        var dropMemory = Allocate(fileDrop);
        nint effectMemory = 0;
        var opened = false;
        try
        {
            effectMemory = Allocate(BitConverter.GetBytes(1)); // DROPEFFECT_COPY
            var effectFormat = RegisterClipboardFormat("Preferred DropEffect");
            if (effectFormat == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            for (var attempt = 0; attempt < 5; attempt++)
            {
                opened = OpenClipboard(owner);
                if (opened)
                {
                    break;
                }
                var error = Marshal.GetLastPInvokeError();
                if (attempt == 4)
                {
                    throw new Win32Exception(error);
                }
                await Task.Delay(50);
            }
            if (!EmptyClipboard())
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            if (SetClipboardData(FileDropFormat, dropMemory) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            dropMemory = 0;
            if (SetClipboardData(effectFormat, effectMemory) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            effectMemory = 0;
        }
        finally
        {
            if (opened)
            {
                CloseClipboard();
            }
            if (dropMemory != 0)
            {
                GlobalFree(dropMemory);
            }
            if (effectMemory != 0)
            {
                GlobalFree(effectMemory);
            }
        }
    }

    private static nint Allocate(byte[] data)
    {
        var memory = GlobalAlloc(MovableMemory, (nuint)data.Length);
        if (memory == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        var buffer = GlobalLock(memory);
        if (buffer == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            GlobalFree(memory);
            throw new Win32Exception(error);
        }
        try
        {
            Marshal.Copy(data, 0, buffer, data.Length);
        }
        catch
        {
            GlobalUnlock(memory);
            GlobalFree(memory);
            throw;
        }
        GlobalUnlock(memory);
        return memory;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetClipboardData(uint format, nint memory);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint RegisterClipboardFormat(string name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalAlloc(uint flags, nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint memory);
}
