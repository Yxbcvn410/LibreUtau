﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LibreUtau.Core.ResamplerDriver.Factories {
    internal class CppDriver : DriverModels, IResamplerDriver {
        readonly string DllPath = string.Empty;

        public CppDriver(string dllPath) {
            IntPtr hModule = LoadLibrary(dllPath);
            if (hModule == IntPtr.Zero) {
                isLegalPlugin = false;
            } else {
                IntPtr Resp = GetProcAddress(hModule, "DoResampler");
                IntPtr Infp = GetProcAddress(hModule, "GetInformation");
                if (Resp != IntPtr.Zero && Infp != IntPtr.Zero) {
                    this.DllPath = dllPath;
                    isLegalPlugin = true;
                }

                FreeLibrary(hModule);
            }
        }

        public bool isLegalPlugin { get; private set; }

        public Stream DoResampler(EngineInput args) {
            MemoryStream ms = new MemoryStream();
            if (!isLegalPlugin) return ms;
            try {
                IntPtr hModule = LoadLibrary(DllPath);
                if (hModule == IntPtr.Zero) isLegalPlugin = false;
                else {
                    IntPtr m = GetProcAddress(hModule, "DoResampler");
                    if (m != IntPtr.Zero) {
                        DoResamplerDelegate g =
                            (DoResamplerDelegate)Marshal.GetDelegateForFunctionPointer(m, typeof(DoResamplerDelegate));
                        EngineOutput Output = Intptr2EngineOutput(g(args));
                        ms = new MemoryStream(Output.wavData);
                    }

                    FreeLibrary(hModule);
                }
            } catch { ; }

            return ms;
        }

        public EngineInfo GetInfo() {
            EngineInfo ret = new EngineInfo {
                Version = "Error"
            };
            if (!isLegalPlugin) return ret;
            try {
                IntPtr hModule = LoadLibrary(DllPath);
                if (hModule == IntPtr.Zero) isLegalPlugin = false;
                else {
                    IntPtr m = GetProcAddress(hModule, "GetInformation");
                    if (m != IntPtr.Zero) {
                        GetInformationDelegate g =
                            (GetInformationDelegate)Marshal.GetDelegateForFunctionPointer(m,
                                typeof(GetInformationDelegate));
                        ret = Intptr2EngineInformation(g());
                    }

                    FreeLibrary(hModule);
                }
            } catch { ; }

            return ret;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibrary", SetLastError = true)]
        static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpLibFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetProcAddress")]
        static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary")]
        static extern bool FreeLibrary(IntPtr hModule);

        #region CppIO模块

        /// <summary>
        ///     Cpp指针转换用中间层
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct EngineOutput_Cpp {
            public readonly int nWavData;
            public readonly IntPtr wavData;
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct EngineInformation_Cpp {
            public readonly string Name;
            public readonly string Version;
            public readonly string Author;
            public readonly string Usuage;
            public readonly int FlagItemCount;
            public IntPtr FlgItem;
        }

        /// <summary>
        ///     Cpp指针转换过程-EngineOutput
        /// </summary>
        /// <param name="Ptr"></param>
        /// <returns></returns>
        protected static EngineOutput Intptr2EngineOutput(IntPtr Ptr) {
            EngineOutput Ret = new EngineOutput();
            Ret.nWavData = 0;
            try {
                if (Ptr != IntPtr.Zero) {
                    EngineOutput_Cpp CTry = (EngineOutput_Cpp)Marshal.PtrToStructure(Ptr, typeof(EngineOutput_Cpp));
                    if (CTry.nWavData > 0) {
                        Ret.wavData = new byte[CTry.nWavData];
                        Marshal.Copy(CTry.wavData, Ret.wavData, 0, CTry.nWavData);
                        Ret.nWavData = CTry.nWavData;
                    }
                }
            } catch { ; }

            return Ret;
        }

        /// <summary>
        ///     Cpp指针转换过程-EngineInformation
        /// </summary>
        /// <param name="Ptr"></param>
        /// <returns></returns>
        protected static EngineInfo Intptr2EngineInformation(IntPtr Ptr) {
            EngineInfo Ret = new EngineInfo();
            Ret.Version = "Error";
            try {
                EngineInformation_Cpp ret =
                    (EngineInformation_Cpp)Marshal.PtrToStructure(Ptr, typeof(EngineInformation_Cpp));
                if (ret.Name != "") {
                    Ret.Author = ret.Author;
                    Ret.FlagItemCount = ret.FlagItemCount;
                    Ret.Name = ret.Name;
                    Ret.Usuage = ret.Usuage;
                    Ret.Version = ret.Version;
                    Ret.FlagItem = new EngineFlagItem[ret.FlagItemCount];
                    int basePtr = ret.FlgItem.ToInt32();
                    int PtrSize = Marshal.SizeOf(Ret.FlagItem[0]);
                    for (int i = 0; i < Ret.FlagItemCount; i++) {
                        Ret.FlagItem[i] = (EngineFlagItem)Marshal.PtrToStructure(new IntPtr(basePtr + i * PtrSize),
                            typeof(EngineFlagItem));
                    }

                    //Ret = ret;
                }
            } catch { ; }

            return Ret;
        }

        /// <summary>
        ///     信息获取执行委托
        /// </summary>
        /// <returns></returns>
        delegate IntPtr GetInformationDelegate();

        /// <summary>
        ///     引擎执行过程委托
        /// </summary>
        /// <param name="Input"></param>
        /// <returns></returns>
        delegate IntPtr DoResamplerDelegate(EngineInput Input);

        #endregion
    }
}
