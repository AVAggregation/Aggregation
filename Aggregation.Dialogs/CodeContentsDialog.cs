using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Manina.Windows.Forms;
using UIKit;

namespace Aggregation.Dialogs;

public class CodeContentsDialog : Form
{
	public const int WM_NCLBUTTONDOWN = 161;

	public const int HT_CAPTION = 2;

	private bool _flashToggle;

	private Color _bgColor = Color.FromArgb(65, 65, 65);

	private IContainer components;

	private ImageList imageList1;

	private RoundedButton roundedButton1;

	private RoundedPanel roundedPanel1;

	private TableLayoutPanel tableLayoutPanel2;

	private RoundedButton roundedButton3;

	private PagedControl pagedControl1;

	private Page page1;

	private Page page2;

	private RoundedPanel roundedPanel2;

	private Button button17;

	private Button button19;

	private TableLayoutPanel tableLayoutPanel3;

	private Label label1;

	private Label label3;

	private Label label2;

	private Label label5;

	private Label label4;

	private string _copyText = string.Empty;

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
				roundedButton1.BGColor = _bgColor;
				roundedPanel1.BackColor = _bgColor;
			}
		}
	}

	[DllImport("user32.dll")]
	public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	public DialogResult ShowCodeWithStatus(string code, string statusText, bool isError)
	{
		string header = (string.IsNullOrWhiteSpace(statusText) ? string.Empty : statusText.Trim());
		string body = code ?? string.Empty;
		string combined = (_copyText = (string.IsNullOrEmpty(header) ? body : (header + Environment.NewLine + body)));
		BGColor = (isError ? Color.FromArgb(120, 40, 40) : Color.FromArgb(65, 65, 65));
		label1.Text = combined;
		pagedControl1.SelectedIndex = 1;
		if (string.IsNullOrEmpty(body))
		{
			label4.Text = "..";
			label5.Text = "..";
		}
		else
		{
			label4.Text = ((body.Length >= 16) ? body.Substring(2, 14) : "..");
			int snStart = 18;
			if (body.Length <= snStart)
			{
				label5.Text = "..";
			}
			else
			{
				int gsTextIndex = body.IndexOf("<GS>", StringComparison.Ordinal);
				int gsCharIndex = body.IndexOf('\u001d');
				int snEnd;
				if (gsTextIndex >= 0)
				{
					snEnd = gsTextIndex;
				}
				else
				{
					snEnd = ((gsCharIndex < 0) ? body.Length : gsCharIndex);
				}
				int snLen = snEnd - snStart;
				label5.Text = ((snLen > 0) ? body.Substring(snStart, snLen) : "..");
			}
		}
		return ShowDialog();
	}

	private void roundedButton1_Click(object sender, EventArgs e)
	{
		try
		{
			string text = _copyText;
			if (string.IsNullOrWhiteSpace(text))
			{
				text = label1?.Text ?? string.Empty;
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				Clipboard.SetText(text);
			}
		}
		catch
		{
		}
	}

	public CodeContentsDialog()
	{
		InitializeComponent();
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 134)
		{
			BGColor = ((!_flashToggle) ? Color.FromArgb(65, 65, 65) : Color.FromArgb(255, 192, 128));
			_flashToggle = !_flashToggle;
		}
		else
		{
			_flashToggle = false;
		}
		base.WndProc(ref m);
	}

	private void textBox1_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	public DialogResult ShowCode(string contents)
	{
		label1.Text = contents ?? string.Empty;
		if (string.IsNullOrEmpty(contents))
		{
			label4.Text = "..";
			label5.Text = "..";
			return ShowDialog();
		}
		label4.Text = ((contents.Length >= 16) ? contents.Substring(2, 14) : "..");
		int snStart = 18;
		if (contents.Length <= snStart)
		{
			label5.Text = "..";
			return ShowDialog();
		}
		int gsTextIndex = contents.IndexOf("<GS>", StringComparison.Ordinal);
		int gsCharIndex = contents.IndexOf('\u001d');
		int snEnd;
		if (gsTextIndex >= 0)
		{
			snEnd = gsTextIndex;
		}
		else
		{
			snEnd = ((gsCharIndex < 0) ? contents.Length : gsCharIndex);
		}
		int snLen = snEnd - snStart;
		if (snLen <= 0)
		{
			label5.Text = "..";
			return ShowDialog();
		}
		label5.Text = contents.Substring(snStart, snLen);
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

	private void button17_Click(object sender, EventArgs e)
	{
		pagedControl1.SelectedIndex = 0;
	}

	private void button19_Click(object sender, EventArgs e)
	{
		pagedControl1.SelectedIndex = 1;
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
		System.ComponentModel.ComponentResourceManager componentResourceManager = new System.ComponentModel.ComponentResourceManager(typeof(Aggregation.Dialogs.CodeContentsDialog));
		this.imageList1 = new System.Windows.Forms.ImageList(this.components);
		this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
		this.roundedPanel1 = new UIKit.RoundedPanel();
		this.roundedButton3 = new UIKit.RoundedButton();
		this.roundedButton1 = new UIKit.RoundedButton();
		this.pagedControl1 = new Manina.Windows.Forms.PagedControl();
		this.page1 = new Manina.Windows.Forms.Page();
		this.page2 = new Manina.Windows.Forms.Page();
		this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
		this.button19 = new System.Windows.Forms.Button();
		this.button17 = new System.Windows.Forms.Button();
		this.roundedPanel2 = new UIKit.RoundedPanel();
		this.label1 = new System.Windows.Forms.Label();
		this.label2 = new System.Windows.Forms.Label();
		this.label3 = new System.Windows.Forms.Label();
		this.label4 = new System.Windows.Forms.Label();
		this.label5 = new System.Windows.Forms.Label();
		this.tableLayoutPanel2.SuspendLayout();
		this.roundedPanel1.SuspendLayout();
		this.pagedControl1.SuspendLayout();
		this.page1.SuspendLayout();
		this.page2.SuspendLayout();
		this.tableLayoutPanel3.SuspendLayout();
		this.roundedPanel2.SuspendLayout();
		base.SuspendLayout();
		this.imageList1.ImageStream = (System.Windows.Forms.ImageListStreamer)componentResourceManager.GetObject("imageList1.ImageStream");
		this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
		this.imageList1.Images.SetKeyName(0, "9057175_play_button_r_icon.png");
		this.tableLayoutPanel2.ColumnCount = 2;
		this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel2.Controls.Add(this.roundedPanel2, 0, 0);
		this.tableLayoutPanel2.Controls.Add(this.tableLayoutPanel3, 1, 0);
		this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tableLayoutPanel2.Location = new System.Drawing.Point(10, 288);
		this.tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(0);
		this.tableLayoutPanel2.Name = "tableLayoutPanel2";
		this.tableLayoutPanel2.RowCount = 1;
		this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tableLayoutPanel2.Size = new System.Drawing.Size(791, 80);
		this.tableLayoutPanel2.TabIndex = 45;
		this.roundedPanel1.BorderColor = System.Drawing.Color.White;
		this.roundedPanel1.Caption = null;
		this.roundedPanel1.Controls.Add(this.pagedControl1);
		this.roundedPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanel1.DrawCaption = false;
		this.roundedPanel1.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel1.HeaderHeight = 20;
		this.roundedPanel1.LeftBottom = false;
		this.roundedPanel1.LeftTop = false;
		this.roundedPanel1.Location = new System.Drawing.Point(10, 10);
		this.roundedPanel1.Name = "roundedPanel1";
		this.roundedPanel1.Padding = new System.Windows.Forms.Padding(6);
		this.roundedPanel1.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanel1.Radius = 20;
		this.roundedPanel1.RightBottom = true;
		this.roundedPanel1.RightTop = true;
		this.roundedPanel1.SelectCaption = false;
		this.roundedPanel1.ShowHeader = false;
		this.roundedPanel1.Size = new System.Drawing.Size(791, 278);
		this.roundedPanel1.TabIndex = 43;
		this.roundedPanel1.Thickness = 0f;
		this.roundedButton3.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton3.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButton3.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton3.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButton3.BorderColor = System.Drawing.Color.Black;
		this.roundedButton3.CornerRadius = 16;
		this.roundedButton3.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.roundedButton3.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButton3.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButton3.ForeColor = System.Drawing.Color.White;
		this.roundedButton3.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButton3.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButton3.HoverEffect = false;
		this.roundedButton3.ImageDisabled = null;
		this.roundedButton3.ImageEnabled = null;
		this.roundedButton3.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButton3.LeftSideRounded = true;
		this.roundedButton3.Location = new System.Drawing.Point(198, 10);
		this.roundedButton3.Margin = new System.Windows.Forms.Padding(5, 10, 0, 0);
		this.roundedButton3.Name = "roundedButton3";
		this.roundedButton3.Pressed = false;
		this.roundedButton3.RightSideRounded = true;
		this.roundedButton3.RoundedCorners = true;
		this.roundedButton3.ShowBorder = false;
		this.roundedButton3.Size = new System.Drawing.Size(188, 70);
		this.roundedButton3.TabIndex = 3;
		this.roundedButton3.Text = "Закрыть";
		this.roundedButton3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButton1.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton1.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButton1.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton1.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButton1.BorderColor = System.Drawing.Color.Black;
		this.roundedButton1.CornerRadius = 16;
		this.roundedButton1.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButton1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButton1.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButton1.ForeColor = System.Drawing.Color.White;
		this.roundedButton1.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButton1.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButton1.HoverEffect = false;
		this.roundedButton1.ImageDisabled = null;
		this.roundedButton1.ImageEnabled = null;
		this.roundedButton1.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButton1.LeftSideRounded = true;
		this.roundedButton1.Location = new System.Drawing.Point(0, 10);
		this.roundedButton1.Margin = new System.Windows.Forms.Padding(0, 10, 5, 0);
		this.roundedButton1.Name = "roundedButton1";
		this.roundedButton1.Pressed = false;
		this.roundedButton1.RightSideRounded = true;
		this.roundedButton1.RoundedCorners = true;
		this.roundedButton1.ShowBorder = false;
		this.roundedButton1.Size = new System.Drawing.Size(188, 70);
		this.roundedButton1.TabIndex = 2;
		this.roundedButton1.Text = "Копировать";
		this.roundedButton1.Click += new System.EventHandler(this.roundedButton1_Click);
		this.roundedButton1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.pagedControl1.Controls.Add(this.page1);
		this.pagedControl1.Controls.Add(this.page2);
		this.pagedControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pagedControl1.Location = new System.Drawing.Point(6, 6);
		this.pagedControl1.Name = "pagedControl1";
		this.pagedControl1.Size = new System.Drawing.Size(779, 266);
		this.pagedControl1.TabIndex = 0;
		this.page1.BackColor = System.Drawing.Color.Black;
		this.page1.Controls.Add(this.label5);
		this.page1.Controls.Add(this.label4);
		this.page1.Controls.Add(this.label3);
		this.page1.Controls.Add(this.label2);
		this.page1.Location = new System.Drawing.Point(1, 1);
		this.page1.Name = "page1";
		this.page1.Size = new System.Drawing.Size(777, 264);
		this.page1.Text = "Page 1";
		this.page1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.textBox1_MouseMove);
		this.page2.BackColor = System.Drawing.Color.Black;
		this.page2.Controls.Add(this.label1);
		this.page2.Location = new System.Drawing.Point(1, 1);
		this.page2.Name = "page2";
		this.page2.Padding = new System.Windows.Forms.Padding(10);
		this.page2.Size = new System.Drawing.Size(777, 264);
		this.page2.Text = "Page 2";
		this.tableLayoutPanel3.ColumnCount = 2;
		this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel3.Controls.Add(this.roundedButton3, 1, 0);
		this.tableLayoutPanel3.Controls.Add(this.roundedButton1, 0, 0);
		this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
		this.tableLayoutPanel3.Location = new System.Drawing.Point(405, 0);
		this.tableLayoutPanel3.Margin = new System.Windows.Forms.Padding(10, 0, 0, 0);
		this.tableLayoutPanel3.Name = "tableLayoutPanel3";
		this.tableLayoutPanel3.RowCount = 1;
		this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80f));
		this.tableLayoutPanel3.Size = new System.Drawing.Size(386, 80);
		this.tableLayoutPanel3.TabIndex = 0;
		this.button19.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.button19.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.button19.Cursor = System.Windows.Forms.Cursors.Hand;
		this.button19.FlatAppearance.BorderSize = 0;
		this.button19.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.button19.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.button19.ForeColor = System.Drawing.Color.FromArgb(0, 204, 255);
		this.button19.Location = new System.Drawing.Point(101, 10);
		this.button19.Name = "button19";
		this.button19.Size = new System.Drawing.Size(106, 70);
		this.button19.TabIndex = 22;
		this.button19.Tag = "1";
		this.button19.Text = "текст";
		this.button19.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.button19.UseVisualStyleBackColor = false;
		this.button19.Click += new System.EventHandler(this.button19_Click);
		this.button17.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.button17.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.button17.Cursor = System.Windows.Forms.Cursors.Hand;
		this.button17.FlatAppearance.BorderSize = 0;
		this.button17.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.button17.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.button17.ForeColor = System.Drawing.Color.FromArgb(0, 204, 255);
		this.button17.Location = new System.Drawing.Point(10, 10);
		this.button17.Name = "button17";
		this.button17.Size = new System.Drawing.Size(83, 70);
		this.button17.TabIndex = 21;
		this.button17.Tag = "1";
		this.button17.Text = "GS1";
		this.button17.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.button17.UseVisualStyleBackColor = false;
		this.button17.Click += new System.EventHandler(this.button17_Click);
		this.roundedPanel2.BorderColor = System.Drawing.Color.White;
		this.roundedPanel2.Caption = null;
		this.roundedPanel2.Controls.Add(this.button17);
		this.roundedPanel2.Controls.Add(this.button19);
		this.roundedPanel2.Dock = System.Windows.Forms.DockStyle.Left;
		this.roundedPanel2.DrawCaption = false;
		this.roundedPanel2.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel2.HeaderHeight = 20;
		this.roundedPanel2.LeftBottom = true;
		this.roundedPanel2.LeftTop = false;
		this.roundedPanel2.Location = new System.Drawing.Point(0, 0);
		this.roundedPanel2.Margin = new System.Windows.Forms.Padding(0);
		this.roundedPanel2.Name = "roundedPanel2";
		this.roundedPanel2.Padding = new System.Windows.Forms.Padding(20, 14, 20, 14);
		this.roundedPanel2.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanel2.Radius = 20;
		this.roundedPanel2.RightBottom = true;
		this.roundedPanel2.RightTop = false;
		this.roundedPanel2.SelectCaption = false;
		this.roundedPanel2.ShowHeader = false;
		this.roundedPanel2.Size = new System.Drawing.Size(217, 80);
		this.roundedPanel2.TabIndex = 44;
		this.roundedPanel2.Thickness = 0f;
		this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.label1.Font = new System.Drawing.Font("Courier New", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label1.ForeColor = System.Drawing.Color.White;
		this.label1.Location = new System.Drawing.Point(0, 0);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(777, 264);
		this.label1.TabIndex = 0;
		this.label1.Text = "..";
		this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.label1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.textBox1_MouseMove);
		this.label2.AutoSize = true;
		this.label2.Font = new System.Drawing.Font("Courier New", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label2.ForeColor = System.Drawing.Color.Silver;
		this.label2.Location = new System.Drawing.Point(70, 96);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(77, 30);
		this.label2.TabIndex = 1;
		this.label2.Text = "GTIN";
		this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.label3.AutoSize = true;
		this.label3.Font = new System.Drawing.Font("Courier New", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label3.ForeColor = System.Drawing.Color.Silver;
		this.label3.Location = new System.Drawing.Point(70, 137);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(45, 30);
		this.label3.TabIndex = 2;
		this.label3.Text = "SN";
		this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.label4.AutoSize = true;
		this.label4.Font = new System.Drawing.Font("Courier New", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label4.ForeColor = System.Drawing.Color.White;
		this.label4.Location = new System.Drawing.Point(186, 96);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(45, 30);
		this.label4.TabIndex = 3;
		this.label4.Text = "..";
		this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.label5.AutoSize = true;
		this.label5.Font = new System.Drawing.Font("Courier New", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label5.ForeColor = System.Drawing.Color.White;
		this.label5.Location = new System.Drawing.Point(186, 137);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(45, 30);
		this.label5.TabIndex = 4;
		this.label5.Text = "..";
		this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(811, 378);
		base.Controls.Add(this.roundedPanel1);
		base.Controls.Add(this.tableLayoutPanel2);
		this.DoubleBuffered = true;
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "CodeContentsDialog";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "VariableListDialog";
		base.Load += new System.EventHandler(this.CodeContentsDialog_Load);
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CodeContentsDialog_KeyDown);
		this.tableLayoutPanel2.ResumeLayout(false);
		this.roundedPanel1.ResumeLayout(false);
		this.pagedControl1.ResumeLayout(false);
		this.page1.ResumeLayout(false);
		this.page1.PerformLayout();
		this.page2.ResumeLayout(false);
		this.tableLayoutPanel3.ResumeLayout(false);
		this.roundedPanel2.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
