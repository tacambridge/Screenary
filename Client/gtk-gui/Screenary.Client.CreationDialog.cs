
// This file has been generated by the GUI designer. Do not modify.
namespace Screenary.Client
{
	public partial class CreationDialog
	{
		private global::Gtk.ScrolledWindow GtkScrolledWindow;
		private global::Gtk.TextView creationMessage;
		private global::Gtk.Button buttonOk;
        
		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget Screenary.Client.CreationDialog
			this.Name = "Screenary.Client.CreationDialog";
			this.Title = global::Mono.Unix.Catalog.GetString ("Session Created Succesfully");
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			this.BorderWidth = ((uint)(3));
			// Internal child Screenary.Client.CreationDialog.VBox
			global::Gtk.VBox w1 = this.VBox;
			w1.Name = "dialog1_VBox";
			w1.BorderWidth = ((uint)(2));
			// Container child dialog1_VBox.Gtk.Box+BoxChild
			this.GtkScrolledWindow = new global::Gtk.ScrolledWindow ();
			this.GtkScrolledWindow.Name = "GtkScrolledWindow";
			this.GtkScrolledWindow.ShadowType = ((global::Gtk.ShadowType)(1));
			// Container child GtkScrolledWindow.Gtk.Container+ContainerChild
			this.creationMessage = new global::Gtk.TextView ();
			this.creationMessage.CanFocus = true;
			this.creationMessage.Name = "creationMessage";
			this.creationMessage.Editable = false;
			this.creationMessage.CursorVisible = false;
			this.creationMessage.AcceptsTab = false;
			this.creationMessage.PixelsAboveLines = 5;
			this.creationMessage.LeftMargin = 15;
			this.creationMessage.RightMargin = 15;
			this.GtkScrolledWindow.Add (this.creationMessage);
			w1.Add (this.GtkScrolledWindow);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(w1 [this.GtkScrolledWindow]));
			w3.Position = 0;
			// Internal child Screenary.Client.CreationDialog.ActionArea
			global::Gtk.HButtonBox w4 = this.ActionArea;
			w4.Name = "dialog1_ActionArea";
			w4.Spacing = 10;
			w4.BorderWidth = ((uint)(5));
			w4.LayoutStyle = ((global::Gtk.ButtonBoxStyle)(4));
			// Container child dialog1_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.buttonOk = new global::Gtk.Button ();
			this.buttonOk.CanDefault = true;
			this.buttonOk.CanFocus = true;
			this.buttonOk.Name = "buttonOk";
			this.buttonOk.UseStock = true;
			this.buttonOk.UseUnderline = true;
			this.buttonOk.Label = "gtk-ok";
			this.AddActionWidget (this.buttonOk, -5);
			global::Gtk.ButtonBox.ButtonBoxChild w5 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w4 [this.buttonOk]));
			w5.Expand = false;
			w5.Fill = false;
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.DefaultWidth = 390;
			this.DefaultHeight = 177;
			this.Show ();
			this.buttonOk.Clicked += new global::System.EventHandler (this.OnButtonOkClicked);
		}
	}
}
