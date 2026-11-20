using System;
using System.Runtime.InteropServices;

namespace AetherWall.Native
{
    internal static class Shell32
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SIZE
        {
            public int cx;
            public int cy;

            public SIZE(int width, int height)
            {
                cx = width;
                cy = height;
            }
        }

        [Flags]
        internal enum SIIGBF : uint
        {
            RESIZETOFIT = 0x00000000,
            BIGGERSIZEOK = 0x00000001,
            MEMORYONLY = 0x00000002,
            ICONONLY = 0x00000004,
            THUMBNAILONLY = 0x00000008,
            INCACHEONLY = 0x00000010,
            CROPTOSQUARE = 0x00000020,
            WIDETHUMBNAILS = 0x00000040,
            ICONBACKGROUND = 0x00000080,
            SCALEUP = 0x00000100
        }

        [ComImport]
        [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemImageFactory
        {
            int GetImage(
                SIZE size,
                SIIGBF flags,
                out IntPtr phbm);
        }

        [DllImport(
            "shell32.dll",
            CharSet = CharSet.Unicode,
            PreserveSig = true)]
        internal static extern int SHCreateItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)]
            out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);
    }
}