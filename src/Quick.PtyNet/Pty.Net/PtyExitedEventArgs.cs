using System;

namespace Pty.Net;

/// <summary>
/// Event arguments that encapsulate data about the pty process exit.
/// </summary>
public class PtyExitedEventArgs : EventArgs
{
	/// <summary>
	/// Gets or sets the exit code of the pty process.
	/// </summary>
	public int ExitCode { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.PtyExitedEventArgs" /> class.
	/// </summary>
	/// <param name="exitCode">Exit code of the pty process.</param>
	internal PtyExitedEventArgs(int exitCode)
	{
		ExitCode = exitCode;
	}
}
