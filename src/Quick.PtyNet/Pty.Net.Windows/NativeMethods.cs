using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace Pty.Net.Windows;

internal static class NativeMethods
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
	internal struct STARTUPINFO
	{
		public int cb;

		public IntPtr lpReserved;

		public IntPtr lpDesktop;

		public IntPtr lpTitle;

		public int dwX;

		public int dwY;

		public int dwXSize;

		public int dwYSize;

		public int dwXCountChars;

		public int dwYCountChars;

		public int dwFillAttribute;

		public int dwFlags;

		public short wShowWindow;

		public short cbReserved2;

		public IntPtr lpReserved2;

		public IntPtr hStdInput;

		public IntPtr hStdOutput;

		public IntPtr hStdError;
	}

	internal struct Coord
	{
		public ushort X;

		public ushort Y;

		public Coord(int x, int y)
		{
			X = (ushort)x;
			Y = (ushort)y;
		}
	}

	internal struct PROCESS_INFORMATION
	{
		public IntPtr hProcess;

		public IntPtr hThread;

		public int dwProcessId;

		public int dwThreadId;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
	internal struct STARTUPINFOEX
	{
		public STARTUPINFO StartupInfo;

		public IntPtr lpAttributeList;

		/// <summary>
		/// Initializes the specified startup info struct with the required properties and
		/// updates its thread attribute list with the specified ConPTY handle.
		/// </summary>
		/// <param name="handle">Pseudo console handle.</param>
		internal void InitAttributeListAttachedToConPTY(SafePseudoConsoleHandle handle)
		{
			StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
			StartupInfo.dwFlags = 256;
			IntPtr lpSize = IntPtr.Zero;
			if (InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize) || lpSize == IntPtr.Zero)
			{
				throw new InvalidOperationException($"Couldn't get the size of the process attribute list for {1} attributes", new Win32Exception());
			}
			lpAttributeList = Marshal.AllocHGlobal(lpSize);
			if (lpAttributeList == IntPtr.Zero)
			{
				throw new OutOfMemoryException("Couldn't reserve space for a new process attribute list");
			}
			if (!InitializeProcThreadAttributeList(lpAttributeList, 1, 0, ref lpSize))
			{
				throw new InvalidOperationException("Couldn't create new process attribute list", new Win32Exception());
			}
			if (!UpdateProcThreadAttribute(lpAttributeList, 0u, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, handle.Handle, (IntPtr)Marshal.SizeOf<IntPtr>(), IntPtr.Zero, IntPtr.Zero))
			{
				throw new InvalidOperationException("Couldn't update process attribute list", new Win32Exception());
			}
		}

		internal void FreeAttributeList()
		{
			if (lpAttributeList != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(lpAttributeList);
				lpAttributeList = IntPtr.Zero;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	[DebuggerStepThrough]
	internal class SECURITY_ATTRIBUTES
	{
		public int nLength = 12;

		public IntPtr lpSecurityDescriptor;

		public bool bInheritHandle;
	}

	[SecurityCritical]
	internal abstract class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public IntPtr Handle => handle;

		protected SafeKernelHandle(bool ownsHandle)
			: base(ownsHandle)
		{
		}

		protected SafeKernelHandle(IntPtr handle, bool ownsHandle)
			: base(ownsHandle)
		{
			SetHandle(handle);
		}

		/// <summary>
		/// Use this method with the default constructor to allow the memory allocation
		/// for the handle to happen before the CER call to get it.
		/// </summary>
		/// <param name="handle">The native handle.</param>
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public void InitialSetHandle(IntPtr handle)
		{
			base.handle = handle;
		}

		[SecurityCritical]
		protected override bool ReleaseHandle()
		{
			return CloseHandle(handle);
		}
	}

	[SecurityCritical]
	internal sealed class SafeProcessHandle : SafeKernelHandle
	{
		public SafeProcessHandle()
			: base(ownsHandle: true)
		{
		}

		public SafeProcessHandle(IntPtr handle, bool ownsHandle = true)
			: base(handle, ownsHandle)
		{
		}
	}

	[SecurityCritical]
	internal sealed class SafeThreadHandle : SafeKernelHandle
	{
		public SafeThreadHandle()
			: base(ownsHandle: true)
		{
		}

		public SafeThreadHandle(IntPtr handle, bool ownsHandle = true)
			: base(handle, ownsHandle)
		{
		}
	}

	[SecurityCritical]
	internal sealed class SafePipeHandle : SafeKernelHandle
	{
		public SafePipeHandle()
			: base(ownsHandle: true)
		{
		}

		public SafePipeHandle(IntPtr handle, bool ownsHandle = true)
			: base(handle, ownsHandle)
		{
		}
	}

	[SecurityCritical]
	internal class SafePseudoConsoleHandle : SafeKernelHandle
	{
		public SafePseudoConsoleHandle()
			: base(ownsHandle: true)
		{
		}

		public SafePseudoConsoleHandle(IntPtr handle, bool ownsHandle)
			: base(ownsHandle)
		{
			SetHandle(handle);
		}

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		[SecurityCritical]
		protected override bool ReleaseHandle()
		{
			ClosePseudoConsole(handle);
			return true;
		}
	}

	public const int S_OK = 0;

	internal const int CREATE_UNICODE_ENVIRONMENT = 1024;

	internal const int EXTENDED_STARTUPINFO_PRESENT = 524288;

	internal const int STARTF_USESTDHANDLES = 256;

	internal static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = new IntPtr(131094);

	internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

	private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(delegate
	{
		IntPtr intPtr = LoadLibraryW("kernel32.dll");
		return intPtr != IntPtr.Zero && GetProcAddress(intPtr, "CreatePseudoConsole") != IntPtr.Zero;
	}, isThreadSafe: true);

	internal static bool IsPseudoConsoleSupported => IsPseudoConsoleSupportedLazy.Value;

	[DllImport("kernel32.dll")]
	public static extern int GetProcessId(SafeProcessHandle hProcess);

	[DllImport("kernel32.dll", SetLastError = true)]
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool CloseHandle(IntPtr hObject);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, SECURITY_ATTRIBUTES lpProcessAttributes, SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, int dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

	internal static int CreatePseudoConsole(Coord coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle)
	{
		if (Environment.Is64BitOperatingSystem)
		{
			return CreatePseudoConsole64(coord, input, output, flags, out consoleHandle);
		}
		return CreatePseudoConsole86(coord, input, output, flags, out consoleHandle);
	}

	internal static int ResizePseudoConsole(SafePseudoConsoleHandle consoleHandle, Coord coord)
	{
		if (Environment.Is64BitOperatingSystem)
		{
			return ResizePseudoConsole64(consoleHandle, coord);
		}
		return ResizePseudoConsole86(consoleHandle, coord);
	}

	internal static void ClosePseudoConsole(IntPtr consoleHandle)
	{
		if (Environment.Is64BitOperatingSystem)
		{
			ClosePseudoConsole64(consoleHandle);
		}
		else
		{
			ClosePseudoConsole86(consoleHandle);
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	[SecurityCritical]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool CreatePipe(out SafePipeHandle hReadPipe, out SafePipeHandle hWritePipe, SECURITY_ATTRIBUTES pipeAttributes, int size);

	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string libName);

	[DllImport("kernel32.dll")]
	internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

	[DllImport("os64\\conpty.dll", EntryPoint = "CreatePseudoConsole")]
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
	private static extern int CreatePseudoConsole64(Coord coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle);

	[DllImport("os64\\conpty.dll", EntryPoint = "ResizePseudoConsole")]
	private static extern int ResizePseudoConsole64(SafePseudoConsoleHandle consoleHandle, Coord coord);

	[DllImport("os64\\conpty.dll", EntryPoint = "ClosePseudoConsole")]
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	private static extern void ClosePseudoConsole64(IntPtr consoleHandle);

	[DllImport("os86\\conpty.dll", EntryPoint = "CreatePseudoConsole")]
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
	private static extern int CreatePseudoConsole86(Coord coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle);

	[DllImport("os86\\conpty.dll", EntryPoint = "ResizePseudoConsole")]
	private static extern int ResizePseudoConsole86(SafePseudoConsoleHandle consoleHandle, Coord coord);

	[DllImport("os86\\conpty.dll", EntryPoint = "ClosePseudoConsole")]
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	private static extern void ClosePseudoConsole86(IntPtr consoleHandle);
}
