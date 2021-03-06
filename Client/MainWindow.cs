/**
 * Screenary: Real-Time Collaboration Redefined.
 * Main Window
 *
 * Copyright 2011-2012 Marc-Andre Moreau <marcandre.moreau@gmail.com>
 * Copyright 2011-2012 Hai-Long Nguyen <hlnguyen21@gmail.com>
 * Copyright 2011-2012 Terri-Anne Cambridge <tacambridge@gmail.com>
 * Copyright 2011-2012 Marwan Samaha <mar6@hotmail.com>
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Gtk;
using System;
using System.IO;
using System.Threading;
using Screenary.Client;
using Screenary;
using FreeRDP;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

public partial class MainWindow : Gtk.Window, IUserAction, ISurfaceClient, ISource, ISessionResponseListener
{	
	internal Gdk.GC gc;
	internal Config config;
	internal SessionClient sessionClient;
	internal SurfaceClient surfaceClient;
	internal InputClient inputClient;
	internal int width, height;
	internal Gdk.Window window;
	internal Gdk.Pixbuf surface;
	internal Gdk.Drawable drawable;
	internal SurfaceReceiver receiver;
	internal string sessionKey;
	internal string username;
	internal string password;
	internal TransportClient transport;
	internal ArrayList participants;
	internal string creator;
	internal string controller;
	private uint messageId = 0;
	private uint contextId = 1;
	private readonly object mainLock = new object();
	internal ArrayList incomingRequestList = new ArrayList();
	internal RdpSource rdpSource;
	internal PcapSource pcapSource;
	internal Keyboard keyboard;
	internal Boolean statusText = false;
	
	internal IClientState currentState;
	internal IClientState[] clientStates;
	internal static int STARTED_STATE = 0;
	internal static int SENDER_CREATED_STATE = 1;
	internal static int RECEIVER_JOINED_STATE = 2;
	internal static int RECEIVER_AUTHENTICATED_STATE = 3;
	internal static int SENDER_SENDING_STATE = 4;
	internal static int RECEIVER_IN_CONTROL_STATE = 5;
	internal static int SENDER_SENDING_REMOTE_STATE = 6;
	
	public MainWindow(int m): base(Gtk.WindowType.Toplevel)
	{
		Build();

		/* Instantiate client states */
		clientStates = new IClientState[7]
		{
			new StartedState(this),
			new SenderCreatedState(this),
			new ReceiverJoinedState(this),
			new ReceiverAuthenticatedState(this),
			new SenderSendingState(this),
			new ReceiverInControlState(this),
			new SenderSendingRemoteState(this)
		};
		
		/* Set current state to STARTED */
		currentState = clientStates[STARTED_STATE];
		currentState.refresh();
		
		width = 1024;
		height = 768;
		
		config = Config.Load();
		
		window = mainDrawingArea.GdkWindow;
		drawable = (Gdk.Drawable) window;
		
		mainDrawingArea.AddEvents(
			(int) Gdk.EventMask.ButtonPressMask |
			(int) Gdk.EventMask.ButtonReleaseMask |
			(int) Gdk.EventMask.PointerMotionMask |
			(int) Gdk.EventMask.KeyPressMask |
			(int) Gdk.EventMask.KeyReleaseMask);
		
		gc = new Gdk.GC(drawable);
		gc.ClipRectangle = new Gdk.Rectangle(0, 0, width, height);
		
		surface = new Gdk.Pixbuf(Gdk.Colorspace.Rgb, true, 8, width, height);		
		window.InvalidateRect(new Gdk.Rectangle(0, 0, width, height), true);
		
		receiver = new SurfaceReceiver(window, surface);
		
		this.transport = null;
		
		pcapSource = new PcapSource(this);
		
		keyboard = new Keyboard();
		
		if (config.BroadcasterAutoconnect)
			OnUserConnect(config.BroadcasterHostname, config.BroadcasterPort);
	}
	
	/**
	 * Invoked when mouse clicks occur on window
	 */ 
	protected void OnMainDrawingAreaButtonPressEvent(object o, Gtk.ButtonPressEventArgs args)
	{
		Gdk.EventButton e = args.Event;
		
		if (inputClient.Active && transport.isConnected() && sessionClient.GetSessionId() != 0)
			inputClient.SendMouseEvent(e.Button, true, (int) e.X, (int) e.Y);
	}
	
	/**
	 * Invoked when mouse clicks are released on window
	 */ 
	protected void OnMainDrawingAreaButtonReleaseEvent(object o, Gtk.ButtonReleaseEventArgs args)
	{
		Gdk.EventButton e = args.Event;
		
		if (transport.isConnected() && sessionClient.GetSessionId() != 0)
		{
			if (inputClient.Active)
				inputClient.SendMouseEvent(e.Button, false, (int) e.X, (int) e.Y);
		}
	}
	
	/**
	 * Invoked when mouse moves across window
	 */ 
	protected void OnMainDrawingAreaMotionNotifyEvent(object o, Gtk.MotionNotifyEventArgs args)
	{
		Gdk.EventMotion e = args.Event;
		
		if (transport.isConnected() && sessionClient.GetSessionId() != 0)
		{
			if (inputClient.Active)
				inputClient.SendMouseMotionEvent((int) e.X, (int) e.Y);
		}
	}
	
	/**
	 * Invoked when keyboard presses occur on window
	 */ 
	protected void OnMainDrawingAreaKeyPressEvent(object o, Gtk.KeyPressEventArgs args)
	{
		UInt32 scancode;
		int extended = 0;
		Gdk.EventKey e = args.Event;
		
		if (transport.isConnected() && sessionClient.GetSessionId() != 0)
		{
			if (inputClient.Active)
			{
				scancode = keyboard.GetRdpScancodeFromX11Keycode((UInt32) e.HardwareKeycode, ref extended);
				inputClient.SendKeyboardEvent(scancode, true, (extended == 0) ? false : true);
			}
		}
	}
	
	/**
	 * Invoked when keyboard press is released on window
	 */ 
	protected void OnMainDrawingAreaKeyReleaseEvent(object o, Gtk.KeyReleaseEventArgs args)
	{
		UInt32 scancode;
		int extended = 0;
		Gdk.EventKey e = args.Event;
		
		if (transport.isConnected() && sessionClient.GetSessionId() != 0)
		{
			if (inputClient.Active)
			{
				scancode = keyboard.GetRdpScancodeFromX11Keycode((UInt32) e.HardwareKeycode, ref extended);
				inputClient.SendKeyboardEvent(scancode, false, (extended == 0) ? false : true);
			}
		}
	}
	
	/**
	 * Configure the drawing area when the application launches
	 */
	protected void OnMainDrawingAreaExposeEvent(object o, Gtk.ExposeEventArgs args)
	{
		Gdk.Rectangle[] rects = args.Event.Region.GetRectangles();
		
		foreach (Gdk.Rectangle rect in rects)
		{
			drawable.DrawPixbuf(gc, surface, rect.X, rect.Y, rect.X, rect.Y,
				rect.Width, rect.Height, Gdk.RgbDither.None, 0, 0);
		}
	}
	
	/**
	 * Invoked when application's window is closed. Depending on the state of
	 * the application, ensure that it closes gracefully
	 */
	protected void OnDeleteEvent(object sender, DeleteEventArgs a)
	{	
		if (currentState == clientStates[RECEIVER_IN_CONTROL_STATE])
		{
			sessionClient.SendTermRemoteAccessReq(this.sessionKey.ToCharArray(), this.username);	
			sessionClient.SendLeaveReq(username);
		}
		else if (currentState == clientStates[RECEIVER_AUTHENTICATED_STATE])
		{
			sessionClient.SendLeaveReq(username);
		}
		else if (currentState == clientStates[SENDER_CREATED_STATE])
		{			
			sessionClient.SendTermReq(sessionKey.ToCharArray());
		}
		
		if (this.transport != null)		
			this.transport.Disconnect();				
			
		Application.Quit();		

		a.RetVal = true;
	}
	
	/**
	 * When application is exited notify the sender/receiver and disconnect from server 
	 */
	protected void OnQuitActionActivated(object sender, System.EventArgs e)
	{				
		if (currentState == clientStates[RECEIVER_IN_CONTROL_STATE])
		{
			sessionClient.SendTermRemoteAccessReq(this.sessionKey.ToCharArray(), this.username);	
			sessionClient.SendLeaveReq(username);
		}
		else if (currentState == clientStates[RECEIVER_AUTHENTICATED_STATE])
		{
			sessionClient.SendLeaveReq(username);
		}
		else if (currentState == clientStates[SENDER_CREATED_STATE] ||
			currentState == clientStates[SENDER_SENDING_STATE] ||
			currentState == clientStates[SENDER_SENDING_REMOTE_STATE])
		{			
			sessionClient.SendTermReq(sessionKey.ToCharArray());
		}
		
		if (this.transport != null)				
			this.transport.Disconnect();							
		
		Application.Quit();		
	}
	
	/**
	 * About Dialog opens to display information about the application 
	 */
	protected void OnAboutActionActivated(object sender, System.EventArgs e)
	{
		AboutDialog about = new AboutDialog();

		about.ProgramName = "Screenary";
		about.Version = "1.0.0";
		about.Comments = "A screencasting application for Ubuntu";
    	about.Authors = new string [] {"Marc Andre", "Hai-Long", "Gina", "Terri-Anne", "Marwan"};
		about.Website = "http://www.screenary.com/";
		
		about.Run();
		about.Destroy();
	}
	
	/**
	 * Open a file for playback. A file chooser dialog opens and allows the user to select the file they want to open. 
	 */
	protected void OnOpenActionActivated (object sender, System.EventArgs e)
	{
		/* Create and display a fileChooserDialog */
		FileChooserDialog chooser = new FileChooserDialog(
		   "Please select a video to view ...",
		   this, FileChooserAction.Open,
		   "Cancel", ResponseType.Cancel,
		   "Open", ResponseType.Accept);
		
		/* Open */
		if (chooser.Run() == (int) ResponseType.Accept)
		{			
			/* Set the MainWindow Title to the filename. */
			this.Title = "Screenary now showing: " + chooser.Filename.ToString();
				
			/* Set the filename */
			String filename = chooser.Filename.ToString();
			
			/* Start playing asynchronously */
			pcapSource.Play(filename);
			
			/* Destroy the fileChooserDialog, otherwise it stays open */
			chooser.Destroy();
		}
		else
		{
			/* Cancel */
			chooser.Destroy();
		}
	} 
	
	/**
	 * Resets the Drawing Area to blank 
	 */
	protected void OnCloseActionActivated (object sender, System.EventArgs e)
	{
		this.Title = "Screenary";
		window = mainDrawingArea.GdkWindow;
		drawable = (Gdk.Drawable) window;
		
		gc = new Gdk.GC(drawable);
		gc.ClipRectangle = new Gdk.Rectangle(0, 0, width, height);
		
		surface = new Gdk.Pixbuf(Gdk.Colorspace.Rgb, true, 8, width, height);		
		window.InvalidateRect(new Gdk.Rectangle(0, 0, width, height), true);
		
		receiver = new SurfaceReceiver(window, surface);
	}
	
	/**
	 * Event when user requests remote access
	 */ 
	protected void OnRequestRemoteAccessActivated (object sender, System.EventArgs e)
	{
		sessionClient.SendRemoteAccessReq(this.sessionKey.ToCharArray(), this.username);
	}
	
	/**
	 * Event occurs when user requests to terminate remote access
	 */ 
	protected void OnTerminateRemoteAccessActivated (object sender, System.EventArgs e)
	{
		sessionClient.SendTermRemoteAccessReq(this.sessionKey.ToCharArray(), this.username);
	}
	
	/**
	 * Event occurs when Client needs to update surface
	 */ 
	public void OnSurfaceCommand(BinaryReader s)
	{
		Gtk.Application.Invoke(delegate {
			
			SurfaceCommand cmd = SurfaceCommand.Parse(s);
			
			if (cmd != null)
			{
				cmd.Execute(receiver);
				window.ProcessUpdates(false);
			}
		});
	}
	
	/**
	 * Event occurs when Client needs to update surface
	 */	
	public void OnSurfaceCommand(SurfaceCommand cmd)
	{
		byte[] pduBuffer = cmd.Write();
		
		surfaceClient.SendSurfaceCommand(pduBuffer, sessionClient.GetSessionId());
	
		string hostname = config.RdpServerHostname;
				
		if (hostname.Equals("localhost") || hostname.Equals("127.0.0.1"))
					return;
		
		Gtk.Application.Invoke(delegate {
			
			if (cmd != null)
			{
				cmd.Execute(receiver);
				window.ProcessUpdates(false);
			}
		});
	}
	
	/**
	 * Invoked when Share/StopShare toggle is clicked. Delegate to current state
	 */
	protected void OnShareScreenActionActivated(object sender, System.EventArgs e)
	{
		currentState.OnShareScreenAction();
	}
	
	/**
	 * Is invoked when application launches. Connects the Client to the Server
	 */
	public void OnUserConnect(string address, int port)
	{
		ChannelDispatcher dispatcher = new ChannelDispatcher();
		this.transport = new TransportClient(dispatcher);
		
		sessionClient = new SessionClient(this.transport, this);
		dispatcher.RegisterChannel(sessionClient);
		
		surfaceClient = new SurfaceClient(this, this.transport);
		dispatcher.RegisterChannel(surfaceClient);
		
		inputClient = new InputClient(this.transport);
		dispatcher.RegisterChannel(inputClient);
		
		try
		{
			this.transport.Connect(address, port);
			DisplayStatusText("Welcome! You are connected to Screenary server at " + address + " : " + port);
		}
		catch(Exception e)
		{
			DisplayStatusText("Could not connect to Screenary server at " + address + " : " + port);
		}
	}
	
	/**
	 * When Create Session is activated launch the Create Session Dialog
	 */
	protected void OnCreateSessionActionActivated(object sender, System.EventArgs e)
	{
		CreateSessionDialog connect = new CreateSessionDialog(this);
	}
	
	/**
	 * When Join Session is activated launch the Join Dialog
	 */
	protected void OnJoinSessionActionActivated(object sender, System.EventArgs e)
	{
		JoinDialog join = new JoinDialog(this);
	}
	
	/**
	 * Called from the Create Session Dialog, sends the create request
	 */
	public void OnUserCreateSession(string username, string password)
	{
		if (this.transport.isConnected())
		{
			this.creator = username;
			this.username = username;
			sessionClient.SendCreateReq(username, password);
		}
		else
		{
			DisplayStatusText("Please connect to the server first");
		}
	}
	
	/**
	 * Called from the Join Dialog, sends the join request
	 */
	public void OnUserJoinSession(string sessionKey, string username, string password)
	{
		if (this.transport.isConnected())
		{
			this.username = username;
			this.password = password;
			sessionClient.SendJoinReq(sessionKey.ToCharArray());
		}
		else
		{
			DisplayStatusText("Please connect to the server first");
		}
	}
	
	
	/**
	 * When Connect is activated, try OnUserConnect
	 */
	protected void OnConnectActionActivated(object sender, System.EventArgs e)
	{
		OnUserConnect(config.BroadcasterHostname, config.BroadcasterPort);
	}
	
	/**
	 * When a session is joined succesfully, change to the receiver state, refresh, and update notification bar 
	 */
	public void OnSessionJoinSuccess(char[] sessionKey, Boolean isPasswordProtected)
	{
		sessionClient.SendAuthReq(username,password);
				
		this.sessionKey = new string(sessionKey);
		
		currentState = clientStates[RECEIVER_JOINED_STATE];
		currentState.refresh();
		
		/* Update the input channel with the sessionId */
		inputClient.SetSessionId(sessionClient.GetSessionId());
	}
	
	/**
	 * Display/update the participants list
	 */
	public void DisplayParticipants()
	{		
		Gtk.Application.Invoke(delegate {
		
			txtParticipants.Buffer.Clear();
	
			if (participants != null)
			{
				foreach (string username in participants)
				{
					if(username.Equals(creator))
					{
						txtParticipants.Buffer.InsertAtCursor(username + " (Sender)\r\n");
					}
					else if (username.Equals(controller))
					{
						txtParticipants.Buffer.InsertAtCursor(username + " (Remote)\r\n");
					}
					else
						txtParticipants.Buffer.InsertAtCursor(username + "\r\n");
				}
			}
			
		});
	}
	
	/** 
	 * Display/update the participants list for the sender 
	 */
	public void DisplayCreator()
	{		
		Gtk.Application.Invoke(delegate {

			txtParticipants.Buffer.Clear();
			
			if (creator != null)
			{
				txtParticipants.Buffer.InsertAtCursor(creator + " (Sender)\r\n");
			}
		});
	}
	
	/**
	 * Display a message in the notifications bar
	 */ 
	public void DisplayStatusText(string text)
	{
		Gtk.Application.Invoke(delegate {
				
			messageId = notificationBar.Push(contextId, text);
			StatusMessage message = new StatusMessage(this,messageId, contextId);
			message.StartTimer();

		});
	}
	
	/**
	 * When a session is left succesfully, change to the starter state, refresh, and update notification bar
	 */
	public void OnSessionLeaveSuccess()
	{		
		currentState = clientStates[STARTED_STATE];
		currentState.refresh();
		
		this.Title = "Screenary";
		
		DisplayStatusText("You have succesfully left the session.");
	}
	
	/**
	 * When a users has succesfully authenticated into a session, change to the authenticated state, refresh, and update notification bar 
	 */
	public void OnSessionAuthenticationSuccess()
	{
		currentState = clientStates[RECEIVER_AUTHENTICATED_STATE];
		currentState.refresh();
		
		this.Title = "Screenary - Session: " + sessionKey;
		
		DisplayStatusText("You have successfully joined the session! SessionKey: " + sessionKey);
	}
	
	/**
	 * When a session is created succesfully, change to the sender state, refresh, and update notification bar
	 */
	public void OnSessionCreationSuccess(char[] sessionKey)
	{
		string sessionKeyString = new string(sessionKey);
		
		this.sessionKey = sessionKeyString;
		
		currentState = clientStates[SENDER_CREATED_STATE];

		currentState.refresh();
		
		this.Title = "Screenary - Session: " + sessionKeyString;
		
		CreationDialog dialog = new CreationDialog(sessionKeyString);
		
	}
	
	/**
	 * When a session is terminated succesfully, change to the starter state, refresh, and update notification bar
	 */
	public void OnSessionTerminationSuccess(char[] sessionKey)
	{
		currentState = clientStates[STARTED_STATE];
		currentState.refresh();
		
		this.Title = "Screenary";
		
		ExceptionDialog exception = new ExceptionDialog("Alert", "The session has been terminated.");
		
		DisplayStatusText("The session has been terminated.");
	}
	
	/**
	 * Default Exception [FOR NOW]
	 */
	public void OnSessionOperationFail(String errorMessage)
	{
		ExceptionDialog exception = new ExceptionDialog("Operation Fail", errorMessage);
	}
	
	/**
	 * Updates the participants lists
	 */
	public void OnSessionParticipantListUpdate(ArrayList participants)
	{
		this.participants = participants;
			
		DisplayParticipants();
	}
	
	/**
	 * Notifies when a user has joined/left a session, who has control of the session. FOR NOW: Switching states when a user has
	 * taken control. 
	 */
	public void OnSessionNotificationUpdate(string type, string username)
	{		
		if (this.username.Equals(username) && type.Equals("joined"))
		{
			DisplayStatusText("You have successfully joined the session.");
		}
		else if (this.username.Equals(username) && type.Equals("control of"))
		{
			DisplayStatusText("You have control of the session.");
		}
		else
		{
			DisplayStatusText(username + " has " + type + " the session.");
		}
		
		if (type.Equals("control of") && this.username.Equals(username))
		{
			// Save the controller's username
			controller = username;
			if (currentState == clientStates[RECEIVER_AUTHENTICATED_STATE])
			{
				currentState = clientStates[RECEIVER_IN_CONTROL_STATE];
				currentState.refresh();
				DisplayParticipants();
			}
			
			if (currentState == clientStates[SENDER_SENDING_REMOTE_STATE])
			{
				currentState = clientStates[SENDER_SENDING_STATE];
				currentState.refresh();
				DisplayParticipants();
			}
			
		}
		
		if (type.Equals("control of") && !this.username.Equals(username))
		{
			// Save controller's username
			controller = username;
			if (currentState == clientStates[RECEIVER_IN_CONTROL_STATE])
			{
				currentState = clientStates[RECEIVER_AUTHENTICATED_STATE];
				currentState.refresh();
				DisplayParticipants();
			}
			
			if (currentState == clientStates[SENDER_CREATED_STATE] || currentState == clientStates[SENDER_SENDING_STATE])
			{
				currentState = clientStates[SENDER_SENDING_REMOTE_STATE];
				currentState.refresh();
				DisplayParticipants();
			}
		}
	}
	
	/**
	 * Notifies when a user has joined a session and sets the creator for the client.
	 */
	public void OnSessionFirstNotificationUpdate(string type, string username, string senderClient)
	{
		if (this.username.Equals(username) && type.Equals("joined"))
		{
			creator = senderClient;
			DisplayStatusText("You have successfully joined the session.");
		}
		else
		{
			DisplayStatusText(username + " has " + type + " the session.");
		}
	}
	
	/**
	 * Sender receives and handles receiver's request from the Broadcaster
	 */
	public void OnSessionRemoteAccessRequestReceived(string receiverUsername)
	{
		/* Can only accept/deny remote access when sharing screen */
		if (currentState == clientStates[SENDER_SENDING_STATE])
		{
			IncomingRequestDialog request = new IncomingRequestDialog(this,receiverUsername);
			incomingRequestList.Add(request);
		}
	}
	
	/**
	 * When the sender either grants/denies access to the receiver requesting control, this method is called
	 */
	public void OnUserRequestResponse(Boolean permission, string receiverUsername)
	{
		/* Deny all other requests  */
		if (permission) 
		{
			foreach (IncomingRequestDialog incomingRequestDialog in incomingRequestList)
			{
				string otherUsername = incomingRequestDialog.GetUsername();
				if (!receiverUsername.Equals(otherUsername))
				{
					sessionClient.SendRemoteAccessPermissionReq(this.sessionKey.ToCharArray(), otherUsername, false);
					incomingRequestDialog.Destroy();
				}
			}
			incomingRequestList.Clear();
		}
		
		/* Grant chosen request */
		sessionClient.SendRemoteAccessPermissionReq(this.sessionKey.ToCharArray(), receiverUsername, permission);

	}
	
	/**
	 * When end session is activated it sends the termination request
	 */
	protected void OnEndSessionActionActivated(object sender, System.EventArgs e)
	{
		sessionClient.SendTermReq(sessionKey.ToCharArray());
	}
	
	/** 
	 * When leave session is activated it sends the leave request 
	 */
	protected void OnLeaveSessionActionActivated(object sender, System.EventArgs e)
	{
		if(currentState == clientStates[RECEIVER_IN_CONTROL_STATE])
		{
			sessionClient.SendTermRemoteAccessReq(this.sessionKey.ToCharArray(), this.username);				
		}
		
		sessionClient.SendLeaveReq(username);
	}
	
	/**
	 * State interface, to be realized by concrete states (State-Pattern)
	 * Functionality is isolated that may differ in behavior based on the 
	 * State of the client
	 */ 
	public abstract class IClientState
	{
		protected MainWindow mainWindow;
		
		/**
		 * Class constructor, set main window
		 */ 
		public IClientState(MainWindow mainWindow)
		{
			this.mainWindow = mainWindow;
		}
		
		/**
		 * Refresh UI layout
		 */ 
		public abstract void refresh();
		
		public abstract void OnShareScreenAction();
	}
	
	/**
	 * Class contains functionality specific to Started State
	 */ 
	public class StartedState : IClientState
	{
		public StartedState(MainWindow mainWindow) : base(mainWindow)
		{ }
		/**
		 * Only show create or join triggers
		 */ 		
		public override void refresh()
		{
			mainWindow.vbox3.Visible = false;
			mainWindow.JoinSessionAction.Visible = true;
			mainWindow.CreateSessionAction.Visible = true;
			mainWindow.shareScreenAction.Visible = false;
			mainWindow.LeaveSessionAction.Visible = false;
			mainWindow.EndSessionAction.Visible = false;
			mainWindow.RequestRemoteAccessAction.Visible = false;
			mainWindow.TerminateRemoteAccessAction.Visible = false;
			
			if (mainWindow.inputClient != null)
				mainWindow.inputClient.Active = false;
		}
		
		public override void OnShareScreenAction()
		{
			// Should not happen
		}
	}	
	
	/**
	 * Class contains functionality specific to Sender-Created State
	 */ 	
	public class SenderCreatedState : IClientState
	{
		public SenderCreatedState(MainWindow mainWindow) : base(mainWindow)
		{ }
		
		/**
		 * Only show participants, recording, and end session triggers
		 **/ 		
		public override void refresh()
		{
			mainWindow.vbox3.Visible = true;
			mainWindow.JoinSessionAction.Visible = false;
			mainWindow.CreateSessionAction.Visible = false;
			mainWindow.shareScreenAction.Visible = true;
			mainWindow.LeaveSessionAction.Visible = false;
			mainWindow.EndSessionAction.Visible = true;
			mainWindow.RequestRemoteAccessAction.Visible = false;
			mainWindow.TerminateRemoteAccessAction.Visible = false;
			
			mainWindow.DisplayCreator();
			if (mainWindow.inputClient != null)
				mainWindow.inputClient.Active = false;
		}
		
		/**
		 * Transition to SenderSendingState and capture live source
		 */ 
		public override void OnShareScreenAction()
		{
			mainWindow.rdpSource = new RdpSource(mainWindow);
			
			mainWindow.inputClient.Active = true;
			mainWindow.inputClient.SetListener(mainWindow.rdpSource);
			mainWindow.inputClient.SetSessionId(mainWindow.sessionClient.GetSessionId());
			
			mainWindow.rdpSource.Connect(mainWindow.config.RdpServerHostname,
				mainWindow.config.RdpServerPort, mainWindow.config.RdpServerUsername,
				mainWindow.config.RdpServerDomain, mainWindow.config.RdpServerPassword);
			mainWindow.currentState = mainWindow.clientStates[SENDER_SENDING_STATE];
			mainWindow.currentState.refresh();
		}
	}
	
	/**
	 * Class isolates behavior to Sender-Sending State
	 */ 
	public class SenderSendingState : IClientState
	{
		public SenderSendingState(MainWindow mainWindow) : base(mainWindow)
		{ }
		
		public override void refresh()
		{
			mainWindow.vbox3.Visible = true;
			mainWindow.JoinSessionAction.Visible = false;
			mainWindow.CreateSessionAction.Visible = false;
			mainWindow.shareScreenAction.Visible = true;
			mainWindow.shareScreenAction.Label = "Stop Sharing Screen";
			mainWindow.LeaveSessionAction.Visible = false;
			mainWindow.EndSessionAction.Visible = true;
			mainWindow.RequestRemoteAccessAction.Visible = false;			
			mainWindow.TerminateRemoteAccessAction.Visible = false;
			
			if (mainWindow.inputClient != null)
			{
				string hostname = mainWindow.config.RdpServerHostname;
				
				if (hostname.Equals("localhost") || hostname.Equals("127.0.0.1"))
					mainWindow.inputClient.Active = false;
			}
		}
		
		/**
		 * Transition to SenderCreatedState and stop live source capture
		 */ 
		public override void OnShareScreenAction()
		{
			mainWindow.rdpSource.Disconnect();
			mainWindow.currentState = mainWindow.clientStates[SENDER_CREATED_STATE];
			mainWindow.currentState.refresh();		
		}
	}	
	
	/**
	 * Class contains functionality specific to Receiver-Joined State
	 */ 	
	public class ReceiverJoinedState : IClientState
	{
		public ReceiverJoinedState(MainWindow mainWindow) : base(mainWindow)
		{ }
		
		/**
		 * Only show participants, recording, and leave session triggers
		 **/ 		
		public override void refresh()
		{
			mainWindow.vbox3.Visible = false;
			mainWindow.JoinSessionAction.Visible = true;
			mainWindow.CreateSessionAction.Visible = true;
			mainWindow.shareScreenAction.Visible = false;
			mainWindow.LeaveSessionAction.Visible = false;
			mainWindow.EndSessionAction.Visible = false;
			mainWindow.RequestRemoteAccessAction.Visible = false;
			mainWindow.TerminateRemoteAccessAction.Visible = false;
			
			if (mainWindow.inputClient != null)
				mainWindow.inputClient.Active = false;
		}
		
		public override void OnShareScreenAction()
		{
			// Should not happen
		}
	
	}
	
	/**
	 * Class contains functionality specific to Receiver-Authenticated State
	 */ 
	public class ReceiverAuthenticatedState : IClientState
	{
		public ReceiverAuthenticatedState(MainWindow mainWindow) : base(mainWindow)
		{ }
		
		/**
		 * Only show participants, recording, and leave session triggers, no terminate remote
		 **/ 
		public override void refresh()
		{
			mainWindow.vbox3.Visible = true;
			mainWindow.JoinSessionAction.Visible = false;
			mainWindow.CreateSessionAction.Visible = false;
			mainWindow.shareScreenAction.Visible = false;
			mainWindow.LeaveSessionAction.Visible = true;
			mainWindow.EndSessionAction.Visible = false;
			mainWindow.RequestRemoteAccessAction.Visible = true;
			mainWindow.TerminateRemoteAccessAction.Visible = false;
			
			if (mainWindow.inputClient != null)
				mainWindow.inputClient.Active = false;
		}
		
		public override void OnShareScreenAction()
		{
			// Should not happen
		}
	}
	/**
	 * Class contains functionality specific to Receiver-In Control State
	 */ 
	public class ReceiverInControlState : IClientState
	{
		public ReceiverInControlState(MainWindow mainWindow) : base(mainWindow)
		{ }
		
		/**
		 * Only show participants, recording, and leave session triggers, no request remote
		 **/ 
		public override void refresh()
		{
			mainWindow.vbox3.Visible = true;
			mainWindow.JoinSessionAction.Visible = false;
			mainWindow.CreateSessionAction.Visible = false;
			mainWindow.shareScreenAction.Visible = false;
			mainWindow.LeaveSessionAction.Visible = true;
			mainWindow.EndSessionAction.Visible = false;
			mainWindow.RequestRemoteAccessAction.Visible = false;
			mainWindow.TerminateRemoteAccessAction.Visible = true;
			
			/* Enable Input Channel */
			if (mainWindow.inputClient != null)
				mainWindow.inputClient.Active = true;
		}
		
		public override void OnShareScreenAction()
		{
			// Should not happen
		}
	}
	
	/**
	 * Class isolates behavior to Sender-Sending Remote State
	 */ 
	public class SenderSendingRemoteState : IClientState
	{
		public SenderSendingRemoteState(MainWindow mainWindow) : base(mainWindow)
		{ }
		
		public override void refresh()
		{
			mainWindow.vbox3.Visible = true;
			mainWindow.JoinSessionAction.Visible = false;
			mainWindow.CreateSessionAction.Visible = false;
			mainWindow.shareScreenAction.Visible = true;
			mainWindow.shareScreenAction.Label = "Stop Sharing Screen";
			mainWindow.LeaveSessionAction.Visible = false;
			mainWindow.EndSessionAction.Visible = true;
			mainWindow.RequestRemoteAccessAction.Visible = false;			
			mainWindow.TerminateRemoteAccessAction.Visible = true;
			
			if (mainWindow.inputClient != null)
				mainWindow.inputClient.Active = false;
		}
		
		/**
		 * Transition to SenderCreatedState and stop live source capture
		 */ 
		public override void OnShareScreenAction()
		{
			mainWindow.rdpSource.Disconnect();
			mainWindow.currentState = mainWindow.clientStates[SENDER_CREATED_STATE];
			mainWindow.currentState.refresh();		
		}
	}	
	
	/**
	 * Class to store information about messages to remove them after a timeout of 5 seconds
	 */ 
	public class StatusMessage
	{
		private uint messageId;
		private uint contextId;
		protected MainWindow mainWindow;
		
		public StatusMessage(MainWindow mainWindow, uint messageId, uint contextId)
		{
			this.messageId = messageId;
			this.contextId = contextId;
			this.mainWindow = mainWindow;
		}
		
		public Boolean ClearStatusText()
		{
			Gtk.Application.Invoke(delegate {
				
					mainWindow.notificationBar.Remove(this.contextId, this.messageId);
			});
			
			return false;
		}
	
		public void StartTimer()
		{
			GLib.Timeout.Add(5000, new GLib.TimeoutHandler(ClearStatusText));
		}
	}
		
		

}
