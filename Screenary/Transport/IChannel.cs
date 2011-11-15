using System;

namespace Screenary
{
	public interface IChannel
	{
		UInt16 GetChannelId();
		bool OnRecv(byte[] buffer, byte pduType);
	}
}
