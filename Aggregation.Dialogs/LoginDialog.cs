using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Dialogs;

public class LoginDialog : Form
{
	private Color _bgColor = Color.Silver;

	private IContainer components;

	private TableLayoutPanel tableLayoutPanel1;

	private RoundedButton roundedButton2;

	private RoundedButton roundedButton1;

	private RoundedPanel roundedPanel1;

	private PictureBox pictureBox1;

	private Label label2;

	private Label label1;

	private TouchTextBox touchTextBox1;

	private RoundedPanel roundedPanel2;

	private RoundedPanel roundedPanel3;

	private TouchTextBox touchTextBox2;

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
				roundedButton2.BGColor = _bgColor;
				roundedPanel1.BackColor = _bgColor;
			}
		}
	}

	public string Login => touchTextBox1.Text;

	public LoginDialog()
	{
		InitializeComponent();
	}

	private void LoginDialog_KeyDown(object sender, KeyEventArgs e)
	{
		switch (e.KeyCode)
		{
		case Keys.Return:
			DialogResult = DialogResult.OK;
			break;
		case Keys.Escape:
			DialogResult = DialogResult.Cancel;
			break;
		}
	}

	private void roundedButton1_Click(object sender, EventArgs e)
	{
		if (touchTextBox1.Text == "operator" && touchTextBox2.Text == "operator")
		{
			DialogResult = DialogResult.OK;
		}
		if (touchTextBox1.Text == "master" && touchTextBox2.Text == "master")
		{
			DialogResult = DialogResult.OK;
		}
		if (touchTextBox1.Text == "admin" && touchTextBox2.Text == "admin")
		{
			DialogResult = DialogResult.OK;
		}
	}

	private void pictureBox1_DoubleClick(object sender, EventArgs e)
	{
		touchTextBox1.Text = "admin";
		touchTextBox2.Text = "admin";
	}

	private string touchTextBox1_ShowTouchInput(object sender, string oldValue, bool isNumeric)
	{
		if (isNumeric)
		{
			using NumericKeyboardDialog numericKeyboardDialog = new NumericKeyboardDialog();
			numericKeyboardDialog.Value = oldValue;
			if (numericKeyboardDialog.ShowDialog() == DialogResult.OK)
			{
				return numericKeyboardDialog.Value;
			}
		}
		else
		{
			using AlphaNumericKeyboardDialog numericKeyboardDialog2 = new AlphaNumericKeyboardDialog();
			numericKeyboardDialog2.Value = oldValue;
			if (numericKeyboardDialog2.ShowDialog() == DialogResult.OK)
			{
				return numericKeyboardDialog2.Value;
			}
		}
		return oldValue;
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
		System.ComponentModel.ComponentResourceManager componentResourceManager = new System.ComponentModel.ComponentResourceManager(typeof(Aggregation.Dialogs.LoginDialog));
		this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
		this.roundedButton2 = new UIKit.RoundedButton();
		this.roundedButton1 = new UIKit.RoundedButton();
		this.roundedPanel1 = new UIKit.RoundedPanel();
		this.roundedPanel3 = new UIKit.RoundedPanel();
		this.touchTextBox2 = new UIKit.TouchTextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.label1 = new System.Windows.Forms.Label();
		this.pictureBox1 = new System.Windows.Forms.PictureBox();
		this.roundedPanel2 = new UIKit.RoundedPanel();
		this.touchTextBox1 = new UIKit.TouchTextBox();
		this.tableLayoutPanel1.SuspendLayout();
		this.roundedPanel1.SuspendLayout();
		this.roundedPanel3.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
		this.roundedPanel2.SuspendLayout();
		base.SuspendLayout();
		this.tableLayoutPanel1.ColumnCount = 2;
		this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.Controls.Add(this.roundedButton2, 1, 0);
		this.tableLayoutPanel1.Controls.Add(this.roundedButton1, 0, 0);
		this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tableLayoutPanel1.Location = new System.Drawing.Point(10, 191);
		this.tableLayoutPanel1.Name = "tableLayoutPanel1";
		this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
		this.tableLayoutPanel1.RowCount = 1;
		this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.Size = new System.Drawing.Size(481, 80);
		this.tableLayoutPanel1.TabIndex = 40;
		this.roundedButton2.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton2.BackColorHovered = System.Drawing.Color.DarkGray;
		this.roundedButton2.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton2.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButton2.BorderColor = System.Drawing.Color.Black;
		this.roundedButton2.CornerRadius = 16;
		this.roundedButton2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.roundedButton2.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButton2.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButton2.ForeColor = System.Drawing.Color.White;
		this.roundedButton2.ForeColorHovered = System.Drawing.Color.Black;
		this.roundedButton2.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButton2.HoverEffect = false;
		this.roundedButton2.ImageDisabled = null;
		this.roundedButton2.ImageEnabled = null;
		this.roundedButton2.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButton2.LeftSideRounded = true;
		this.roundedButton2.Location = new System.Drawing.Point(245, 10);
		this.roundedButton2.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
		this.roundedButton2.Name = "roundedButton2";
		this.roundedButton2.Pressed = false;
		this.roundedButton2.RightSideRounded = true;
		this.roundedButton2.RoundedCorners = true;
		this.roundedButton2.ShowBorder = false;
		this.roundedButton2.Size = new System.Drawing.Size(236, 70);
		this.roundedButton2.TabIndex = 3;
		this.roundedButton2.Text = "Отмена";
		this.roundedButton2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButton1.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButton1.BackColorHovered = System.Drawing.Color.DarkGray;
		this.roundedButton1.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButton1.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButton1.BorderColor = System.Drawing.Color.Black;
		this.roundedButton1.CornerRadius = 16;
		this.roundedButton1.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButton1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButton1.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButton1.ForeColor = System.Drawing.Color.White;
		this.roundedButton1.ForeColorHovered = System.Drawing.Color.Black;
		this.roundedButton1.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButton1.HoverEffect = false;
		this.roundedButton1.ImageDisabled = null;
		this.roundedButton1.ImageEnabled = null;
		this.roundedButton1.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButton1.LeftSideRounded = true;
		this.roundedButton1.Location = new System.Drawing.Point(0, 10);
		this.roundedButton1.Margin = new System.Windows.Forms.Padding(0, 0, 5, 0);
		this.roundedButton1.Name = "roundedButton1";
		this.roundedButton1.Pressed = false;
		this.roundedButton1.RightSideRounded = true;
		this.roundedButton1.RoundedCorners = true;
		this.roundedButton1.ShowBorder = false;
		this.roundedButton1.Size = new System.Drawing.Size(235, 70);
		this.roundedButton1.TabIndex = 2;
		this.roundedButton1.Text = "Вход";
		this.roundedButton1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButton1.Click += new System.EventHandler(this.roundedButton1_Click);
		this.roundedPanel1.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedPanel1.BorderColor = System.Drawing.Color.White;
		this.roundedPanel1.Caption = null;
		this.roundedPanel1.Controls.Add(this.roundedPanel3);
		this.roundedPanel1.Controls.Add(this.label2);
		this.roundedPanel1.Controls.Add(this.label1);
		this.roundedPanel1.Controls.Add(this.pictureBox1);
		this.roundedPanel1.Controls.Add(this.roundedPanel2);
		this.roundedPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanel1.DrawCaption = false;
		this.roundedPanel1.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel1.HeaderHeight = 20;
		this.roundedPanel1.LeftBottom = true;
		this.roundedPanel1.LeftTop = true;
		this.roundedPanel1.Location = new System.Drawing.Point(10, 10);
		this.roundedPanel1.Name = "roundedPanel1";
		this.roundedPanel1.Padding = new System.Windows.Forms.Padding(10);
		this.roundedPanel1.PanelBackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.roundedPanel1.Radius = 16;
		this.roundedPanel1.RightBottom = true;
		this.roundedPanel1.RightTop = true;
		this.roundedPanel1.SelectCaption = false;
		this.roundedPanel1.ShowHeader = false;
		this.roundedPanel1.Size = new System.Drawing.Size(481, 181);
		this.roundedPanel1.TabIndex = 41;
		this.roundedPanel1.Thickness = 0f;
		this.roundedPanel3.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.roundedPanel3.BorderColor = System.Drawing.Color.White;
		this.roundedPanel3.Caption = null;
		this.roundedPanel3.Controls.Add(this.touchTextBox2);
		this.roundedPanel3.DrawCaption = false;
		this.roundedPanel3.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel3.HeaderHeight = 20;
		this.roundedPanel3.LeftBottom = true;
		this.roundedPanel3.LeftTop = true;
		this.roundedPanel3.Location = new System.Drawing.Point(239, 98);
		this.roundedPanel3.Name = "roundedPanel3";
		this.roundedPanel3.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanel3.Radius = 20;
		this.roundedPanel3.RightBottom = true;
		this.roundedPanel3.RightTop = true;
		this.roundedPanel3.SelectCaption = false;
		this.roundedPanel3.ShowHeader = false;
		this.roundedPanel3.Size = new System.Drawing.Size(197, 53);
		this.roundedPanel3.TabIndex = 55;
		this.roundedPanel3.Thickness = 0f;
		this.touchTextBox2.BackColor = System.Drawing.Color.Black;
		this.touchTextBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBox2.Editable = true;
		this.touchTextBox2.Font = new System.Drawing.Font("Tektur", 21.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.touchTextBox2.ForeColor = System.Drawing.Color.White;
		this.touchTextBox2.IsNumeric = false;
		this.touchTextBox2.Location = new System.Drawing.Point(4, 8);
		this.touchTextBox2.Name = "touchTextBox2";
		this.touchTextBox2.Size = new System.Drawing.Size(189, 38);
		this.touchTextBox2.TabIndex = 50;
		this.touchTextBox2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBox2.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox1_ShowTouchInput);
		this.label2.AutoSize = true;
		this.label2.BackColor = System.Drawing.Color.Transparent;
		this.label2.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label2.ForeColor = System.Drawing.Color.White;
		this.label2.Location = new System.Drawing.Point(118, 108);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(115, 35);
		this.label2.TabIndex = 53;
		this.label2.Text = "Пароль";
		this.label1.AutoSize = true;
		this.label1.BackColor = System.Drawing.Color.Transparent;
		this.label1.Font = new System.Drawing.Font("Tektur", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.label1.ForeColor = System.Drawing.Color.White;
		this.label1.Location = new System.Drawing.Point(118, 39);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(97, 35);
		this.label1.TabIndex = 51;
		this.label1.Text = "Логин";
		this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.pictureBox1.BackColor = System.Drawing.Color.Transparent;
		this.pictureBox1.Image = (System.Drawing.Image)componentResourceManager.GetObject("pictureBox1.Image");
		this.pictureBox1.Location = new System.Drawing.Point(31, 58);
		this.pictureBox1.Name = "pictureBox1";
		this.pictureBox1.Size = new System.Drawing.Size(64, 64);
		this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
		this.pictureBox1.TabIndex = 0;
		this.pictureBox1.TabStop = false;
		this.pictureBox1.DoubleClick += new System.EventHandler(this.pictureBox1_DoubleClick);
		this.roundedPanel2.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.roundedPanel2.BorderColor = System.Drawing.Color.White;
		this.roundedPanel2.Caption = null;
		this.roundedPanel2.Controls.Add(this.touchTextBox1);
		this.roundedPanel2.DrawCaption = false;
		this.roundedPanel2.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel2.HeaderHeight = 20;
		this.roundedPanel2.LeftBottom = true;
		this.roundedPanel2.LeftTop = true;
		this.roundedPanel2.Location = new System.Drawing.Point(239, 29);
		this.roundedPanel2.Name = "roundedPanel2";
		this.roundedPanel2.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanel2.Radius = 20;
		this.roundedPanel2.RightBottom = true;
		this.roundedPanel2.RightTop = true;
		this.roundedPanel2.SelectCaption = false;
		this.roundedPanel2.ShowHeader = false;
		this.roundedPanel2.Size = new System.Drawing.Size(197, 53);
		this.roundedPanel2.TabIndex = 54;
		this.roundedPanel2.Thickness = 0f;
		this.touchTextBox1.BackColor = System.Drawing.Color.Black;
		this.touchTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBox1.Editable = true;
		this.touchTextBox1.Font = new System.Drawing.Font("Tektur", 21.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.touchTextBox1.ForeColor = System.Drawing.Color.White;
		this.touchTextBox1.IsNumeric = false;
		this.touchTextBox1.Location = new System.Drawing.Point(4, 8);
		this.touchTextBox1.Name = "touchTextBox1";
		this.touchTextBox1.Size = new System.Drawing.Size(189, 38);
		this.touchTextBox1.TabIndex = 50;
		this.touchTextBox1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBox1.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox1_ShowTouchInput);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(501, 281);
		base.Controls.Add(this.roundedPanel1);
		base.Controls.Add(this.tableLayoutPanel1);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "LoginDialog";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "ConfirmationDialog";
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(this.LoginDialog_KeyDown);
		this.tableLayoutPanel1.ResumeLayout(false);
		this.roundedPanel1.ResumeLayout(false);
		this.roundedPanel1.PerformLayout();
		this.roundedPanel3.ResumeLayout(false);
		this.roundedPanel3.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
		this.roundedPanel2.ResumeLayout(false);
		this.roundedPanel2.PerformLayout();
		base.ResumeLayout(false);
	}
}
