using System;

namespace Screenary
{
	public interface ISessionRequestListener
	{
		void OnSessionJoinRequested(char[] sessionKey);
		void OnSessionLeaveRequested(UInt32 sessionId, string username);
		void OnSessionAuthenticationRequested(UInt32 sessionId, string username, string password);
		void OnSessionCreateRequested(string username, string password);
		void OnSessionTerminationRequested(UInt32 sessionId, char[] sessionKey, UInt32 sessionStatus);
		void OnSessionScreenControlRequested(char[] sessionKey, string username);
		void OnSessionOperationFail(string errorMessage);
		void OnRecvMouseEvent(UInt32 sessionId, UInt16 pointerFlag, double x, double y);
	}
}

