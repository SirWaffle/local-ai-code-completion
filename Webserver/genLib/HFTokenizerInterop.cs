using System;
using System.Runtime.InteropServices;

namespace genLib
{
    public class HFTokenizerInterop
    {
        private const string DLLName = @"hftokenizer.dll";


        [DllImport(DLLName)]
        public static extern IntPtr from_file([MarshalAs(UnmanagedType.LPUTF8Str)] String file);


        [DllImport(DLLName)]
        public static extern IntPtr from_pretrained([MarshalAs(UnmanagedType.LPUTF8Str)] String file);


        [DllImport(DLLName)]
        public static extern UInt32 encode(IntPtr tok, [MarshalAs(UnmanagedType.LPUTF8Str)] String input, UInt32[] idBuffer, int length, bool add_special_tokens);


        [DllImport(DLLName)]
        [return: MarshalAs(UnmanagedType.LPUTF8Str)]
        public static extern String decode(IntPtr tok, UInt32[] idArray, UInt32 length, bool skip_special_tokens);

    }
}
