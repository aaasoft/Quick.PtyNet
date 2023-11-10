using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Pty.Net.Unix;

/// <summary>
/// Abstract class that provides a pty connection for unix-like machines.
/// </summary>
internal abstract class PtyProvider : IPtyProvider
{
	/// <inheritdoc />
	public abstract Task<IPtyConnection> StartTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the arguments to pass to execvp.
	/// </summary>
	/// <param name="options">The options for spawning the pty.</param>
	/// <returns>An array of arguments to pass to execvp.</returns>
	protected static string[] GetExecvpArgs(PtyOptions options)
	{
		if (options.CommandLine.Length == 0)
		{
			return new string[2] { options.App, null };
		}
		string[] array = new string[options.CommandLine.Length + 2];
		Array.Copy(options.CommandLine, 0, array, 1, options.CommandLine.Length);
		array[0] = options.App;
		return array;
	}
}
