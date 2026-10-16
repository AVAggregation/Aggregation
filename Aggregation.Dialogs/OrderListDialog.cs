using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeviceInterfaces.L3;
using NLog;
using UIKit;

namespace Aggregation.Dialogs;

public class OrderListDialog : Form
{
	private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

	private const int WM_NCLBUTTONDOWN = 161;

	private const int WM_NCACTIVATE = 134;

	private const int HT_CAPTION = 2;

	private bool _flashToggle;

	private Color _bgColor = Color.FromArgb(65, 65, 65);

	private Controller _controller;

	private IContainer components;

	private ImageList orderImageList;

	private RoundedPanel pnlHeader;

	private Label lblDialogTitle;

	private RoundedPanel pnlOrderListContainer;

	private ListView lvOrders;

	private ColumnHeader colOrderName;

	private ColumnHeader colSeries;

	private ColumnHeader colQuantity;

	private ColumnHeader colDocument;

	private TableLayoutPanel tlpFooter;

	private TableLayoutPanel tlpActionButtons;

	private RoundedButton btnLoadOrder;

	private RoundedButton btnClose;

	public Order Selected { get; set; }

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
				btnLoadOrder.BGColor = _bgColor;
				btnClose.BGColor = _bgColor;
				pnlOrderListContainer.BackColor = _bgColor;
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	public OrderListDialog()
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

	public DialogResult ShowOrdersList(Controller controller)
	{
		_controller = controller;
		return ShowDialog();
	}

	private void OnHeaderPanelMouseMove(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(Handle, 161, 2, 0);
		}
	}

	private void OnDialogKeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			DialogResult = DialogResult.Cancel;
		}
	}

	private async void OnDialogLoad(object sender, EventArgs e)
	{
		try
		{
			OrdersPage ordersPage = await _controller.GetOrdersFromL3Async();
			lvOrders.Visible = ordersPage != null;
			if (ordersPage != null)
			{
				PopulateOrdersList(ordersPage);
			}
		}
		catch (Exception value)
		{
			lvOrders.Hide();
			_logger.Error(value);
		}
	}

	private void PopulateOrdersList(OrdersPage ordersPage)
	{
		foreach (Order order in ordersPage.Items)
		{
			ListViewItem listItem = new ListViewItem
			{
				Text = order.Name,
				Tag = order,
				ImageIndex = 0,
				UseItemStyleForSubItems = false
			};
			ListViewItem.ListViewSubItem seriesSubItem = new ListViewItem.ListViewSubItem
			{
				Text = order.Series,
				ForeColor = Color.Gray
			};
			ListViewItem.ListViewSubItem quantitySubItem = new ListViewItem.ListViewSubItem
			{
				Text = $"{order.LayersInPack} x {order.CountInLayer}",
				ForeColor = Color.Gray
			};
			ListViewItem.ListViewSubItem documentSubItem = new ListViewItem.ListViewSubItem
			{
				Text = order.Document,
				ForeColor = Color.Gray
			};
			listItem.SubItems.Add(seriesSubItem);
			listItem.SubItems.Add(quantitySubItem);
			listItem.SubItems.Add(documentSubItem);
			lvOrders.Items.Add(listItem);
		}
	}

	private void OnOrdersListDoubleClick(object sender, MouseEventArgs e)
	{
		SelectOrderAndClose();
	}

	private void OnLoadOrderButtonClick(object sender, EventArgs e)
	{
		SelectOrderAndClose();
	}

	private void SelectOrderAndClose()
	{
		if (lvOrders.SelectedIndices.Count > 0)
		{
			Selected = lvOrders.SelectedItems[0].Tag as Order;
			DialogResult = DialogResult.OK;
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
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Aggregation.Dialogs.OrderListDialog));
		this.orderImageList = new System.Windows.Forms.ImageList(this.components);
		this.tlpFooter = new System.Windows.Forms.TableLayoutPanel();
		this.tlpActionButtons = new System.Windows.Forms.TableLayoutPanel();
		this.btnClose = new UIKit.RoundedButton();
		this.btnLoadOrder = new UIKit.RoundedButton();
		this.pnlOrderListContainer = new UIKit.RoundedPanel();
		this.lvOrders = new System.Windows.Forms.ListView();
		this.colOrderName = new System.Windows.Forms.ColumnHeader();
		this.colSeries = new System.Windows.Forms.ColumnHeader();
		this.colQuantity = new System.Windows.Forms.ColumnHeader();
		this.colDocument = new System.Windows.Forms.ColumnHeader();
		this.pnlHeader = new UIKit.RoundedPanel();
		this.lblDialogTitle = new System.Windows.Forms.Label();
		this.tlpFooter.SuspendLayout();
		this.tlpActionButtons.SuspendLayout();
		this.pnlOrderListContainer.SuspendLayout();
		this.pnlHeader.SuspendLayout();
		base.SuspendLayout();
		this.orderImageList.ImageStream = (System.Windows.Forms.ImageListStreamer)resources.GetObject("orderImageList.ImageStream");
		this.orderImageList.TransparentColor = System.Drawing.Color.Transparent;
		this.orderImageList.Images.SetKeyName(0, "box-add.png");
		this.tlpFooter.ColumnCount = 2;
		this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tlpFooter.Controls.Add(this.tlpActionButtons, 1, 0);
		this.tlpFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.tlpFooter.Location = new System.Drawing.Point(10, 505);
		this.tlpFooter.Margin = new System.Windows.Forms.Padding(0);
		this.tlpFooter.Name = "tlpFooter";
		this.tlpFooter.RowCount = 1;
		this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80f));
		this.tlpFooter.Size = new System.Drawing.Size(947, 80);
		this.tlpFooter.TabIndex = 44;
		this.tlpActionButtons.ColumnCount = 2;
		this.tlpActionButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tlpActionButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50f));
		this.tlpActionButtons.Controls.Add(this.btnClose, 1, 0);
		this.tlpActionButtons.Controls.Add(this.btnLoadOrder, 0, 0);
		this.tlpActionButtons.Dock = System.Windows.Forms.DockStyle.Fill;
		this.tlpActionButtons.Location = new System.Drawing.Point(483, 0);
		this.tlpActionButtons.Margin = new System.Windows.Forms.Padding(10, 0, 0, 0);
		this.tlpActionButtons.Name = "tlpActionButtons";
		this.tlpActionButtons.RowCount = 1;
		this.tlpActionButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tlpActionButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80f));
		this.tlpActionButtons.Size = new System.Drawing.Size(464, 80);
		this.tlpActionButtons.TabIndex = 0;
		this.btnClose.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.btnClose.BackColorHovered = System.Drawing.Color.Silver;
		this.btnClose.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnClose.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.btnClose.BorderColor = System.Drawing.Color.Black;
		this.btnClose.CornerRadius = 16;
		this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btnClose.Dock = System.Windows.Forms.DockStyle.Fill;
		this.btnClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnClose.ForeColor = System.Drawing.Color.White;
		this.btnClose.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnClose.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.btnClose.HoverEffect = false;
		this.btnClose.ImageDisabled = null;
		this.btnClose.ImageEnabled = null;
		this.btnClose.ImageSize = new System.Drawing.Size(0, 0);
		this.btnClose.LeftSideRounded = true;
		this.btnClose.LeftTextOffset = 0;
		this.btnClose.Location = new System.Drawing.Point(237, 10);
		this.btnClose.Margin = new System.Windows.Forms.Padding(5, 10, 0, 0);
		this.btnClose.Name = "btnClose";
		this.btnClose.Pressed = false;
		this.btnClose.RightSideRounded = true;
		this.btnClose.RoundedCorners = true;
		this.btnClose.ShowBorder = false;
		this.btnClose.Size = new System.Drawing.Size(227, 70);
		this.btnClose.TabIndex = 3;
		this.btnClose.Text = "Закрыть";
		this.btnClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnLoadOrder.BackColor = System.Drawing.Color.FromArgb(192, 192, 255);
		this.btnLoadOrder.BackColorHovered = System.Drawing.Color.Silver;
		this.btnLoadOrder.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnLoadOrder.BGColor = System.Drawing.Color.FromArgb(65, 65, 65);
		this.btnLoadOrder.BorderColor = System.Drawing.Color.Black;
		this.btnLoadOrder.CornerRadius = 16;
		this.btnLoadOrder.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnLoadOrder.Dock = System.Windows.Forms.DockStyle.Fill;
		this.btnLoadOrder.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLoadOrder.ForeColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.btnLoadOrder.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnLoadOrder.ForeColorPressed = System.Drawing.Color.FromArgb(128, 255, 128);
		this.btnLoadOrder.HoverEffect = false;
		this.btnLoadOrder.ImageDisabled = null;
		this.btnLoadOrder.ImageEnabled = null;
		this.btnLoadOrder.ImageSize = new System.Drawing.Size(0, 0);
		this.btnLoadOrder.LeftSideRounded = true;
		this.btnLoadOrder.LeftTextOffset = 0;
		this.btnLoadOrder.Location = new System.Drawing.Point(0, 10);
		this.btnLoadOrder.Margin = new System.Windows.Forms.Padding(0, 10, 5, 0);
		this.btnLoadOrder.Name = "btnLoadOrder";
		this.btnLoadOrder.Pressed = false;
		this.btnLoadOrder.RightSideRounded = true;
		this.btnLoadOrder.RoundedCorners = true;
		this.btnLoadOrder.ShowBorder = false;
		this.btnLoadOrder.Size = new System.Drawing.Size(227, 70);
		this.btnLoadOrder.TabIndex = 2;
		this.btnLoadOrder.Text = "Загрузить";
		this.btnLoadOrder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnLoadOrder.Click += new System.EventHandler(this.OnLoadOrderButtonClick);
		this.pnlOrderListContainer.BorderColor = System.Drawing.Color.White;
		this.pnlOrderListContainer.Caption = "Ошибка связи с L3";
		this.pnlOrderListContainer.Controls.Add(this.lvOrders);
		this.pnlOrderListContainer.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pnlOrderListContainer.DrawCaption = true;
		this.pnlOrderListContainer.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.pnlOrderListContainer.ForeColor = System.Drawing.Color.FromArgb(255, 128, 128);
		this.pnlOrderListContainer.HeaderBackColor = System.Drawing.Color.Silver;
		this.pnlOrderListContainer.HeaderHeight = 20;
		this.pnlOrderListContainer.LeftBottom = false;
		this.pnlOrderListContainer.LeftTop = false;
		this.pnlOrderListContainer.Location = new System.Drawing.Point(10, 80);
		this.pnlOrderListContainer.Name = "pnlOrderListContainer";
		this.pnlOrderListContainer.Padding = new System.Windows.Forms.Padding(20, 14, 20, 14);
		this.pnlOrderListContainer.PanelBackColor = System.Drawing.Color.Black;
		this.pnlOrderListContainer.Radius = 20;
		this.pnlOrderListContainer.RightBottom = true;
		this.pnlOrderListContainer.RightTop = false;
		this.pnlOrderListContainer.SelectCaption = false;
		this.pnlOrderListContainer.ShowHeader = false;
		this.pnlOrderListContainer.Size = new System.Drawing.Size(947, 425);
		this.pnlOrderListContainer.TabIndex = 43;
		this.pnlOrderListContainer.Thickness = 0f;
		this.lvOrders.BackColor = System.Drawing.Color.Black;
		this.lvOrders.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.lvOrders.Columns.AddRange(new System.Windows.Forms.ColumnHeader[4] { this.colOrderName, this.colSeries, this.colQuantity, this.colDocument });
		this.lvOrders.Dock = System.Windows.Forms.DockStyle.Fill;
		this.lvOrders.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lvOrders.ForeColor = System.Drawing.Color.White;
		this.lvOrders.FullRowSelect = true;
		this.lvOrders.HideSelection = false;
		this.lvOrders.LargeImageList = this.orderImageList;
		this.lvOrders.Location = new System.Drawing.Point(20, 14);
		this.lvOrders.MultiSelect = false;
		this.lvOrders.Name = "lvOrders";
		this.lvOrders.Size = new System.Drawing.Size(907, 397);
		this.lvOrders.TabIndex = 43;
		this.lvOrders.TileSize = new System.Drawing.Size(860, 120);
		this.lvOrders.UseCompatibleStateImageBehavior = false;
		this.lvOrders.View = System.Windows.Forms.View.Tile;
		this.lvOrders.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.OnOrdersListDoubleClick);
		this.colOrderName.Width = 150;
		this.colSeries.Width = 120;
		this.colQuantity.Width = 100;
		this.colDocument.Width = 100;
		this.pnlHeader.BorderColor = System.Drawing.Color.White;
		this.pnlHeader.Caption = null;
		this.pnlHeader.Controls.Add(this.lblDialogTitle);
		this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
		this.pnlHeader.DrawCaption = false;
		this.pnlHeader.HeaderBackColor = System.Drawing.Color.Silver;
		this.pnlHeader.HeaderHeight = 20;
		this.pnlHeader.LeftBottom = false;
		this.pnlHeader.LeftTop = true;
		this.pnlHeader.Location = new System.Drawing.Point(10, 10);
		this.pnlHeader.Name = "pnlHeader";
		this.pnlHeader.Padding = new System.Windows.Forms.Padding(10);
		this.pnlHeader.PanelBackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.pnlHeader.Radius = 20;
		this.pnlHeader.RightBottom = false;
		this.pnlHeader.RightTop = true;
		this.pnlHeader.SelectCaption = false;
		this.pnlHeader.ShowHeader = false;
		this.pnlHeader.Size = new System.Drawing.Size(947, 70);
		this.pnlHeader.TabIndex = 44;
		this.pnlHeader.Thickness = 0f;
		this.lblDialogTitle.BackColor = System.Drawing.Color.Transparent;
		this.lblDialogTitle.Dock = System.Windows.Forms.DockStyle.Fill;
		this.lblDialogTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 24f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblDialogTitle.ForeColor = System.Drawing.Color.White;
		this.lblDialogTitle.Location = new System.Drawing.Point(10, 10);
		this.lblDialogTitle.Name = "lblDialogTitle";
		this.lblDialogTitle.Size = new System.Drawing.Size(927, 50);
		this.lblDialogTitle.TabIndex = 0;
		this.lblDialogTitle.Text = "Загрузка заказа";
		this.lblDialogTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.lblDialogTitle.MouseMove += new System.Windows.Forms.MouseEventHandler(this.OnHeaderPanelMouseMove);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(65, 65, 65);
		base.ClientSize = new System.Drawing.Size(967, 595);
		base.Controls.Add(this.pnlOrderListContainer);
		base.Controls.Add(this.tlpFooter);
		base.Controls.Add(this.pnlHeader);
		this.DoubleBuffered = true;
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.KeyPreview = true;
		base.Name = "OrderListDialog";
		base.Padding = new System.Windows.Forms.Padding(10);
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "OrderListDialog";
		base.Load += new System.EventHandler(this.OnDialogLoad);
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnDialogKeyDown);
		this.tlpFooter.ResumeLayout(false);
		this.tlpActionButtons.ResumeLayout(false);
		this.pnlOrderListContainer.ResumeLayout(false);
		this.pnlHeader.ResumeLayout(false);
		base.ResumeLayout(false);
	}
}
