using Pty.Net.Unix;

namespace Pty.Net.Mac;

/// <summary>
/// A connection to a pseudoterminal on MacOS machines.
/// </summary>
internal class PtyConnection : Pty.Net.Unix.PtyConnection
{
	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.Mac.PtyConnection" /> class.
	/// </summary>
	/// <param name="controller">The fd of the pty controller.</param>
	/// <param name="pid">The id of the spawned process.</param>
	public PtyConnection(int controller, int pid)
		: base(controller, pid)
	{
	}

	/// <inheritdoc />
	protected override bool Kill(int fd)
	{
		return NativeMethods.ioctl(fd, 536900703uL, 1) != -1;
	}

	/// <inheritdoc />
	protected override bool Resize(int fd, int cols, int rows)
	{
		NativeMethods.WinSize winSize = new NativeMethods.WinSize((ushort)rows, (ushort)cols);
		return NativeMethods.ioctl(fd, 2148037735uL, ref winSize) != -1;
	}

	/// <inheritdoc />
	protected override bool WaitPid(int pid, ref int status)
	{
		return NativeMethods.waitpid(pid, ref status, 0) != -1;
	}
}
