/**
 * Screenary: Real-Time Collaboration Redefined.
 * RDP Source
 *
 * Copyright 2011-2012 Marc-Andre Moreau <marcandre.moreau@gmail.com>
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

using System;
using FreeRDP;
using System.Threading;

namespace Screenary.Client
{
	public unsafe class RdpSource : IUpdate, IPrimaryUpdate, IInputListener
	{
		private RDP rdp;
		private int port;
		private string hostname;
		private string username;
		private string domain;
		private string password;
		
		private Thread thread;
		private static bool procRunning = true;
		private ISource iSource;
		
		/**
		 * Instantiate RDP and Thread
		 */ 
		public RdpSource(ISource iSource)
		{
			port = 3389;
			hostname = "localhost";
			username = "Administrator";
			domain = "";
			password = "";
			rdp = new RDP();
			this.iSource = iSource;
			thread = new Thread(() => ThreadProc(rdp));
		}
		
		/**
		 * Connect to FreeRDP server, start thread
		 */ 
		public void Connect(string hostname, int port, string username, string domain, string password)
		{
			rdp.SetUpdateInterface(this);
			rdp.SetPrimaryUpdateInterface(this);
			rdp.Connect(hostname, port, username, domain, password);
			procRunning = true;
			thread.Start();
		}
		
		/**
		 * Disconnect from FreeRDP server, stop thread
		 */ 
		public void Disconnect()
		{
			rdp.Disconnect();
			procRunning = false;

			/* Prepare for next call */
			thread = new Thread(() => ThreadProc(rdp));
		}
		
		public void OnMouseEvent(UInt16 pointerFlags, UInt16 x, UInt16 y)
		{
			//Console.WriteLine("RdpSource.OnMouseEvent");
			rdp.SendInputMouseEvent(pointerFlags, x, y);
		}
		
		public void OnKeyboardEvent(UInt16 keyboardFlags, UInt16 keyCode)
		{
			Console.WriteLine("RdpSource.OnKeyboardEvent: {0}", keyCode);
			rdp.SendInputKeyboardEvent(keyboardFlags, keyCode);
		}
		
		public void BeginPaint(rdpContext* context) { }
		public void EndPaint(rdpContext* context) { }
		public void SetBounds(rdpContext* context, rdpBounds* bounds) { }
		public void Synchronize(rdpContext* context) { }
		public void DesktopResize(rdpContext* context) { }
		public void BitmapUpdate(rdpContext* context, BitmapUpdate* bitmap) { }
		public void Palette(rdpContext* context, PaletteUpdate* palette) { }
		public void PlaySound(rdpContext* context, PlaySoundUpdate* playSound) { }
		
		public void SurfaceBits(rdpContext* context, SurfaceBits* surfaceBits)
		{
			SurfaceBitsCommand cmd = new SurfaceBitsCommand();
			cmd.Read(surfaceBits);
			iSource.OnSurfaceCommand(cmd);
		}
		
		public void DstBlt(rdpContext* context, DstBltOrder* dstblt) { }
		public void PatBlt(rdpContext* context, PatBltOrder* patblt) { }
		public void ScrBlt(rdpContext* context, ScrBltOrder* scrblt) { }
		public void OpaqueRect(rdpContext* context, OpaqueRectOrder* opaqueRect) { }
		public void DrawNineGrid(rdpContext* context, DrawNineGridOrder* drawNineGrid) { }	
		public void MultiDstBlt(rdpContext* context, MultiDstBltOrder* multi_dstblt) { }
		public void MultiPatBlt(rdpContext* context, MultiPatBltOrder* multi_patblt) { }		
		public void MultiScrBlt(rdpContext* context, MultiScrBltOrder* multi_scrblt) { }
		public void MultiOpaqueRect(rdpContext* context, MultiOpaqueRectOrder* multi_opaque_rect) { }
		public void MultiDrawNineGrid(rdpContext* context, MultiDrawNineGridOrder* multi_draw_nine_grid) { }
		public void LineTo(rdpContext* context, LineToOrder* line_to) { }
		public void Polyline(rdpContext* context, PolylineOrder* polyline) { }
		public void MemBlt(rdpContext* context, MemBltOrder* memblt) { }
		public void Mem3Blt(rdpContext* context, Mem3BltOrder* mem3blt) { }
		public void SaveBitmap(rdpContext* context, SaveBitmapOrder* save_bitmap) { }
		public void GlyphIndex(rdpContext* context, GlyphIndexOrder* glyph_index) { }
		public void FastIndex(rdpContext* context, FastIndexOrder* fast_index) { }
		public void FastGlyph(rdpContext* context, FastGlyphOrder* fast_glyph) { }
		public void PolygonSC(rdpContext* context, PolygonSCOrder* polygon_sc) { }
		public void PolygonCB(rdpContext* context, PolygonCBOrder* polygon_cb) { }
		public void EllipseSC(rdpContext* context, EllipseSCOrder* ellipse_sc) { }
		public void EllipseCB(rdpContext* context, EllipseCBOrder* ellipse_cb) { }
		
		static void ThreadProc(RDP rdp)
		{
			while (procRunning)
			{
				rdp.CheckFileDescriptor();
				Thread.Sleep(10);
			}
		}
	}
}

