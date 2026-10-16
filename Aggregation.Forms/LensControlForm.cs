using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Forms;

public class LensControlForm : Form
{
	private const int WM_NCLBUTTONDOWN = 161;

	private const int HT_CAPTION = 2;

	private const int MinAddress = 0;

	private const int MaxAddress = 65535;

	private const int MinLayer = 1;

	private const int MaxLayer = 20;

	private readonly Controller _controller;

	private int _lastKnownPosition = -1;

	private IContainer components;

	private RoundedPanel rpMainContainer;

	private RoundedPanel rpRow1;

	private RoundedButton btnHome;

	private Label lblFocusPosition;

	private RoundedButton btnInit;

	private RoundedButton btnClose;

	private RoundedPanel rpRow2;

	private Label lblAddress;

	private RoundedPanel rpAddress;

	private TouchTextBox touchTextBoxAddress;

	private RoundedButton btnGoToAddress;

	private Label lblLayer;

	private RoundedPanel rpLayer;

	private TouchTextBox touchTextBoxLayer;

	private RoundedButton btnGoToLayer;

	private RoundedButton btnRefresh;

	private Timer timerBringToFront;

	private Timer timerPositionUpdate;

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	public LensControlForm()
	{
		InitializeComponent();
	}

	public LensControlForm(Controller controller)
	{
		_controller = controller;
		InitializeComponent();
	}

	private void LensControlForm_Load(object sender, EventArgs e)
	{
		UpdateFocusPositionDisplay();
		if (_controller != null)
		{
			int currentPos = _controller.focusCurrentPos();
			if (currentPos >= 0 && currentPos <= 65535)
			{
				touchTextBoxAddress.Text = currentPos.ToString();
			}
		}
		UpdateLayerMaximum();
	}

	private void rpMainContainer_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	private void lblFocusPosition_MouseMove(object sender, MouseEventArgs e)
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
		return oldValue;
	}

	private void btnHome_Click(object sender, EventArgs e)
	{
		_controller?.irisHome();
		_controller?.zoomHome();
		_controller?.focusHome();
		UpdateFocusPositionDisplay();
	}

	private void btnInit_Click(object sender, EventArgs e)
	{
		_controller?.FocusInitWithRetry();
		_controller?.IrisInitWithRetry();
		_controller?.ZoomInitWithRetry();
		UpdateFocusPositionDisplay();
	}

	private void btnGoToAddress_Click(object sender, EventArgs e)
	{
		if (_controller == null)
		{
			return;
		}
		if (!int.TryParse(touchTextBoxAddress.Text, out var address))
		{
			ShowError("Введите корректный адрес.");
			return;
		}
		if (address < 0 || address > 65535)
		{
			ShowError($"Адрес должен быть от {0} до {65535}.");
			return;
		}
		ushort targetAddress = (ushort)address;
		LensOperationResult result = _controller.FocusToAddressWithRetry(targetAddress);
		UpdateFocusPositionDisplay();
		if (!result.Success)
		{
			ShowError($"Ошибка перемещения на адрес, возможно адрес уже достигнут  {targetAddress}:\n{result.ErrorMessage}");
		}
	}

	private void btnGoToLayer_Click(object sender, EventArgs e)
	{
		if (_controller == null)
		{
			return;
		}
		if (!int.TryParse(touchTextBoxLayer.Text, out var targetLayer))
		{
			ShowError("Введите корректный номер слоя.");
			return;
		}
		int maxLayer = GetMaxLayer();
		if (targetLayer < 1 || targetLayer > maxLayer)
		{
			ShowError($"Номер слоя должен быть от {1} до {maxLayer}.");
			return;
		}
		if (_controller.CurrentOrder == null)
		{
			ShowError("Заказ не загружен.\nДля перемещения по слоям необходимо выбрать заказ.");
			return;
		}
		if (_controller.CurrentOrder.LayerHeight <= 0)
		{
			ShowError("Высота слоя не задана в заказе.");
			return;
		}
		LensOperationResult result = _controller.FocusLayerWithRetry(targetLayer);
		UpdateFocusPositionDisplay();
		if (!result.Success)
		{
			ShowError($"Ошибка перемещения на слой, возможно слой уже достигнут {targetLayer}:\n{result.ErrorMessage}");
		}
	}

	private void btnRefresh_Click(object sender, EventArgs e)
	{
		UpdateFocusPositionDisplay();
		UpdateLayerMaximum();
	}

	private void btnClose_Click(object sender, EventArgs e)
	{
		Close();
	}

	private void timerBringToFront_Tick(object sender, EventArgs e)
	{
		BringToFront();
	}

	private void timerPositionUpdate_Tick(object sender, EventArgs e)
	{
		if (_controller != null)
		{
			int currentPos = _controller.focusCurrentPos();
			if (currentPos != _lastKnownPosition)
			{
				_lastKnownPosition = currentPos;
				lblFocusPosition.Text = currentPos.ToString();
			}
		}
	}

	private void UpdateFocusPositionDisplay()
	{
		if (_controller != null)
		{
			int pos = (_lastKnownPosition = _controller.focusCurrentPos());
			lblFocusPosition.Text = pos.ToString();
		}
	}

	private void UpdateLayerMaximum()
	{
	}

	private int GetMaxLayer()
	{
		if (_controller?.CurrentOrder != null && _controller.CurrentOrder.LayersInPack > 0)
		{
			return _controller.CurrentOrder.LayersInPack;
		}
		return 20;
	}

	private void ShowError(string message)
	{
		using ErrorDialog ed = new ErrorDialog();
		ed.Height = 300;
		ed.Width = 500;
		ed.ShowError(message);
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Aggregation.Forms.LensControlForm));
		this.rpMainContainer = new UIKit.RoundedPanel();
		this.rpRow2 = new UIKit.RoundedPanel();
		this.btnRefresh = new UIKit.RoundedButton();
		this.btnGoToLayer = new UIKit.RoundedButton();
		this.rpLayer = new UIKit.RoundedPanel();
		this.touchTextBoxLayer = new UIKit.TouchTextBox();
		this.lblLayer = new System.Windows.Forms.Label();
		this.btnGoToAddress = new UIKit.RoundedButton();
		this.rpAddress = new UIKit.RoundedPanel();
		this.touchTextBoxAddress = new UIKit.TouchTextBox();
		this.lblAddress = new System.Windows.Forms.Label();
		this.rpRow1 = new UIKit.RoundedPanel();
		this.btnClose = new UIKit.RoundedButton();
		this.btnInit = new UIKit.RoundedButton();
		this.lblFocusPosition = new System.Windows.Forms.Label();
		this.btnHome = new UIKit.RoundedButton();
		this.timerBringToFront = new System.Windows.Forms.Timer(this.components);
		this.timerPositionUpdate = new System.Windows.Forms.Timer(this.components);
		this.rpMainContainer.SuspendLayout();
		this.rpRow2.SuspendLayout();
		this.rpLayer.SuspendLayout();
		this.rpAddress.SuspendLayout();
		this.rpRow1.SuspendLayout();
		base.SuspendLayout();
		this.rpMainContainer.BackColor = System.Drawing.Color.White;
		this.rpMainContainer.BorderColor = System.Drawing.Color.White;
		this.rpMainContainer.Caption = null;
		this.rpMainContainer.Controls.Add(this.rpRow2);
		this.rpMainContainer.Controls.Add(this.rpRow1);
		this.rpMainContainer.Dock = System.Windows.Forms.DockStyle.Fill;
		this.rpMainContainer.DrawCaption = false;
		this.rpMainContainer.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpMainContainer.HeaderHeight = 20;
		this.rpMainContainer.LeftBottom = true;
		this.rpMainContainer.LeftTop = true;
		this.rpMainContainer.Location = new System.Drawing.Point(10, 10);
		this.rpMainContainer.Name = "rpMainContainer";
		this.rpMainContainer.Padding = new System.Windows.Forms.Padding(10);
		this.rpMainContainer.PanelBackColor = System.Drawing.Color.LightGray;
		this.rpMainContainer.Radius = 24;
		this.rpMainContainer.RightBottom = true;
		this.rpMainContainer.RightTop = true;
		this.rpMainContainer.SelectCaption = false;
		this.rpMainContainer.ShowHeader = false;
		this.rpMainContainer.Size = new System.Drawing.Size(583, 196);
		this.rpMainContainer.TabIndex = 0;
		this.rpMainContainer.Thickness = 0f;
		this.rpMainContainer.MouseMove += new System.Windows.Forms.MouseEventHandler(this.rpMainContainer_MouseMove);
		this.rpRow2.BackColor = System.Drawing.Color.Transparent;
		this.rpRow2.BorderColor = System.Drawing.Color.Transparent;
		this.rpRow2.Caption = null;
		this.rpRow2.Controls.Add(this.btnRefresh);
		this.rpRow2.Controls.Add(this.btnGoToLayer);
		this.rpRow2.Controls.Add(this.rpLayer);
		this.rpRow2.Controls.Add(this.lblLayer);
		this.rpRow2.Controls.Add(this.btnGoToAddress);
		this.rpRow2.Controls.Add(this.rpAddress);
		this.rpRow2.Controls.Add(this.lblAddress);
		this.rpRow2.Dock = System.Windows.Forms.DockStyle.Top;
		this.rpRow2.DrawCaption = false;
		this.rpRow2.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpRow2.HeaderHeight = 20;
		this.rpRow2.LeftBottom = false;
		this.rpRow2.LeftTop = false;
		this.rpRow2.Location = new System.Drawing.Point(10, 80);
		this.rpRow2.Name = "rpRow2";
		this.rpRow2.PanelBackColor = System.Drawing.Color.Transparent;
		this.rpRow2.Radius = 0;
		this.rpRow2.RightBottom = false;
		this.rpRow2.RightTop = false;
		this.rpRow2.SelectCaption = false;
		this.rpRow2.ShowHeader = false;
		this.rpRow2.Size = new System.Drawing.Size(563, 103);
		this.rpRow2.TabIndex = 1;
		this.rpRow2.Thickness = 0f;
		this.rpRow2.MouseMove += new System.Windows.Forms.MouseEventHandler(this.rpMainContainer_MouseMove);
		this.btnRefresh.BackColor = System.Drawing.Color.White;
		this.btnRefresh.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnRefresh.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnRefresh.BGColor = System.Drawing.Color.LightGray;
		this.btnRefresh.BorderColor = System.Drawing.Color.White;
		this.btnRefresh.CornerRadius = 24;
		this.btnRefresh.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnRefresh.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnRefresh.ForeColor = System.Drawing.Color.Black;
		this.btnRefresh.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnRefresh.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnRefresh.HoverEffect = false;
		this.btnRefresh.ImageDisabled = null;
		this.btnRefresh.ImageEnabled = null;
		this.btnRefresh.ImageSize = new System.Drawing.Size(0, 0);
		this.btnRefresh.LeftSideRounded = true;
		this.btnRefresh.LeftTextOffset = 0;
		this.btnRefresh.Location = new System.Drawing.Point(504, 47);
		this.btnRefresh.Name = "btnRefresh";
		this.btnRefresh.Pressed = false;
		this.btnRefresh.RightSideRounded = true;
		this.btnRefresh.RoundedCorners = true;
		this.btnRefresh.ShowBorder = false;
		this.btnRefresh.Size = new System.Drawing.Size(56, 53);
		this.btnRefresh.TabIndex = 6;
		this.btnRefresh.Text = "↻";
		this.btnRefresh.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
		this.btnGoToLayer.BackColor = System.Drawing.Color.White;
		this.btnGoToLayer.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnGoToLayer.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnGoToLayer.BGColor = System.Drawing.Color.LightGray;
		this.btnGoToLayer.BorderColor = System.Drawing.Color.White;
		this.btnGoToLayer.CornerRadius = 24;
		this.btnGoToLayer.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnGoToLayer.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnGoToLayer.ForeColor = System.Drawing.Color.Black;
		this.btnGoToLayer.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnGoToLayer.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnGoToLayer.HoverEffect = false;
		this.btnGoToLayer.ImageDisabled = null;
		this.btnGoToLayer.ImageEnabled = null;
		this.btnGoToLayer.ImageSize = new System.Drawing.Size(0, 0);
		this.btnGoToLayer.LeftSideRounded = true;
		this.btnGoToLayer.LeftTextOffset = 0;
		this.btnGoToLayer.Location = new System.Drawing.Point(347, 50);
		this.btnGoToLayer.Name = "btnGoToLayer";
		this.btnGoToLayer.Pressed = false;
		this.btnGoToLayer.RightSideRounded = true;
		this.btnGoToLayer.RoundedCorners = true;
		this.btnGoToLayer.ShowBorder = false;
		this.btnGoToLayer.Size = new System.Drawing.Size(55, 50);
		this.btnGoToLayer.TabIndex = 5;
		this.btnGoToLayer.Text = "GO";
		this.btnGoToLayer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnGoToLayer.Click += new System.EventHandler(this.btnGoToLayer_Click);
		this.rpLayer.BackColor = System.Drawing.Color.White;
		this.rpLayer.BorderColor = System.Drawing.Color.Silver;
		this.rpLayer.Caption = null;
		this.rpLayer.Controls.Add(this.touchTextBoxLayer);
		this.rpLayer.DrawCaption = false;
		this.rpLayer.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpLayer.HeaderHeight = 20;
		this.rpLayer.LeftBottom = true;
		this.rpLayer.LeftTop = true;
		this.rpLayer.Location = new System.Drawing.Point(271, 50);
		this.rpLayer.Name = "rpLayer";
		this.rpLayer.PanelBackColor = System.Drawing.Color.White;
		this.rpLayer.Radius = 16;
		this.rpLayer.RightBottom = true;
		this.rpLayer.RightTop = true;
		this.rpLayer.SelectCaption = false;
		this.rpLayer.ShowHeader = false;
		this.rpLayer.Size = new System.Drawing.Size(70, 50);
		this.rpLayer.TabIndex = 4;
		this.rpLayer.Thickness = 1f;
		this.touchTextBoxLayer.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.touchTextBoxLayer.BackColor = System.Drawing.Color.White;
		this.touchTextBoxLayer.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBoxLayer.Editable = true;
		this.touchTextBoxLayer.Font = new System.Drawing.Font("Tahoma", 20.25f);
		this.touchTextBoxLayer.ForeColor = System.Drawing.Color.Black;
		this.touchTextBoxLayer.IsNumeric = true;
		this.touchTextBoxLayer.Location = new System.Drawing.Point(5, 9);
		this.touchTextBoxLayer.Name = "touchTextBoxLayer";
		this.touchTextBoxLayer.Size = new System.Drawing.Size(60, 33);
		this.touchTextBoxLayer.TabIndex = 0;
		this.touchTextBoxLayer.Text = "1";
		this.touchTextBoxLayer.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBoxLayer.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox_ShowTouchInput);
		this.lblLayer.BackColor = System.Drawing.Color.Transparent;
		this.lblLayer.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblLayer.ForeColor = System.Drawing.Color.Black;
		this.lblLayer.Location = new System.Drawing.Point(255, 22);
		this.lblLayer.Name = "lblLayer";
		this.lblLayer.Size = new System.Drawing.Size(182, 25);
		this.lblLayer.TabIndex = 3;
		this.lblLayer.Text = "Перемещение на слой";
		this.lblLayer.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnGoToAddress.BackColor = System.Drawing.Color.White;
		this.btnGoToAddress.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnGoToAddress.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnGoToAddress.BGColor = System.Drawing.Color.LightGray;
		this.btnGoToAddress.BorderColor = System.Drawing.Color.White;
		this.btnGoToAddress.CornerRadius = 24;
		this.btnGoToAddress.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnGoToAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnGoToAddress.ForeColor = System.Drawing.Color.Black;
		this.btnGoToAddress.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnGoToAddress.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnGoToAddress.HoverEffect = false;
		this.btnGoToAddress.ImageDisabled = null;
		this.btnGoToAddress.ImageEnabled = null;
		this.btnGoToAddress.ImageSize = new System.Drawing.Size(0, 0);
		this.btnGoToAddress.LeftSideRounded = true;
		this.btnGoToAddress.LeftTextOffset = 0;
		this.btnGoToAddress.Location = new System.Drawing.Point(100, 50);
		this.btnGoToAddress.Name = "btnGoToAddress";
		this.btnGoToAddress.Pressed = false;
		this.btnGoToAddress.RightSideRounded = true;
		this.btnGoToAddress.RoundedCorners = true;
		this.btnGoToAddress.ShowBorder = false;
		this.btnGoToAddress.Size = new System.Drawing.Size(55, 50);
		this.btnGoToAddress.TabIndex = 2;
		this.btnGoToAddress.Text = "GO";
		this.btnGoToAddress.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnGoToAddress.Click += new System.EventHandler(this.btnGoToAddress_Click);
		this.rpAddress.BackColor = System.Drawing.Color.White;
		this.rpAddress.BorderColor = System.Drawing.Color.Silver;
		this.rpAddress.Caption = null;
		this.rpAddress.Controls.Add(this.touchTextBoxAddress);
		this.rpAddress.DrawCaption = false;
		this.rpAddress.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpAddress.HeaderHeight = 20;
		this.rpAddress.LeftBottom = true;
		this.rpAddress.LeftTop = true;
		this.rpAddress.Location = new System.Drawing.Point(4, 50);
		this.rpAddress.Name = "rpAddress";
		this.rpAddress.PanelBackColor = System.Drawing.Color.White;
		this.rpAddress.Radius = 16;
		this.rpAddress.RightBottom = true;
		this.rpAddress.RightTop = true;
		this.rpAddress.SelectCaption = false;
		this.rpAddress.ShowHeader = false;
		this.rpAddress.Size = new System.Drawing.Size(90, 50);
		this.rpAddress.TabIndex = 1;
		this.rpAddress.Thickness = 1f;
		this.touchTextBoxAddress.BackColor = System.Drawing.Color.White;
		this.touchTextBoxAddress.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.touchTextBoxAddress.Editable = true;
		this.touchTextBoxAddress.Font = new System.Drawing.Font("Tahoma", 20.25f);
		this.touchTextBoxAddress.ForeColor = System.Drawing.Color.Black;
		this.touchTextBoxAddress.IsNumeric = true;
		this.touchTextBoxAddress.Location = new System.Drawing.Point(7, 8);
		this.touchTextBoxAddress.Name = "touchTextBoxAddress";
		this.touchTextBoxAddress.Size = new System.Drawing.Size(80, 33);
		this.touchTextBoxAddress.TabIndex = 0;
		this.touchTextBoxAddress.Text = "500";
		this.touchTextBoxAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.touchTextBoxAddress.ShowTouchInput += new UIKit.TouchTextBox.ShowTouchInputHandler(this.touchTextBox_ShowTouchInput);
		this.lblAddress.BackColor = System.Drawing.Color.Transparent;
		this.lblAddress.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblAddress.ForeColor = System.Drawing.Color.Black;
		this.lblAddress.Location = new System.Drawing.Point(3, 22);
		this.lblAddress.Name = "lblAddress";
		this.lblAddress.Size = new System.Drawing.Size(181, 25);
		this.lblAddress.TabIndex = 0;
		this.lblAddress.Text = "Перемещение на адрес";
		this.lblAddress.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.rpRow1.BackColor = System.Drawing.Color.Transparent;
		this.rpRow1.BorderColor = System.Drawing.Color.Transparent;
		this.rpRow1.Caption = null;
		this.rpRow1.Controls.Add(this.btnClose);
		this.rpRow1.Controls.Add(this.btnInit);
		this.rpRow1.Controls.Add(this.lblFocusPosition);
		this.rpRow1.Controls.Add(this.btnHome);
		this.rpRow1.Dock = System.Windows.Forms.DockStyle.Top;
		this.rpRow1.DrawCaption = false;
		this.rpRow1.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpRow1.HeaderHeight = 20;
		this.rpRow1.LeftBottom = false;
		this.rpRow1.LeftTop = false;
		this.rpRow1.Location = new System.Drawing.Point(10, 10);
		this.rpRow1.Name = "rpRow1";
		this.rpRow1.PanelBackColor = System.Drawing.Color.Transparent;
		this.rpRow1.Radius = 0;
		this.rpRow1.RightBottom = false;
		this.rpRow1.RightTop = false;
		this.rpRow1.SelectCaption = false;
		this.rpRow1.ShowHeader = false;
		this.rpRow1.Size = new System.Drawing.Size(563, 70);
		this.rpRow1.TabIndex = 0;
		this.rpRow1.Thickness = 0f;
		this.rpRow1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.rpMainContainer_MouseMove);
		this.btnClose.BackColor = System.Drawing.Color.White;
		this.btnClose.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnClose.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnClose.BGColor = System.Drawing.Color.LightGray;
		this.btnClose.BorderColor = System.Drawing.Color.White;
		this.btnClose.CornerRadius = 24;
		this.btnClose.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnClose.Dock = System.Windows.Forms.DockStyle.Right;
		this.btnClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnClose.ForeColor = System.Drawing.Color.Black;
		this.btnClose.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnClose.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnClose.HoverEffect = false;
		this.btnClose.ImageDisabled = null;
		this.btnClose.ImageEnabled = (System.Drawing.Image)resources.GetObject("btnClose.ImageEnabled");
		this.btnClose.ImageSize = new System.Drawing.Size(50, 50);
		this.btnClose.LeftSideRounded = true;
		this.btnClose.LeftTextOffset = 0;
		this.btnClose.Location = new System.Drawing.Point(493, 0);
		this.btnClose.Name = "btnClose";
		this.btnClose.Pressed = false;
		this.btnClose.RightSideRounded = true;
		this.btnClose.RoundedCorners = true;
		this.btnClose.ShowBorder = false;
		this.btnClose.Size = new System.Drawing.Size(70, 70);
		this.btnClose.TabIndex = 6;
		this.btnClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
		this.btnInit.Anchor = System.Windows.Forms.AnchorStyles.None;
		this.btnInit.BackColor = System.Drawing.Color.White;
		this.btnInit.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnInit.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnInit.BGColor = System.Drawing.Color.LightGray;
		this.btnInit.BorderColor = System.Drawing.Color.White;
		this.btnInit.CornerRadius = 24;
		this.btnInit.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnInit.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnInit.ForeColor = System.Drawing.Color.Black;
		this.btnInit.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnInit.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnInit.HoverEffect = false;
		this.btnInit.ImageDisabled = null;
		this.btnInit.ImageEnabled = (System.Drawing.Image)resources.GetObject("btnInit.ImageEnabled");
		this.btnInit.ImageSize = new System.Drawing.Size(36, 36);
		this.btnInit.LeftSideRounded = true;
		this.btnInit.LeftTextOffset = 0;
		this.btnInit.Location = new System.Drawing.Point(423, 0);
		this.btnInit.Name = "btnInit";
		this.btnInit.Pressed = false;
		this.btnInit.RightSideRounded = true;
		this.btnInit.RoundedCorners = true;
		this.btnInit.ShowBorder = false;
		this.btnInit.Size = new System.Drawing.Size(70, 70);
		this.btnInit.TabIndex = 5;
		this.btnInit.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnInit.Click += new System.EventHandler(this.btnInit_Click);
		this.lblFocusPosition.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblFocusPosition.BackColor = System.Drawing.Color.Transparent;
		this.lblFocusPosition.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblFocusPosition.Location = new System.Drawing.Point(3, 0);
		this.lblFocusPosition.Name = "lblFocusPosition";
		this.lblFocusPosition.Size = new System.Drawing.Size(200, 70);
		this.lblFocusPosition.TabIndex = 4;
		this.lblFocusPosition.Text = "0";
		this.lblFocusPosition.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.lblFocusPosition.MouseMove += new System.Windows.Forms.MouseEventHandler(this.lblFocusPosition_MouseMove);
		this.btnHome.Anchor = System.Windows.Forms.AnchorStyles.Right;
		this.btnHome.BackColor = System.Drawing.Color.White;
		this.btnHome.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnHome.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.btnHome.BGColor = System.Drawing.Color.LightGray;
		this.btnHome.BorderColor = System.Drawing.Color.White;
		this.btnHome.CornerRadius = 24;
		this.btnHome.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnHome.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnHome.ForeColor = System.Drawing.Color.Black;
		this.btnHome.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnHome.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnHome.HoverEffect = false;
		this.btnHome.ImageDisabled = null;
		this.btnHome.ImageEnabled = (System.Drawing.Image)resources.GetObject("btnHome.ImageEnabled");
		this.btnHome.ImageSize = new System.Drawing.Size(32, 32);
		this.btnHome.LeftSideRounded = true;
		this.btnHome.LeftTextOffset = 0;
		this.btnHome.Location = new System.Drawing.Point(352, 0);
		this.btnHome.Name = "btnHome";
		this.btnHome.Pressed = false;
		this.btnHome.RightSideRounded = true;
		this.btnHome.RoundedCorners = true;
		this.btnHome.ShowBorder = false;
		this.btnHome.Size = new System.Drawing.Size(70, 70);
		this.btnHome.TabIndex = 2;
		this.btnHome.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnHome.Click += new System.EventHandler(this.btnHome_Click);
		this.timerBringToFront.Enabled = true;
		this.timerBringToFront.Interval = 500;
		this.timerBringToFront.Tick += new System.EventHandler(this.timerBringToFront_Tick);
		this.timerPositionUpdate.Enabled = true;
		this.timerPositionUpdate.Interval = 250;
		this.timerPositionUpdate.Tick += new System.EventHandler(this.timerPositionUpdate_Tick);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.White;
		base.ClientSize = new System.Drawing.Size(603, 216);
		base.Controls.Add(this.rpMainContainer);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.Name = "LensControlForm";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Управление объективом";
		base.Load += new System.EventHandler(this.LensControlForm_Load);
		this.rpMainContainer.ResumeLayout(false);
		this.rpRow2.ResumeLayout(false);
		this.rpLayer.ResumeLayout(false);
		this.rpLayer.PerformLayout();
		this.rpAddress.ResumeLayout(false);
		this.rpAddress.PerformLayout();
		this.rpRow1.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
