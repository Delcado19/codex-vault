using System.Runtime.InteropServices;

namespace WindowsSecretManager.Core;

// Pins an unmanaged buffer in physical RAM for as long as it holds a decrypted
// secret, so the OS cannot write it to the pagefile while it is in use. Best
// effort: VirtualLock needs working-set quota and a failure is not reported,
// same trade-off the Windows API itself makes.
public static class NativeMemoryLock
{
    public static IDisposable Pin(IntPtr pointer, int byteLength) => new Scope(pointer, byteLength);

    private sealed class Scope : IDisposable
    {
        private readonly IntPtr _pointer;
        private readonly UIntPtr _length;

        public Scope(IntPtr pointer, int byteLength)
        {
            _pointer = pointer;
            _length = (UIntPtr)byteLength;
            VirtualLock(_pointer, _length);
        }

        public void Dispose() => VirtualUnlock(_pointer, _length);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);
}
