using Pty.Net.Unix;

namespace Pty.Net.Linux;

/// <summary>
/// A connection to a pseudoterminal on linux machines.
/// </summary>
internal class PtyConnection : Pty.Net.Unix.PtyConnection
{
	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.Linux.PtyConnection" /> class.
	/// </summary>
	/// <param name="controller">The fd of the pty controller.</param>
	/// <param name="pid">The id of the spawned process.</param>
	public PtyConnection(int controller, int pid)
		: base(controller, pid)
	{
	}

	/// <inheritdoc />
	protected override bool Kill(int controller)
	{
		return NativeMethods.kill(base.Pid, 1) != -1;
	}

	/// <inheritdoc />
	protected override bool Resize(int fd, int cols, int rows)
	{
		NativeMethods.WinSize winSize = new NativeMethods.WinSize((ushort)rows, (ushort)cols);
		return NativeMethods.ioctl(fd, 21524uL, ref winSize) != -1;
	}

	/// <inheritdoc />
	protected override bool WaitPid(int pid, ref int status)
	{
		return NativeMethods.waitpid(pid, ref status, 0) != -1;
	}
}
