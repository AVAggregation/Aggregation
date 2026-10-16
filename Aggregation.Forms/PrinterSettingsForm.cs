using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Aggregation.Settings;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Forms;

public class PrinterSettingsForm : Form
{
	private const int WM_NCLBUTTONDOWN = 161;

	private const int HT_CAPTION = 2;

	private const string CONFIG_KEY_PRINTER_NAME = "PRINTER_name";

	private const string CONFIG_KEY_PRINTER_COPIES = "PRINTER_copies_default";

	private const string CONFIG_KEY_TEMPLATE_SOURCE = "PRINTER_template_source";

	private const string TEMPLATE_SOURCE_SERVER = "server";

	private const string TEMPLATE_SOURCE_LOCAL = "local";

	private const int MinCopies = 1;

	private const int MaxCopies = 10;

	private readonly AppSettingsManager _config;

	private readonly bool _ownsConfig;

	private string _originalPrinterName;

	private int _originalCopies;

	private bool _originalUseServerTemplate;

	private bool _useServerTemplate = true;

	private IContainer components;

	private RoundedPanel rpMainContainer;

	private Label lblTitle;

	private Label lblPrinterName;

	private RoundedPanel rpPrinterName;

	private TouchTextBox touchTextBoxPrinterName;

	private Label lblCopies;

	private RoundedPanel rpCopies;

	private TouchTextBox touchTextBoxCopies;

	private Label lblTemplateSource;

	private Panel panelTemplateSource;

	private RoundedButton btnTemplateServer;

	private RoundedButton btnTemplateLocal;

	private TableLayoutPanel tableLayoutPanelButtons;

	private RoundedButton btnSave;

	private RoundedButton btnClose;

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	public PrinterSettingsForm()
		: this(null)
	{
	}

	public PrinterSettingsForm(AppSettingsManager config)
	{
		InitializeComponent();
		if (config != null)
		{
			_config = config;
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
		LoadSettings();
	}

	private void PrinterSettingsForm_Load(object sender, EventArgs e)
	{
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		base.OnFormClosed(e);
		if (_ownsConfig && _config != null)
		{
			_config.Dispose();
		}
	}

	private void rpMainContainer_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	private string touchTextBox_ShowTouchInput(object sender, string oldValue, bool isNumeric)
	{
		if (isNumeric)
		{
			using NumericKeyboardDialog d = new NumericKeyboardDialog();
			d.Value = oldValue;
			if (d.ShowDialog() == DialogResult.OK)
			{
				return d.Value;
			}
		}
		else
		{
			using AlphaNumericKeyboardDialog d2 = new AlphaNumericKeyboardDialog();
			d2.Value = oldValue;
			if (d2.ShowDialog() == DialogResult.OK)
			{
				return d2.Value;
			}
		}
		return oldValue;
	}

	private void touchTextBoxCopies_TextChanged(object sender, EventArgs e)
	{
		ValidateCopiesInput();
	}

	private void ValidateCopiesInput()
	{
		string text = touchTextBoxCopies.Text.Trim();
		if (!string.IsNullOrEmpty(text))
		{
			if (!int.TryParse(text, out var value))
			{
				touchTextBoxCopies.Text = "1";
			}
			else if (value < 1)
			{
				touchTextBoxCopies.Text = 1.ToString();
			}
			else if (value > 10)
			{
				touchTextBoxCopies.Text = 10.ToString();
			}
		}
	}

	private void LoadSettings()
	{
		try
		{
			touchTextBoxPrinterName.Text = _config.Get("PRINTER_name", "");
			int copies = _config.GetInt("PRINTER_copies_default", 1);
			copies = Math.Max(1, Math.Min(10, copies));
			touchTextBoxCopies.Text = copies.ToString();
			string source = _config.Get("PRINTER_template_source", "server");
			_useServerTemplate = source.Equals("server", StringComparison.OrdinalIgnoreCase);
			UpdateTemplateButtonVisuals();
			StoreOriginalValues();
		}
		catch (Exception ex)
		{
			ShowError("Ошибка при загрузке настроек:\n" + ex.Message);
		}
	}

	private bool SaveSettings()
	{
		try
		{
			int copies = GetCopiesValue();
			_config.Set("PRINTER_name", touchTextBoxPrinterName.Text.Trim());
			_config.Set("PRINTER_copies_default", copies);
			_config.Set("PRINTER_template_source", _useServerTemplate ? "server" : "local");
			_config.Save();
			StoreOriginalValues();
			return true;
		}
		catch (Exception ex)
		{
			ShowError("Ошибка при сохранении настроек:\n" + ex.Message);
			return false;
		}
	}

	private int GetCopiesValue()
	{
		if (!int.TryParse(touchTextBoxCopies.Text, out var value) || value < 1)
		{
			return 1;
		}
		return Math.Min(value, 10);
	}

	private void StoreOriginalValues()
	{
		_originalPrinterName = touchTextBoxPrinterName.Text;
		_originalCopies = GetCopiesValue();
		_originalUseServerTemplate = _useServerTemplate;
	}

	private bool HasUnsavedChanges()
	{
		if (!(touchTextBoxPrinterName.Text != _originalPrinterName) && GetCopiesValue() == _originalCopies)
		{
			return _useServerTemplate != _originalUseServerTemplate;
		}
		return true;
	}

	private void UpdateTemplateButtonVisuals()
	{
		if (_useServerTemplate)
		{
			btnTemplateServer.BackColor = Color.FromArgb(0, 120, 0);
			btnTemplateServer.BackColorHovered = Color.FromArgb(0, 100, 0);
			btnTemplateServer.BackColorPressed = Color.FromArgb(0, 150, 0);
			btnTemplateServer.BorderColor = Color.FromArgb(0, 180, 0);
			btnTemplateServer.ForeColor = Color.White;
			btnTemplateServer.Pressed = true;
			btnTemplateLocal.BackColor = Color.FromArgb(45, 45, 45);
			btnTemplateLocal.BackColorHovered = Color.FromArgb(60, 60, 60);
			btnTemplateLocal.BackColorPressed = Color.FromArgb(0, 150, 0);
			btnTemplateLocal.BorderColor = Color.Gray;
			btnTemplateLocal.ForeColor = Color.Silver;
			btnTemplateLocal.Pressed = false;
		}
		else
		{
			btnTemplateServer.BackColor = Color.FromArgb(45, 45, 45);
			btnTemplateServer.BackColorHovered = Color.FromArgb(60, 60, 60);
			btnTemplateServer.BackColorPressed = Color.FromArgb(0, 150, 0);
			btnTemplateServer.BorderColor = Color.Gray;
			btnTemplateServer.ForeColor = Color.Silver;
			btnTemplateServer.Pressed = false;
			btnTemplateLocal.BackColor = Color.FromArgb(0, 120, 0);
			btnTemplateLocal.BackColorHovered = Color.FromArgb(0, 100, 0);
			btnTemplateLocal.BackColorPressed = Color.FromArgb(0, 150, 0);
			btnTemplateLocal.BorderColor = Color.FromArgb(0, 180, 0);
			btnTemplateLocal.ForeColor = Color.White;
			btnTemplateLocal.Pressed = true;
		}
		btnTemplateServer.Refresh();
		btnTemplateLocal.Refresh();
	}

	private void ShowInfo(string message, int seconds = 3)
	{
		using TimeOutDialog td = new TimeOutDialog();
		td.Height = 300;
		td.ShowInfo(message, seconds, 2);
	}

	private void ShowSuccess(string message, int seconds = 4)
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

	private bool ShowConfirmation(string message)
	{
		using ConfirmationDialog cd = new ConfirmationDialog();
		return cd.ShowConfirmation(message) == DialogResult.Yes;
	}

	private void btnTemplateServer_Click(object sender, EventArgs e)
	{
		if (!_useServerTemplate)
		{
			_useServerTemplate = true;
			UpdateTemplateButtonVisuals();
		}
	}

	private void btnTemplateLocal_Click(object sender, EventArgs e)
	{
		if (_useServerTemplate)
		{
			_useServerTemplate = false;
			UpdateTemplateButtonVisuals();
		}
	}

	private void btnSave_Click(object sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(touchTextBoxPrinterName.Text))
		{
			ShowInfo("Введите имя принтера.");
			touchTextBoxPrinterName.Focus();
			return;
		}
		int copies = GetCopiesValue();
		if (copies < 1 || copies > 10)
		{
			ShowInfo($"Количество копий должно быть от {1} до {10}.");
			touchTextBoxCopies.Focus();
		}
		else if (SaveSettings())
		{
			ShowSuccess("Настройки сохранены.\n\nИзменения вступят в силу после перезапуска.");
			Close();
		}
	}

	private void btnClose_Click(object sender, EventArgs e)
	{
		if (!HasUnsavedChanges() || ShowConfirmation("Закрыть без сохранения изменений?"))
		{
			Close();
		}
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
		this.lblPrinterName = new System.Windows.Forms.Label();
		this.rpPrinterName = new UIKit.RoundedPanel();
		this.touchTextBoxPrinterName = new UIKit.TouchTextBox();
		this.lblCopies = new System.Windows.Forms.Label();
		this.rpCopies = new UIKit.RoundedPanel();
		this.touchTextBoxCopies = new UIKit.TouchTextBox();
		this.lblTemplateSource = new System.Windows.Forms.Label();
		this.panelTemplateSource = new System.Windows.Forms.Panel();
		this.btnTemplateServer = new UIKit.RoundedButton();
		this.btnTemplateLocal = new UIKit.RoundedButton();
		this.tableLayoutPanelButtons = new System.Windows.Forms.TableLayoutPanel();
		this.btnClose = new UIKit.RoundedButton();
		this.btnSave = new UIKit.RoundedButton();
		this.rpMainContainer.SuspendLayout();
		this.rpPrinterName.SuspendLayout();
		this.rpCopies.SuspendLayout();
		this.panelTemplateSource.SuspendLayout();
		this.tableLayoutPanelButtons.SuspendLayout();
		base.SuspendLayout();
		this.rpMainContainer.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.rpMainContainer.BorderColor = System.Drawing.Color.White;
		this.rpMainContainer.Caption = null;
		this.rpMainContainer.Controls.Add(this.lblTitle);
		this.rpMainContainer.Controls.Add(this.lblPrinterName);
		this.rpMainContainer.Controls.Add(this.rpPrinterName);
		this.rpMainContainer.Controls.Add(this.lblCopies);
		this.rpMainContainer.Controls.Add(this.rpCopies);
		this.rpMainContainer.Controls.Add(this.lblTemplateSource);
		this.rpMainContainer.Controls.Add(this.panelTemplateSource);
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
		this.rpMainContainer.Size = new System.Drawing.Size(580, 390);
		this.rpMainContainer.TabIndex = 0;
		this.rpMainContainer.Thickness = 0f;
		this.rpMainContainer.MouseMove += new System.Windows.Forms.MouseEventHandler(this.rpMainContainer_MouseMove);
		this.lblTitle.AutoSize = true;
		this.lblTitle.BackColor = System.Drawing.Color.Transparent;
		this.lblTitle.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblTitle.ForeColor = System.Drawing.Color.White;
		this.lblTitle.Location = new System.Drawing.Point(20, 20);
		this.lblTitle.Name = "lblTitle";
		this.lblTitle.Size = new System.Drawing.Size(274, 29);
		this.lblTitle.TabIndex = 0;
		this.lblTitle.Text = "НАСТРОЙКИ ПЕЧАТИ";
		this.lblPrinterName.AutoSize = true;
		this.lblPrinterName.BackColor = System.Drawing.Color.Transparent;
		this.lblPrinterName.Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblPrinterName.ForeColor = System.Drawing.Color.White;
		this.lblPrinterName.Location = new System.Drawing.Point(20, 70);
		this.lblPrinterName.Name = "lblPrinterName";
		this.lblPrinterName.Size = new System.Drawing.Size(135, 23);
		this.lblPrinterName.TabIndex = 1;
		this.lblPrinterName.Text = "Имя принтера";
		this.rpPrinterName.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.rpPrinterName.BorderColor = System.Drawing.Color.White;
		this.rpPrinterName.Caption = null;
		this.rpPrinterName.Controls.Add(this.touchTextBoxPrinterName);
		this.rpPrinterName.DrawCaption = false;
		this.rpPrinterName.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpPrinterName.HeaderHeight = 20;
		this.rpPrinterName.LeftBottom = true;
		this.rpPrinterName.LeftTop = true;
		this.rpPrinterName.Location = new System.Drawing.Point(20, 98);
		this.rpPrinterName.Name = "rpPrinterName";
		this.rpPrinterName.PanelBackColor = System.Drawing.Color.Black;
		this.rpPrinterName.Radius = 20;
		this.rpPrinterName.RightBottom = true;
		this.rpPrinterName.RightTop = true;
		this.rpPrinterName.SelectCaption = false;
		this.rpPrinterName.ShowHeader = false;
		this.rpPrinterName.Size = new System.Drawing.Size(540, 60);
		this.rpPrinterName.TabIndex = 2;
		this.rpPrinterName.Thickness = 0f;
		this.touchTextBoxPrinterName.BackColor = System.Drawing.Color.Black;
		this.touchTextBoxPrinterName.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBoxPrinterName.Editable = true;
		this.touchTextBoxPrinterName.Font = new System.Drawing.Font("Tahoma", 20f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.touchTextBoxPrinterName.ForeColor = System.Drawing.Color.White;
		this.touchTextBoxPrinterName.IsNumeric = false;
		this.touchTextBoxPrinterName.Location = new System.Drawing.Point(4, 13);
		this.touchTextBoxPrinterName.Name = "touchTextBoxPrinterName";
		this.touchTextBoxPrinterName.Size = new System.Drawing.Size(530, 33);
		this.touchTextBoxPrinterName.TabIndex = 0;
		this.touchTextBoxPrinterName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBoxPrinterName.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox_ShowTouchInput);
		this.lblCopies.AutoSize = true;
		this.lblCopies.BackColor = System.Drawing.Color.Transparent;
		this.lblCopies.Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblCopies.ForeColor = System.Drawing.Color.White;
		this.lblCopies.Location = new System.Drawing.Point(20, 170);
		this.lblCopies.Name = "lblCopies";
		this.lblCopies.Size = new System.Drawing.Size(300, 23);
		this.lblCopies.TabIndex = 3;
		this.lblCopies.Text = "Количество копий по умолчанию";
		this.rpCopies.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.rpCopies.BorderColor = System.Drawing.Color.White;
		this.rpCopies.Caption = null;
		this.rpCopies.Controls.Add(this.touchTextBoxCopies);
		this.rpCopies.DrawCaption = false;
		this.rpCopies.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpCopies.HeaderHeight = 20;
		this.rpCopies.LeftBottom = true;
		this.rpCopies.LeftTop = true;
		this.rpCopies.Location = new System.Drawing.Point(20, 198);
		this.rpCopies.Name = "rpCopies";
		this.rpCopies.PanelBackColor = System.Drawing.Color.Black;
		this.rpCopies.Radius = 20;
		this.rpCopies.RightBottom = true;
		this.rpCopies.RightTop = true;
		this.rpCopies.SelectCaption = false;
		this.rpCopies.ShowHeader = false;
		this.rpCopies.Size = new System.Drawing.Size(150, 60);
		this.rpCopies.TabIndex = 4;
		this.rpCopies.Thickness = 0f;
		this.touchTextBoxCopies.BackColor = System.Drawing.Color.Black;
		this.touchTextBoxCopies.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBoxCopies.Editable = true;
		this.touchTextBoxCopies.Font = new System.Drawing.Font("Tahoma", 20f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.touchTextBoxCopies.ForeColor = System.Drawing.Color.White;
		this.touchTextBoxCopies.IsNumeric = true;
		this.touchTextBoxCopies.Location = new System.Drawing.Point(4, 13);
		this.touchTextBoxCopies.Name = "touchTextBoxCopies";
		this.touchTextBoxCopies.Size = new System.Drawing.Size(140, 33);
		this.touchTextBoxCopies.TabIndex = 0;
		this.touchTextBoxCopies.Text = "1";
		this.touchTextBoxCopies.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBoxCopies.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox_ShowTouchInput);
		this.touchTextBoxCopies.TextChanged += new System.EventHandler(this.touchTextBoxCopies_TextChanged);
		this.lblTemplateSource.AutoSize = true;
		this.lblTemplateSource.BackColor = System.Drawing.Color.Transparent;
		this.lblTemplateSource.Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblTemplateSource.ForeColor = System.Drawing.Color.White;
		this.lblTemplateSource.Location = new System.Drawing.Point(20, 275);
		this.lblTemplateSource.Name = "lblTemplateSource";
		this.lblTemplateSource.Size = new System.Drawing.Size(175, 23);
		this.lblTemplateSource.TabIndex = 5;
		this.lblTemplateSource.Text = "Источник шаблона";
		this.panelTemplateSource.BackColor = System.Drawing.Color.Transparent;
		this.panelTemplateSource.Controls.Add(this.btnTemplateServer);
		this.panelTemplateSource.Controls.Add(this.btnTemplateLocal);
		this.panelTemplateSource.Location = new System.Drawing.Point(20, 303);
		this.panelTemplateSource.Name = "panelTemplateSource";
		this.panelTemplateSource.Size = new System.Drawing.Size(300, 50);
		this.panelTemplateSource.TabIndex = 6;
		this.btnTemplateServer.BackColor = System.Drawing.Color.FromArgb(0, 120, 0);
		this.btnTemplateServer.BackColorHovered = System.Drawing.Color.FromArgb(0, 100, 0);
		this.btnTemplateServer.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.btnTemplateServer.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.btnTemplateServer.BorderColor = System.Drawing.Color.FromArgb(0, 180, 0);
		this.btnTemplateServer.CornerRadius = 12;
		this.btnTemplateServer.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnTemplateServer.Dock = System.Windows.Forms.DockStyle.Left;
		this.btnTemplateServer.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.btnTemplateServer.ForeColor = System.Drawing.Color.White;
		this.btnTemplateServer.ForeColorHovered = System.Drawing.Color.White;
		this.btnTemplateServer.ForeColorPressed = System.Drawing.Color.White;
		this.btnTemplateServer.HoverEffect = true;
		this.btnTemplateServer.ImageDisabled = null;
		this.btnTemplateServer.ImageEnabled = null;
		this.btnTemplateServer.ImageSize = new System.Drawing.Size(0, 0);
		this.btnTemplateServer.LeftSideRounded = true;
		this.btnTemplateServer.LeftTextOffset = 0;
		this.btnTemplateServer.Location = new System.Drawing.Point(0, 0);
		this.btnTemplateServer.Margin = new System.Windows.Forms.Padding(0);
		this.btnTemplateServer.Name = "btnTemplateServer";
		this.btnTemplateServer.Pressed = true;
		this.btnTemplateServer.RightSideRounded = true;
		this.btnTemplateServer.RoundedCorners = true;
		this.btnTemplateServer.ShowBorder = true;
		this.btnTemplateServer.Size = new System.Drawing.Size(145, 50);
		this.btnTemplateServer.TabIndex = 0;
		this.btnTemplateServer.Text = "Из заказа";
		this.btnTemplateServer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnTemplateServer.Click += new System.EventHandler(this.btnTemplateServer_Click);
		this.btnTemplateLocal.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
		this.btnTemplateLocal.BackColorHovered = System.Drawing.Color.FromArgb(60, 60, 60);
		this.btnTemplateLocal.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.btnTemplateLocal.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.btnTemplateLocal.BorderColor = System.Drawing.Color.Gray;
		this.btnTemplateLocal.CornerRadius = 12;
		this.btnTemplateLocal.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnTemplateLocal.Dock = System.Windows.Forms.DockStyle.Right;
		this.btnTemplateLocal.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.btnTemplateLocal.ForeColor = System.Drawing.Color.Silver;
		this.btnTemplateLocal.ForeColorHovered = System.Drawing.Color.White;
		this.btnTemplateLocal.ForeColorPressed = System.Drawing.Color.White;
		this.btnTemplateLocal.HoverEffect = true;
		this.btnTemplateLocal.ImageDisabled = null;
		this.btnTemplateLocal.ImageEnabled = null;
		this.btnTemplateLocal.ImageSize = new System.Drawing.Size(0, 0);
		this.btnTemplateLocal.LeftSideRounded = true;
		this.btnTemplateLocal.LeftTextOffset = 0;
		this.btnTemplateLocal.Location = new System.Drawing.Point(155, 0);
		this.btnTemplateLocal.Margin = new System.Windows.Forms.Padding(0);
		this.btnTemplateLocal.Name = "btnTemplateLocal";
		this.btnTemplateLocal.Pressed = false;
		this.btnTemplateLocal.RightSideRounded = true;
		this.btnTemplateLocal.RoundedCorners = true;
		this.btnTemplateLocal.ShowBorder = true;
		this.btnTemplateLocal.Size = new System.Drawing.Size(145, 50);
		this.btnTemplateLocal.TabIndex = 1;
		this.btnTemplateLocal.Text = "Локальный";
		this.btnTemplateLocal.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnTemplateLocal.Click += new System.EventHandler(this.btnTemplateLocal_Click);
		this.tableLayoutPanelButtons.ColumnCount = 2;
		this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanelButtons.Controls.Add(this.btnClose, 1, 0);
		this.tableLayoutPanelButtons.Controls.Add(this.btnSave, 0, 0);
		this.tableLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tableLayoutPanelButtons.Location = new System.Drawing.Point(10, 400);
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
		this.btnClose.Location = new System.Drawing.Point(295, 10);
		this.btnClose.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
		this.btnClose.Name = "btnClose";
		this.btnClose.Pressed = false;
		this.btnClose.RightSideRounded = true;
		this.btnClose.RoundedCorners = true;
		this.btnClose.ShowBorder = false;
		this.btnClose.Size = new System.Drawing.Size(285, 70);
		this.btnClose.TabIndex = 1;
		this.btnClose.Text = "Закрыть";
		this.btnClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
		this.btnSave.BackColor = System.Drawing.Color.FromArgb(0, 120, 0);
		this.btnSave.BackColorHovered = System.Drawing.Color.FromArgb(0, 150, 0);
		this.btnSave.BackColorPressed = System.Drawing.Color.FromArgb(0, 100, 0);
		this.btnSave.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.btnSave.BorderColor = System.Drawing.Color.Black;
		this.btnSave.CornerRadius = 16;
		this.btnSave.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnSave.Dock = System.Windows.Forms.DockStyle.Fill;
		this.btnSave.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnSave.ForeColor = System.Drawing.Color.White;
		this.btnSave.ForeColorHovered = System.Drawing.Color.White;
		this.btnSave.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.btnSave.HoverEffect = false;
		this.btnSave.ImageDisabled = null;
		this.btnSave.ImageEnabled = null;
		this.btnSave.ImageSize = new System.Drawing.Size(0, 0);
		this.btnSave.LeftSideRounded = true;
		this.btnSave.LeftTextOffset = 0;
		this.btnSave.Location = new System.Drawing.Point(0, 10);
		this.btnSave.Margin = new System.Windows.Forms.Padding(0, 0, 5, 0);
		this.btnSave.Name = "btnSave";
		this.btnSave.Pressed = false;
		this.btnSave.RightSideRounded = true;
		this.btnSave.RoundedCorners = true;
		this.btnSave.ShowBorder = false;
		this.btnSave.Size = new System.Drawing.Size(285, 70);
		this.btnSave.TabIndex = 0;
		this.btnSave.Text = "Сохранить";
		this.btnSave.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(600, 490);
		base.Controls.Add(this.rpMainContainer);
		base.Controls.Add(this.tableLayoutPanelButtons);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "PrinterSettingsForm";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Настройки печати";
		base.Load += new System.EventHandler(this.PrinterSettingsForm_Load);
		this.rpMainContainer.ResumeLayout(false);
		this.rpMainContainer.PerformLayout();
		this.rpPrinterName.ResumeLayout(false);
		this.rpPrinterName.PerformLayout();
		this.rpCopies.ResumeLayout(false);
		this.rpCopies.PerformLayout();
		this.panelTemplateSource.ResumeLayout(false);
		this.tableLayoutPanelButtons.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
