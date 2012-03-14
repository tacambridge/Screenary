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
		void OnSessionRemoteAccessRequested(char[] sessionKey, string username);
		void OnSessionRemoteAccessPermissionSet(char[] sessionKey, string username, Boolean permission);
		void OnSessionTermRemoteAccessRequested(char[] sessionKey, string username);
		void OnRecvMouseEvent(UInt32 sessionId, UInt16 pointerFlags, int x, int y);
		void OnRecvKeyboardEvent(UInt32 sessionId, UInt16 pointerFlags, UInt16 keyCode);
	}
}
