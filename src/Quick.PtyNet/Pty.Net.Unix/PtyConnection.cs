using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Pty.Net.Unix;

/// <summary>
/// A connection to a Unix-style pseudoterminal.
/// </summary>
internal abstract class PtyConnection : IPtyConnection, IDisposable
{
	private const int EINTR = 4;

	private const int ECHILD = 10;

	private readonly int controller;

	private readonly int pid;

	private readonly ManualResetEvent terminalProcessTerminatedEvent = new ManualResetEvent(initialState: false);

	private int exitCode;

	private int exitSignal;

	/// <inheritdoc />
	public Stream ReaderStream { get; }

	/// <inheritdoc />
	public Stream WriterStream { get; }

	/// <inheritdoc />
	public int Pid => pid;

	/// <inheritdoc />
	public int ExitCode => exitCode;

	/// <inheritdoc />
	public event EventHandler<PtyExitedEventArgs> ProcessExited;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.Unix.PtyConnection" /> class.
	/// </summary>
	/// <param name="controller">The fd of the pty controller.</param>
	/// <param name="pid">The id of the spawned process.</param>
	public PtyConnection(int controller, int pid)
	{
		ReaderStream = new PtyStream(controller, FileAccess.Read);
		WriterStream = new PtyStream(controller, FileAccess.Write);
		this.controller = controller;
		this.pid = pid;
		Thread thread = new Thread(ChildWatcherThreadProc);
		thread.IsBackground = true;
		thread.Priority = ThreadPriority.Lowest;
		thread.Name = $"Watcher thread for child process {pid}";
		thread.Start();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		ReaderStream?.Dispose();
		WriterStream?.Dispose();
		Kill();
	}

	/// <inheritdoc />
	public void Kill()
	{
		if (!Kill(controller))
		{
			throw new InvalidOperationException($"Killing terminal failed with error {Marshal.GetLastWin32Error()}");
		}
	}

	/// <inheritdoc />
	public void Resize(int cols, int rows)
	{
		if (!Resize(controller, cols, rows))
		{
			throw new InvalidOperationException($"Resizing terminal failed with error {Marshal.GetLastWin32Error()}");
		}
	}

	/// <inheritdoc />
	public bool WaitForExit(int milliseconds)
	{
		return terminalProcessTerminatedEvent.WaitOne(milliseconds);
	}

	/// <summary>
	/// OS-specific implementation of the pty-resize function.
	/// </summary>
	/// <param name="controller">The fd of the pty controller.</param>
	/// <param name="cols">The number of columns to resize to.</param>
	/// <param name="rows">The number of rows to resize to.</param>
	/// <returns>True if the function suceeded to resize the pty, false otherwise.</returns>
	protected abstract bool Resize(int controller, int cols, int rows);

	/// <summary>
	/// Kills the terminal process.
	/// </summary>
	/// <param name="controller">The fd of the pty controller.</param>
	/// <returns>True if the function succeeded in killing the process, false otherwise.</returns>
	protected abstract bool Kill(int controller);

	/// <summary>
	/// OS-specific implementation of waiting on the given process id.
	/// </summary>
	/// <param name="pid">The process id to wait on.</param>
	/// <param name="status">The status of the process.</param>
	/// <returns>True if the function succeeded to get the status of the process, false otherwise.</returns>
	protected abstract bool WaitPid(int pid, ref int status);

	private void ChildWatcherThreadProc()
	{
		Console.WriteLine($"Waiting on {pid}");
		int status = 0;
		if (!WaitPid(pid, ref status))
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			Console.WriteLine($"Wait failed with {lastWin32Error}");
			if (lastWin32Error == 4)
			{
				ChildWatcherThreadProc();
			}
			else
			{
				_ = 10;
			}
		}
		else
		{
			Console.WriteLine("Wait succeeded");
			exitSignal = status & 0x7F;
			exitCode = ((exitSignal == 0) ? ((status >> 8) & 0xFF) : 0);
			terminalProcessTerminatedEvent.Set();
			this.ProcessExited?.Invoke(this, new PtyExitedEventArgs(exitCode));
		}
	}
}
