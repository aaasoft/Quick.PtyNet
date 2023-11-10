using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Pty.Net;

/// <summary>
/// A provider of pseudoterminal connections.
/// </summary>
internal interface IPtyProvider
{
	/// <summary>
	/// Spawns a process as a pseudoterminal.
	/// </summary>
	/// <param name="options">The options for spawning the pty.</param>
	/// <param name="trace">The tracer to trace execution with.</param>
	/// <param name="cancellationToken">A token to cancel the task early.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that completes once the process has spawned.</returns>
	Task<IPtyConnection> StartTerminalAsync(PtyOptions options, TraceSource trace, CancellationToken cancellationToken);
}
