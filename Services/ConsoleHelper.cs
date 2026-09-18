using System.Runtime.InteropServices;
using System.Text;

namespace StartFlow.Services;

internal static class ConsoleHelper
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    public static void Print(string text)
    {
        try
        {
            AttachConsole(AttachParentProcess);
            var output = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            var stdout = GetStdHandle(-11);
            _ = WriteFile(stdout, output, (uint)output.Length, out _, IntPtr.Zero);
            FreeConsole();
        }
        catch
        {
            // No console available; silently ignore.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
}