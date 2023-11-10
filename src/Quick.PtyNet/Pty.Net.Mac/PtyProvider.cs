using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Pty.Net.Unix;

namespace Pty.Net.Mac;

/// <summary>
/// Provides a pty connection for MacOS machines.
/// </summary>
internal class PtyProvider : Pty.Net.Unix.PtyProvider
{
	/// <inheritdoc />
	public override Task<IPtyConnection> StartTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken)
	{
		NativeMethods.WinSize winsize = new NativeMethods.WinSize((ushort)options.Rows, (ushort)options.Cols);
		string[] execvpArgs = Pty.Net.Unix.PtyProvider.GetExecvpArgs(options);
		Dictionary<NativeMethods.TermSpecialControlCharacter, sbyte> controlCharacters = new Dictionary<NativeMethods.TermSpecialControlCharacter, sbyte>
		{
			{
				NativeMethods.TermSpecialControlCharacter.VEOF,
				4
			},
			{
				NativeMethods.TermSpecialControlCharacter.VEOL,
				-1
			},
			{
				NativeMethods.TermSpecialControlCharacter.VEOL2,
				-1
			},
			{
				NativeMethods.TermSpecialControlCharacter.VERASE,
				sbyte.MaxValue
			},
			{
				NativeMethods.TermSpecialControlCharacter.VWERASE,
				23
			},
			{
				NativeMethods.TermSpecialControlCharacter.VKILL,
				21
			},
			{
				NativeMethods.TermSpecialControlCharacter.VREPRINT,
				18
			},
			{
				NativeMethods.TermSpecialControlCharacter.VINTR,
				3
			},
			{
				NativeMethods.TermSpecialControlCharacter.VQUIT,
				28
			},
			{
				NativeMethods.TermSpecialControlCharacter.VSUSP,
				26
			},
			{
				NativeMethods.TermSpecialControlCharacter.VSTART,
				17
			},
			{
				NativeMethods.TermSpecialControlCharacter.VSTOP,
				19
			},
			{
				NativeMethods.TermSpecialControlCharacter.VLNEXT,
				22
			},
			{
				NativeMethods.TermSpecialControlCharacter.VDISCARD,
				15
			},
			{
				NativeMethods.TermSpecialControlCharacter.VMIN,
				1
			},
			{
				NativeMethods.TermSpecialControlCharacter.VTIME,
				0
			},
			{
				NativeMethods.TermSpecialControlCharacter.VDSUSP,
				25
			},
			{
				NativeMethods.TermSpecialControlCharacter.VSTATUS,
				20
			}
		};
		NativeMethods.Termios termp = new NativeMethods.Termios(NativeMethods.TermInputFlag.BRKINT | NativeMethods.TermInputFlag.ICRNL | NativeMethods.TermInputFlag.IXON | NativeMethods.TermInputFlag.IXANY | NativeMethods.TermInputFlag.IMAXBEL | NativeMethods.TermInputFlag.IUTF8, NativeMethods.TermOuptutFlag.OPOST | NativeMethods.TermOuptutFlag.ONLCR, NativeMethods.TermConrolFlag.CS8 | NativeMethods.TermConrolFlag.CREAD | NativeMethods.TermConrolFlag.HUPCL, NativeMethods.TermLocalFlag.ECHOKE | NativeMethods.TermLocalFlag.ECHOE | NativeMethods.TermLocalFlag.ECHOK | NativeMethods.TermLocalFlag.ECHO | NativeMethods.TermLocalFlag.ECHOCTL | NativeMethods.TermLocalFlag.ISIG | NativeMethods.TermLocalFlag.ICANON | NativeMethods.TermLocalFlag.IEXTEN, NativeMethods.TermSpeed.B38400, controlCharacters);
		int master = 0;
		int num = NativeMethods.forkpty(ref master, null, ref termp, ref winsize);
		switch (num)
		{
		case -1:
			throw new InvalidOperationException($"forkpty(4) failed with error {Marshal.GetLastWin32Error()}");
		case 0:
			Environment.CurrentDirectory = options.Cwd;
			NativeMethods.execvpe(options.App, execvpArgs, options.Environment);
			break;
		}
		return Task.FromResult((IPtyConnection)new PtyConnection(master, num));
	}
}
