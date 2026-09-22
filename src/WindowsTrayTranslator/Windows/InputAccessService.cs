using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WindowsTrayTranslator.Windows;

public sealed class InputAccessService
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;

    public InputAccessResult Check(WindowIdentity target)
    {
        if (target.ProcessId == 0)
        {
            return InputAccessResult.Allowed();
        }

        try
        {
            uint currentIntegrity = GetProcessIntegrityLevel((uint)Environment.ProcessId);
            uint targetIntegrity = GetProcessIntegrityLevel(target.ProcessId);
            return CanSendInput(currentIntegrity, targetIntegrity)
                ? InputAccessResult.Allowed()
                : InputAccessResult.Denied(
                    "관리자 권한으로 실행된 프로그램에는 자동 입력할 수 없습니다. 번역 프로그램도 관리자 권한으로 실행하거나 결과 복사를 사용해 주세요.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // If Windows does not allow inspection, SendInput still provides the final enforcement.
            return InputAccessResult.Allowed();
        }
    }

    internal static bool CanSendInput(uint currentIntegrityRid, uint targetIntegrityRid) =>
        currentIntegrityRid >= targetIntegrityRid;

    private static uint GetProcessIntegrityLevel(uint processId)
    {
        using SafeProcessHandle process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!OpenProcessToken(process, TokenQuery, out SafeFileHandle token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        using (token)
        {
            GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int length);
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, length, out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                IntPtr sid = Marshal.ReadIntPtr(buffer);
                IntPtr countPointer = GetSidSubAuthorityCount(sid);
                byte count = Marshal.ReadByte(countPointer);
                return (uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle processHandle, uint desiredAccess, out SafeFileHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeFileHandle tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);
}

public sealed record InputAccessResult(bool IsAllowed, string? ErrorMessage)
{
    public static InputAccessResult Allowed() => new(true, null);
    public static InputAccessResult Denied(string message) => new(false, message);
}
