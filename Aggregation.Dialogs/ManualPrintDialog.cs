using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Dialogs;

public class ManualPrintDialog : Form
{
	private readonly Controller _controller;

	private string _verifiedCode;

	private string _loadedTemplate;

	private bool _templateFromServer;

	private bool _useServerTemplate = true;

	private Color _bgColor = Color.Silver;

	private const int ManualCodeLength = 18;

	private const int MaxPrintCount = 10;

	private bool _isProcessing;

	private const string DefaultStatusText = "Введите SSCC код и количество, затем нажмите 'Проверить'.";

	private static readonly Color DefaultStatusColor = Color.Silver;

	private const string ButtonTextVerify = "Проверить";

	private const string ButtonTextPrint = "Печать";

	private IContainer components;

	private TableLayoutPanel tableLayoutPanel1;

	private RoundedButton roundedButtonCancel;

	private RoundedButton roundedButtonAction;

	private RoundedPanel roundedPanel1;

	private RoundedPanel roundedPanelCode;

	private RoundedPanel roundedPanelCount;

	private TouchTextBox touchTextBoxCode;

	private TouchTextBox touchTextBoxCount;

	private Label labelCode;

	private Label labelCount;

	private Label labelStatus;

	private Panel panelTemplate;

	private RoundedButton roundedButtonServer;

	private RoundedButton roundedButtonLocal;

	private Label labelTemplateStatus;

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
				roundedButtonAction.BGColor = _bgColor;
				roundedButtonCancel.BGColor = _bgColor;
				roundedPanel1.BackColor = _bgColor;
			}
		}
	}

	public ManualPrintDialog(Controller controller)
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		InitializeComponent();
		roundedButtonCancel.DialogResult = DialogResult.None;
		InitializeTemplateButtons();
		UpdateActionButtonState();
	}

	private void InitializeTemplateButtons()
	{
		UpdateTemplateButtonVisuals(_useServerTemplate);
	}

	private bool IsReadyToPrint()
	{
		if (!string.IsNullOrWhiteSpace(_verifiedCode))
		{
			return !string.IsNullOrWhiteSpace(_loadedTemplate);
		}
		return false;
	}

	private void UpdateActionButtonState()
	{
		string newText = (IsReadyToPrint() ? "Печать" : "Проверить");
		if (roundedButtonAction.Text != newText)
		{
			roundedButtonAction.Text = newText;
			roundedButtonAction.Refresh();
		}
	}

	private void ManualPrintDialog_KeyDown(object sender, KeyEventArgs e)
	{
		if (_isProcessing)
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			return;
		}
		switch (e.KeyCode)
		{
		case Keys.Return:
			e.Handled = true;
			e.SuppressKeyPress = true;
			roundedButtonAction_Click(roundedButtonAction, EventArgs.Empty);
			break;
		case Keys.Escape:
			e.Handled = true;
			e.SuppressKeyPress = true;
			CloseDialog(DialogResult.Cancel);
			break;
		}
	}

	private void roundedButtonCancel_Click(object sender, EventArgs e)
	{
		if (_isProcessing)
		{
			ShowStatus("Пожалуйста, дождитесь завершения текущей операции.", Color.Orange);
		}
		else
		{
			CloseDialog(DialogResult.Cancel);
		}
	}

	private async void roundedButtonAction_Click(object sender, EventArgs e)
	{
		if (_isProcessing)
		{
			return;
		}
		try
		{
			SetProcessingState(isProcessing: true);
			if (!IsReadyToPrint())
			{
				await VerifyAsync();
			}
			else
			{
				Print();
			}
		}
		catch (Exception ex)
		{
			ShowStatus("Неожиданная ошибка: " + ex.Message, Color.OrangeRed);
			_controller.ErrorMessage($"Unexpected error in Action: {ex}", "DIALOG", "UNEXPECTED_ERR");
			ResetToInitialState();
		}
		finally
		{
			SetProcessingState(isProcessing: false);
		}
	}

	private void touchTextBoxCode_TextChanged(object sender, EventArgs e)
	{
		InvalidateVerification();
		string code = (touchTextBoxCode.Text ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(code))
		{
			ShowStatus("Введите SSCC код и количество, затем нажмите 'Проверить'.");
			UpdateTemplateStatusDisplay();
		}
		else if (!IsValidNumericCode18(code))
		{
			ShowStatus($"Код не подходит. SSCC должен содержать ровно {18} цифр.", Color.OrangeRed);
			labelTemplateStatus.Text = "";
		}
		else
		{
			ShowStatus("Нажмите «Проверить» для валидации кода и загрузки шаблона.");
			UpdateTemplateStatusDisplay();
		}
	}

	private void touchTextBoxCount_TextChanged(object sender, EventArgs e)
	{
		UpdateStatusForCountChange();
	}

	private void roundedButtonServer_Click(object sender, EventArgs e)
	{
		if (!_isProcessing && !_useServerTemplate)
		{
			SetTemplateSource(useServer: true);
		}
	}

	private void roundedButtonLocal_Click(object sender, EventArgs e)
	{
		if (!_isProcessing && _useServerTemplate)
		{
			SetTemplateSource(useServer: false);
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

	private void SetProcessingState(bool isProcessing)
	{
		_isProcessing = isProcessing;
		roundedButtonAction.Enabled = !isProcessing;
		roundedButtonCancel.Enabled = !isProcessing;
		roundedButtonServer.Enabled = !isProcessing;
		roundedButtonLocal.Enabled = !isProcessing;
		touchTextBoxCode.Enabled = !isProcessing;
		touchTextBoxCount.Enabled = !isProcessing;
		Cursor = (isProcessing ? Cursors.WaitCursor : Cursors.Default);
	}

	private void InvalidateVerification()
	{
		_verifiedCode = null;
		_loadedTemplate = null;
		_templateFromServer = false;
		UpdateActionButtonState();
	}

	private void ResetToInitialState()
	{
		InvalidateVerification();
		labelTemplateStatus.Text = "";
		ShowStatus("Введите SSCC код и количество, затем нажмите 'Проверить'.", DefaultStatusColor);
		UpdateTemplateStatusDisplay();
	}

	private void CloseDialog(DialogResult result)
	{
		DialogResult = result;
		Close();
	}

	private void SetTemplateSource(bool useServer)
	{
		bool flag = IsReadyToPrint();
		_useServerTemplate = useServer;
		UpdateTemplateButtonVisuals(useServer);
		_verifiedCode = null;
		_loadedTemplate = null;
		_templateFromServer = false;
		UpdateActionButtonState();
		if (flag)
		{
			ShowStatus("Источник шаблона изменен. Нажмите «Проверить» снова.", Color.Orange);
		}
		UpdateTemplateStatusDisplay();
		UpdateActionButtonState();
	}

	private void UpdateTemplateStatusDisplay()
	{
		if (!_useServerTemplate)
		{
			PreloadLocalTemplateForDisplay();
		}
		else
		{
			labelTemplateStatus.Text = "";
		}
	}

	private void PreloadLocalTemplateForDisplay()
	{
		string template = _controller.GetDefaultTemplate();
		if (string.IsNullOrWhiteSpace(template))
		{
			_loadedTemplate = null;
			labelTemplateStatus.ForeColor = Color.OrangeRed;
			labelTemplateStatus.Text = " Локальный шаблон не найден!";
		}
		else if (!Controller.IsTemplateValid(template))
		{
			_loadedTemplate = null;
			labelTemplateStatus.ForeColor = Color.OrangeRed;
			labelTemplateStatus.Text = " Локальный шаблон не содержит {{CODE}}!";
		}
		else
		{
			_loadedTemplate = template;
			_templateFromServer = false;
			labelTemplateStatus.ForeColor = Color.LightGreen;
			labelTemplateStatus.Text = "Локальный шаблон загружен.";
			UpdateActionButtonState();
		}
	}

	private void UpdateTemplateButtonVisuals(bool serverSelected)
	{
		roundedButtonServer.BackColor = (serverSelected ? Color.FromArgb(0, 120, 0) : Color.FromArgb(45, 45, 45));
		roundedButtonServer.ForeColor = (serverSelected ? Color.White : Color.Silver);
		roundedButtonServer.BorderColor = (serverSelected ? Color.FromArgb(0, 180, 0) : Color.Gray);
		roundedButtonServer.Pressed = serverSelected;
		roundedButtonLocal.BackColor = ((!serverSelected) ? Color.FromArgb(0, 120, 0) : Color.FromArgb(45, 45, 45));
		roundedButtonLocal.ForeColor = ((!serverSelected) ? Color.White : Color.Silver);
		roundedButtonLocal.BorderColor = ((!serverSelected) ? Color.FromArgb(0, 180, 0) : Color.Gray);
		roundedButtonLocal.Pressed = !serverSelected;
	}

	private bool LoadLocalTemplate()
	{
		_loadedTemplate = _controller.GetDefaultTemplate();
		_templateFromServer = false;
		if (string.IsNullOrWhiteSpace(_loadedTemplate))
		{
			labelTemplateStatus.ForeColor = Color.OrangeRed;
			labelTemplateStatus.Text = " Локальный шаблон не найден!";
			return false;
		}
		if (!Controller.IsTemplateValid(_loadedTemplate))
		{
			labelTemplateStatus.ForeColor = Color.OrangeRed;
			labelTemplateStatus.Text = " Локальный шаблон не содержит {{CODE}}!";
			_loadedTemplate = null;
			return false;
		}
		labelTemplateStatus.ForeColor = Color.LightGreen;
		labelTemplateStatus.Text = "Локальный шаблон загружен.";
		return true;
	}

	private async Task<bool> LoadServerTemplateAsync(string code)
	{
		try
		{
			ShowStatus("Загрузка шаблона с сервера...");
			(string, string, bool) templateResult = await _controller.GetTemplateForCodeAsync(code);
			if (string.IsNullOrWhiteSpace(templateResult.Item1))
			{
				return false;
			}
			if (!Controller.IsTemplateValid(templateResult.Item1))
			{
				labelTemplateStatus.ForeColor = Color.OrangeRed;
				labelTemplateStatus.Text = " Серверный шаблон не содержит {{CODE}}!";
				return false;
			}
			_loadedTemplate = templateResult.Item1;
			_templateFromServer = true;
			labelTemplateStatus.ForeColor = Color.LightGreen;
			labelTemplateStatus.Text = "Шаблон: " + (templateResult.Item2 ?? "сервер");
			return true;
		}
		catch (Exception arg)
		{
			_controller.ErrorMessage($"Error loading server template: {arg}", "TEMPLATE", "SERVER_ERR");
			return false;
		}
	}

	private void ShowStatus(string message, Color color)
	{
		labelStatus.ForeColor = color;
		labelStatus.Text = message;
	}

	private void ShowStatus(string message)
	{
		ShowStatus(message, DefaultStatusColor);
	}

	private void UpdateStatusForCountChange()
	{
		bool validCount = int.TryParse(touchTextBoxCount.Text, out var count);
		if (validCount && count > 10)
		{
			ShowStatus($"Максимум {10} копий. Значение будет ограничено.", Color.Orange);
			return;
		}
		if (validCount && count <= 0)
		{
			ShowStatus("Количество должно быть больше 0.", Color.Orange);
			return;
		}
		if (IsReadyToPrint())
		{
			ShowStatus("Код и шаблон готовы. Можно печатать.", Color.LightGreen);
			return;
		}
		string code = (touchTextBoxCode.Text ?? string.Empty).Trim();
		if (!string.IsNullOrWhiteSpace(code) && IsValidNumericCode18(code))
		{
			ShowStatus("Нажмите «Проверить» для валидации кода и загрузки шаблона.");
		}
	}

	private static bool IsValidNumericCode18(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return false;
		}
		code = code.Trim();
		if (code.Length != 18)
		{
			return false;
		}
		string text = code;
		for (int i = 0; i < text.Length; i++)
		{
			if (!char.IsDigit(text[i]))
			{
				return false;
			}
		}
		return true;
	}

	private (int requestedCount, int actualCount) GetCountValues()
	{
		if (!int.TryParse(touchTextBoxCount.Text, out var requested) || requested <= 0)
		{
			requested = 1;
		}
		int actual = Math.Min(requested, 10);
		return (requestedCount: requested, actualCount: actual);
	}

	private async Task VerifyAsync()
	{
		string code = (touchTextBoxCode.Text ?? string.Empty).Trim();
		_verifiedCode = null;
		_loadedTemplate = null;
		_templateFromServer = false;
		UpdateActionButtonState();
		if (string.IsNullOrWhiteSpace(code))
		{
			ShowStatus("Код пустой.", Color.OrangeRed);
			return;
		}
		if (!IsValidNumericCode18(code))
		{
			ShowStatus($"Код должен быть числовым и содержать ровно {18} цифр.", Color.OrangeRed);
			return;
		}
		try
		{
			ShowStatus("Проверка кода...");
			(bool, string) codeResult = await _controller.GetCodeInfo(code);
			if (!codeResult.Item1)
			{
				ShowStatus(string.IsNullOrWhiteSpace(codeResult.Item2) ? "Код не прошел проверку на сервере." : codeResult.Item2, Color.OrangeRed);
				UpdateActionButtonState();
				return;
			}
			_verifiedCode = code;
			bool templateLoaded;
			if (_useServerTemplate)
			{
				templateLoaded = await LoadServerTemplateAsync(code);
				if (!templateLoaded)
				{
					labelTemplateStatus.ForeColor = Color.Orange;
					labelTemplateStatus.Text = " Сервер недоступен. Пробуем локальный...";
					templateLoaded = LoadLocalTemplate();
					if (templateLoaded)
					{
						labelTemplateStatus.ForeColor = Color.Orange;
						labelTemplateStatus.Text = " Используется локальный шаблон (fallback).";
					}
				}
			}
			else
			{
				templateLoaded = LoadLocalTemplate();
			}
			if (!templateLoaded || string.IsNullOrWhiteSpace(_loadedTemplate))
			{
				ShowStatus("Шаблон не найден. Печать невозможна.", Color.OrangeRed);
				_verifiedCode = null;
				UpdateActionButtonState();
			}
			else
			{
				UpdateActionButtonState();
				ShowStatus("Код и шаблон готовы. Можно печатать.", Color.LightGreen);
			}
		}
		catch (Exception ex)
		{
			ShowStatus("Ошибка при проверке: " + ex.Message, Color.OrangeRed);
			_controller.ErrorMessage($"Error in VerifyAsync: {ex}", "DIALOG", "VERIFY_ERR");
			_verifiedCode = null;
			_loadedTemplate = null;
			UpdateActionButtonState();
		}
	}

	private void Print()
	{
		if (!IsReadyToPrint())
		{
			ShowStatus("Сначала выполните проверку.", Color.OrangeRed);
			UpdateActionButtonState();
			return;
		}
		(int requestedCount, int actualCount) countValues = GetCountValues();
		int requestedCount = countValues.requestedCount;
		int actualCount = countValues.actualCount;
		int printed = 0;
		string printErrorMessage = null;
		if (requestedCount > actualCount)
		{
			_controller.InfoMessage($"Print count limited: requested {requestedCount}, printing {actualCount}", "PRINTER", "COUNT_LIMITED");
		}
		string codeForLog = _verifiedCode;
		string source = (_templateFromServer ? "сервер" : "локальный");
		try
		{
			for (int i = 0; i < actualCount; i++)
			{
				if (!_controller.printCode(_verifiedCode, _loadedTemplate, out var error))
				{
					printErrorMessage = (string.IsNullOrWhiteSpace(error) ? "Неизвестная ошибка принтера." : error);
					break;
				}
				printed++;
			}
		}
		catch (Exception ex)
		{
			printErrorMessage = "Исключение: " + ex.Message;
			_controller.ErrorMessage($"Exception during print: {ex}", "PRINTER", "PRINT_EXC");
		}
		DisplayPrintResult(printed, actualCount, requestedCount, source, printErrorMessage, codeForLog);
		InvalidateVerification();
		UpdateTemplateStatusDisplay();
	}

	private void DisplayPrintResult(int printed, int actualCount, int requestedCount, string source, string errorMessage, string codeForLog)
	{
		string countInfo = ((requestedCount > actualCount) ? $"{printed} из {actualCount} (запрошено: {requestedCount}, макс: {10})" : $"{printed} из {actualCount}");
		if (printed == actualCount)
		{
			ShowStatus("Отправлено на печать: " + countInfo + ". (Шаблон: " + source + ")", Color.LightGreen);
			_controller.InfoMessage($"Print SSCC: {codeForLog} ({printed} copies, {source})", "PRINTER", "MANUAL_OK");
		}
		else if (printed > 0)
		{
			ShowStatus(" Частично напечатано: " + countInfo + ". " + errorMessage, Color.Orange);
			_controller.ErrorMessage($"Partial print SSCC: {codeForLog} ({printed}/{actualCount})", "PRINTER", "MANUAL_PARTIAL");
		}
		else
		{
			ShowStatus(" Ошибка печати: " + errorMessage, Color.OrangeRed);
			_controller.ErrorMessage("Print error SSCC: " + codeForLog, "PRINTER", "MANUAL_ERR");
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
		this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
		this.roundedButtonCancel = new UIKit.RoundedButton();
		this.roundedButtonAction = new UIKit.RoundedButton();
		this.roundedPanel1 = new UIKit.RoundedPanel();
		this.labelTemplateStatus = new System.Windows.Forms.Label();
		this.panelTemplate = new System.Windows.Forms.Panel();
		this.roundedButtonServer = new UIKit.RoundedButton();
		this.roundedButtonLocal = new UIKit.RoundedButton();
		this.labelStatus = new System.Windows.Forms.Label();
		this.roundedPanelCount = new UIKit.RoundedPanel();
		this.touchTextBoxCount = new UIKit.TouchTextBox();
		this.roundedPanelCode = new UIKit.RoundedPanel();
		this.touchTextBoxCode = new UIKit.TouchTextBox();
		this.labelCount = new System.Windows.Forms.Label();
		this.labelCode = new System.Windows.Forms.Label();
		this.tableLayoutPanel1.SuspendLayout();
		this.roundedPanel1.SuspendLayout();
		this.panelTemplate.SuspendLayout();
		this.roundedPanelCount.SuspendLayout();
		this.roundedPanelCode.SuspendLayout();
		base.SuspendLayout();
		this.tableLayoutPanel1.ColumnCount = 2;
		this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.Controls.Add(this.roundedButtonCancel, 1, 0);
		this.tableLayoutPanel1.Controls.Add(this.roundedButtonAction, 0, 0);
		this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tableLayoutPanel1.Location = new System.Drawing.Point(10, 451);
		this.tableLayoutPanel1.Name = "tableLayoutPanel1";
		this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
		this.tableLayoutPanel1.RowCount = 1;
		this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tableLayoutPanel1.Size = new System.Drawing.Size(681, 80);
		this.tableLayoutPanel1.TabIndex = 40;
		this.roundedButtonCancel.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButtonCancel.BackColorHovered = System.Drawing.Color.DarkGray;
		this.roundedButtonCancel.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButtonCancel.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonCancel.BorderColor = System.Drawing.Color.Black;
		this.roundedButtonCancel.CornerRadius = 16;
		this.roundedButtonCancel.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonCancel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButtonCancel.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonCancel.ForeColor = System.Drawing.Color.White;
		this.roundedButtonCancel.ForeColorHovered = System.Drawing.Color.Black;
		this.roundedButtonCancel.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButtonCancel.HoverEffect = false;
		this.roundedButtonCancel.ImageDisabled = null;
		this.roundedButtonCancel.ImageEnabled = null;
		this.roundedButtonCancel.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonCancel.LeftSideRounded = true;
		this.roundedButtonCancel.LeftTextOffset = 0;
		this.roundedButtonCancel.Location = new System.Drawing.Point(345, 10);
		this.roundedButtonCancel.Margin = new System.Windows.Forms.Padding(5, 0, 0, 0);
		this.roundedButtonCancel.Name = "roundedButtonCancel";
		this.roundedButtonCancel.Pressed = false;
		this.roundedButtonCancel.RightSideRounded = true;
		this.roundedButtonCancel.RoundedCorners = true;
		this.roundedButtonCancel.ShowBorder = false;
		this.roundedButtonCancel.Size = new System.Drawing.Size(336, 70);
		this.roundedButtonCancel.TabIndex = 3;
		this.roundedButtonCancel.Text = "Отмена";
		this.roundedButtonCancel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonCancel.Click += new System.EventHandler(this.roundedButtonCancel_Click);
		this.roundedButtonAction.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.roundedButtonAction.BackColorHovered = System.Drawing.Color.DarkGray;
		this.roundedButtonAction.BackColorPressed = System.Drawing.Color.DarkGray;
		this.roundedButtonAction.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonAction.BorderColor = System.Drawing.Color.Black;
		this.roundedButtonAction.CornerRadius = 16;
		this.roundedButtonAction.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonAction.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedButtonAction.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonAction.ForeColor = System.Drawing.Color.White;
		this.roundedButtonAction.ForeColorHovered = System.Drawing.Color.Black;
		this.roundedButtonAction.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.roundedButtonAction.HoverEffect = false;
		this.roundedButtonAction.ImageDisabled = null;
		this.roundedButtonAction.ImageEnabled = null;
		this.roundedButtonAction.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonAction.LeftSideRounded = true;
		this.roundedButtonAction.LeftTextOffset = 0;
		this.roundedButtonAction.Location = new System.Drawing.Point(0, 10);
		this.roundedButtonAction.Margin = new System.Windows.Forms.Padding(0, 0, 5, 0);
		this.roundedButtonAction.Name = "roundedButtonAction";
		this.roundedButtonAction.Pressed = false;
		this.roundedButtonAction.RightSideRounded = true;
		this.roundedButtonAction.RoundedCorners = true;
		this.roundedButtonAction.ShowBorder = false;
		this.roundedButtonAction.Size = new System.Drawing.Size(335, 70);
		this.roundedButtonAction.TabIndex = 2;
		this.roundedButtonAction.Text = "Проверить";
		this.roundedButtonAction.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonAction.Click += new System.EventHandler(this.roundedButtonAction_Click);
		this.roundedPanel1.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedPanel1.BorderColor = System.Drawing.Color.White;
		this.roundedPanel1.Caption = null;
		this.roundedPanel1.Controls.Add(this.labelTemplateStatus);
		this.roundedPanel1.Controls.Add(this.panelTemplate);
		this.roundedPanel1.Controls.Add(this.labelStatus);
		this.roundedPanel1.Controls.Add(this.roundedPanelCount);
		this.roundedPanel1.Controls.Add(this.roundedPanelCode);
		this.roundedPanel1.Controls.Add(this.labelCount);
		this.roundedPanel1.Controls.Add(this.labelCode);
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
		this.roundedPanel1.Size = new System.Drawing.Size(681, 441);
		this.roundedPanel1.TabIndex = 41;
		this.roundedPanel1.Thickness = 0f;
		this.labelTemplateStatus.BackColor = System.Drawing.Color.Transparent;
		this.labelTemplateStatus.Font = new System.Drawing.Font("Tahoma", 14f);
		this.labelTemplateStatus.ForeColor = System.Drawing.Color.Gray;
		this.labelTemplateStatus.Location = new System.Drawing.Point(20, 185);
		this.labelTemplateStatus.Name = "labelTemplateStatus";
		this.labelTemplateStatus.Size = new System.Drawing.Size(635, 25);
		this.labelTemplateStatus.TabIndex = 58;
		this.panelTemplate.BackColor = System.Drawing.Color.Transparent;
		this.panelTemplate.Controls.Add(this.roundedButtonServer);
		this.panelTemplate.Controls.Add(this.roundedButtonLocal);
		this.panelTemplate.Location = new System.Drawing.Point(399, 80);
		this.panelTemplate.Name = "panelTemplate";
		this.panelTemplate.Size = new System.Drawing.Size(256, 60);
		this.panelTemplate.TabIndex = 57;
		this.roundedButtonServer.BackColor = System.Drawing.Color.FromArgb(0, 120, 0);
		this.roundedButtonServer.BackColorHovered = System.Drawing.Color.FromArgb(0, 100, 0);
		this.roundedButtonServer.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.roundedButtonServer.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonServer.BorderColor = System.Drawing.Color.FromArgb(0, 180, 0);
		this.roundedButtonServer.CornerRadius = 12;
		this.roundedButtonServer.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonServer.Dock = System.Windows.Forms.DockStyle.Left;
		this.roundedButtonServer.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
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
		this.roundedButtonServer.Size = new System.Drawing.Size(120, 60);
		this.roundedButtonServer.TabIndex = 1;
		this.roundedButtonServer.Text = "Шаблон Сервер";
		this.roundedButtonServer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonServer.Click += new System.EventHandler(this.roundedButtonServer_Click);
		this.roundedButtonLocal.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
		this.roundedButtonLocal.BackColorHovered = System.Drawing.Color.FromArgb(60, 60, 60);
		this.roundedButtonLocal.BackColorPressed = System.Drawing.Color.FromArgb(0, 150, 0);
		this.roundedButtonLocal.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.roundedButtonLocal.BorderColor = System.Drawing.Color.Gray;
		this.roundedButtonLocal.CornerRadius = 12;
		this.roundedButtonLocal.DialogResult = System.Windows.Forms.DialogResult.None;
		this.roundedButtonLocal.Dock = System.Windows.Forms.DockStyle.Right;
		this.roundedButtonLocal.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.roundedButtonLocal.ForeColor = System.Drawing.Color.Silver;
		this.roundedButtonLocal.ForeColorHovered = System.Drawing.Color.White;
		this.roundedButtonLocal.ForeColorPressed = System.Drawing.Color.White;
		this.roundedButtonLocal.HoverEffect = true;
		this.roundedButtonLocal.ImageDisabled = null;
		this.roundedButtonLocal.ImageEnabled = null;
		this.roundedButtonLocal.ImageSize = new System.Drawing.Size(0, 0);
		this.roundedButtonLocal.LeftSideRounded = true;
		this.roundedButtonLocal.LeftTextOffset = 0;
		this.roundedButtonLocal.Location = new System.Drawing.Point(126, 0);
		this.roundedButtonLocal.Margin = new System.Windows.Forms.Padding(0);
		this.roundedButtonLocal.Name = "roundedButtonLocal";
		this.roundedButtonLocal.Pressed = false;
		this.roundedButtonLocal.RightSideRounded = true;
		this.roundedButtonLocal.RoundedCorners = true;
		this.roundedButtonLocal.ShowBorder = true;
		this.roundedButtonLocal.Size = new System.Drawing.Size(130, 60);
		this.roundedButtonLocal.TabIndex = 2;
		this.roundedButtonLocal.Text = "Шаблон Локальный";
		this.roundedButtonLocal.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.roundedButtonLocal.Click += new System.EventHandler(this.roundedButtonLocal_Click);
		this.labelStatus.BackColor = System.Drawing.Color.Transparent;
		this.labelStatus.Font = new System.Drawing.Font("Tahoma", 14f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.labelStatus.ForeColor = System.Drawing.Color.Silver;
		this.labelStatus.Location = new System.Drawing.Point(21, 220);
		this.labelStatus.Name = "labelStatus";
		this.labelStatus.Size = new System.Drawing.Size(635, 120);
		this.labelStatus.TabIndex = 56;
		this.labelStatus.Text = "Введите SSCC код и количество копий, затем нажмите 'Проверить'.";
		this.roundedPanelCount.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.roundedPanelCount.BorderColor = System.Drawing.Color.White;
		this.roundedPanelCount.Caption = null;
		this.roundedPanelCount.Controls.Add(this.touchTextBoxCount);
		this.roundedPanelCount.DrawCaption = false;
		this.roundedPanelCount.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelCount.HeaderHeight = 20;
		this.roundedPanelCount.LeftBottom = true;
		this.roundedPanelCount.LeftTop = true;
		this.roundedPanelCount.Location = new System.Drawing.Point(140, 80);
		this.roundedPanelCount.Name = "roundedPanelCount";
		this.roundedPanelCount.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanelCount.Radius = 20;
		this.roundedPanelCount.RightBottom = true;
		this.roundedPanelCount.RightTop = true;
		this.roundedPanelCount.SelectCaption = false;
		this.roundedPanelCount.ShowHeader = false;
		this.roundedPanelCount.Size = new System.Drawing.Size(150, 60);
		this.roundedPanelCount.TabIndex = 55;
		this.roundedPanelCount.Thickness = 0f;
		this.touchTextBoxCount.BackColor = System.Drawing.Color.Black;
		this.touchTextBoxCount.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBoxCount.Editable = true;
		this.touchTextBoxCount.Font = new System.Drawing.Font("Tahoma", 20f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.touchTextBoxCount.ForeColor = System.Drawing.Color.White;
		this.touchTextBoxCount.IsNumeric = true;
		this.touchTextBoxCount.Location = new System.Drawing.Point(4, 8);
		this.touchTextBoxCount.Name = "touchTextBoxCount";
		this.touchTextBoxCount.Size = new System.Drawing.Size(140, 33);
		this.touchTextBoxCount.TabIndex = 51;
		this.touchTextBoxCount.Text = "1";
		this.touchTextBoxCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBoxCount.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox_ShowTouchInput);
		this.touchTextBoxCount.TextChanged += new System.EventHandler(this.touchTextBoxCount_TextChanged);
		this.roundedPanelCode.BackColor = System.Drawing.Color.FromArgb(35, 35, 35);
		this.roundedPanelCode.BorderColor = System.Drawing.Color.White;
		this.roundedPanelCode.Caption = null;
		this.roundedPanelCode.Controls.Add(this.touchTextBoxCode);
		this.roundedPanelCode.DrawCaption = false;
		this.roundedPanelCode.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelCode.HeaderHeight = 20;
		this.roundedPanelCode.LeftBottom = true;
		this.roundedPanelCode.LeftTop = true;
		this.roundedPanelCode.Location = new System.Drawing.Point(140, 15);
		this.roundedPanelCode.Name = "roundedPanelCode";
		this.roundedPanelCode.PanelBackColor = System.Drawing.Color.Black;
		this.roundedPanelCode.Radius = 20;
		this.roundedPanelCode.RightBottom = true;
		this.roundedPanelCode.RightTop = true;
		this.roundedPanelCode.SelectCaption = false;
		this.roundedPanelCode.ShowHeader = false;
		this.roundedPanelCode.Size = new System.Drawing.Size(515, 60);
		this.roundedPanelCode.TabIndex = 54;
		this.roundedPanelCode.Thickness = 0f;
		this.touchTextBoxCode.BackColor = System.Drawing.Color.Black;
		this.touchTextBoxCode.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBoxCode.Editable = true;
		this.touchTextBoxCode.Font = new System.Drawing.Font("Tahoma", 20f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.touchTextBoxCode.ForeColor = System.Drawing.Color.White;
		this.touchTextBoxCode.IsNumeric = false;
		this.touchTextBoxCode.Location = new System.Drawing.Point(4, 8);
		this.touchTextBoxCode.Name = "touchTextBoxCode";
		this.touchTextBoxCode.Size = new System.Drawing.Size(505, 33);
		this.touchTextBoxCode.TabIndex = 50;
		this.touchTextBoxCode.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBoxCode.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox_ShowTouchInput);
		this.touchTextBoxCode.TextChanged += new System.EventHandler(this.touchTextBoxCode_TextChanged);
		this.labelCount.AutoSize = true;
		this.labelCount.BackColor = System.Drawing.Color.Transparent;
		this.labelCount.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.labelCount.ForeColor = System.Drawing.Color.White;
		this.labelCount.Location = new System.Drawing.Point(20, 90);
		this.labelCount.Name = "labelCount";
		this.labelCount.Size = new System.Drawing.Size(88, 29);
		this.labelCount.TabIndex = 53;
		this.labelCount.Text = "Кол-во";
		this.labelCode.AutoSize = true;
		this.labelCode.BackColor = System.Drawing.Color.Transparent;
		this.labelCode.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.labelCode.ForeColor = System.Drawing.Color.White;
		this.labelCode.Location = new System.Drawing.Point(20, 25);
		this.labelCode.Name = "labelCode";
		this.labelCode.Size = new System.Drawing.Size(53, 29);
		this.labelCode.TabIndex = 51;
		this.labelCode.Text = "Код";
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(701, 541);
		base.Controls.Add(this.roundedPanel1);
		base.Controls.Add(this.tableLayoutPanel1);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "ManualPrintDialog";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "ManualPrintDialog";
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ManualPrintDialog_KeyDown);
		this.tableLayoutPanel1.ResumeLayout(false);
		this.roundedPanel1.ResumeLayout(false);
		this.roundedPanel1.PerformLayout();
		this.panelTemplate.ResumeLayout(false);
		this.roundedPanelCount.ResumeLayout(false);
		this.roundedPanelCount.PerformLayout();
		this.roundedPanelCode.ResumeLayout(false);
		this.roundedPanelCode.PerformLayout();
		base.ResumeLayout(false);
	}
}
