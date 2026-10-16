using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AG.Entities;
using DeviceInterfaces.Recognizer;
using Newtonsoft.Json;
using UIKit;

namespace Aggregation.Dialogs;

public class BoxInfoDialog : Form
{
	public const int WM_NCLBUTTONDOWN = 161;

	public const int HT_CAPTION = 2;

	private bool _flashToggle;

	private Color _bgColor = Color.FromArgb(65, 65, 65);

	private Controller _controller;

	private AggregationEntity _entity;

	private Dictionary<int, Dictionary<string, RecognitionResult>> _codes;

	private StringFormat _centerFormat;

	private StringFormat _leftFormat;

	private StringFormat _rightFormat;

	private IContainer components;

	private ImageList imageList1;

	private RoundedPanel roundedPanel1;

	private PictureBox pictureBox1;

	private RoundedButton roundedButton1;

	private RoundedButton roundedButton3;

	private LayerSelector layerSelector1;

	private RoundedButton roundedButton4;

	public Color BGColor
	{
		get
		{
			return _bgColor;
		}
		set
		{
			if (_bgColor != value)
			{
				_bgColor = value;
				BackColor = _bgColor;
				roundedPanel1.BackColor = _bgColor;
			}
		}
	}

	[DllImport("user32.dll")]
	public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	public BoxInfoDialog()
	{
		InitializeComponent();
		_centerFormat = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};
		_leftFormat = new StringFormat
		{
			Alignment = StringAlignment.Near,
			LineAlignment = StringAlignment.Center
		};
		_rightFormat = new StringFormat
		{
			Alignment = StringAlignment.Far,
			LineAlignment = StringAlignment.Center
		};
	}

	private void textBox1_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	public DialogResult ShowInfo(Controller controller, int id)
	{
		_controller = controller;
		_entity = _controller.GetBoxInfo(id);
		_codes = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, RecognitionResult>>>(_entity.Codes);
		layerSelector1.LayerCount = _entity.Layers;
		return ShowDialog();
	}

	private void CodeContentsDialog_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			DialogResult = DialogResult.Cancel;
		}
	}

	private void CodeContentsDialog_Load(object sender, EventArgs e)
	{
	}

	private void roundedButton1_MouseDown(object sender, MouseEventArgs e)
	{
		layerSelector1.Up();
	}

	private void roundedButton3_MouseDown(object sender, MouseEventArgs e)
	{
		layerSelector1.Down();
	}

	private void layerSelector1_LayerChanged(int layer)
	{
		pictureBox1.Refresh();
	}

	private void pictureBox1_Paint(object sender, PaintEventArgs e)
	{
		Dictionary<string, RecognitionResult>.ValueCollection values = _codes[layerSelector1.ActiveLayerIndex].Values;
		float xMin = values.Min((RecognitionResult c) => c.Points.Min((RecognitionResultPosition p) => p.X));
		float num1 = values.Max((RecognitionResult c) => c.Points.Max((RecognitionResultPosition p) => p.X));
		float yMin = values.Min((RecognitionResult c) => c.Points.Min((RecognitionResultPosition p) => p.Y));
		float num2 = values.Max((RecognitionResult c) => c.Points.Max((RecognitionResultPosition p) => p.Y));
		float maxCoeff = Math.Max((num1 - xMin) / (float)(pictureBox1.Width - 100), (num2 - yMin) / (float)(pictureBox1.Height - 100));
		_ = (num1 - xMin) / maxCoeff;
		_ = (num2 - yMin) / maxCoeff;
		float xOffset = Math.Abs((float)((((double)num1 - (double)xMin) / (double)maxCoeff - (double)pictureBox1.Width) / 2.0));
		float yOffset = Math.Abs((float)((((double)num2 - (double)yMin) / (double)maxCoeff - (double)pictureBox1.Height) / 2.0));
		foreach (RecognitionResult recognitionResult in values)
		{
			e.Graphics.FillPolygon(Brushes.DodgerBlue, recognitionResult.Points.Select((RecognitionResultPosition p) => new PointF((p.X - xMin) / maxCoeff + xOffset, (p.Y - yMin) / maxCoeff + yOffset)).ToArray());
		}
		using Font font1 = new Font("Tektur", 36f);
		Graphics graphics = e.Graphics;
		string s1 = $"Слой: {layerSelector1.ActiveLayerIndex}";
		Font font2 = font1;
		Brush gray1 = Brushes.Gray;
		double x1 = (double)e.Graphics.ClipBounds.Left + 20.0;
		double y1 = (double)e.Graphics.ClipBounds.Top + 64.0;
		StringFormat leftFormat = _leftFormat;
		graphics.DrawString(s1, font2, gray1, (float)x1, (float)y1, leftFormat);
		Graphics graphics2 = e.Graphics;
		string s2 = "Код: " + _entity.GroupCode;
		Font font3 = font1;
		Brush white = Brushes.White;
		double x2 = (double)e.Graphics.ClipBounds.Width / 2.0;
		double y2 = (double)e.Graphics.ClipBounds.Top + 64.0;
		StringFormat centerFormat = _centerFormat;
		graphics2.DrawString(s2, font3, white, (float)x2, (float)y2, centerFormat);
		Graphics graphics3 = e.Graphics;
		string s3 = $"Короб: {_entity.Id}";
		Font font4 = font1;
		Brush gray2 = Brushes.Gray;
		double x3 = (double)e.Graphics.ClipBounds.Right - 20.0;
		double y3 = (double)e.Graphics.ClipBounds.Top + 64.0;
		StringFormat rightFormat = _rightFormat;
		graphics3.DrawString(s3, font4, gray2, (float)x3, (float)y3, rightFormat);
	}

	private void roundedButton4_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
	{
	}

	private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
	{
		Dictionary<string, RecognitionResult>.ValueCollection values = _codes[layerSelector1.ActiveLayerIndex].Values;
		float xMin = values.Min((RecognitionResult c) => c.Points.Min((RecognitionResultPosition p) => p.X));
		float num1 = values.Max((RecognitionResult c) => c.Points.Max((RecognitionResultPosition p) => p.X));
		float yMin = values.Min((RecognitionResult c) => c.Points.Min((RecognitionResultPosition p) => p.Y));
		float num2 = values.Max((RecognitionResult c) => c.Points.Max((RecognitionResultPosition p) => p.Y));
		float maxCoeff = Math.Max((num1 - xMin) / (float)(pictureBox1.Width - 100), (num2 - yMin) / (float)(pictureBox1.Height - 100));
		_ = (num1 - xMin) / maxCoeff;
		_ = (num2 - yMin) / maxCoeff;
		float xOffset = Math.Abs((float)((((double)num1 - (double)xMin) / (double)maxCoeff - (double)pictureBox1.Width) / 2.0));
		float yOffset = Math.Abs((float)((((double)num2 - (double)yMin) / (double)maxCoeff - (double)pictureBox1.Height) / 2.0));
		string contents = null;
		int num5 = 0;
		foreach (RecognitionResult recognitionResult in values)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddPolygon(recognitionResult.Points.Select((RecognitionResultPosition p) => new PointF((p.X - xMin) / maxCoeff + xOffset, (p.Y - yMin) / maxCoeff + yOffset)).ToArray());
			if (graphicsPath.IsVisible(e.Location))
			{
				contents = recognitionResult.Data;
				break;
			}
			num5++;
		}
		if (e.Button != MouseButtons.Left || contents == null)
		{
			return;
		}
		using CodeContentsDialog codeContentsDialog = new CodeContentsDialog();
		codeContentsDialog.ShowCode(contents);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager componentResourceManager = new System.ComponentModel.ComponentResourceManager(typeof(Aggregation.Dialogs.BoxInfoDialog));
		this.imageList1 = new System.Windows.Forms.ImageList(this.components);
		this.roundedPanel1 = new UIKit.RoundedPanel();
		this.layerSelector1 = new UIKit.LayerSelector();
		this.roundedButton4 = new UIKit.RoundedButton();
		this.roundedButton3 = new UIKit.RoundedButton();
		this.roundedButton1 = new UIKit.RoundedButton();
		this.pictureBox1 = new System.Windows.Forms.PictureBox();
		this.roundedPanel1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
		base.SuspendLayout();
		this.imageList1.ImageStream = (System.Windows.Forms.ImageListStreamer)componentResourceManager.GetObject("imageList1.ImageStream");
		this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
		this.imageList1.Images.SetKeyName(0, "9057175_play_button_r_icon.png");
		this.roundedPanel1.BorderColor = System.Drawing.Color.White;
		this.roundedPanel1.Caption = null;
		this.roundedPanel1.Controls.Add(this.layerSelector1);
		this.roundedPanel1.Controls.Add(this.roundedButton4);
		this.roundedPanel1.Controls.Add(this.roundedButton3);
		this.roundedPanel1.Controls.Add(this.roundedButton1);
		this.roundedPanel1.Controls.Add(this.pictureBox1);
		this.roundedPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanel1.DrawCaption = false;
		this.roundedPanel1.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel1.HeaderHeight = 20;
		this.roundedPanel1.LeftBottom = true;
		this.roundedPanel1.LeftTop = true;
		this.roundedPanel1.Location = new System.Drawing.Point(10, 10);
		this.roundedPanel1.Name = "roundedPanel1";
		this.roundedPanel1.Padding = new System.Windows.Forms.Padding(6);
		this.roundedPanel1.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanel1.Radius = 20;
		this.roundedPanel1.RightBottom = true;
		this.roundedPanel1.RightTop = true;
		this.roundedPanel1.SelectCaption = false;
		this.roundedPanel1.ShowHeader = false;
		this.roundedPanel1.Size = new System.Drawing.Size(1228, 776);
		this.roundedPanel1.TabIndex = 43;
		this.roundedPanel1.Thickness = 0f;
		this.roundedPanel1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.textBox1_MouseMove);
		this.layerSelector1.ActiveLayerIndex = 1;
		this.layerSelector1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.layerSelector1.BackColor = System.Drawing.Color.Black;
		this.layerSelector1.LayerCount = 1;
		this.layerSelector1.Location = new System.Drawing.Point(4, 144);
		this.layerSelector1.Name = "layerSelector1";
		this.layerSelector1.Size = new System.Drawing.Size(134, 488);
		this.layerSelector1.TabIndex = 9;
		this.layerSelector1.LayerChanged += new UIKit.LayerChangedEventHandler(this.layerSelector1_LayerChanged);
		this.roundedButton4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.roundedButton4.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton4.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButton4.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton4.BGColor = System.Drawing.Color.Black;
		this.roundedButton4.BorderColor = System.Drawing.Color.Black;
		this.roundedButton4.CornerRadius = 20;
		this.roundedButton4.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButton4.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButton4.ForeColorPressed = System.Drawing.Color.Lime;
		this.roundedButton4.HoverEffect = false;
		this.roundedButton4.ImageDisabled = null;
		this.roundedButton4.ImageEnabled = (System.Drawing.Image)componentResourceManager.GetObject("roundedButton4.ImageEnabled");
		this.roundedButton4.ImageSize = new System.Drawing.Size(100, 100);
		this.roundedButton4.LeftSideRounded = true;
		this.roundedButton4.Location = new System.Drawing.Point(1102, 16);
		this.roundedButton4.Name = "roundedButton4";
		this.roundedButton4.Pressed = false;
		this.roundedButton4.RightSideRounded = true;
		this.roundedButton4.RoundedCorners = true;
		this.roundedButton4.ShowBorder = false;
		this.roundedButton4.Size = new System.Drawing.Size(110, 110);
		this.roundedButton4.TabIndex = 10;
		this.roundedButton4.Text = "roundedButton4";
		this.roundedButton4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButton4.Click += new System.EventHandler(this.roundedButton4_Click);
		this.roundedButton3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.roundedButton3.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton3.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButton3.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton3.BGColor = System.Drawing.Color.Black;
		this.roundedButton3.BorderColor = System.Drawing.Color.Black;
		this.roundedButton3.CornerRadius = 20;
		this.roundedButton3.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButton3.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButton3.ForeColorPressed = System.Drawing.Color.Lime;
		this.roundedButton3.HoverEffect = false;
		this.roundedButton3.ImageDisabled = null;
		this.roundedButton3.ImageEnabled = (System.Drawing.Image)componentResourceManager.GetObject("roundedButton3.ImageEnabled");
		this.roundedButton3.ImageSize = new System.Drawing.Size(100, 100);
		this.roundedButton3.LeftSideRounded = true;
		this.roundedButton3.Location = new System.Drawing.Point(16, 649);
		this.roundedButton3.Name = "roundedButton3";
		this.roundedButton3.Pressed = false;
		this.roundedButton3.RightSideRounded = true;
		this.roundedButton3.RoundedCorners = true;
		this.roundedButton3.ShowBorder = false;
		this.roundedButton3.Size = new System.Drawing.Size(110, 110);
		this.roundedButton3.TabIndex = 7;
		this.roundedButton3.Text = "roundedButton3";
		this.roundedButton3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButton3.MouseDown += new System.Windows.Forms.MouseEventHandler(this.roundedButton3_MouseDown);
		this.roundedButton1.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton1.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButton1.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton1.BGColor = System.Drawing.Color.Black;
		this.roundedButton1.BorderColor = System.Drawing.Color.Black;
		this.roundedButton1.CornerRadius = 20;
		this.roundedButton1.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButton1.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButton1.ForeColorPressed = System.Drawing.Color.Lime;
		this.roundedButton1.HoverEffect = false;
		this.roundedButton1.ImageDisabled = null;
		this.roundedButton1.ImageEnabled = (System.Drawing.Image)componentResourceManager.GetObject("roundedButton1.ImageEnabled");
		this.roundedButton1.ImageSize = new System.Drawing.Size(100, 100);
		this.roundedButton1.LeftSideRounded = true;
		this.roundedButton1.Location = new System.Drawing.Point(16, 16);
		this.roundedButton1.Name = "roundedButton1";
		this.roundedButton1.Pressed = false;
		this.roundedButton1.RightSideRounded = true;
		this.roundedButton1.RoundedCorners = true;
		this.roundedButton1.ShowBorder = false;
		this.roundedButton1.Size = new System.Drawing.Size(110, 110);
		this.roundedButton1.TabIndex = 6;
		this.roundedButton1.Text = "roundedButton1";
		this.roundedButton1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButton1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.roundedButton1_MouseDown);
		this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pictureBox1.BackColor = System.Drawing.Color.Black;
		this.pictureBox1.Location = new System.Drawing.Point(132, 9);
		this.pictureBox1.Name = "pictureBox1";
		this.pictureBox1.Size = new System.Drawing.Size(964, 758);
		this.pictureBox1.TabIndex = 0;
		this.pictureBox1.TabStop = false;
		this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBox1_Paint);
		this.pictureBox1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseClick);
		this.pictureBox1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseMove);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(1248, 796);
		base.Controls.Add(this.roundedPanel1);
		this.DoubleBuffered = true;
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "BoxInfoDialog";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "VariableListDialog";
		base.WindowState = System.Windows.Forms.FormWindowState.Maximized;
		base.Load += new System.EventHandler(this.CodeContentsDialog_Load);
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CodeContentsDialog_KeyDown);
		base.MouseMove += new System.Windows.Forms.MouseEventHandler(this.textBox1_MouseMove);
		this.roundedPanel1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
		base.ResumeLayout(false);
	}
}
