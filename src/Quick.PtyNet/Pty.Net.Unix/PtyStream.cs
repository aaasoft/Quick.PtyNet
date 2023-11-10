using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Pty.Net.Unix;

/// <summary>
/// A stream connected to a pty.
/// </summary>
internal sealed class PtyStream : FileStream
{
	/// <inheritdoc />
	public override bool CanSeek => false;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:Pty.Net.Unix.PtyStream" /> class.
	/// </summary>
	/// <param name="fd">The fd to connect the stream to.</param>
	/// <param name="fileAccess">The access permissions to set on the fd.</param>
	public PtyStream(int fd, FileAccess fileAccess)
		: base(new SafeFileHandle((IntPtr)fd, ownsHandle: false), fileAccess, 1024, isAsync: false)
	{
	}
}
