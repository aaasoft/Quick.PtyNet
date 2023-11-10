using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Pty.Net.Mac;

/// <summary>
/// Defines native types and methods for interop with Mac OS system APIs.
/// </summary>
internal static class NativeMethods
{
	public enum TermSpeed : uint
	{
		B38400 = 38400u
	}

	[Flags]
	public enum TermInputFlag : uint
	{
		/// <summary>
		/// Map BREAK to SIGINTR
		/// </summary>
		BRKINT = 2u,
		/// <summary>
		/// Map CR to NL (ala CRMOD)
		/// </summary>
		ICRNL = 0x100u,
		/// <summary>
		/// Enable output flow control
		/// </summary>
		IXON = 0x200u,
		/// <summary>
		/// Any char will restart after stop
		/// </summary>
		IXANY = 0x800u,
		/// <summary>
		/// Ring bell on input queue full
		/// </summary>
		IMAXBEL = 0x2000u,
		/// <summary>
		/// Maintain state for UTF-8 VERASE
		/// </summary>
		IUTF8 = 0x4000u
	}

	[Flags]
	public enum TermOuptutFlag : uint
	{
		/// <summary>
		/// No output processing
		/// </summary>
		NONE = 0u,
		/// <summary>
		/// Enable following output processing
		/// </summary>
		OPOST = 1u,
		/// <summary>
		/// Map NL to CR-NL (ala CRMOD)
		/// </summary>
		ONLCR = 2u,
		/// <summary>
		/// Map CR to NL
		/// </summary>
		OCRNL = 0x10u,
		/// <summary>
		/// Don't output CR
		/// </summary>
		ONLRET = 0x40u
	}

	[Flags]
	public enum TermConrolFlag : uint
	{
		/// <summary>
		/// 8 bits
		/// </summary>
		CS8 = 0x300u,
		/// <summary>
		/// Enable receiver
		/// </summary>
		CREAD = 0x800u,
		/// <summary>
		/// Hang up on last close
		/// </summary>
		HUPCL = 0x4000u
	}

	[Flags]
	public enum TermLocalFlag : uint
	{
		/// <summary>
		/// Visual erase for line kill
		/// </summary>
		ECHOKE = 1u,
		/// <summary>
		/// Visually erase chars
		/// </summary>
		ECHOE = 2u,
		/// <summary>
		/// Echo NL after line kill
		/// </summary>
		ECHOK = 4u,
		/// <summary>
		/// Enable echoing
		/// </summary>
		ECHO = 8u,
		/// <summary>
		/// Echo control chars as ^(Char)
		/// </summary>
		ECHOCTL = 0x40u,
		/// <summary>
		/// Enable signals INTR, QUIT, [D]SUSP
		/// </summary>
		ISIG = 0x80u,
		/// <summary>
		/// Canonicalize input lines
		/// </summary>
		ICANON = 0x100u,
		/// <summary>
		/// Enable DISCARD and LNEXT
		/// </summary>
		IEXTEN = 0x400u
	}

	public enum TermSpecialControlCharacter
	{
		VEOF = 0,
		VEOL = 1,
		VEOL2 = 2,
		VERASE = 3,
		VWERASE = 4,
		VKILL = 5,
		VREPRINT = 6,
		VINTR = 8,
		VQUIT = 9,
		VSUSP = 10,
		VDSUSP = 11,
		VSTART = 12,
		VSTOP = 13,
		VLNEXT = 14,
		VDISCARD = 15,
		VMIN = 16,
		VTIME = 17,
		VSTATUS = 18
	}

	public struct WinSize
	{
		public ushort Rows;

		public ushort Cols;

		public ushort XPixel;

		public ushort YPixel;

		public WinSize(ushort rows, ushort cols)
		{
			Rows = rows;
			Cols = cols;
			XPixel = 0;
			YPixel = 0;
		}
	}

	public struct Termios
	{
		public const int NCCS = 20;

		public IntPtr IFlag;

		public IntPtr OFlag;

		public IntPtr CFlag;

		public IntPtr LFlag;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
		public sbyte[] CC;

		public IntPtr ISpeed;

		public IntPtr OSpeed;

		public Termios(TermInputFlag inputFlag, TermOuptutFlag outputFlag, TermConrolFlag controlFlag, TermLocalFlag localFlag, TermSpeed speed, IDictionary<TermSpecialControlCharacter, sbyte> controlCharacters)
		{
			IFlag = (IntPtr)(long)inputFlag;
			OFlag = (IntPtr)(long)outputFlag;
			CFlag = (IntPtr)(long)controlFlag;
			LFlag = (IntPtr)(long)localFlag;
			CC = new sbyte[20];
			foreach (KeyValuePair<TermSpecialControlCharacter, sbyte> controlCharacter in controlCharacters)
			{
				CC[(int)controlCharacter.Key] = controlCharacter.Value;
			}
			ISpeed = IntPtr.Zero;
			OSpeed = IntPtr.Zero;
			cfsetispeed(ref this, (IntPtr)(long)speed);
			cfsetospeed(ref this, (IntPtr)(long)speed);
		}
	}

	internal const int STDIN_FILENO = 0;

	internal const int TCSANOW = 0;

	internal const uint TIOCSIG = 536900703u;

	internal const ulong TIOCSWINSZ = 2148037735uL;

	internal const int SIGHUP = 1;

	private const string LibSystem = "libSystem.dylib";

	private static readonly int SizeOfIntPtr = Marshal.SizeOf(typeof(IntPtr));

	[DllImport("libSystem.dylib")]
	internal static extern int cfsetispeed(ref Termios termios, IntPtr speed);

	[DllImport("libSystem.dylib")]
	internal static extern int cfsetospeed(ref Termios termios, IntPtr speed);

	[DllImport("libSystem.dylib", SetLastError = true)]
	internal static extern int forkpty(ref int master, StringBuilder name, ref Termios termp, ref WinSize winsize);

	[DllImport("libSystem.dylib", SetLastError = true)]
	internal static extern int waitpid(int pid, ref int status, int options);

	[DllImport("libSystem.dylib", SetLastError = true)]
	internal static extern int ioctl(int fd, ulong request, int data);

	[DllImport("libSystem.dylib", SetLastError = true)]
	internal static extern int ioctl(int fd, ulong request, ref WinSize winSize);

	[DllImport("libSystem.dylib", SetLastError = true)]
	internal static extern int kill(int pid, int signal);

	internal static void execvpe(string file, string[] args, IDictionary<string, string> environment)
	{
		if (environment != null)
		{
			IntPtr intPtr = Marshal.AllocHGlobal((environment.Count + 1) * SizeOfIntPtr);
			int num = 0;
			foreach (KeyValuePair<string, string> item in environment)
			{
				IntPtr val = Marshal.StringToHGlobalAnsi(item.Key + "=" + item.Value);
				Marshal.WriteIntPtr(intPtr, num, val);
				num += SizeOfIntPtr;
			}
			Marshal.WriteIntPtr(intPtr, num, IntPtr.Zero);
			Marshal.WriteIntPtr(_NSGetEnviron(), intPtr);
		}
		if (execvp(file, args) == -1)
		{
			Environment.Exit(Marshal.GetLastWin32Error());
		}
		else
		{
			Environment.Exit(-1);
		}
	}

	[DllImport("libSystem.dylib", SetLastError = true)]
	private static extern int execvp([MarshalAs(UnmanagedType.LPStr)] string file, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] args);

	[DllImport("libSystem.dylib")]
	private static extern IntPtr _NSGetEnviron();
}
