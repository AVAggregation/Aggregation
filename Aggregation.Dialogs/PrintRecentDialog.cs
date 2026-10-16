using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AG.Entities;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Dialogs;

public class PrintRecentDialog : Form
{
	private readonly Controller _controller;

	private bool _isProcessing;

	private bool _flashToggle;

	private int _count = 1;

	private Color _bgColor = Color.FromArgb(65, 65, 65);

	private const int MaxPrintCount = 10;

	private const int MinPrintCount = 1;

	private const int DefaultLoadCount = 10;

	private const int WM_NCLBUTTONDOWN = 161;

	private const int HT_CAPTION = 2;

	private IContainer components;

	private TableLayoutPanel tableLayoutPanelButtons;

	private RoundedButton roundedButtonClose;

	private RoundedButton roundedButtonPrint;

	private TableLayoutPanel tableLayoutPanelCount;

	private RoundedButton roundedButtonPlus;

	private RoundedButton roundedButtonMinus;

	private Label labelCount;

	private Panel panelTemplate;

	private RoundedButton roundedButtonServer;

	private RoundedButton roundedButtonLocal;

	private RoundedPanel roundedPanel1;

	private ListView listView1;

	private ColumnHeader columnHeaderCode;

	private ColumnHeader columnHeaderDate;

	public AggregationEntity Selected { get; private set; }

	public Color BGColor
	{
		get
		{
			return _bgColor;
		}
		set
		{
			if (!(_bgColor == value))
			{
				_bgColor = value;
				BackColor = _bgColor;
				roundedButtonPrint.BGColor = _bgColor;
				roundedButtonClose.BGColor = _bgColor;
				roundedButtonPlus.BGColor = _bgColor;
				roundedButtonMinus.BGColor = _bgColor;
				roundedPanel1.BackColor = _bgColor;
			}
		}
	}

	public int Count
	{
		get
		{
			return _count;
		}
		set
		{
			_count = Math.Max(1, Math.Min(10, value));
			labelCount.Text = _count.ToString();
		}
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	public PrintRecentDialog()
	{
		InitializeComponent();
	}

	public PrintRecentDialog(Controller controller)
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		InitializeComponent();
		InitializeTemplateButtons();
	}

	private void InitializeTemplateButtons()
	{
		UpdateTemplateButtonVisuals(useServer: true);
	}

	private void UpdateTemplateButtonVisuals(bool useServer)
	{
		roundedButtonServer.BackColor = (useServer ? Color.FromArgb(0, 120, 0) : Color.FromArgb(45, 45, 45));
		roundedButtonServer.ForeColor = (useServer ? Color.White : Color.Silver);
		roundedButtonServer.BorderColor = (useServer ? Color.FromArgb(0, 180, 0) : Color.Gray);
		roundedButtonServer.Pressed = useServer;
		roundedButtonLocal.BackColor = ((!useServer) ? Color.FromArgb(0, 120, 0) : Color.FromArgb(45, 45, 45));
		roundedButtonLocal.ForeColor = ((!useServer) ? Color.White : Color.Silver);
		roundedButtonLocal.BorderColor = ((!useServer) ? Color.FromArgb(0, 180, 0) : Color.Gray);
		roundedButtonLocal.Pressed = !useServer;
	}

	private async void PrintRecentDialog_Load(object sender, EventArgs e)
	{
		await LoadCodesAsync();
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 134)
		{
			BGColor = (_flashToggle ? Color.FromArgb(255, 192, 128) : Color.FromArgb(65, 65, 65));
			_flashToggle = !_flashToggle;
		}
		else
		{
			_flashToggle = false;
		}
		base.WndProc(ref m);
	}

	private void roundedButtonMinus_MouseDown(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && Count > 1)
		{
			Count--;
		}
	}

	private void roundedButtonPlus_MouseDown(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && Count < 10)
		{
			Count++;
		}
	}

	private async void roundedButtonPrint_Click(object sender, EventArgs e)
	{
		if (!_isProcessing)
		{
			if (listView1.SelectedIndices.Count <= 0)
			{
				ShowInfoMessage("Выберите код из списка.", 2);
				return;
			}
			Selected = listView1.SelectedItems[0].Tag as AggregationEntity;
			await PrintSelectedAsync();
		}
	}

	private void roundedButtonServer_Click(object sender, EventArgs e)
	{
		if (!_isProcessing && !roundedButtonServer.Pressed)
		{
			UpdateTemplateButtonVisuals(useServer: true);
		}
	}

	private void roundedButtonLocal_Click(object sender, EventArgs e)
	{
		if (!_isProcessing && !roundedButtonLocal.Pressed)
		{
			UpdateTemplateButtonVisuals(useServer: false);
		}
	}

	private void listView1_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	private void labelCount_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	private async void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
	{
		if (!_isProcessing && listView1.SelectedIndices.Count > 0)
		{
			Selected = listView1.SelectedItems[0].Tag as AggregationEntity;
			await PrintSelectedAsync();
		}
	}

	private async Task LoadCodesAsync()
	{
		try
		{
			List<AggregationEntity> codes = await _controller.GetLastNGroupCodes(10);
			listView1.Visible = codes != null;
			listView1.Items.Clear();
			if (codes == null)
			{
				return;
			}
			foreach (AggregationEntity entity in codes)
			{
				ListViewItem item = new ListViewItem
				{
					Text = entity.GroupCode,
					Tag = entity,
					ImageIndex = 0,
					UseItemStyleForSubItems = false
				};
				ListViewItem.ListViewSubItem dateSubItem = new ListViewItem.ListViewSubItem
				{
					Text = (entity.DateTime ?? ""),
					ForeColor = Color.Gray
				};
				item.SubItems.Add(dateSubItem);
				listView1.Items.Add(item);
			}
		}
		catch (Exception arg)
		{
			_controller?.ErrorMessage($"Error loading codes: {arg}", "DIALOG", "LOAD_ERR");
			listView1.Visible = false;
		}
	}

	private async Task PrintSelectedAsync()
	{
		if (_controller == null)
		{
			ShowErrorMessage("Контроллер не инициализирован.");
			return;
		}
		string code = Selected?.GroupCode;
		if (string.IsNullOrWhiteSpace(code))
		{
			return;
		}
		SetProcessingState(isProcessing: true);
		try
		{
			var (templateData, templateSource) = await LoadTemplateAsync(code);
			if (!Controller.IsTemplateValid(templateData))
			{
				ShowErrorMessage("Шаблон (" + (templateSource ?? "неизвестный") + ") не содержит {{CODE}}.\n\nПечать невозможна.");
				return;
			}
			var (printed, lastError) = ExecutePrint(code, templateData);
			DisplayPrintResult(code, printed, Count, templateSource, lastError);
		}
		catch (Exception ex)
		{
			_controller.ErrorMessage($"Error in PrintSelectedAsync: {ex}", "DIALOG", "PRINT_ERR");
			ShowErrorMessage("Неожиданная ошибка: " + ex.Message);
		}
		finally
		{
			SetProcessingState(isProcessing: false);
		}
	}

	private async Task<(string TemplateData, string Source)> LoadTemplateAsync(string code)
	{
		bool pressed = roundedButtonServer.Pressed;
		string templateData = null;
		string source = null;
		if (pressed)
		{
			try
			{
				(string, string, bool) result = await _controller.GetTemplateForCodeAsync(code);
				if (!string.IsNullOrWhiteSpace(result.Item1))
				{
					templateData = result.Item1;
					source = result.Item2 ?? "сервер";
				}
			}
			catch (Exception ex)
			{
				templateData = _controller.GetDefaultTemplate();
				source = "локальный (" + ex.Message + ")";
			}
			if (string.IsNullOrWhiteSpace(templateData))
			{
				templateData = _controller.GetDefaultTemplate();
				source = "локальный (сервер вернул пустой шаблон)";
			}
		}
		else
		{
			templateData = _controller.GetDefaultTemplate();
			source = "локальный";
		}
		return (TemplateData: templateData, Source: source);
	}

	private (int Printed, string LastError) ExecutePrint(string code, string templateData)
	{
		int count = Count;
		int printed = 0;
		string lastError = null;
		for (int i = 0; i < count; i++)
		{
			if (!_controller.printCode(code, templateData, out var error))
			{
				lastError = (string.IsNullOrWhiteSpace(error) ? "Неизвестная ошибка принтера." : error);
				break;
			}
			printed++;
		}
		return (Printed: printed, LastError: lastError);
	}

	private void DisplayPrintResult(string code, int printed, int requested, string source, string error)
	{
		if (printed == requested)
		{
			_controller.InfoMessage($"Печать: {code} ({printed} коп., {source})", "PRINTER", "RECENT_OK");
			ShowInfoMessage($"Отправлено на печать: {printed} из {requested}.\nШаблон: {source}\n\n{code}", 3);
		}
		else if (printed > 0)
		{
			_controller.ErrorMessage($"Частичная печать: {code} ({printed}/{requested})", "PRINTER", "RECENT_PARTIAL");
			ShowErrorMessage($"Частично напечатано: {printed} из {requested}.\n\n{code}\n\n{error}");
		}
		else
		{
			_controller.ErrorMessage("Ошибка печати: " + code, "PRINTER", "RECENT_ERR");
			ShowErrorMessage($"Ошибка печати: {printed} из {requested}.\n\n{code}\n\n{error}");
		}
	}

	private void SetProcessingState(bool isProcessing)
	{
		_isProcessing = isProcessing;
		roundedButtonPrint.Enabled = !isProcessing;
		roundedButtonClose.Enabled = !isProcessing;
		roundedButtonPlus.Enabled = !isProcessing;
		roundedButtonMinus.Enabled = !isProcessing;
		roundedButtonServer.Enabled = !isProcessing;
		roundedButtonLocal.Enabled = !isProcessing;
		listView1.Enabled = !isProcessing;
		Cursor = (isProcessing ? Cursors.WaitCursor : Cursors.Default);
	}

	private void ShowInfoMessage(string message, int timeoutSeconds)
	{
		using TimeOutDialog dialog = new TimeOutDialog();
		dialog.Height = 620;
		dialog.ShowInfo(message, timeoutSeconds, 2);
	}

	private void ShowErrorMessage(string message)
	{
		using ErrorDialog dialog = new ErrorDialog();
		dialog.Height = 620;
		dialog.ShowError(message);
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
		this.tableLayoutPanelButtons = new System.Windows.Forms.TableLayoutPanel();
		this.roundedButtonClose = new UIKit.RoundedButton();
		this.roundedButtonPrint = new UIKit.RoundedButton();
		this.tableLayoutPanelCount = new System.Windows.Forms.TableLayoutPanel();
		this.roundedButtonPlus = new UIKit.RoundedButton();
		this.roundedButtonMinus = new UIKit.RoundedButton();
		this.labelCount = new System.Windows.Forms.Label();
		this.panelTemplate = new System.Windows.Forms.Panel();
		this.roundedButtonServer = new UIKit.RoundedButton();
		this.roundedButtonLocal = new UIKit.RoundedButton();
		this.roundedPanel1 = new UIKit.RoundedPanel();
		this.listView1 = new System.Windows.Forms.ListView();
		this.columnHeaderCode = new System.Windows.Forms.ColumnHeader();
		this.columnHeaderDate = new System.Windows.Forms.ColumnHeader();
		this.tableLayoutPanelButtons.SuspendLayout();
		this.tableLayoutPanelCount.SuspendLayout();
		this.panelTemplate.SuspendLayout();
		this.roundedPanel1.SuspendLayout();
		base.SuspendLayout();
		this.tableLayoutPanelButtons.ColumnCount = 2;
		this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanelButtons.Controls.Add(this.roundedButtonClose, 1, 0);
		this.tableLayoutPanelButtons.Controls.Add(this.roundedButtonPrint, 0, 0);
		this.tableLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tableLayoutPanelButtons.Location = new System.Drawing.Point(10, 560);
		this.tableLayoutPanelButtons.Margin = new System.Windows.Forms.Padding(10, 0, 0, 0);
		this.tableLayoutPanelButtons.Name = "tableLayoutPanelButtons";
		this.tableLayoutPanelButtons.RowCount = 1;
		this.tableLayoutPanelButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tableLayoutPanelButtons.Size = new System.Drawing.Size(495, 80);
		this.tableLayoutPanelButtons.TabIndex = 1;
		this.roundedButtonClose.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButtonClose.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButtonClose.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButtonClose.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonClose.BorderColor = System.Drawing.Color.Black;
		this.roundedButtonClose.CornerRadius = 16;
		this.roundedButtonClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.roundedButtonClose.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButtonClose.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonClose.ForeColor = System.Drawing.Color.White;
		this.roundedButtonClose.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButtonClose.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButtonClose.HoverEffect = false;
		this.roundedButtonClose.ImageDisabled = null;
		this.roundedButtonClose.ImageEnabled = null;
		this.roundedButtonClose.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonClose.LeftSideRounded = true;
		this.roundedButtonClose.LeftTextOffset = 0;
		this.roundedButtonClose.Location = new System.Drawing.Point(252, 10);
		this.roundedButtonClose.Margin = new System.Windows.Forms.Padding(5, 10, 0, 0);
		this.roundedButtonClose.Name = "roundedButtonClose";
		this.roundedButtonClose.Pressed = false;
		this.roundedButtonClose.RightSideRounded = true;
		this.roundedButtonClose.RoundedCorners = true;
		this.roundedButtonClose.ShowBorder = false;
		this.roundedButtonClose.Size = new System.Drawing.Size(243, 70);
		this.roundedButtonClose.TabIndex = 3;
		this.roundedButtonClose.Text = "Закрыть";
		this.roundedButtonClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonPrint.BackColor = System.Drawing.Color.FromArgb(192, 192, 255);
		this.roundedButtonPrint.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButtonPrint.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButtonPrint.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonPrint.BorderColor = System.Drawing.Color.Black;
		this.roundedButtonPrint.CornerRadius = 16;
		this.roundedButtonPrint.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonPrint.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButtonPrint.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonPrint.ForeColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButtonPrint.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButtonPrint.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButtonPrint.HoverEffect = false;
		this.roundedButtonPrint.ImageDisabled = null;
		this.roundedButtonPrint.ImageEnabled = null;
		this.roundedButtonPrint.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonPrint.LeftSideRounded = true;
		this.roundedButtonPrint.LeftTextOffset = 0;
		this.roundedButtonPrint.Location = new System.Drawing.Point(0, 10);
		this.roundedButtonPrint.Margin = new System.Windows.Forms.Padding(0, 10, 5, 0);
		this.roundedButtonPrint.Name = "roundedButtonPrint";
		this.roundedButtonPrint.Pressed = false;
		this.roundedButtonPrint.RightSideRounded = true;
		this.roundedButtonPrint.RoundedCorners = true;
		this.roundedButtonPrint.ShowBorder = false;
		this.roundedButtonPrint.Size = new System.Drawing.Size(242, 70);
		this.roundedButtonPrint.TabIndex = 2;
		this.roundedButtonPrint.Text = "Печать";
		this.roundedButtonPrint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonPrint.Click += new System.EventHandler(this.roundedButtonPrint_Click);
		this.tableLayoutPanelCount.BackColor = System.Drawing.Color.Transparent;
		this.tableLayoutPanelCount.ColumnCount = 3;
		this.tableLayoutPanelCount.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20f));
		this.tableLayoutPanelCount.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60f));
		this.tableLayoutPanelCount.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20f));
		this.tableLayoutPanelCount.Controls.Add(this.roundedButtonPlus, 2, 0);
		this.tableLayoutPanelCount.Controls.Add(this.roundedButtonMinus, 0, 0);
		this.tableLayoutPanelCount.Controls.Add(this.labelCount, 1, 0);
		this.tableLayoutPanelCount.Dock = System.Windows.Forms.DockStyle.Top;
		this.tableLayoutPanelCount.Location = new System.Drawing.Point(10, 10);
		this.tableLayoutPanelCount.Margin = new System.Windows.Forms.Padding(0, 10, 5, 0);
		this.tableLayoutPanelCount.Name = "tableLayoutPanelCount";
		this.tableLayoutPanelCount.RowCount = 1;
		this.tableLayoutPanelCount.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tableLayoutPanelCount.Size = new System.Drawing.Size(495, 60);
		this.tableLayoutPanelCount.TabIndex = 2;
		this.roundedButtonPlus.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButtonPlus.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButtonPlus.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButtonPlus.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonPlus.BorderColor = System.Drawing.Color.Black;
		this.roundedButtonPlus.CornerRadius = 16;
		this.roundedButtonPlus.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonPlus.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButtonPlus.Font = new System.Drawing.Font("Tahoma", 28f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonPlus.ForeColor = System.Drawing.Color.DeepSkyBlue;
		this.roundedButtonPlus.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButtonPlus.ForeColorPressed = System.Drawing.Color.Lime;
		this.roundedButtonPlus.HoverEffect = false;
		this.roundedButtonPlus.ImageDisabled = null;
		this.roundedButtonPlus.ImageEnabled = null;
		this.roundedButtonPlus.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonPlus.LeftSideRounded = false;
		this.roundedButtonPlus.LeftTextOffset = 0;
		this.roundedButtonPlus.Location = new System.Drawing.Point(396, 0);
		this.roundedButtonPlus.Margin = new System.Windows.Forms.Padding(0);
		this.roundedButtonPlus.Name = "roundedButtonPlus";
		this.roundedButtonPlus.Pressed = false;
		this.roundedButtonPlus.RightSideRounded = true;
		this.roundedButtonPlus.RoundedCorners = true;
		this.roundedButtonPlus.ShowBorder = false;
		this.roundedButtonPlus.Size = new System.Drawing.Size(99, 60);
		this.roundedButtonPlus.TabIndex = 27;
		this.roundedButtonPlus.Text = "+";
		this.roundedButtonPlus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonPlus.MouseDown += new System.Windows.Forms.MouseEventHandler(this.roundedButtonPlus_MouseDown);
		this.roundedButtonMinus.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButtonMinus.BackColorHovered = System.Drawing.Color.Silver;
		this.roundedButtonMinus.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButtonMinus.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonMinus.BorderColor = System.Drawing.Color.Black;
		this.roundedButtonMinus.CornerRadius = 16;
		this.roundedButtonMinus.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonMinus.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButtonMinus.Font = new System.Drawing.Font("Tahoma", 28f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonMinus.ForeColor = System.Drawing.Color.DeepSkyBlue;
		this.roundedButtonMinus.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.roundedButtonMinus.ForeColorPressed = System.Drawing.Color.Lime;
		this.roundedButtonMinus.HoverEffect = false;
		this.roundedButtonMinus.ImageDisabled = null;
		this.roundedButtonMinus.ImageEnabled = null;
		this.roundedButtonMinus.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonMinus.LeftSideRounded = true;
		this.roundedButtonMinus.LeftTextOffset = 0;
		this.roundedButtonMinus.Location = new System.Drawing.Point(0, 0);
		this.roundedButtonMinus.Margin = new System.Windows.Forms.Padding(0);
		this.roundedButtonMinus.Name = "roundedButtonMinus";
		this.roundedButtonMinus.Pressed = false;
		this.roundedButtonMinus.RightSideRounded = false;
		this.roundedButtonMinus.RoundedCorners = true;
		this.roundedButtonMinus.ShowBorder = false;
		this.roundedButtonMinus.Size = new System.Drawing.Size(99, 60);
		this.roundedButtonMinus.TabIndex = 2;
		this.roundedButtonMinus.Text = "-";
		this.roundedButtonMinus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonMinus.MouseDown += new System.Windows.Forms.MouseEventHandler(this.roundedButtonMinus_MouseDown);
		this.labelCount.BackColor = System.Drawing.Color.Black;
		this.labelCount.Dock = System.Windows.Forms.DockStyle.Fill;
		this.labelCount.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.labelCount.ForeColor = System.Drawing.Color.White;
		this.labelCount.Location = new System.Drawing.Point(99, 0);
		this.labelCount.Margin = new System.Windows.Forms.Padding(0);
		this.labelCount.Name = "labelCount";
		this.labelCount.Size = new System.Drawing.Size(297, 60);
		this.labelCount.TabIndex = 26;
		this.labelCount.Text = "1";
		this.labelCount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.labelCount.MouseMove += new System.Windows.Forms.MouseEventHandler(this.labelCount_MouseMove);
		this.panelTemplate.BackColor = System.Drawing.Color.Transparent;
		this.panelTemplate.Controls.Add(this.roundedButtonServer);
		this.panelTemplate.Controls.Add(this.roundedButtonLocal);
		this.panelTemplate.Location = new System.Drawing.Point(10, 75);
		this.panelTemplate.Name = "panelTemplate";
		this.panelTemplate.Size = new System.Drawing.Size(495, 40);
		this.panelTemplate.TabIndex = 4;
		this.roundedButtonServer.BackColor = System.Drawing.Color.FromArgb(0, 120, 0);
		this.roundedButtonServer.BackColorHovered = System.Drawing.Color.FromArgb(0, 100, 0);
		this.roundedButtonServer.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.roundedButtonServer.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonServer.BorderColor = System.Drawing.Color.FromArgb(0, 180, 0);
		this.roundedButtonServer.CornerRadius = 8;
		this.roundedButtonServer.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonServer.Dock = System.Windows.Forms.DockStyle.Left;
		this.roundedButtonServer.Font = new System.Drawing.Font("Tahoma", 11f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonServer.ForeColor = System.Drawing.Color.White;
		this.roundedButtonServer.ForeColorHovered = System.Drawing.Color.White;
		this.roundedButtonServer.ForeColorPressed = System.Drawing.Color.White;
		this.roundedButtonServer.HoverEffect = true;
		this.roundedButtonServer.ImageDisabled = null;
		this.roundedButtonServer.ImageEnabled = null;
		this.roundedButtonServer.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonServer.LeftSideRounded = true;
		this.roundedButtonServer.LeftTextOffset = 0;
		this.roundedButtonServer.Location = new System.Drawing.Point(0, 0);
		this.roundedButtonServer.Margin = new System.Windows.Forms.Padding(0);
		this.roundedButtonServer.Name = "roundedButtonServer";
		this.roundedButtonServer.Pressed = true;
		this.roundedButtonServer.RightSideRounded = true;
		this.roundedButtonServer.RoundedCorners = true;
		this.roundedButtonServer.ShowBorder = true;
		this.roundedButtonServer.Size = new System.Drawing.Size(242, 40);
		this.roundedButtonServer.TabIndex = 0;
		this.roundedButtonServer.Text = "Шаблон: Сервер";
		this.roundedButtonServer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonServer.Click += new System.EventHandler(this.roundedButtonServer_Click);
		this.roundedButtonLocal.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
		this.roundedButtonLocal.BackColorHovered = System.Drawing.Color.FromArgb(60, 60, 60);
		this.roundedButtonLocal.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.roundedButtonLocal.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonLocal.BorderColor = System.Drawing.Color.Gray;
		this.roundedButtonLocal.CornerRadius = 8;
		this.roundedButtonLocal.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonLocal.Dock = System.Windows.Forms.DockStyle.Right;
		this.roundedButtonLocal.Font = new System.Drawing.Font("Tahoma", 11f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonLocal.ForeColor = System.Drawing.Color.Silver;
		this.roundedButtonLocal.ForeColorHovered = System.Drawing.Color.White;
		this.roundedButtonLocal.ForeColorPressed = System.Drawing.Color.White;
		this.roundedButtonLocal.HoverEffect = true;
		this.roundedButtonLocal.ImageDisabled = null;
		this.roundedButtonLocal.ImageEnabled = null;
		this.roundedButtonLocal.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonLocal.LeftSideRounded = true;
		this.roundedButtonLocal.LeftTextOffset = 0;
		this.roundedButtonLocal.Location = new System.Drawing.Point(252, 0);
		this.roundedButtonLocal.Margin = new System.Windows.Forms.Padding(0);
		this.roundedButtonLocal.Name = "roundedButtonLocal";
		this.roundedButtonLocal.Pressed = false;
		this.roundedButtonLocal.RightSideRounded = true;
		this.roundedButtonLocal.RoundedCorners = true;
		this.roundedButtonLocal.ShowBorder = true;
		this.roundedButtonLocal.Size = new System.Drawing.Size(243, 40);
		this.roundedButtonLocal.TabIndex = 1;
		this.roundedButtonLocal.Text = "Шаблон: Локальный";
		this.roundedButtonLocal.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonLocal.Click += new System.EventHandler(this.roundedButtonLocal_Click);
		this.roundedPanel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.roundedPanel1.BorderColor = System.Drawing.Color.White;
		this.roundedPanel1.Caption = null;
		this.roundedPanel1.Controls.Add(this.listView1);
		this.roundedPanel1.DrawCaption = false;
		this.roundedPanel1.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanel1.HeaderHeight = 20;
		this.roundedPanel1.LeftBottom = true;
		this.roundedPanel1.LeftTop = true;
		this.roundedPanel1.Location = new System.Drawing.Point(10, 120);
		this.roundedPanel1.Margin = new System.Windows.Forms.Padding(0);
		this.roundedPanel1.Name = "roundedPanel1";
		this.roundedPanel1.Padding = new System.Windows.Forms.Padding(10);
		this.roundedPanel1.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanel1.Radius = 16;
		this.roundedPanel1.RightBottom = true;
		this.roundedPanel1.RightTop = true;
		this.roundedPanel1.SelectCaption = false;
		this.roundedPanel1.ShowHeader = false;
		this.roundedPanel1.Size = new System.Drawing.Size(495, 430);
		this.roundedPanel1.TabIndex = 3;
		this.roundedPanel1.Thickness = 0f;
		this.listView1.BackColor = System.Drawing.Color.Black;
		this.listView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[2] { this.columnHeaderCode, this.columnHeaderDate });
		this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.listView1.Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.listView1.ForeColor = System.Drawing.Color.White;
		this.listView1.FullRowSelect = true;
		this.listView1.HideSelection = false;
		this.listView1.Location = new System.Drawing.Point(10, 10);
		this.listView1.MultiSelect = false;
		this.listView1.Name = "listView1";
		this.listView1.Size = new System.Drawing.Size(475, 410);
		this.listView1.TabIndex = 44;
		this.listView1.TileSize = new System.Drawing.Size(450, 60);
		this.listView1.UseCompatibleStateImageBehavior = false;
		this.listView1.View = System.Windows.Forms.View.Tile;
		this.listView1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listView1_MouseDoubleClick);
		this.listView1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.listView1_MouseMove);
		this.columnHeaderCode.Text = "Code";
		this.columnHeaderCode.Width = 150;
		this.columnHeaderDate.Text = "Date";
		this.columnHeaderDate.Width = 100;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(515, 650);
		base.Controls.Add(this.roundedPanel1);
		base.Controls.Add(this.panelTemplate);
		base.Controls.Add(this.tableLayoutPanelCount);
		base.Controls.Add(this.tableLayoutPanelButtons);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.Name = "PrintRecentDialog";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "PrintRecentDialog";
		base.Load += new System.EventHandler(this.PrintRecentDialog_Load);
		this.tableLayoutPanelButtons.ResumeLayout(false);
		this.tableLayoutPanelCount.ResumeLayout(false);
		this.panelTemplate.ResumeLayout(false);
		this.roundedPanel1.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
