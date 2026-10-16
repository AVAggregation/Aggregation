using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Aggregation.Settings;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Forms;

public class Settings : Form
{
	private const int WM_NCLBUTTONDOWN = 161;

	private const int HT_CAPTION = 2;

	private const string CONFIG_KEY_AUTO_START = "box_workflow_auto_start";

	private bool _autoStartEnabled;

	private readonly Controller _controller;

	private readonly AppSettingsManager _config;

	private readonly bool _ownsConfig;

	private IContainer components;

	private RoundedPanel rpMainContainer;

	private Label lblTitle;

	private RoundedButton btnOpenLensControl;

	private RoundedButton btnOpenPrinterSettings;

	private Label lblAutoStartDescription;

	private RoundedButton btnToggleAutoStart;

	private TableLayoutPanel tableLayoutPanelButtons;

	private RoundedButton btnClose;

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	public Settings()
		: this(null)
	{
	}

	public Settings(Controller controller)
	{
		_controller = controller;
		if (_controller != null && _controller.Config != null)
		{
			_config = _controller.Config;
			_ownsConfig = false;
		}
		else
		{
			_config = new AppSettingsManager
			{
				WriteChecksum = true,
				EnforceChecksum = false,
				AutoBackup = true
			};
			_ownsConfig = true;
		}
		InitializeComponent();
		LoadSettings();
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		base.OnFormClosed(e);
		if (_ownsConfig && _config != null)
		{
			_config.Dispose();
		}
	}

	private void LoadSettings()
	{
		try
		{
			_autoStartEnabled = _config.GetBool("box_workflow_auto_start");
			UpdateAutoStartButton();
		}
		catch (Exception ex)
		{
			ShowError("Ошибка при загрузке настроек:\n" + ex.Message);
		}
	}

	private bool SaveAutoStartSetting()
	{
		try
		{
			_config.Set("box_workflow_auto_start", _autoStartEnabled);
			_config.Save();
			return true;
		}
		catch (Exception ex)
		{
			ShowError("Ошибка при сохранении настроек:\n" + ex.Message);
			return false;
		}
	}

	private void UpdateAutoStartButton()
	{
		if (_autoStartEnabled)
		{
			btnToggleAutoStart.Text = "АВТО-СТАРТ: ВКЛ";
			btnToggleAutoStart.BackColor = Color.FromArgb(0, 120, 0);
			btnToggleAutoStart.BackColorHovered = Color.FromArgb(0, 100, 0);
			btnToggleAutoStart.BackColorPressed = Color.FromArgb(0, 150, 0);
			btnToggleAutoStart.BorderColor = Color.FromArgb(0, 180, 0);
			btnToggleAutoStart.ForeColor = Color.White;
			btnToggleAutoStart.Pressed = true;
		}
		else
		{
			btnToggleAutoStart.Text = "АВТО-СТАРТ: ВЫКЛ";
			btnToggleAutoStart.BackColor = Color.FromArgb(45, 45, 45);
			btnToggleAutoStart.BackColorHovered = Color.FromArgb(60, 60, 60);
			btnToggleAutoStart.BackColorPressed = Color.FromArgb(0, 150, 0);
			btnToggleAutoStart.BorderColor = Color.Gray;
			btnToggleAutoStart.ForeColor = Color.Silver;
			btnToggleAutoStart.Pressed = false;
		}
		btnToggleAutoStart.Refresh();
	}

	private void ShowInfo(string message, int seconds = 4)
	{
		using TimeOutDialog td = new TimeOutDialog();
		td.Height = 300;
		td.ShowInfo(message, seconds, 1);
	}

	private void ShowError(string message)
	{
		using ErrorDialog ed = new ErrorDialog();
		ed.Height = 300;
		ed.Width = 500;
		ed.ShowError(message);
	}

	private void rpMainContainer_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	private void btnOpenLensControl_Click(object sender, EventArgs e)
	{
		new LensControlForm(_controller).Show();
		Close();
	}

	private void btnOpenPrinterSettings_Click(object sender, EventArgs e)
	{
		using PrinterSettingsForm printerSettingsForm = new PrinterSettingsForm(_config);
		printerSettingsForm.ShowDialog();
	}

	private void btnToggleAutoStart_Click(object sender, EventArgs e)
	{
		_autoStartEnabled = !_autoStartEnabled;
		if (SaveAutoStartSetting())
		{
			UpdateAutoStartButton();
			string status = (_autoStartEnabled ? "включен" : "выключен");
			ShowInfo("Авто-старт " + status + ".\n\nИзменения вступят в силу после перезапуска.");
		}
		else
		{
			_autoStartEnabled = !_autoStartEnabled;
		}
	}

	private void btnClose_Click(object sender, EventArgs e)
	{
		Close();
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
		this.rpMainContainer = new UIKit.RoundedPanel();
		this.lblTitle = new System.Windows.Forms.Label();
		this.btnOpenLensControl = new UIKit.RoundedButton();
		this.btnOpenPrinterSettings = new UIKit.RoundedButton();
		this.lblAutoStartDescription = new System.Windows.Forms.Label();
		this.btnToggleAutoStart = new UIKit.RoundedButton();
		this.tableLayoutPanelButtons = new System.Windows.Forms.TableLayoutPanel();
		this.btnClose = new UIKit.RoundedButton();
		this.rpMainContainer.SuspendLayout();
		this.tableLayoutPanelButtons.SuspendLayout();
		base.SuspendLayout();
		this.rpMainContainer.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.rpMainContainer.BorderColor = System.Drawing.Color.White;
		this.rpMainContainer.Caption = null;
		this.rpMainContainer.Controls.Add(this.lblTitle);
		this.rpMainContainer.Controls.Add(this.btnOpenLensControl);
		this.rpMainContainer.Controls.Add(this.btnOpenPrinterSettings);
		this.rpMainContainer.Controls.Add(this.lblAutoStartDescription);
		this.rpMainContainer.Controls.Add(this.btnToggleAutoStart);
		this.rpMainContainer.Dock = System.Windows.Forms.DockStyle.Fill;
		this.rpMainContainer.DrawCaption = false;
		this.rpMainContainer.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpMainContainer.HeaderHeight = 20;
		this.rpMainContainer.LeftBottom = true;
		this.rpMainContainer.LeftTop = true;
		this.rpMainContainer.Location = new System.Drawing.Point(10, 10);
		this.rpMainContainer.Name = "rpMainContainer";
		this.rpMainContainer.Padding = new System.Windows.Forms.Padding(10);
		this.rpMainContainer.PanelBackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.rpMainContainer.Radius = 16;
		this.rpMainContainer.RightBottom = true;
		this.rpMainContainer.RightTop = true;
		this.rpMainContainer.SelectCaption = false;
		this.rpMainContainer.ShowHeader = false;
		this.rpMainContainer.Size = new System.Drawing.Size(580, 310);
		this.rpMainContainer.TabIndex = 0;
		this.rpMainContainer.Thickness = 0f;
		this.rpMainContainer.MouseMove += new System.Windows.Forms.MouseEventHandler(this.rpMainContainer_MouseMove);
		this.lblTitle.AutoSize = true;
		this.lblTitle.BackColor = System.Drawing.Color.Transparent;
		this.lblTitle.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblTitle.ForeColor = System.Drawing.Color.White;
		this.lblTitle.Location = new System.Drawing.Point(20, 20);
		this.lblTitle.Name = "lblTitle";
		this.lblTitle.Size = new System.Drawing.Size(167, 29);
		this.lblTitle.TabIndex = 0;
		this.lblTitle.Text = "НАСТРОЙКИ";
		this.btnOpenLensControl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.btnOpenLensControl.BackColor = System.Drawing.Color.FromArgb(0, 120, 0);
		this.btnOpenLensControl.BackColorHovered = System.Drawing.Color.FromArgb(0, 150, 0);
		this.btnOpenLensControl.BackColorPressed = System.Drawing.Color.FromArgb(0, 100, 0);
		this.btnOpenLensControl.BGColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.btnOpenLensControl.BorderColor = System.Drawing.Color.Black;
		this.btnOpenLensControl.CornerRadius = 16;
		this.btnOpenLensControl.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnOpenLensControl.Font = new System.Drawing.Font("Tahoma", 16f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.btnOpenLensControl.ForeColor = System.Drawing.Color.White;
		this.btnOpenLensControl.ForeColorHovered = System.Drawing.Color.White;
		this.btnOpenLensControl.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.btnOpenLensControl.HoverEffect = false;
		this.btnOpenLensControl.ImageDisabled = null;
		this.btnOpenLensControl.ImageEnabled = null;
		this.btnOpenLensControl.ImageSize = new System.Drawing.Size(0, 0);
		this.btnOpenLensControl.LeftSideRounded = true;
		this.btnOpenLensControl.LeftTextOffset = 0;
		this.btnOpenLensControl.Location = new System.Drawing.Point(20, 65);
		this.btnOpenLensControl.Name = "btnOpenLensControl";
		this.btnOpenLensControl.Pressed = false;
		this.btnOpenLensControl.RightSideRounded = true;
		this.btnOpenLensControl.RoundedCorners = true;
		this.btnOpenLensControl.ShowBorder = false;
		this.btnOpenLensControl.Size = new System.Drawing.Size(540, 60);
		this.btnOpenLensControl.TabIndex = 1;
		this.btnOpenLensControl.Text = "Объектив";
		this.btnOpenLensControl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnOpenLensControl.Click += new System.EventHandler(this.btnOpenLensControl_Click);
		this.btnOpenPrinterSettings.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.btnOpenPrinterSettings.BackColor = System.Drawing.Color.FromArgb(0, 120, 0);
		this.btnOpenPrinterSettings.BackColorHovered = System.Drawing.Color.FromArgb(0, 150, 0);
		this.btnOpenPrinterSettings.BackColorPressed = System.Drawing.Color.FromArgb(0, 100, 0);
		this.btnOpenPrinterSettings.BGColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.btnOpenPrinterSettings.BorderColor = System.Drawing.Color.Black;
		this.btnOpenPrinterSettings.CornerRadius = 16;
		this.btnOpenPrinterSettings.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnOpenPrinterSettings.Font = new System.Drawing.Font("Tahoma", 16f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.btnOpenPrinterSettings.ForeColor = System.Drawing.Color.White;
		this.btnOpenPrinterSettings.ForeColorHovered = System.Drawing.Color.White;
		this.btnOpenPrinterSettings.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.btnOpenPrinterSettings.HoverEffect = false;
		this.btnOpenPrinterSettings.ImageDisabled = null;
		this.btnOpenPrinterSettings.ImageEnabled = null;
		this.btnOpenPrinterSettings.ImageSize = new System.Drawing.Size(0, 0);
		this.btnOpenPrinterSettings.LeftSideRounded = true;
		this.btnOpenPrinterSettings.LeftTextOffset = 0;
		this.btnOpenPrinterSettings.Location = new System.Drawing.Point(20, 135);
		this.btnOpenPrinterSettings.Name = "btnOpenPrinterSettings";
		this.btnOpenPrinterSettings.Pressed = false;
		this.btnOpenPrinterSettings.RightSideRounded = true;
		this.btnOpenPrinterSettings.RoundedCorners = true;
		this.btnOpenPrinterSettings.ShowBorder = false;
		this.btnOpenPrinterSettings.Size = new System.Drawing.Size(540, 60);
		this.btnOpenPrinterSettings.TabIndex = 2;
		this.btnOpenPrinterSettings.Text = "Печать";
		this.btnOpenPrinterSettings.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnOpenPrinterSettings.Click += new System.EventHandler(this.btnOpenPrinterSettings_Click);
		this.lblAutoStartDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblAutoStartDescription.BackColor = System.Drawing.Color.Transparent;
		this.lblAutoStartDescription.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblAutoStartDescription.ForeColor = System.Drawing.Color.Silver;
		this.lblAutoStartDescription.Location = new System.Drawing.Point(20, 210);
		this.lblAutoStartDescription.Name = "lblAutoStartDescription";
		this.lblAutoStartDescription.Size = new System.Drawing.Size(540, 25);
		this.lblAutoStartDescription.TabIndex = 3;
		this.lblAutoStartDescription.Text = "Авто-старт следующего короба после завершения:";
		this.btnToggleAutoStart.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.btnToggleAutoStart.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
		this.btnToggleAutoStart.BackColorHovered = System.Drawing.Color.FromArgb(60, 60, 60);
		this.btnToggleAutoStart.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.btnToggleAutoStart.BGColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.btnToggleAutoStart.BorderColor = System.Drawing.Color.Gray;
		this.btnToggleAutoStart.CornerRadius = 16;
		this.btnToggleAutoStart.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnToggleAutoStart.Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.btnToggleAutoStart.ForeColor = System.Drawing.Color.Silver;
		this.btnToggleAutoStart.ForeColorHovered = System.Drawing.Color.White;
		this.btnToggleAutoStart.ForeColorPressed = System.Drawing.Color.White;
		this.btnToggleAutoStart.HoverEffect = true;
		this.btnToggleAutoStart.ImageDisabled = null;
		this.btnToggleAutoStart.ImageEnabled = null;
		this.btnToggleAutoStart.ImageSize = new System.Drawing.Size(0, 0);
		this.btnToggleAutoStart.LeftSideRounded = true;
		this.btnToggleAutoStart.LeftTextOffset = 0;
		this.btnToggleAutoStart.Location = new System.Drawing.Point(20, 240);
		this.btnToggleAutoStart.Name = "btnToggleAutoStart";
		this.btnToggleAutoStart.Pressed = false;
		this.btnToggleAutoStart.RightSideRounded = true;
		this.btnToggleAutoStart.RoundedCorners = true;
		this.btnToggleAutoStart.ShowBorder = true;
		this.btnToggleAutoStart.Size = new System.Drawing.Size(540, 50);
		this.btnToggleAutoStart.TabIndex = 4;
		this.btnToggleAutoStart.Text = "АВТО-СТАРТ: ВЫКЛ";
		this.btnToggleAutoStart.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnToggleAutoStart.Click += new System.EventHandler(this.btnToggleAutoStart_Click);
		this.tableLayoutPanelButtons.ColumnCount = 1;
		this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tableLayoutPanelButtons.Controls.Add(this.btnClose, 0, 0);
		this.tableLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tableLayoutPanelButtons.Location = new System.Drawing.Point(10, 320);
		this.tableLayoutPanelButtons.Name = "tableLayoutPanelButtons";
		this.tableLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
		this.tableLayoutPanelButtons.RowCount = 1;
		this.tableLayoutPanelButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tableLayoutPanelButtons.Size = new System.Drawing.Size(580, 80);
		this.tableLayoutPanelButtons.TabIndex = 1;
		this.btnClose.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.btnClose.BackColorHovered = System.Drawing.Color.DarkGray;
		this.btnClose.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnClose.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.btnClose.BorderColor = System.Drawing.Color.Black;
		this.btnClose.CornerRadius = 16;
		this.btnClose.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnClose.Dock = System.Windows.Forms.DockStyle.Fill;
		this.btnClose.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnClose.ForeColor = System.Drawing.Color.White;
		this.btnClose.ForeColorHovered = System.Drawing.Color.Black;
		this.btnClose.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.btnClose.HoverEffect = false;
		this.btnClose.ImageDisabled = null;
		this.btnClose.ImageEnabled = null;
		this.btnClose.ImageSize = new System.Drawing.Size(0, 0);
		this.btnClose.LeftSideRounded = true;
		this.btnClose.LeftTextOffset = 0;
		this.btnClose.Location = new System.Drawing.Point(0, 10);
		this.btnClose.Margin = new System.Windows.Forms.Padding(0);
		this.btnClose.Name = "btnClose";
		this.btnClose.Pressed = false;
		this.btnClose.RightSideRounded = true;
		this.btnClose.RoundedCorners = true;
		this.btnClose.ShowBorder = false;
		this.btnClose.Size = new System.Drawing.Size(580, 70);
		this.btnClose.TabIndex = 0;
		this.btnClose.Text = "Закрыть";
		this.btnClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(600, 410);
		base.Controls.Add(this.rpMainContainer);
		base.Controls.Add(this.tableLayoutPanelButtons);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "Settings";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Настройки";
		this.rpMainContainer.ResumeLayout(false);
		this.rpMainContainer.PerformLayout();
		this.tableLayoutPanelButtons.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
