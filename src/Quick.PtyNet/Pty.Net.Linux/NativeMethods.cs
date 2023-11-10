using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Pty.Net.Linux;

internal static class NativeMethods
{
	public enum TermSpeed : uint
	{
		B38400 = 15u
	}

	[Flags]
	public enum TermInputFlag : uint
	{
		BRKINT = 2u,
		ICRNL = 0x100u,
		IXON = 0x400u,
		IXANY = 0x800u,
		IMAXBEL = 0x2000u,
		IUTF8 = 0x4000u
	}

	[Flags]
	public enum TermOuptutFlag : uint
	{
		OPOST = 1u,
		ONLCR = 4u
	}

	[Flags]
	public enum TermConrolFlag : uint
	{
		CS8 = 0x30u,
		CREAD = 0x80u,
		HUPCL = 0x400u
	}

	[Flags]
	public enum TermLocalFlag : uint
	{
		ECHOKE = 0x800u,
		ECHOE = 0x10u,
		ECHOK = 0x20u,
		ECHO = 8u,
		ECHOCTL = 0x200u,
		ISIG = 1u,
		ICANON = 2u,
		IEXTEN = 0x8000u
	}

	public enum TermSpecialControlCharacter
	{
		VEOF = 4,
		VEOL = 11,
		VEOL2 = 16,
		VERASE = 2,
		VWERASE = 14,
		VKILL = 3,
		VREPRINT = 12,
		VINTR = 0,
		VQUIT = 1,
		VSUSP = 10,
		VSTART = 8,
		VSTOP = 9,
		VLNEXT = 15,
		VDISCARD = 13,
		VMIN = 6,
		VTIME = 5
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
		public const int NCCS = 32;

		public uint IFlag;

		public uint OFlag;

		public uint CFlag;

		public uint LFlag;

		public sbyte line;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public sbyte[] CC;

		public uint ISpeed;

		public uint OSpeed;

		public Termios(TermInputFlag inputFlag, TermOuptutFlag outputFlag, TermConrolFlag controlFlag, TermLocalFlag localFlag, TermSpeed speed, IDictionary<TermSpecialControlCharacter, sbyte> controlCharacters)
		{
			IFlag = (uint)inputFlag;
			OFlag = (uint)outputFlag;
			CFlag = (uint)controlFlag;
			LFlag = (uint)localFlag;
			CC = new sbyte[32];
			foreach (KeyValuePair<TermSpecialControlCharacter, sbyte> controlCharacter in controlCharacters)
			{
				CC[(int)controlCharacter.Key] = controlCharacter.Value;
			}
			line = 0;
			ISpeed = 0u;
			OSpeed = 0u;
			cfsetispeed(ref this, (IntPtr)(long)speed);
			cfsetospeed(ref this, (IntPtr)(long)speed);
		}
	}

	internal const int STDIN_FILENO = 0;

	internal const uint TIOCSIG = 1074025526u;

	internal const ulong TIOCSWINSZ = 21524uL;

	internal const int SIGHUP = 1;

	private const string LibSystem = "libc.so.6";

	private static readonly int SizeOfIntPtr = Marshal.SizeOf(typeof(IntPtr));

	[DllImport("libc.so.6")]
	internal static extern int cfsetispeed(ref Termios termios, IntPtr speed);

	[DllImport("libc.so.6")]
	internal static extern int cfsetospeed(ref Termios termios, IntPtr speed);

	[DllImport("libutil.so.1", SetLastError = true)]
	internal static extern int forkpty(ref int master, StringBuilder name, ref Termios termp, ref WinSize winsize);

	[DllImport("libc.so.6", SetLastError = true)]
	internal static extern int waitpid(int pid, ref int status, int options);

	[DllImport("libc.so.6", SetLastError = true)]
	internal static extern int ioctl(int fd, ulong request, int data);

	[DllImport("libc.so.6", SetLastError = true)]
	internal static extern int ioctl(int fd, ulong request, ref WinSize winSize);

	[DllImport("libc.so.6", SetLastError = true)]
	internal static extern int kill(int pid, int signal);

	internal static void execvpe(string file, string[] args, IDictionary<string, string> environment)
	{
		foreach (KeyValuePair<string, string> item in environment)
		{
			setenv(item.Key, item.Value, 1);
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

	[DllImport("libc.so.6", SetLastError = true)]
	private static extern int setenv(string name, string value, int overwrite);

	[DllImport("libc.so.6", SetLastError = true)]
	private static extern int execvp([MarshalAs(UnmanagedType.LPStr)] string file, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] args);
}
