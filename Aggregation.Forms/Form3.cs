using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Aggregation.Dialogs;
using Aggregation.Exceptions;
using DeviceInterfaces.L3;
using DeviceInterfaces.Module;
using DeviceInterfaces.Recognizer;
using Manina.Windows.Forms;
using NLog;
using UIKit;
using UIKit.Dialogs;

namespace Aggregation.Forms;

public class Form3 : Form
{
	private enum AppState
	{
		LoggedOut,
		ReadyNoOrder,
		WaitingNewBox,
		Filling,
		Finalizing,
		WaitingLayerRepeat,
		ErrorNeedNewBox
	}

	private sealed class AppModel
	{
		private int _state;

		public volatile bool IncompleteBox;

		public volatile int BoxNumber = 1;

		public int Layer = 1;

		public int LayersReady;

		public int CodesFoundInLayer;

		public bool AutoRunning;

		public Dictionary<int, Dictionary<string, RecognitionResult>> ResultsByLayer = new Dictionary<int, Dictionary<string, RecognitionResult>>();

		public Dictionary<string, int> FirstSeenLayerByCode = new Dictionary<string, int>(StringComparer.Ordinal);

		private HashSet<string> _previousBoxCodes = new HashSet<string>(StringComparer.Ordinal);

		public AppState State
		{
			get
			{
				return (AppState)Volatile.Read(ref _state);
			}
			set
			{
				Volatile.Write(ref _state, (int)value);
			}
		}

		public bool IsFromPreviousBox(string code)
		{
			if (!string.IsNullOrEmpty(code))
			{
				return _previousBoxCodes.Contains(code);
			}
			return false;
		}

		public void ResetBox(bool preserveCodesForNextBox = false)
		{
			if (preserveCodesForNextBox && ResultsByLayer.TryGetValue(Layer, out var lastLayerDict))
			{
				_previousBoxCodes = new HashSet<string>(lastLayerDict.Keys.ToArray(), StringComparer.Ordinal);
			}
			else
			{
				_previousBoxCodes = new HashSet<string>(StringComparer.Ordinal);
			}
			IncompleteBox = false;
			Layer = 1;
			LayersReady = 0;
			CodesFoundInLayer = 0;
			ResultsByLayer.Clear();
			ResultsByLayer[1] = new Dictionary<string, RecognitionResult>(StringComparer.Ordinal);
			FirstSeenLayerByCode.Clear();
		}
	}

	private sealed class InitializingDialog : Form
	{
		public InitializingDialog()
		{
			InitializingDialog initializingDialog = this;
			Text = "";
			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.CenterScreen;
			ShowInTaskbar = false;
			TopMost = true;
			BackColor = Color.FromArgb(65, 65, 65);
			Width = 520;
			Height = 180;
			Label lbl = new Label();
			lbl.AutoSize = true;
			lbl.Location = new Point(150, 50);
			lbl.ForeColor = Color.White;
			lbl.Text = "Инициализация";
			lbl.Font = new Font(FontFamily.GenericSansSerif, 24f, FontStyle.Bold);
			Controls.Add(lbl);
			Label lbl2 = new Label();
			lbl2.AutoSize = true;
			lbl2.Location = new Point(150, 100);
			lbl2.ForeColor = Color.Gainsboro;
			lbl2.Text = "Подождите…";
			lbl2.Font = new Font(FontFamily.GenericSansSerif, 24f, FontStyle.Regular);
			Controls.Add(lbl2);
			Shown += (object s, EventArgs e) =>
			{
				try
				{
					lbl.Left = Math.Max(0, (initializingDialog.ClientSize.Width - lbl.Width) / 2);
					lbl.Top = 50;
					lbl2.Left = Math.Max(0, (initializingDialog.ClientSize.Width - lbl2.Width) / 2);
					lbl2.Top = lbl.Bottom + 10;
				}
				catch
				{
				}
			};
		}
	}

	private sealed class CodeOverlay
	{
		public RecognitionResult Result { get; set; }

		public CodeOverlayType Type { get; set; }

		public int FirstSeenLayer { get; set; }

		public string IgnoreReason { get; set; }
	}

	private enum CodeOverlayType
	{
		Normal,
		ServerBad,
		IgnoredPreviousBox,
		IgnoredOtherLayer
	}

	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	private readonly object _lifecycleLock = new object();

	private volatile bool _isDisposing;

	private Controller _controller;

	private readonly AppModel _m = new AppModel
	{
		State = AppState.LoggedOut
	};

	private readonly SemaphoreSlim _frameGate = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim _newBoxGate = new SemaphoreSlim(1, 1);

	private volatile CancellationTokenSource _cts;

	private Image _latestFrame;

	private const int LogsPageSize = 30;

	private readonly SemaphoreSlim _logsGate = new SemaphoreSlim(1, 1);

	private int _logsCurrentPage = 1;

	private int _logsMaxPage = 1;

	private readonly Brush _codeRegionBrush = new SolidBrush(Color.FromArgb(140, 0, 200, 0));

	private readonly Brush _codeRegionBrushWarning = new SolidBrush(Color.FromArgb(140, 255, 170, 0));

	private readonly Brush _ignoredCodeBrush = new SolidBrush(Color.FromArgb(200, 255, 20, 147));

	private readonly object _overlayLock = new object();

	private const int OverlayTtlFrames = 3;

	private readonly Dictionary<string, int> _overlayTtlByCode = new Dictionary<string, int>(StringComparer.Ordinal);

	private HashSet<string> _seenCodesInLastFrame = new HashSet<string>(StringComparer.Ordinal);

	private const string ServerBadCodesPrefix = "Код не найден (агрегирован ранее), имеет неверный статус, или принадлежит другому плану производства:";

	private readonly Brush _serverBadBrush = new SolidBrush(Color.FromArgb(170, 120, 0, 20));

	private readonly HashSet<string> _serverBadMarkedCodes = new HashSet<string>(StringComparer.Ordinal);

	private volatile bool _hardwareOk = true;

	private volatile string _hardwareStatusText;

	private readonly Dictionary<string, CodeOverlay> _allOverlays = new Dictionary<string, CodeOverlay>(StringComparer.Ordinal);

	private IContainer components;

	private Panel pnlRight;

	private RoundedPanel rpPagesContainer;

	private RoundedPanel rpCamera;

	private RoundedPanel rpOrderInfo;

	private RoundedPanel rpOrderHeader;

	private RoundedPanel rpOrderSelect;

	private Label lblOrderSelect;

	private Label lblOrderCaption;

	private RoundedButton rbResetErrors;

	private RoundedButton rbNewBox;

	private RoundedButton rbStart;

	private RoundedPanel panelBoxActions;

	private RoundedPanel panelVideoControls;

	private Label labelBoxActions;

	private Label labelVideoControls;

	private RoundedButton rbOperator;

	private RoundedPanel rpClockContainer;

	private UIKit.ProgressBar progressRecognized;

	private Label lblRecognizedCount;

	private Label lblLayer;

	private Label lblBoxNumber;

	private Label lblBoxStatus;

	private Label lblStatusCaption;

	private RoundedButton rbManualMode;

	private RoundedButton rbAutomaticMode;

	private PictureBox pbCamera;

	private System.Windows.Forms.Timer uiClockTimer;

	private RoundedButton rbStop;

	private Panel pnlMain;

	private Panel pnlHeader;

	private Panel pnlPagesHost;

	private PagedControl pagedMain;

	private Page pageMain;

	private Page pageLogs;

	private DataGridView dgvLogs;

	private RoundedPanel rpLogsNav;

	private Button btnLogsClear;

	private Button btnLogsRefresh;

	private Button btnLogsFirst;

	private Label lblLogsPage;

	private Button btnLogsPrev;

	private Button btnLogsLast;

	private Button btnLogsNext;

	private Panel pnlLogsPage;

	private Label lblSeriesValue;

	private Label lblExpirationDateValue;

	private Label lblProductionDateValue;

	private Label lblExpirationDateCaption;

	private Label lblProductionDateCaption;

	private Label lblProductCaption;

	private Label lblProductHeight;

	private Label lblSeriesCaption;

	private Label lblProductValue;

	private Label lblProductHeightValue;

	private Label lblClock;

	private RoundedButton rbIncompleteLayer;

	private DataGridViewTextBoxColumn colId;

	private DataGridViewTextBoxColumn colTime;

	private DataGridViewTextBoxColumn colCode;

	private DataGridViewTextBoxColumn colModule;

	private DataGridViewTextBoxColumn colLevel;

	private DataGridViewTextBoxColumn colMessage;

	private const string AdminLoginName = "admin";

	private bool _isAdmin;

	private string _operatorLogin;

	private RoundedButton btnHelp;

	private RoundedButton btnSettings;

	private RoundedPanel roundedPanelFooter;

	private TableLayoutPanel tlpFooter;

	private RoundedButton btnCloseApp;

	private RoundedPanel tabLogs;

	private RoundedPanel tabMain;

	private RoundedButton btnPrintManual;

	private RoundedButton btnPrintRecent;

	private Panel pnlFooter;

	private RoundedPanel roundedPanelHeader;

	private RoundedPanel roundedPanelRight;

	private RoundedPanel roundedPanelStationID;

	private Label lblStationId;

	private RoundedPanel roundedPanelRepeatPrint;

	private Label labelRepeatPrint;

	private Label labelApplication;

	private bool NeedsFocusCalibration()
	{
		int interval = 0;
		try
		{
			interval = _controller?.FocusCalibrationInterval ?? 0;
		}
		catch
		{
		}
		if (interval <= 0)
		{
			return true;
		}
		return (_m.BoxNumber - 1) % interval == 0;
	}

	private async Task<bool> InitLensForNewBoxAsync()
	{
		bool success = true;
		string errorMessage = null;
		bool needsCalibration = NeedsFocusCalibration();
		await Task.Run(() =>
		{
			try
			{
				if (needsCalibration)
				{
					Logger.Info($"FocusInit: Kalibrierung fällig (Box {_m.BoxNumber})");
					LensOperationResult? lensOperationResult = _controller?.FocusInitWithRetry();
					if (!lensOperationResult.HasValue || !lensOperationResult.GetValueOrDefault().Success)
					{
						Logger.Warn($"FocusInit failed: {lensOperationResult}");
						success = false;
						errorMessage = "FocusInit: " + (lensOperationResult?.ErrorMessage ?? "Unknown error");
					}
				}
				LensOperationResult? lensOperationResult2 = _controller?.FocusLayerWithRetry(1);
				if (!lensOperationResult2.HasValue || !lensOperationResult2.GetValueOrDefault().Success)
				{
					Logger.Warn($"FocusLayer(1) failed: {lensOperationResult2}");
					success = false;
					errorMessage = "FocusLayer(1): " + (lensOperationResult2?.ErrorMessage ?? "Unknown error");
				}
			}
			catch (Exception ex)
			{
				Logger.Warn(ex, "Lens init failed");
				success = false;
				errorMessage = ex.Message;
			}
		}).ConfigureAwait(continueOnCapturedContext: true);
		success = true;
		return success;
	}

	private async void OnFormLoad(object sender, EventArgs e)
	{
		try
		{
			SuspendLayout();
			Opacity = 0.0;
		}
		catch
		{
		}
		using LoginDialog loginDialog = new LoginDialog();
		if (loginDialog.ShowDialog() != DialogResult.OK)
		{
			Close();
			return;
		}
		_operatorLogin = loginDialog.Login;
		_isAdmin = loginDialog.Login == "admin";
		rbOperator.Text = loginDialog.Login;
		ApplyAuthToUi();
		pnlHeader.Show();
		pnlFooter.Show();
		pnlRight.Show();
		pnlMain.Show();
		_controller = new Controller(Application.StartupPath);
		_controller.OnImageReceived += Controller_OnImageReceived;
		_controller.OnCloseRequest += Controller_OnCloseRequest;
		_controller.AdminMode = _isAdmin;
		lblStationId.Text = _controller.EquipmentName;
		_cts = new CancellationTokenSource();
		InitializingDialog initDlg = null;
		try
		{
			initDlg = new InitializingDialog();
			initDlg.Show();
			initDlg.Refresh();
		}
		catch
		{
		}
		LogInfo("Старт на " + Environment.MachineName + ", пользователь " + Environment.UserName + ".");
		try
		{
			if (CheckHardwareInitialized())
			{
				await Task.Run(() =>
				{
					LensOperationResult? lensOperationResult = _controller?.ZoomInitWithRetry();
					if (!lensOperationResult.HasValue || !lensOperationResult.GetValueOrDefault().Success)
					{
						Logger.Warn($"Startup ZoomInit failed: {lensOperationResult}");
					}
					LensOperationResult? lensOperationResult2 = _controller?.IrisInitWithRetry();
					if (!lensOperationResult2.HasValue || !lensOperationResult2.GetValueOrDefault().Success)
					{
						Logger.Warn($"Startup IrisInit failed: {lensOperationResult2}");
					}
					LensOperationResult? lensOperationResult3 = _controller?.FocusInitWithRetry();
					if (!lensOperationResult3.HasValue || !lensOperationResult3.GetValueOrDefault().Success)
					{
						Logger.Warn($"Startup FocusInit failed: {lensOperationResult3}");
					}
					LensOperationResult? lensOperationResult4 = _controller?.ZoomHomeWithRetry();
					if (!lensOperationResult4.HasValue || !lensOperationResult4.GetValueOrDefault().Success)
					{
						Logger.Warn($"Startup ZoomHome failed: {lensOperationResult4}");
					}
					LensOperationResult? lensOperationResult5 = _controller?.IrisHomeWithRetry();
					if (!lensOperationResult5.HasValue || !lensOperationResult5.GetValueOrDefault().Success)
					{
						Logger.Warn($"Startup IrisHome failed: {lensOperationResult5}");
					}
					LensOperationResult? lensOperationResult6 = _controller?.FocusHomeWithRetry();
					if (!lensOperationResult6.HasValue || !lensOperationResult6.GetValueOrDefault().Success)
					{
						Logger.Warn($"Startup FocusHome failed: {lensOperationResult6}");
					}
				}).ConfigureAwait(continueOnCapturedContext: true);
			}
		}
		catch (Exception exception)
		{
			Logger.Warn(exception, "Startup lens initialization failed");
		}
		finally
		{
			try
			{
				initDlg?.Close();
			}
			catch
			{
			}
			try
			{
				initDlg?.Dispose();
			}
			catch
			{
			}
		}
		LogInfo("Инициализация завершена.");
		Transition(AppState.ReadyNoOrder);
		try
		{
			Opacity = 1.0;
			ResumeLayout(performLayout: true);
		}
		catch
		{
		}
	}

	private void OnFormClosing(object sender, FormClosingEventArgs e)
	{
		lock (_lifecycleLock)
		{
			_isDisposing = true;
		}
		try
		{
			_cts?.Cancel();
		}
		catch
		{
		}
		try
		{
			if (_controller != null)
			{
				_controller.OnImageReceived -= Controller_OnImageReceived;
				_controller.OnCloseRequest -= Controller_OnCloseRequest;
				try
				{
					_controller.StopImageReceiving();
				}
				catch
				{
				}
				try
				{
					_controller.GlobalStop();
				}
				catch
				{
				}
				try
				{
					_controller.Free();
				}
				catch
				{
				}
			}
		}
		catch (Exception exception)
		{
			Logger.Warn(exception, "FormClosing cleanup failed");
		}
		try
		{
			_codeRegionBrush?.Dispose();
			_codeRegionBrushWarning?.Dispose();
			_serverBadBrush?.Dispose();
			_ignoredCodeBrush?.Dispose();
		}
		catch
		{
		}
		try
		{
			if (pbCamera?.Image != null)
			{
				pbCamera.Image.Dispose();
				pbCamera.Image = null;
			}
		}
		catch
		{
		}
	}

	private void OnHelpClicked(object sender, EventArgs e)
	{
		UiAsync(() =>
		{
			using TimeOutDialog timeOutDialog = new TimeOutDialog();
			timeOutDialog.Height = 500;
			timeOutDialog.Width = 800;
			timeOutDialog.ShowInfo(_controller.ApplicationInfo ?? "", 10, 1);
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void OnCloseAppClicked(object sender, EventArgs e)
	{
		if (ShowConfirmation("Закрыть приложение?"))
		{
			TryClearFramesDir();
			Close();
		}
	}

	private void OnStartClicked(object sender, EventArgs e)
	{
		if (_m.State != AppState.LoggedOut && CheckHardwareInitialized())
		{
			_m.AutoRunning = true;
			TryStartPreview();
			ApplyUiFromState();
		}
	}

	private void OnStopClicked(object sender, EventArgs e)
	{
		_m.AutoRunning = false;
		TryStopPreview();
		ApplyUiFromState();
	}

	private void OnSingleFrameClicked(object sender, EventArgs e)
	{
		if (_m.State != AppState.LoggedOut && !_m.AutoRunning && _m.State == AppState.Filling)
		{
			CancellationToken token = _cts?.Token ?? CancellationToken.None;
			Task.Run(() => ManualSingleShotAsync(token));
		}
	}

	private void OnRepeatLayerClicked(object sender, EventArgs e)
	{
		if (_m.State != AppState.WaitingLayerRepeat)
		{
			return;
		}
		lock (_overlayLock)
		{
			if (_m.ResultsByLayer.TryGetValue(_m.Layer, out var dict))
			{
				dict?.Clear();
			}
			List<string> keysToRemove = new List<string>();
			foreach (KeyValuePair<string, int> kvp in _m.FirstSeenLayerByCode)
			{
				if (kvp.Value == _m.Layer)
				{
					keysToRemove.Add(kvp.Key);
				}
			}
			foreach (string key in keysToRemove)
			{
				_m.FirstSeenLayerByCode.Remove(key);
			}
			_serverBadMarkedCodes.Clear();
			_overlayTtlByCode.Clear();
			_allOverlays.Clear();
			_m.CodesFoundInLayer = 0;
			_m.IncompleteBox = false;
		}
		ApplyUiFromState();
		Transition(AppState.Filling);
	}

	private void ResetBoxAndOverlays(bool preserveCodesForNextBox = false)
	{
		lock (_overlayLock)
		{
			_m.ResetBox(preserveCodesForNextBox);
			_serverBadMarkedCodes.Clear();
			_overlayTtlByCode.Clear();
			_allOverlays.Clear();
		}
	}

	private void OnResetErrorsClicked(object sender, EventArgs e)
	{
		if (_m.State == AppState.LoggedOut)
		{
			return;
		}
		if (GetCurrentOrder() == null)
		{
			Transition(AppState.ReadyNoOrder);
			return;
		}
		CancellationTokenSource oldCts = _cts;
		_cts = new CancellationTokenSource();
		try
		{
			oldCts?.Cancel();
		}
		catch
		{
		}
		ResetBoxAndOverlays();
		Transition(AppState.WaitingNewBox);
	}

	private async void OnNewBoxClicked(object sender, EventArgs e)
	{
		if (GetCurrentOrder() == null || (_m.State != AppState.WaitingNewBox && _m.State != AppState.ErrorNeedNewBox && _m.State != AppState.WaitingLayerRepeat) || !(await _newBoxGate.WaitAsync(0).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return;
		}
		try
		{
			if (ShowConfirmation("Начать новый короб?"))
			{
				ResetBoxAndOverlays();
				try
				{
					_controller?.NewBox();
				}
				catch
				{
				}
				Transition(AppState.Finalizing);
				if (!(await InitLensForNewBoxAsync()))
				{
					Transition(AppState.ErrorNeedNewBox);
				}
				else
				{
					Transition(AppState.Filling);
				}
			}
		}
		finally
		{
			_newBoxGate.Release();
		}
	}

	private void OnOrderSelectClicked(object sender, EventArgs e)
	{
		if (_m.State == AppState.Filling || _m.State == AppState.Finalizing || _m.State == AppState.WaitingLayerRepeat)
		{
			ShowError("Нельзя менять заказ во время заполнения/финализации/повтора слоя.\nНажмите «СБРОСИТЬ / ОТМЕНА КОРОБА» или «НОВЫЙ КОРОБ».");
			return;
		}
		using OrderListDialog d = new OrderListDialog();
		if (d.ShowOrdersList(_controller) != DialogResult.OK)
		{
			return;
		}
		SetCurrentOrder(d.Selected);
		ResetBoxAndOverlays();
		try
		{
			Order o = GetCurrentOrder();
			if (o != null)
			{
				lblProductHeightValue.Text = o.LayerHeight.ToString();
				lblOrderSelect.Text = o.Document;
				lblProductValue.Text = o.Name;
				lblSeriesValue.Text = o.Series;
				lblProductionDateValue.Text = o.ProductionDate.ToString("dd.MM.yyyy");
				lblExpirationDateValue.Text = o.ExpirationDate.ToString("dd.MM.yyyy");
			}
		}
		catch
		{
		}
		Transition(AppState.WaitingNewBox);
	}

	private void OnManualPrintClicked(object sender, EventArgs e)
	{
		if (IsFilling())
		{
			ShowNotAllowedDuringFilling("Печать");
			return;
		}
		using ManualPrintDialog d = new ManualPrintDialog(_controller);
		d.ShowDialog(this);
	}

	private void OnPrintRecentClicked(object sender, EventArgs e)
	{
		if (IsFilling())
		{
			ShowNotAllowedDuringFilling("ПП SSCC");
		}
		else if (_controller != null)
		{
			using (PrintRecentDialog d = new PrintRecentDialog(_controller))
			{
				d.ShowDialog(this);
			}
		}
	}

	private void OnSettingsClicked(object sender, EventArgs e)
	{
		if (IsFilling())
		{
			ShowNotAllowedDuringFilling("Настройки");
		}
		else if (_controller != null)
		{
			new Settings(_controller).Show(this);
		}
	}

	private void OnUiClockTick(object sender, EventArgs e)
	{
		lblClock.Text = DateTime.Now.ToString("HH:mm  |  MM/dd/yyyy");
	}

	private void OnLogsPageShown(object sender, EventArgs e)
	{
		if (IsFilling())
		{
			ShowNotAllowedDuringFilling("Журнал");
		}
		else if (_controller != null)
		{
			Task.Run(() => LoadLogsAsync(refreshCount: true, _cts.Token));
		}
	}

	private void OnLogsCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
	{
		try
		{
			if (e.RowIndex >= 0)
			{
				DataGridViewRow row = dgvLogs.Rows[e.RowIndex];
				if (string.Equals(row.Cells[colLevel.Index]?.Value?.ToString(), "ERROR", StringComparison.OrdinalIgnoreCase))
				{
					row.DefaultCellStyle.ForeColor = Color.FromArgb(180, 0, 0);
					row.DefaultCellStyle.SelectionForeColor = Color.FromArgb(180, 0, 0);
				}
			}
		}
		catch
		{
		}
	}

	private void OnLogsClearClicked(object sender, EventArgs e)
	{
		if (!_isAdmin || _controller == null)
		{
			return;
		}
		Task.Run(async () =>
		{
			if (ShowConfirmation("Очистить журнал?"))
			{
				try
				{
					_controller.ClearAllLogs();
				}
				catch (Exception exception)
				{
					Logger.Warn(exception, "ClearAllLogs failed");
				}
				_logsCurrentPage = 1;
				await LoadLogsAsync(refreshCount: true, _cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
		});
	}

	private void OnLogsRefreshClicked(object sender, EventArgs e)
	{
		if (_controller == null)
		{
			return;
		}
		Task.Run(async () =>
		{
			await UiAsync(() => btnLogsRefresh.Enabled = false).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				await LoadLogsAsync(refreshCount: true, _cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				await UiAsync(() => btnLogsRefresh.Enabled = true).ConfigureAwait(continueOnCapturedContext: false);
			}
		});
	}

	private void NavigateLogs(int page)
	{
		if (_controller != null)
		{
			_logsCurrentPage = Math.Max(1, Math.Min(page, _logsMaxPage));
			Task.Run(() => LoadLogsAsync(refreshCount: false, _cts.Token));
		}
	}

	private void OnLogsFirstClicked(object sender, EventArgs e)
	{
		NavigateLogs(1);
	}

	private void OnLogsPrevClicked(object sender, EventArgs e)
	{
		NavigateLogs(_logsCurrentPage - 1);
	}

	private void OnLogsNextClicked(object sender, EventArgs e)
	{
		NavigateLogs(_logsCurrentPage + 1);
	}

	private void OnLogsLastClicked(object sender, EventArgs e)
	{
		NavigateLogs(_logsMaxPage);
	}

	private async Task LoadLogsAsync(bool refreshCount, CancellationToken ct)
	{
		if (_isDisposing || ct.IsCancellationRequested || _controller == null || !(await _logsGate.WaitAsync(0, ct).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return;
		}
		try
		{
			if (refreshCount)
			{
				int total;
				try
				{
					total = await _controller.GetLogsCountAsync().ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception exception)
				{
					Logger.Warn(exception, "GetLogsCountAsync failed");
					total = 0;
				}
				_logsMaxPage = Math.Max(1, total / 30 + 1);
				_logsCurrentPage = Math.Max(1, Math.Min(_logsCurrentPage, _logsMaxPage));
			}
			List<LogEvent> logs = null;
			try
			{
				logs = await _controller.GetLogsByPageAsync(_logsCurrentPage).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception exception2)
			{
				Logger.Warn(exception2, "GetLogsByPageAsync failed");
				logs = new List<LogEvent>();
			}
			await UiAsync(() =>
			{
				dgvLogs.Rows.Clear();
				if (logs != null)
				{
					foreach (LogEvent current in logs)
					{
						dgvLogs.Rows.Add(current?.Id.ToString() ?? "", current?.Time ?? "", current?.Code ?? "", current?.Module ?? "", current?.Level ?? "", current?.Text ?? "");
					}
				}
				lblLogsPage.Text = $"{_logsCurrentPage} / {_logsMaxPage}";
			}).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			_logsGate.Release();
		}
	}

	private void OnCameraPaint(object sender, PaintEventArgs e)
	{
		try
		{
			if (pbCamera?.Image == null)
			{
				return;
			}
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			float f = GetScaleFactor(pbCamera);
			float vOffset = ((float)pbCamera.Height - (float)pbCamera.Image.Height * f) / 2f;
			float hOffset = ((float)pbCamera.Width - (float)pbCamera.Image.Width * f) / 2f;
			List<(string, CodeOverlay)> overlaySnapshot;
			HashSet<string> visibleCodes;
			lock (_overlayLock)
			{
				overlaySnapshot = new List<(string, CodeOverlay)>(_allOverlays.Count);
				foreach (KeyValuePair<string, CodeOverlay> kvp in _allOverlays)
				{
					overlaySnapshot.Add((kvp.Key, kvp.Value));
				}
				visibleCodes = new HashSet<string>(_seenCodesInLastFrame, StringComparer.Ordinal);
			}
			foreach (var (code, overlay) in overlaySnapshot)
			{
				if (!visibleCodes.Contains(code))
				{
					continue;
				}
				RecognitionResult rr = overlay.Result;
				if (rr?.Points != null && rr.Points.Count >= 4)
				{
					PointF[] poly = new PointF[4];
					for (int j = 0; j < 4; j++)
					{
						RecognitionResultPosition p = rr.Points[j];
						poly[j] = new PointF(hOffset + p.X * f, vOffset + p.Y * f);
					}
					Brush brush = GetBrushForOverlayType(overlay.Type);
					e.Graphics.FillPolygon(brush, poly);
				}
			}
		}
		catch
		{
		}
	}

	private Brush GetBrushForOverlayType(CodeOverlayType type)
	{
		switch (type)
		{
		case CodeOverlayType.ServerBad:
			return _serverBadBrush;
		case CodeOverlayType.IgnoredPreviousBox:
		case CodeOverlayType.IgnoredOtherLayer:
			return _ignoredCodeBrush;
		default:
			try
			{
				Order o = GetCurrentOrder();
				if (o != null)
				{
					int need = o.CountInLayer;
					if (need > 0 && _m.CodesFoundInLayer >= need)
					{
						return _codeRegionBrush;
					}
				}
			}
			catch
			{
			}
			return _codeRegionBrushWarning;
		}
	}

	private void OnCameraMouseClick(object sender, MouseEventArgs e)
	{
		try
		{
			if (pbCamera?.Image == null)
			{
				return;
			}
			List<(string, CodeOverlay)> overlaySnapshot;
			HashSet<string> visibleCodes;
			lock (_overlayLock)
			{
				overlaySnapshot = new List<(string, CodeOverlay)>(_allOverlays.Count);
				foreach (KeyValuePair<string, CodeOverlay> kvp in _allOverlays)
				{
					overlaySnapshot.Add((kvp.Key, kvp.Value));
				}
				visibleCodes = new HashSet<string>(_seenCodesInLastFrame, StringComparer.Ordinal);
			}
			float f = GetScaleFactor(pbCamera);
			float vOffset = ((float)pbCamera.Height - (float)pbCamera.Image.Height * f) / 2f;
			float hOffset = ((float)pbCamera.Width - (float)pbCamera.Image.Width * f) / 2f;
			PointF click = new PointF((float)e.X - hOffset, (float)e.Y - vOffset);
			foreach (var (code, overlay) in overlaySnapshot)
			{
				if (!visibleCodes.Contains(code))
				{
					continue;
				}
				RecognitionResult rr = overlay.Result;
				if (rr?.Points == null || rr.Points.Count < 4)
				{
					continue;
				}
				using GraphicsPath gp = new GraphicsPath();
				PointF[] poly = new PointF[4];
				for (int i = 0; i < 4; i++)
				{
					RecognitionResultPosition p = rr.Points[i];
					poly[i] = new PointF(p.X * f, p.Y * f);
				}
				gp.AddPolygon(poly);
				if (!gp.IsVisible(click))
				{
					continue;
				}
				using CodeContentsDialog d = new CodeContentsDialog();
				bool isError = overlay.Type != CodeOverlayType.Normal;
				string reason = overlay.IgnoreReason;
				if (isError && !string.IsNullOrEmpty(reason))
				{
					d.ShowCodeWithStatus(code, reason, isError: true);
				}
				else
				{
					d.ShowCode(code);
				}
				break;
			}
		}
		catch (Exception exception)
		{
			Logger.Warn(exception, "OnCameraMouseClick failed");
		}
	}

	private void OnIncompleteBoxClicked(object sender, EventArgs e)
	{
		if (GetCurrentOrder() != null && _m.State == AppState.Filling && HasAnyCodesInBox() && ShowConfirmation("Агрегировать неполный короб?"))
		{
			_m.IncompleteBox = true;
			ApplyUiFromState();
			CancellationToken token = _cts?.Token ?? CancellationToken.None;
			Task.Run(() => TriggerFinalizeAsync(token));
		}
	}

	private void Controller_OnImageReceived(Image image)
	{
		if (image == null)
		{
			return;
		}
		if (_isDisposing)
		{
			image.Dispose();
			return;
		}
		Image cloneForUi = null;
		try
		{
			cloneForUi = (Image)image.Clone();
		}
		catch
		{
		}
		image.Dispose();
		if (cloneForUi != null)
		{
			bool queued = false;
			try
			{
				if (!_isDisposing && IsHandleCreated && !IsDisposed && !Disposing)
				{
					BeginInvoke((Action)(() =>
					{
						if (_isDisposing)
						{
							cloneForUi.Dispose();
							return;
						}
						try
						{
							pbCamera.Image?.Dispose();
							pbCamera.Image = cloneForUi;
							_latestFrame = cloneForUi;
						}
						catch
						{
							cloneForUi.Dispose();
						}
					}));
					queued = true;
				}
			}
			catch (InvalidOperationException)
			{
			}
			if (!queued)
			{
				cloneForUi.Dispose();
			}
		}
		AppState state = _m.State;
		CancellationToken token;
		try
		{
			token = _cts?.Token ?? CancellationToken.None;
		}
		catch (ObjectDisposedException)
		{
			return;
		}
		switch (state)
		{
		case AppState.Filling:
			Task.Run(() => ProcessFrameAsync(token));
			break;
		case AppState.WaitingLayerRepeat:
		case AppState.ErrorNeedNewBox:
			Task.Run(() => ProcessFrameAsync(token, positionUpdateOnly: true));
			break;
		}
	}

	private void Controller_OnCloseRequest(object sender, EventArgs e)
	{
		if (!_isDisposing)
		{
			BeginInvoke((Action)(() =>
			{
				Close();
			}));
		}
	}

	private Order GetCurrentOrder()
	{
		try
		{
			return _controller?.CurrentOrder;
		}
		catch
		{
			return null;
		}
	}

	private void SetCurrentOrder(Order order)
	{
		try
		{
			if (_controller != null)
			{
				_controller.CurrentOrder = order;
			}
		}
		catch
		{
		}
	}

	private async Task ManualSingleShotAsync(CancellationToken ct)
	{
		if (_isDisposing || ct.IsCancellationRequested)
		{
			return;
		}
		try
		{
			if (!(await _frameGate.WaitAsync(0, ct).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return;
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		try
		{
			if (_m.State != AppState.Filling)
			{
				return;
			}
			Image img = null;
			try
			{
				img = _controller?.GetSingleImage();
			}
			catch (Exception exception)
			{
				Logger.Warn(exception, "GetSingleImage failed");
			}
			if (img == null)
			{
				if (!_isDisposing)
				{
					ShowError("Невозможно получить кадр с камеры!\nКамера доступна?");
				}
				return;
			}
			Image cloneForUi = null;
			try
			{
				cloneForUi = (Image)img.Clone();
			}
			catch
			{
			}
			if (cloneForUi != null)
			{
				bool assigned = false;
				await UiAsync(() =>
				{
					try
					{
						pbCamera.Image?.Dispose();
						pbCamera.Image = cloneForUi;
						_latestFrame = cloneForUi;
						assigned = true;
					}
					catch
					{
						cloneForUi.Dispose();
						assigned = true;
					}
				}).ConfigureAwait(continueOnCapturedContext: false);
				if (!assigned)
				{
					try
					{
						cloneForUi.Dispose();
					}
					catch
					{
					}
				}
			}
			await ProcessImageAsync(img, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			_frameGate.Release();
		}
	}

	private bool ShouldCountForAggregation(string code)
	{
		if (_m.FirstSeenLayerByCode.TryGetValue(code, out var firstLayer) && firstLayer != _m.Layer)
		{
			return false;
		}
		if (_m.IsFromPreviousBox(code))
		{
			return false;
		}
		if (_serverBadMarkedCodes.Contains(code))
		{
			return false;
		}
		return true;
	}

	private CodeOverlayType GetOverlayType(string code)
	{
		if (_serverBadMarkedCodes.Contains(code))
		{
			return CodeOverlayType.ServerBad;
		}
		if (_m.IsFromPreviousBox(code))
		{
			return CodeOverlayType.IgnoredPreviousBox;
		}
		if (_m.FirstSeenLayerByCode.TryGetValue(code, out var firstLayer) && firstLayer != _m.Layer)
		{
			return CodeOverlayType.IgnoredOtherLayer;
		}
		return CodeOverlayType.Normal;
	}

	private string GetIgnoreReason(CodeOverlayType type, string code)
	{
		switch (type)
		{
		case CodeOverlayType.ServerBad:
			return "Код отклонен сервером";
		case CodeOverlayType.IgnoredPreviousBox:
			return "Информация. Дупликация, код учитываться не будет потому что принадлежит к последнему слою предыдущего короба";
		case CodeOverlayType.IgnoredOtherLayer:
		{
			if (_m.FirstSeenLayerByCode.TryGetValue(code, out var layer))
			{
				return $"Код был уже отсканирован в {layer} слое";
			}
			return "Информация. Дупликация, код учитываться не будет потому что уже отсканирован в другом слое";
		}
		default:
			return null;
		}
	}

	private int CountNormalCodesInCurrentLayer()
	{
		if (!_m.ResultsByLayer.TryGetValue(_m.Layer, out var dict) || dict == null)
		{
			return 0;
		}
		return dict.Count;
	}

	private void UpdateOverlayVisibility(HashSet<string> seenThisFrame)
	{
		_seenCodesInLastFrame = new HashSet<string>(seenThisFrame, StringComparer.Ordinal);
		foreach (string code in seenThisFrame)
		{
			_overlayTtlByCode[code] = 3;
		}
		foreach (string code2 in _overlayTtlByCode.Keys.ToList())
		{
			if (!seenThisFrame.Contains(code2))
			{
				int ttl = _overlayTtlByCode[code2];
				ttl--;
				if (ttl <= 0)
				{
					_overlayTtlByCode.Remove(code2);
					continue;
				}
				_overlayTtlByCode[code2] = ttl;
				_seenCodesInLastFrame.Add(code2);
			}
		}
	}

	private async Task ProcessImageAsync(Image img, CancellationToken ct, bool positionUpdateOnly = false)
	{
		if (img == null)
		{
			return;
		}
		List<RecognitionResult> rr = null;
		try
		{
			if (_isDisposing || ct.IsCancellationRequested)
			{
				return;
			}
			AppState stateSnapshot = _m.State;
			if (!((!positionUpdateOnly) ? (stateSnapshot == AppState.Filling) : (stateSnapshot == AppState.WaitingLayerRepeat || stateSnapshot == AppState.ErrorNeedNewBox)))
			{
				return;
			}
			rr = await _controller.RecognizeDatamatrixAsync(img).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (RecognizeServiceUnavailableException)
		{
			ShowError("Сервис распознавания недоступен\nРабота будет остановлена\nПроверьте сервис распознавания и перезагрузите приложение");
			_m.AutoRunning = false;
			TryStopPreview();
			Transition(AppState.ErrorNeedNewBox);
			return;
		}
		catch (Exception exception)
		{
			Logger.Error(exception, "ProcessImageAsync recognition failed");
			return;
		}
		finally
		{
			img?.Dispose();
		}
		if (ct.IsCancellationRequested || _isDisposing || rr == null)
		{
			return;
		}
		AppState currentState = _m.State;
		if (!((!positionUpdateOnly) ? (currentState == AppState.Filling) : (currentState == AppState.WaitingLayerRepeat || currentState == AppState.ErrorNeedNewBox)))
		{
			return;
		}
		try
		{
			await ProcessRecognitionResultsAsync(rr, ct, positionUpdateOnly).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception2)
		{
			Logger.Error(exception2, "ProcessImageAsync result processing failed");
		}
	}

	private async Task ProcessRecognitionResultsAsync(List<RecognitionResult> rr, CancellationToken ct, bool positionUpdateOnly)
	{
		HashSet<string> seenThisFrame = new HashSet<string>(StringComparer.Ordinal);
		lock (_overlayLock)
		{
			foreach (RecognitionResult r in rr)
			{
				string code = r?.Data;
				if (string.IsNullOrWhiteSpace(code))
				{
					continue;
				}
				seenThisFrame.Add(code);
				if (positionUpdateOnly)
				{
					if (_allOverlays.TryGetValue(code, out var existing))
					{
						existing.Result = r;
						existing.Type = GetOverlayType(code);
						existing.IgnoreReason = GetIgnoreReason(existing.Type, code);
					}
				}
				else
				{
					ProcessSingleCode(code, r);
				}
			}
			UpdateOverlayVisibility(seenThisFrame);
			if (!positionUpdateOnly)
			{
				int codesInLayerSnapshot = CountNormalCodesInCurrentLayer();
				_m.CodesFoundInLayer = codesInLayerSnapshot;
			}
		}
		await UiAsync(() =>
		{
			ApplyUiFromState();
			try
			{
				pbCamera?.Invalidate();
			}
			catch
			{
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
		if (positionUpdateOnly || ct.IsCancellationRequested || _isDisposing)
		{
			return;
		}
		Order order = GetCurrentOrder();
		if (order != null)
		{
			int countInLayer = order.CountInLayer;
			if (!_m.IncompleteBox && countInLayer > 0 && _m.CodesFoundInLayer > countInLayer)
			{
				LogError($"Превышено количество кодов на слое ({_m.CodesFoundInLayer})");
				ShowError("Превышено количество кодов на слое\n\nНачните новый короб");
				Transition(AppState.ErrorNeedNewBox);
			}
			else if (_m.IncompleteBox || _m.CodesFoundInLayer >= countInLayer)
			{
				await FinalizeLayerOrBoxAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	private void ProcessSingleCode(string code, RecognitionResult r)
	{
		bool flag = ShouldCountForAggregation(code);
		CodeOverlayType overlayType = GetOverlayType(code);
		if (flag)
		{
			int firstLayer;
			Dictionary<string, RecognitionResult> dict2;
			if (!_m.FirstSeenLayerByCode.ContainsKey(code))
			{
				_m.FirstSeenLayerByCode[code] = _m.Layer;
				if (!_m.ResultsByLayer.TryGetValue(_m.Layer, out var dict))
				{
					dict = new Dictionary<string, RecognitionResult>(StringComparer.Ordinal);
					_m.ResultsByLayer[_m.Layer] = dict;
				}
				dict[code] = r;
			}
			else if (_m.FirstSeenLayerByCode.TryGetValue(code, out firstLayer) && firstLayer == _m.Layer && _m.ResultsByLayer.TryGetValue(_m.Layer, out dict2) && dict2 != null)
			{
				dict2[code] = r;
			}
		}
		if (_allOverlays.TryGetValue(code, out var overlay))
		{
			overlay.Result = r;
			overlay.Type = overlayType;
			overlay.IgnoreReason = GetIgnoreReason(overlayType, code);
		}
		else
		{
			_allOverlays[code] = new CodeOverlay
			{
				Result = r,
				Type = overlayType,
				FirstSeenLayer = _m.Layer,
				IgnoreReason = GetIgnoreReason(overlayType, code)
			};
		}
	}

	private async Task ProcessFrameAsync(CancellationToken ct, bool positionUpdateOnly = false)
	{
		if (_isDisposing || ct.IsCancellationRequested)
		{
			return;
		}
		try
		{
			if (!(await _frameGate.WaitAsync(0, ct).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return;
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		try
		{
			AppState state = _m.State;
			if ((!positionUpdateOnly) ? (state == AppState.Filling) : (state == AppState.WaitingLayerRepeat || state == AppState.ErrorNeedNewBox))
			{
				Image img = await UiAsync(() => (_latestFrame != null) ? ((Image)_latestFrame.Clone()) : null).ConfigureAwait(continueOnCapturedContext: false);
				if (img != null)
				{
					await ProcessImageAsync(img, ct, positionUpdateOnly).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		finally
		{
			_frameGate.Release();
		}
	}

	private async Task TriggerFinalizeAsync(CancellationToken ct)
	{
		if (_isDisposing || ct.IsCancellationRequested || _m.State != AppState.Filling)
		{
			return;
		}
		try
		{
			if (!(await _frameGate.WaitAsync(0, ct).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return;
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		try
		{
			await FinalizeLayerOrBoxAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			_frameGate.Release();
		}
	}

	private bool AskRetryOrNewBox(string message)
	{
		return ShowConfirmation(message + "\n\nДа = повторить\nНет = новый короб", 900, 900);
	}

	private bool AskNewBoxOrRepeatLayer(string serverMessage)
	{
		return ShowConfirmation("Ошибка проверки SGTIN после заполнения слоя.\n\"" + serverMessage + "\"\n\nНовый короб или повторить слой?\n\nДа = повторить слой\nНет = новый короб", 900, 900);
	}

	private void MarkBadCodesFromServerMessage(string serverMessage)
	{
		if (string.IsNullOrEmpty(serverMessage))
		{
			return;
		}
		int idx = serverMessage.IndexOf("Код не найден (агрегирован ранее), имеет неверный статус, или принадлежит другому плану производства:", StringComparison.Ordinal);
		if (idx < 0)
		{
			return;
		}
		string tail = serverMessage.Substring(idx + "Код не найден (агрегирован ранее), имеет неверный статус, или принадлежит другому плану производства:".Length).Trim();
		if (tail.Length == 0)
		{
			return;
		}
		HashSet<string> needles4 = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i <= tail.Length - 19; i++)
		{
			bool ok = true;
			for (int k = 0; k < 15; k++)
			{
				if (!char.IsDigit(tail[i + k]))
				{
					ok = false;
					break;
				}
			}
			if (ok)
			{
				int digitsEndIndex = i + 15;
				if (digitsEndIndex + 4 <= tail.Length)
				{
					string suffix4raw = tail.Substring(digitsEndIndex, 4);
					needles4.Add(suffix4raw);
					i = digitsEndIndex - 1;
				}
			}
		}
		if (needles4.Count == 0)
		{
			return;
		}
		lock (_overlayLock)
		{
			foreach (KeyValuePair<int, Dictionary<string, RecognitionResult>> item in _m.ResultsByLayer)
			{
				Dictionary<string, RecognitionResult> dict = item.Value;
				if (dict == null)
				{
					continue;
				}
				foreach (string fullKey in dict.Keys)
				{
					if (string.IsNullOrEmpty(fullKey))
					{
						continue;
					}
					foreach (string needle4 in needles4)
					{
						if (fullKey.Contains(needle4))
						{
							_serverBadMarkedCodes.Add(fullKey);
							if (_allOverlays.TryGetValue(fullKey, out var overlay))
							{
								overlay.Type = CodeOverlayType.ServerBad;
								overlay.IgnoreReason = "Код был отклонен L3 сервером";
							}
							break;
						}
					}
				}
			}
		}
	}

	private async Task FinalizeLayerOrBoxAsync(CancellationToken ct)
	{
		if (_isDisposing || ct.IsCancellationRequested)
		{
			return;
		}
		Order order = GetCurrentOrder();
		if (order == null)
		{
			return;
		}
		int layersInPack = order.LayersInPack;
		Transition(AppState.Finalizing);
		if (!HasAnyCodesInBox())
		{
			Transition(AppState.ErrorNeedNewBox);
			return;
		}
		List<string> layerCodes;
		lock (_overlayLock)
		{
			layerCodes = ((!_m.ResultsByLayer.TryGetValue(_m.Layer, out var layerDict) || layerDict == null) ? new List<string>(0) : new List<string>(layerDict.Keys));
		}
		if (ct.IsCancellationRequested || _isDisposing)
		{
			return;
		}
		string check;
		try
		{
			check = await _controller.CheckCodes(layerCodes).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			Logger.Warn(ex, "CheckCodes failed");
			check = ex.InnerException?.Message ?? ex.Message;
		}
		if (ct.IsCancellationRequested || _isDisposing)
		{
			return;
		}
		if (!string.Equals(check, "Ok", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				MarkBadCodesFromServerMessage(check);
			}
			catch
			{
			}
			try
			{
				await UiAsync(() =>
				{
					pbCamera?.Invalidate();
				}).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
			}
			LogError("Проверка SGTIN не прошла: " + check, "L3", "000003");
			if (!ct.IsCancellationRequested && !_isDisposing)
			{
				if (AskNewBoxOrRepeatLayer(check))
				{
					Transition(AppState.WaitingLayerRepeat);
				}
				else
				{
					Transition(AppState.ErrorNeedNewBox);
				}
			}
			return;
		}
		_m.LayersReady++;
		int nextLayer = _m.LayersReady + 1;
		if (nextLayer <= order.LayersInPack)
		{
			LensOperationResult? focusResult = _controller?.FocusLayerWithRetry(nextLayer);
			if (!focusResult.HasValue || !focusResult.GetValueOrDefault().Success)
			{
				Logger.Warn($"FinalizeLayerOrBoxAsync: FocusLayer({nextLayer}) failed: {focusResult}");
				LogError($"Fokus auf Layer {nextLayer} fehlgeschlagen: {focusResult?.ErrorMessage}", "LENS", "FOCUS_FAIL");
			}
		}
		if (_m.Layer < layersInPack && !_m.IncompleteBox)
		{
			lock (_overlayLock)
			{
				_m.Layer++;
				_m.CodesFoundInLayer = 0;
				_m.ResultsByLayer[_m.Layer] = new Dictionary<string, RecognitionResult>(StringComparer.Ordinal);
				_overlayTtlByCode.Clear();
			}
			await UiAsync(() =>
			{
				using TimeOutDialog timeOutDialog = new TimeOutDialog();
				timeOutDialog.ShowInfo($"Слой {_m.Layer - 1} распознан", 3, 1);
			}).ConfigureAwait(continueOnCapturedContext: false);
			Transition(AppState.Filling);
			return;
		}
		List<string> allCodes;
		Dictionary<int, Dictionary<string, RecognitionResult>> resultsByLayerSnapshot;
		lock (_overlayLock)
		{
			allCodes = new List<string>();
			resultsByLayerSnapshot = new Dictionary<int, Dictionary<string, RecognitionResult>>();
			foreach (KeyValuePair<int, Dictionary<string, RecognitionResult>> kvp in _m.ResultsByLayer)
			{
				if (kvp.Value != null)
				{
					allCodes.AddRange(kvp.Value.Keys);
					resultsByLayerSnapshot[kvp.Key] = new Dictionary<string, RecognitionResult>(kvp.Value, StringComparer.Ordinal);
				}
			}
		}
		try
		{
			_controller?.CacheCodes(resultsByLayerSnapshot);
		}
		catch (Exception exception)
		{
			Logger.Warn(exception, "CacheCodes failed - continuing anyway");
		}
		if (!TryValidateCodesForGroupCode(allCodes, out var invalidReason))
		{
			LogError("Ошибка получения группового кода: " + invalidReason, "L3", "000002");
			ShowError("Ошибка получения группового кода.\n" + invalidReason + "\n\nНачните новый короб");
			Transition(AppState.ErrorNeedNewBox);
			return;
		}
		string groupCode = null;
		while (true)
		{
			if (ct.IsCancellationRequested || _isDisposing)
			{
				return;
			}
			try
			{
				groupCode = await _controller.GetGroupCode(allCodes).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex2)
			{
				Logger.Warn(ex2, "GetGroupCode failed");
				groupCode = null;
				string msg = ((ex2 is ArgumentOutOfRangeException) ? ("Некорректный код (формат/длина). " + (ex2.InnerException?.Message ?? ex2.Message)) : (ex2.InnerException?.Message ?? ex2.Message));
				if (AskRetryOrNewBox("Ошибка получения группового кода.\n\"" + msg + "\"\n\nПовторить или начать новый короб?"))
				{
					continue;
				}
				Transition(AppState.ErrorNeedNewBox);
				return;
			}
			if (!string.IsNullOrWhiteSpace(groupCode))
			{
				break;
			}
			if (!AskRetryOrNewBox("Ошибка получения группового кода.\n\nПовторить или начать новый короб?"))
			{
				Transition(AppState.ErrorNeedNewBox);
				return;
			}
		}
		while (true)
		{
			if (ct.IsCancellationRequested || _isDisposing)
			{
				return;
			}
			try
			{
				_controller.Aggregate(groupCode, resultsByLayerSnapshot);
			}
			catch (Exception ex3)
			{
				Logger.Warn(ex3, "Aggregate failed");
				LogError("Ошибка агрегации:\n\"" + (ex3.InnerException?.Message ?? ex3.Message) + "\"", "L3", "AGGREGATE");
				if (AskRetryOrNewBox("Ошибка агрегации.\n\"" + (ex3.InnerException?.Message ?? ex3.Message) + "\"\n\nПовторить или начать новый короб?"))
				{
					continue;
				}
				Transition(AppState.ErrorNeedNewBox);
				return;
			}
			break;
		}
		bool printedOk = true;
		while (true)
		{
			if (ct.IsCancellationRequested || _isDisposing)
			{
				return;
			}
			bool ok;
			string printerError;
			try
			{
				int copies;
				try
				{
					copies = _controller?.PrinterCopiesDefault ?? 1;
				}
				catch
				{
					copies = 1;
				}
				ok = _controller.printCode(groupCode, out printerError, copies);
			}
			catch (Exception ex4)
			{
				Logger.Warn(ex4, "printCode failed");
				ok = false;
				printerError = ex4.InnerException?.Message ?? ex4.Message;
			}
			if (ok)
			{
				await UiAsync(() =>
				{
					using TimeOutDialog timeOutDialog = new TimeOutDialog();
					int num = 1;
					try
					{
						num = _controller?.PrinterCopiesDefault ?? 1;
					}
					catch
					{
						num = 1;
					}
					string message = ((num > 1) ? ("Печать группового кода (" + num + " коп.) " + groupCode) : ("Печать группового кода " + groupCode));
					timeOutDialog.ShowInfo(message, 3, 2);
				}).ConfigureAwait(continueOnCapturedContext: false);
				break;
			}
			string err = (string.IsNullOrWhiteSpace(printerError) ? "неизвестная ошибка" : printerError);
			LogError("Ошибка печати группового кода: " + groupCode + "\n" + err, "PRINTER", "000001");
			if (!ShowConfirmation("Ошибка печати группового кода:\n" + groupCode + "\n" + err + "\n\nПовторить печать?\n\nДа = повторить\nНет = продолжить без печати", 650, 600))
			{
				printedOk = false;
				break;
			}
		}
		if (!printedOk)
		{
			await UiAsync(() =>
			{
				using TimeOutDialog timeOutDialog = new TimeOutDialog();
				timeOutDialog.Height = 650;
				timeOutDialog.ShowInfo("Короб завершен, но печать НЕ выполнена\n\nГрупповой код: " + groupCode, 4, 2);
			}).ConfigureAwait(continueOnCapturedContext: false);
		}
		Controller controller = _controller;
		bool autoStart = controller != null && controller.BoxWorkflowConfig?.AutoStartNextBox == true;
		_m.BoxNumber++;
		ResetBoxAndOverlays(autoStart);
		if (autoStart)
		{
			LogInfo($"Короб {_m.BoxNumber - 1} завершён. Авто-старт короба {_m.BoxNumber}.", "WORKFLOW", "AUTO_NEXT");
			try
			{
				_controller?.NewBox();
			}
			catch
			{
			}
			if (!(await InitLensForNewBoxAsync()))
			{
				Transition(AppState.ErrorNeedNewBox);
				return;
			}
			await UiAsync(() =>
			{
				using TimeOutDialog timeOutDialog = new TimeOutDialog();
				timeOutDialog.ShowInfo($"Короб {_m.BoxNumber - 1} → Короб {_m.BoxNumber} начат", 2, 1);
			}).ConfigureAwait(continueOnCapturedContext: false);
			Transition(AppState.Filling);
		}
		else
		{
			Transition(AppState.WaitingNewBox);
		}
	}

	private void Transition(AppState next)
	{
		_m.State = next;
		if (InvokeRequired)
		{
			BeginInvoke(new Action(ApplyUiFromState));
		}
		else
		{
			ApplyUiFromState();
		}
	}

	private bool IsFilling()
	{
		return _m.State == AppState.Filling;
	}

	private void ShowNotAllowedDuringFilling(string feature)
	{
		try
		{
			using TimeOutDialog td = new TimeOutDialog();
			td.Height = 350;
			td.Width = 800;
			td.ShowInfo("Недоступно во время заполнения короба.\n\n Сначала остановите заполнение с помощью  \n\n «СБРОСИТЬ / ОТМЕНА КОРОБА».", 3, 0);
		}
		catch
		{
		}
	}

	private bool ShowConfirmation(string message, int height = 0, int width = 0)
	{
		if (_isDisposing || IsDisposed || Disposing || !IsHandleCreated)
		{
			return false;
		}
		if (InvokeRequired)
		{
			try
			{
				return (bool)Invoke((Func<bool>)(() => ShowConfirmation(message, height, width)));
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}
		using ConfirmationDialog cd = new ConfirmationDialog();
		if (height > 0)
		{
			cd.Height = height;
		}
		if (width > 0)
		{
			cd.Width = width;
		}
		return cd.ShowConfirmation(message) == DialogResult.Yes;
	}

	private void ShowError(string message)
	{
		if (_isDisposing || IsDisposed || Disposing || !IsHandleCreated)
		{
			return;
		}
		if (InvokeRequired)
		{
			try
			{
				Invoke((Action)(() =>
				{
					ShowError(message);
				}));
				return;
			}
			catch (ObjectDisposedException)
			{
				return;
			}
			catch (InvalidOperationException)
			{
				return;
			}
		}
		using ErrorDialog ed = new ErrorDialog();
		ed.Height = 1000;
		ed.Width = 1200;
		ed.ShowError(message);
	}

	private void LogError(string message, string module = "CORE", string code = "")
	{
		try
		{
			_controller?.ErrorMessage(message, module, code);
		}
		catch
		{
		}
	}

	private void LogInfo(string message, string module = "CORE", string code = "")
	{
		try
		{
			_controller?.InfoMessage(message, module, code);
		}
		catch
		{
		}
	}

	private void LogTrace(string message)
	{
		try
		{
			_controller?.TraceMessage(message);
		}
		catch
		{
		}
	}

	private void ApplyUiFromState()
	{
		bool hasOrder = GetCurrentOrder() != null;
		rbStart.Enabled = _hardwareOk && !_m.AutoRunning;
		rbStop.Enabled = _hardwareOk && _m.AutoRunning;
		bool singleFrameEnabled = _hardwareOk && !_m.AutoRunning && _m.State == AppState.Filling;
		rbManualMode.Enabled = singleFrameEnabled;
		bool repeatLayerEnabled = hasOrder && _m.State == AppState.WaitingLayerRepeat;
		rbAutomaticMode.Enabled = repeatLayerEnabled;
		bool newBoxEnabled = hasOrder && (_m.State == AppState.WaitingNewBox || _m.State == AppState.ErrorNeedNewBox || _m.State == AppState.WaitingLayerRepeat);
		rbNewBox.Enabled = newBoxEnabled;
		rbIncompleteLayer.Enabled = hasOrder && _m.State == AppState.Filling && HasAnyCodesInBox();
		rbResetErrors.Enabled = hasOrder && _m.State != AppState.LoggedOut && _m.State != AppState.Finalizing;
		try
		{
			rbStart.BackColor = (rbStart.Enabled ? Color.White : Color.Gray);
			rbStart.ForeColor = Color.Black;
			rbStop.BackColor = (rbStop.Enabled ? Color.White : Color.Gray);
			rbStop.ForeColor = Color.Black;
			if (_m.State == AppState.WaitingNewBox || _m.State == AppState.ErrorNeedNewBox)
			{
				rbNewBox.BackColor = Color.Orange;
				rbNewBox.ForeColor = Color.Black;
			}
			else
			{
				rbNewBox.BackColor = (newBoxEnabled ? Color.White : Color.Gray);
				rbNewBox.ForeColor = Color.Black;
			}
			rbIncompleteLayer.BackColor = (rbIncompleteLayer.Enabled ? Color.White : Color.Gray);
			rbIncompleteLayer.ForeColor = Color.Black;
			rbResetErrors.BackColor = (rbResetErrors.Enabled ? Color.White : Color.Gray);
			rbResetErrors.ForeColor = (rbResetErrors.Enabled ? Color.Black : Color.Black);
			if (repeatLayerEnabled)
			{
				rbAutomaticMode.BackColor = Color.Orange;
				rbAutomaticMode.ForeColor = Color.Black;
			}
			else
			{
				rbAutomaticMode.BackColor = (rbAutomaticMode.Enabled ? Color.White : Color.Gray);
				rbAutomaticMode.ForeColor = Color.Black;
			}
			rbManualMode.BackColor = (singleFrameEnabled ? Color.White : Color.Gray);
		}
		catch
		{
		}
		lblBoxNumber.Text = $"Короб №{_m.BoxNumber}";
		int layersInPack = 0;
		int countInLayer = 0;
		try
		{
			Order order = GetCurrentOrder();
			if (order != null)
			{
				layersInPack = order.LayersInPack;
				countInLayer = order.CountInLayer;
			}
		}
		catch
		{
		}
		lblLayer.Text = $"Слой {_m.Layer} из {layersInPack}";
		lblRecognizedCount.Text = $"Распознано: {_m.CodesFoundInLayer} из {countInLayer}";
		progressRecognized.Progress = ((countInLayer != 0) ? Math.Min(100, (int)((double)_m.CodesFoundInLayer / (double)countInLayer * 100.0)) : 0);
		lblBoxStatus.ForeColor = Color.Black;
		lblBoxStatus.Text = (_m.IncompleteBox ? "Неполный" : "Полный");
		switch (_m.State)
		{
		case AppState.LoggedOut:
			lblBoxStatus.Text = "Вход…";
			break;
		case AppState.ReadyNoOrder:
			lblBoxStatus.Text = "Готов (нет заказа)";
			break;
		case AppState.WaitingNewBox:
			lblBoxStatus.Text = "Ожидание нового короба";
			break;
		case AppState.Filling:
			lblBoxStatus.Text = (_m.IncompleteBox ? "Заполнение (неполный)" : "Заполнение");
			break;
		case AppState.Finalizing:
			lblBoxStatus.Text = "Финализация";
			break;
		case AppState.WaitingLayerRepeat:
			lblBoxStatus.ForeColor = Color.FromArgb(180, 0, 0);
			lblBoxStatus.Text = "Ошибка слоя: нажмите ПОВТОР СЛОЯ после замены упаковок";
			break;
		case AppState.ErrorNeedNewBox:
			lblBoxStatus.ForeColor = Color.FromArgb(180, 0, 0);
			lblBoxStatus.Text = "Ошибка: начните новый короб (или СБРОСИТЬ)";
			break;
		}
		if (!_hardwareOk && !string.IsNullOrWhiteSpace(_hardwareStatusText) && _m.State != AppState.LoggedOut)
		{
			lblBoxStatus.ForeColor = Color.FromArgb(180, 0, 0);
			lblBoxStatus.Text = _hardwareStatusText;
		}
		ApplyAuthToUi();
	}

	private float GetScaleFactor(PictureBox pictureBox)
	{
		float scaleFactor = 1f;
		try
		{
			if (pictureBox?.Image != null)
			{
				float iw = pictureBox.Image.Width;
				float ih = pictureBox.Image.Height;
				float pw = pictureBox.Width;
				float ph = pictureBox.Height;
				if (iw > 0f && ih > 0f)
				{
					scaleFactor = Math.Min(pw / iw, ph / ih);
				}
			}
		}
		catch
		{
		}
		return scaleFactor;
	}

	private bool HasAnyCodesInBox()
	{
		lock (_overlayLock)
		{
			foreach (Dictionary<string, RecognitionResult> dict in _m.ResultsByLayer.Values)
			{
				if (dict != null && dict.Count > 0)
				{
					return true;
				}
			}
			return false;
		}
	}

	private bool TryValidateCodesForGroupCode(List<string> codes, out string reason)
	{
		reason = null;
		if (codes == null || codes.Count == 0)
		{
			reason = "нет кодов";
			return false;
		}
		foreach (string c in codes)
		{
			if (string.IsNullOrWhiteSpace(c) || c.Length < 16)
			{
				reason = "некорректный код: \"" + c + "\"";
				return false;
			}
		}
		if (new HashSet<string>(codes, StringComparer.Ordinal).Count != codes.Count)
		{
			reason = "обнаружены дублирующиеся коды";
			return false;
		}
		Order order = GetCurrentOrder();
		if (!_m.IncompleteBox && order != null)
		{
			int expected = order.LayersInPack * order.CountInLayer;
			if (codes.Count != expected)
			{
				reason = $"количество кодов ({codes.Count}) ≠ ожидаемому ({expected})";
				return false;
			}
		}
		return true;
	}

	private bool CheckHardwareInitialized()
	{
		bool ok = true;
		string statusText = null;
		try
		{
			bool camOk = _controller != null && _controller.IsCameraConnected;
			bool lensOk = _controller != null && _controller.IsLensConnected;
			bool lookupOk = _controller != null && _controller.IsLookupAvailable;
			if (camOk)
			{
				LogTrace("Источник изображения: " + _controller.CameraName + ".");
			}
			if (lensOk)
			{
				LogTrace("Объектив: " + _controller.LensName + ".");
			}
			if (!camOk || !lensOk || !lookupOk)
			{
				ok = false;
				if (!camOk || !lensOk)
				{
					string missing;
					if (!camOk && !lensOk)
					{
						missing = "камера и объектив";
					}
					else
					{
						missing = ((!camOk) ? "камера" : "объектив");
					}
					statusText = "Ошибка: " + missing + " не обнаружен(а).\nЗанято другим приложением? Не подключено? Перезагрузите приложение!";
				}
				else if (!lookupOk)
				{
					statusText = "Ошибка: таблица фокусировки (lens.lookup) не найдена.\nАвтоматическая фокусировка по слоям невозможна.\nПоместите файл lens.lookup в папку приложения.";
				}
				LogError(statusText + ". Работа невозможна.", "CORE", "000001");
			}
		}
		catch (Exception exception)
		{
			ok = false;
			statusText = "Ошибка: проверка оборудования";
			Logger.Warn(exception, "CheckHardwareInitialized failed");
		}
		_hardwareOk = ok;
		_hardwareStatusText = (ok ? null : statusText);
		try
		{
			UiAsync(ApplyUiFromState);
		}
		catch
		{
		}
		return ok;
	}

	private void TryStartPreview()
	{
		try
		{
			_controller?.StartImageReceiving();
		}
		catch (Exception exception)
		{
			Logger.Warn(exception, "StartImageReceiving failed");
		}
	}

	private void TryStopPreview()
	{
		try
		{
			_controller?.StopImageReceiving();
		}
		catch (Exception exception)
		{
			Logger.Warn(exception, "StopImageReceiving failed");
		}
	}

	private Task UiAsync(Action action)
	{
		if (_isDisposing || IsDisposed || Disposing || !IsHandleCreated)
		{
			return Task.CompletedTask;
		}
		if (!InvokeRequired)
		{
			action();
			return Task.CompletedTask;
		}
		TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
		try
		{
			BeginInvoke((Action)(() =>
			{
				try
				{
					if (!_isDisposing && !IsDisposed && !Disposing)
					{
						action();
					}
					tcs.TrySetResult(null);
				}
				catch (Exception exception)
				{
					tcs.TrySetException(exception);
				}
			}));
		}
		catch (ObjectDisposedException)
		{
			tcs.TrySetResult(null);
		}
		catch (InvalidOperationException)
		{
			tcs.TrySetResult(null);
		}
		return tcs.Task;
	}

	private Task<T> UiAsync<T>(Func<T> func)
	{
		if (_isDisposing || IsDisposed || Disposing || !IsHandleCreated)
		{
			return Task.FromResult<T>(default);
		}
		if (!InvokeRequired)
		{
			return Task.FromResult(func());
		}
		TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
		try
		{
			BeginInvoke((Action)(() =>
			{
				try
				{
					if (_isDisposing || IsDisposed || Disposing)
					{
						tcs.TrySetResult(default);
					}
					else
					{
						tcs.TrySetResult(func());
					}
				}
				catch (Exception exception)
				{
					tcs.TrySetException(exception);
				}
			}));
		}
		catch (ObjectDisposedException)
		{
			tcs.TrySetResult(default);
		}
		catch (InvalidOperationException)
		{
			tcs.TrySetResult(default);
		}
		return tcs.Task;
	}

	private void panelBoxActions_Paint(object sender, PaintEventArgs e)
	{
	}

	public Form3()
	{
		InitializeComponent();
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
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle25 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle31 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle32 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle33 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle34 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle35 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle36 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle37 = new System.Windows.Forms.DataGridViewCellStyle();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Aggregation.Forms.Form3));
		this.pnlRight = new System.Windows.Forms.Panel();
		this.roundedPanelRight = new UIKit.RoundedPanel();
		this.panelVideoControls = new UIKit.RoundedPanel();
		this.labelVideoControls = new System.Windows.Forms.Label();
		this.rbStart = new UIKit.RoundedButton();
		this.rbStop = new UIKit.RoundedButton();
		this.rbManualMode = new UIKit.RoundedButton();
		this.panelBoxActions = new UIKit.RoundedPanel();
		this.labelBoxActions = new System.Windows.Forms.Label();
		this.rbAutomaticMode = new UIKit.RoundedButton();
		this.rbIncompleteLayer = new UIKit.RoundedButton();
		this.rbNewBox = new UIKit.RoundedButton();
		this.rbResetErrors = new UIKit.RoundedButton();
		this.rbOperator = new UIKit.RoundedButton();
		this.rpOrderInfo = new UIKit.RoundedPanel();
		this.lblSeriesValue = new System.Windows.Forms.Label();
		this.lblExpirationDateValue = new System.Windows.Forms.Label();
		this.lblProductionDateValue = new System.Windows.Forms.Label();
		this.lblExpirationDateCaption = new System.Windows.Forms.Label();
		this.lblProductionDateCaption = new System.Windows.Forms.Label();
		this.lblProductCaption = new System.Windows.Forms.Label();
		this.lblProductHeight = new System.Windows.Forms.Label();
		this.lblProductHeightValue = new System.Windows.Forms.Label();
		this.lblSeriesCaption = new System.Windows.Forms.Label();
		this.lblProductValue = new System.Windows.Forms.Label();
		this.rpOrderHeader = new UIKit.RoundedPanel();
		this.rpOrderSelect = new UIKit.RoundedPanel();
		this.lblOrderSelect = new System.Windows.Forms.Label();
		this.lblOrderCaption = new System.Windows.Forms.Label();
		this.uiClockTimer = new System.Windows.Forms.Timer(this.components);
		this.pnlMain = new System.Windows.Forms.Panel();
		this.pnlPagesHost = new System.Windows.Forms.Panel();
		this.rpPagesContainer = new UIKit.RoundedPanel();
		this.pagedMain = new Manina.Windows.Forms.PagedControl();
		this.pageMain = new Manina.Windows.Forms.Page();
		this.rpCamera = new UIKit.RoundedPanel();
		this.pbCamera = new System.Windows.Forms.PictureBox();
		this.progressRecognized = new UIKit.ProgressBar();
		this.lblRecognizedCount = new System.Windows.Forms.Label();
		this.lblBoxStatus = new System.Windows.Forms.Label();
		this.lblLayer = new System.Windows.Forms.Label();
		this.lblStatusCaption = new System.Windows.Forms.Label();
		this.lblBoxNumber = new System.Windows.Forms.Label();
		this.pageLogs = new Manina.Windows.Forms.Page();
		this.pnlLogsPage = new System.Windows.Forms.Panel();
		this.dgvLogs = new System.Windows.Forms.DataGridView();
		this.colId = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.colTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.colCode = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.colModule = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.colLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.colMessage = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.rpLogsNav = new UIKit.RoundedPanel();
		this.btnLogsClear = new System.Windows.Forms.Button();
		this.btnLogsRefresh = new System.Windows.Forms.Button();
		this.btnLogsFirst = new System.Windows.Forms.Button();
		this.lblLogsPage = new System.Windows.Forms.Label();
		this.btnLogsPrev = new System.Windows.Forms.Button();
		this.btnLogsLast = new System.Windows.Forms.Button();
		this.btnLogsNext = new System.Windows.Forms.Button();
		this.pnlFooter = new System.Windows.Forms.Panel();
		this.tlpFooter = new System.Windows.Forms.TableLayoutPanel();
		this.roundedPanelFooter = new UIKit.RoundedPanel();
		this.tabMain = new UIKit.RoundedPanel();
		this.labelApplication = new System.Windows.Forms.Label();
		this.btnSettings = new UIKit.RoundedButton();
		this.btnHelp = new UIKit.RoundedButton();
		this.btnCloseApp = new UIKit.RoundedButton();
		this.tabLogs = new UIKit.RoundedPanel();
		this.roundedPanelRepeatPrint = new UIKit.RoundedPanel();
		this.labelRepeatPrint = new System.Windows.Forms.Label();
		this.btnPrintManual = new UIKit.RoundedButton();
		this.btnPrintRecent = new UIKit.RoundedButton();
		this.pnlHeader = new System.Windows.Forms.Panel();
		this.roundedPanelHeader = new UIKit.RoundedPanel();
		this.roundedPanelStationID = new UIKit.RoundedPanel();
		this.lblStationId = new System.Windows.Forms.Label();
		this.rpClockContainer = new UIKit.RoundedPanel();
		this.lblClock = new System.Windows.Forms.Label();
		this.pnlRight.SuspendLayout();
		this.roundedPanelRight.SuspendLayout();
		this.panelVideoControls.SuspendLayout();
		this.panelBoxActions.SuspendLayout();
		this.rpOrderInfo.SuspendLayout();
		this.rpOrderHeader.SuspendLayout();
		this.rpOrderSelect.SuspendLayout();
		this.pnlMain.SuspendLayout();
		this.pnlPagesHost.SuspendLayout();
		this.rpPagesContainer.SuspendLayout();
		this.pagedMain.SuspendLayout();
		this.pageMain.SuspendLayout();
		this.rpCamera.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.pbCamera).BeginInit();
		this.pageLogs.SuspendLayout();
		this.pnlLogsPage.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.dgvLogs).BeginInit();
		this.rpLogsNav.SuspendLayout();
		this.pnlFooter.SuspendLayout();
		this.tlpFooter.SuspendLayout();
		this.roundedPanelFooter.SuspendLayout();
		this.roundedPanelRepeatPrint.SuspendLayout();
		this.pnlHeader.SuspendLayout();
		this.roundedPanelHeader.SuspendLayout();
		this.roundedPanelStationID.SuspendLayout();
		this.rpClockContainer.SuspendLayout();
		base.SuspendLayout();
		this.pnlRight.Controls.Add(this.roundedPanelRight);
		this.pnlRight.Dock = System.Windows.Forms.DockStyle.Right;
		this.pnlRight.Location = new System.Drawing.Point(1270, 0);
		this.pnlRight.Name = "pnlRight";
		this.pnlRight.Size = new System.Drawing.Size(430, 942);
		this.pnlRight.TabIndex = 2;
		this.pnlRight.Visible = false;
		this.roundedPanelRight.BackColor = System.Drawing.Color.Transparent;
		this.roundedPanelRight.BorderColor = System.Drawing.Color.Silver;
		this.roundedPanelRight.Caption = null;
		this.roundedPanelRight.Controls.Add(this.panelVideoControls);
		this.roundedPanelRight.Controls.Add(this.panelBoxActions);
		this.roundedPanelRight.Controls.Add(this.rbOperator);
		this.roundedPanelRight.Controls.Add(this.rpOrderInfo);
		this.roundedPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanelRight.DrawCaption = false;
		this.roundedPanelRight.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelRight.HeaderHeight = 20;
		this.roundedPanelRight.LeftBottom = true;
		this.roundedPanelRight.LeftTop = true;
		this.roundedPanelRight.Location = new System.Drawing.Point(0, 0);
		this.roundedPanelRight.Name = "roundedPanelRight";
		this.roundedPanelRight.PanelBackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.roundedPanelRight.Radius = 18;
		this.roundedPanelRight.RightBottom = true;
		this.roundedPanelRight.RightTop = true;
		this.roundedPanelRight.SelectCaption = false;
		this.roundedPanelRight.ShowHeader = false;
		this.roundedPanelRight.Size = new System.Drawing.Size(430, 942);
		this.roundedPanelRight.TabIndex = 19;
		this.roundedPanelRight.Thickness = 2f;
		this.panelVideoControls.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.panelVideoControls.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.panelVideoControls.BorderColor = System.Drawing.Color.Silver;
		this.panelVideoControls.Caption = null;
		this.panelVideoControls.Controls.Add(this.labelVideoControls);
		this.panelVideoControls.Controls.Add(this.rbStart);
		this.panelVideoControls.Controls.Add(this.rbStop);
		this.panelVideoControls.Controls.Add(this.rbManualMode);
		this.panelVideoControls.DrawCaption = false;
		this.panelVideoControls.HeaderBackColor = System.Drawing.Color.Silver;
		this.panelVideoControls.HeaderHeight = 20;
		this.panelVideoControls.LeftBottom = true;
		this.panelVideoControls.LeftTop = true;
		this.panelVideoControls.Location = new System.Drawing.Point(17, 750);
		this.panelVideoControls.Name = "panelVideoControls";
		this.panelVideoControls.PanelBackColor = System.Drawing.Color.FromArgb(0, 64, 0);
		this.panelVideoControls.Radius = 18;
		this.panelVideoControls.RightBottom = true;
		this.panelVideoControls.RightTop = true;
		this.panelVideoControls.SelectCaption = false;
		this.panelVideoControls.ShowHeader = false;
		this.panelVideoControls.Size = new System.Drawing.Size(393, 185);
		this.panelVideoControls.TabIndex = 10;
		this.panelVideoControls.Thickness = 2f;
		this.labelVideoControls.BackColor = System.Drawing.Color.Transparent;
		this.labelVideoControls.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.labelVideoControls.ForeColor = System.Drawing.Color.White;
		this.labelVideoControls.Location = new System.Drawing.Point(12, 8);
		this.labelVideoControls.Name = "labelVideoControls";
		this.labelVideoControls.Size = new System.Drawing.Size(360, 24);
		this.labelVideoControls.TabIndex = 0;
		this.labelVideoControls.Text = "Видео";
		this.rbStart.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbStart.BackColor = System.Drawing.Color.Gray;
		this.rbStart.BackColorHovered = System.Drawing.Color.White;
		this.rbStart.BackColorPressed = System.Drawing.Color.WhiteSmoke;
		this.rbStart.BGColor = System.Drawing.Color.FromArgb(0, 64, 0);
		this.rbStart.BorderColor = System.Drawing.Color.Black;
		this.rbStart.CornerRadius = 24;
		this.rbStart.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbStart.Enabled = false;
		this.rbStart.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbStart.ForeColor = System.Drawing.Color.Black;
		this.rbStart.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbStart.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbStart.HoverEffect = false;
		this.rbStart.ImageDisabled = null;
		this.rbStart.ImageEnabled = null;
		this.rbStart.ImageSize = new System.Drawing.Size(0, 0);
		this.rbStart.LeftSideRounded = true;
		this.rbStart.LeftTextOffset = 0;
		this.rbStart.Location = new System.Drawing.Point(10, 40);
		this.rbStart.Name = "rbStart";
		this.rbStart.Pressed = false;
		this.rbStart.RightSideRounded = true;
		this.rbStart.RoundedCorners = true;
		this.rbStart.ShowBorder = true;
		this.rbStart.Size = new System.Drawing.Size(173, 60);
		this.rbStart.TabIndex = 6;
		this.rbStart.Text = "АВТОМАТ СТАРТ";
		this.rbStart.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbStart.Click += new System.EventHandler(this.rbStart_Click);
		this.rbStart.MouseDown += new System.Windows.Forms.MouseEventHandler(this.rbStart_MouseDown);
		this.rbStop.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbStop.BackColor = System.Drawing.Color.Gray;
		this.rbStop.BackColorHovered = System.Drawing.Color.White;
		this.rbStop.BackColorPressed = System.Drawing.Color.White;
		this.rbStop.BGColor = System.Drawing.Color.FromArgb(0, 64, 0);
		this.rbStop.BorderColor = System.Drawing.Color.Black;
		this.rbStop.CornerRadius = 24;
		this.rbStop.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbStop.Enabled = false;
		this.rbStop.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbStop.ForeColor = System.Drawing.Color.Black;
		this.rbStop.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbStop.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbStop.HoverEffect = false;
		this.rbStop.ImageDisabled = null;
		this.rbStop.ImageEnabled = null;
		this.rbStop.ImageSize = new System.Drawing.Size(0, 0);
		this.rbStop.LeftSideRounded = true;
		this.rbStop.LeftTextOffset = 0;
		this.rbStop.Location = new System.Drawing.Point(210, 40);
		this.rbStop.Name = "rbStop";
		this.rbStop.Pressed = false;
		this.rbStop.RightSideRounded = true;
		this.rbStop.RoundedCorners = true;
		this.rbStop.ShowBorder = true;
		this.rbStop.Size = new System.Drawing.Size(173, 60);
		this.rbStop.TabIndex = 11;
		this.rbStop.Text = "АВТОМАТ  СТОП";
		this.rbStop.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbStop.Click += new System.EventHandler(this.rbStop_Click);
		this.rbStop.MouseDown += new System.Windows.Forms.MouseEventHandler(this.rbStop_MouseDown);
		this.rbManualMode.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbManualMode.BackColor = System.Drawing.Color.White;
		this.rbManualMode.BackColorHovered = System.Drawing.Color.White;
		this.rbManualMode.BackColorPressed = System.Drawing.Color.White;
		this.rbManualMode.BGColor = System.Drawing.Color.FromArgb(0, 64, 0);
		this.rbManualMode.BorderColor = System.Drawing.Color.Black;
		this.rbManualMode.CornerRadius = 24;
		this.rbManualMode.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbManualMode.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbManualMode.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbManualMode.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbManualMode.HoverEffect = false;
		this.rbManualMode.ImageDisabled = null;
		this.rbManualMode.ImageEnabled = null;
		this.rbManualMode.ImageSize = new System.Drawing.Size(0, 0);
		this.rbManualMode.LeftSideRounded = true;
		this.rbManualMode.LeftTextOffset = 0;
		this.rbManualMode.Location = new System.Drawing.Point(10, 110);
		this.rbManualMode.Name = "rbManualMode";
		this.rbManualMode.Pressed = false;
		this.rbManualMode.RightSideRounded = true;
		this.rbManualMode.RoundedCorners = true;
		this.rbManualMode.ShowBorder = true;
		this.rbManualMode.Size = new System.Drawing.Size(373, 60);
		this.rbManualMode.TabIndex = 10;
		this.rbManualMode.Text = "ЕДИНИЧНЫЙ КАДР";
		this.rbManualMode.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbManualMode.Click += new System.EventHandler(this.btnModeManual_Click);
		this.panelBoxActions.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.panelBoxActions.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.panelBoxActions.BorderColor = System.Drawing.Color.Silver;
		this.panelBoxActions.Caption = null;
		this.panelBoxActions.Controls.Add(this.labelBoxActions);
		this.panelBoxActions.Controls.Add(this.rbAutomaticMode);
		this.panelBoxActions.Controls.Add(this.rbIncompleteLayer);
		this.panelBoxActions.Controls.Add(this.rbNewBox);
		this.panelBoxActions.Controls.Add(this.rbResetErrors);
		this.panelBoxActions.DrawCaption = false;
		this.panelBoxActions.HeaderBackColor = System.Drawing.Color.Silver;
		this.panelBoxActions.HeaderHeight = 20;
		this.panelBoxActions.LeftBottom = true;
		this.panelBoxActions.LeftTop = true;
		this.panelBoxActions.Location = new System.Drawing.Point(17, 414);
		this.panelBoxActions.Name = "panelBoxActions";
		this.panelBoxActions.PanelBackColor = System.Drawing.Color.FromArgb(50, 0, 0);
		this.panelBoxActions.Radius = 18;
		this.panelBoxActions.RightBottom = true;
		this.panelBoxActions.RightTop = true;
		this.panelBoxActions.SelectCaption = false;
		this.panelBoxActions.ShowHeader = false;
		this.panelBoxActions.Size = new System.Drawing.Size(393, 330);
		this.panelBoxActions.TabIndex = 9;
		this.panelBoxActions.Thickness = 2f;
		this.panelBoxActions.Paint += new System.Windows.Forms.PaintEventHandler(this.panelBoxActions_Paint);
		this.labelBoxActions.BackColor = System.Drawing.Color.Transparent;
		this.labelBoxActions.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.labelBoxActions.ForeColor = System.Drawing.Color.White;
		this.labelBoxActions.Location = new System.Drawing.Point(12, 8);
		this.labelBoxActions.Name = "labelBoxActions";
		this.labelBoxActions.Size = new System.Drawing.Size(360, 24);
		this.labelBoxActions.TabIndex = 0;
		this.labelBoxActions.Text = "Короб";
		this.rbAutomaticMode.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbAutomaticMode.BackColor = System.Drawing.Color.White;
		this.rbAutomaticMode.BackColorHovered = System.Drawing.Color.White;
		this.rbAutomaticMode.BackColorPressed = System.Drawing.Color.White;
		this.rbAutomaticMode.BGColor = System.Drawing.Color.FromArgb(50, 0, 0);
		this.rbAutomaticMode.BorderColor = System.Drawing.Color.Black;
		this.rbAutomaticMode.CornerRadius = 24;
		this.rbAutomaticMode.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbAutomaticMode.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbAutomaticMode.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbAutomaticMode.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbAutomaticMode.HoverEffect = false;
		this.rbAutomaticMode.ImageDisabled = null;
		this.rbAutomaticMode.ImageEnabled = null;
		this.rbAutomaticMode.ImageSize = new System.Drawing.Size(0, 0);
		this.rbAutomaticMode.LeftSideRounded = true;
		this.rbAutomaticMode.LeftTextOffset = 0;
		this.rbAutomaticMode.Location = new System.Drawing.Point(10, 40);
		this.rbAutomaticMode.Name = "rbAutomaticMode";
		this.rbAutomaticMode.Pressed = false;
		this.rbAutomaticMode.RightSideRounded = true;
		this.rbAutomaticMode.RoundedCorners = true;
		this.rbAutomaticMode.ShowBorder = true;
		this.rbAutomaticMode.Size = new System.Drawing.Size(373, 60);
		this.rbAutomaticMode.TabIndex = 9;
		this.rbAutomaticMode.Text = "ПОВТОР СЛОЯ";
		this.rbAutomaticMode.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbAutomaticMode.Click += new System.EventHandler(this.btnModeAuto_Click);
		this.rbIncompleteLayer.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbIncompleteLayer.BackColor = System.Drawing.Color.Gray;
		this.rbIncompleteLayer.BackColorHovered = System.Drawing.Color.White;
		this.rbIncompleteLayer.BackColorPressed = System.Drawing.Color.White;
		this.rbIncompleteLayer.BGColor = System.Drawing.Color.FromArgb(50, 0, 0);
		this.rbIncompleteLayer.BorderColor = System.Drawing.Color.Black;
		this.rbIncompleteLayer.CornerRadius = 24;
		this.rbIncompleteLayer.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbIncompleteLayer.Enabled = false;
		this.rbIncompleteLayer.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbIncompleteLayer.ForeColor = System.Drawing.Color.Black;
		this.rbIncompleteLayer.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbIncompleteLayer.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbIncompleteLayer.HoverEffect = false;
		this.rbIncompleteLayer.ImageDisabled = null;
		this.rbIncompleteLayer.ImageEnabled = null;
		this.rbIncompleteLayer.ImageSize = new System.Drawing.Size(0, 0);
		this.rbIncompleteLayer.LeftSideRounded = true;
		this.rbIncompleteLayer.LeftTextOffset = 0;
		this.rbIncompleteLayer.Location = new System.Drawing.Point(10, 110);
		this.rbIncompleteLayer.Name = "rbIncompleteLayer";
		this.rbIncompleteLayer.Pressed = false;
		this.rbIncompleteLayer.RightSideRounded = true;
		this.rbIncompleteLayer.RoundedCorners = true;
		this.rbIncompleteLayer.ShowBorder = true;
		this.rbIncompleteLayer.Size = new System.Drawing.Size(373, 60);
		this.rbIncompleteLayer.TabIndex = 12;
		this.rbIncompleteLayer.Text = "НЕПОЛНЫЙ КОРОБ";
		this.rbIncompleteLayer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbIncompleteLayer.Click += new System.EventHandler(this.rbIncompleteLayer_Click);
		this.rbIncompleteLayer.MouseClick += new System.Windows.Forms.MouseEventHandler(this.rbIncompleteLayer_MouseClick);
		this.rbNewBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbNewBox.BackColor = System.Drawing.Color.Gray;
		this.rbNewBox.BackColorHovered = System.Drawing.Color.FromArgb(0, 80, 255);
		this.rbNewBox.BackColorPressed = System.Drawing.Color.FromArgb(0, 80, 255);
		this.rbNewBox.BGColor = System.Drawing.Color.FromArgb(50, 0, 0);
		this.rbNewBox.BorderColor = System.Drawing.Color.Black;
		this.rbNewBox.CornerRadius = 24;
		this.rbNewBox.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbNewBox.Enabled = false;
		this.rbNewBox.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbNewBox.ForeColor = System.Drawing.Color.Black;
		this.rbNewBox.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbNewBox.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbNewBox.HoverEffect = false;
		this.rbNewBox.ImageDisabled = null;
		this.rbNewBox.ImageEnabled = null;
		this.rbNewBox.ImageSize = new System.Drawing.Size(0, 0);
		this.rbNewBox.LeftSideRounded = true;
		this.rbNewBox.LeftTextOffset = 0;
		this.rbNewBox.Location = new System.Drawing.Point(10, 180);
		this.rbNewBox.Name = "rbNewBox";
		this.rbNewBox.Pressed = false;
		this.rbNewBox.RightSideRounded = true;
		this.rbNewBox.RoundedCorners = true;
		this.rbNewBox.ShowBorder = true;
		this.rbNewBox.Size = new System.Drawing.Size(373, 60);
		this.rbNewBox.TabIndex = 7;
		this.rbNewBox.Text = "НОВЫЙ КОРОБ";
		this.rbNewBox.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbNewBox.Click += new System.EventHandler(this.rbNewBox_Click);
		this.rbResetErrors.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbResetErrors.BackColor = System.Drawing.Color.Black;
		this.rbResetErrors.BackColorHovered = System.Drawing.Color.Silver;
		this.rbResetErrors.BackColorPressed = System.Drawing.Color.DarkGray;
		this.rbResetErrors.BGColor = System.Drawing.Color.FromArgb(50, 0, 0);
		this.rbResetErrors.BorderColor = System.Drawing.Color.Black;
		this.rbResetErrors.CornerRadius = 24;
		this.rbResetErrors.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbResetErrors.Font = new System.Drawing.Font("Tahoma", 16f);
		this.rbResetErrors.ForeColor = System.Drawing.Color.White;
		this.rbResetErrors.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbResetErrors.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbResetErrors.HoverEffect = false;
		this.rbResetErrors.ImageDisabled = null;
		this.rbResetErrors.ImageEnabled = null;
		this.rbResetErrors.ImageSize = new System.Drawing.Size(0, 0);
		this.rbResetErrors.LeftSideRounded = true;
		this.rbResetErrors.LeftTextOffset = 0;
		this.rbResetErrors.Location = new System.Drawing.Point(10, 250);
		this.rbResetErrors.Name = "rbResetErrors";
		this.rbResetErrors.Pressed = false;
		this.rbResetErrors.RightSideRounded = true;
		this.rbResetErrors.RoundedCorners = true;
		this.rbResetErrors.ShowBorder = true;
		this.rbResetErrors.Size = new System.Drawing.Size(373, 60);
		this.rbResetErrors.TabIndex = 3;
		this.rbResetErrors.Text = "СБРОСИТЬ / ОТМЕНА КОРОБА";
		this.rbResetErrors.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rbResetErrors.Click += new System.EventHandler(this.rbResetErrors_Click);
		this.rbOperator.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rbOperator.BackColor = System.Drawing.Color.Silver;
		this.rbOperator.BackColorHovered = System.Drawing.Color.Silver;
		this.rbOperator.BackColorPressed = System.Drawing.Color.DarkGray;
		this.rbOperator.BGColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.rbOperator.BorderColor = System.Drawing.Color.Black;
		this.rbOperator.CornerRadius = 24;
		this.rbOperator.DialogResult = System.Windows.Forms.DialogResult.None;
		this.rbOperator.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.rbOperator.ForeColor = System.Drawing.Color.Gray;
		this.rbOperator.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.rbOperator.ForeColorPressed = System.Drawing.Color.Lime;
		this.rbOperator.HoverEffect = false;
		this.rbOperator.ImageDisabled = null;
		this.rbOperator.ImageEnabled = null;
		this.rbOperator.ImageSize = new System.Drawing.Size(0, 0);
		this.rbOperator.LeftSideRounded = true;
		this.rbOperator.LeftTextOffset = 0;
		this.rbOperator.Location = new System.Drawing.Point(17, 11);
		this.rbOperator.Name = "rbOperator";
		this.rbOperator.Pressed = false;
		this.rbOperator.RightSideRounded = true;
		this.rbOperator.RoundedCorners = true;
		this.rbOperator.ShowBorder = false;
		this.rbOperator.Size = new System.Drawing.Size(393, 60);
		this.rbOperator.TabIndex = 8;
		this.rbOperator.Text = "Оператор";
		this.rbOperator.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rpOrderInfo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rpOrderInfo.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.rpOrderInfo.BorderColor = System.Drawing.Color.White;
		this.rpOrderInfo.Caption = null;
		this.rpOrderInfo.Controls.Add(this.lblSeriesValue);
		this.rpOrderInfo.Controls.Add(this.lblExpirationDateValue);
		this.rpOrderInfo.Controls.Add(this.lblProductionDateValue);
		this.rpOrderInfo.Controls.Add(this.lblExpirationDateCaption);
		this.rpOrderInfo.Controls.Add(this.lblProductionDateCaption);
		this.rpOrderInfo.Controls.Add(this.lblProductCaption);
		this.rpOrderInfo.Controls.Add(this.lblProductHeight);
		this.rpOrderInfo.Controls.Add(this.lblProductHeightValue);
		this.rpOrderInfo.Controls.Add(this.lblSeriesCaption);
		this.rpOrderInfo.Controls.Add(this.lblProductValue);
		this.rpOrderInfo.Controls.Add(this.rpOrderHeader);
		this.rpOrderInfo.DrawCaption = false;
		this.rpOrderInfo.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpOrderInfo.HeaderHeight = 20;
		this.rpOrderInfo.LeftBottom = true;
		this.rpOrderInfo.LeftTop = true;
		this.rpOrderInfo.Location = new System.Drawing.Point(17, 85);
		this.rpOrderInfo.Name = "rpOrderInfo";
		this.rpOrderInfo.PanelBackColor = System.Drawing.SystemColors.InactiveCaption;
		this.rpOrderInfo.Radius = 24;
		this.rpOrderInfo.RightBottom = true;
		this.rpOrderInfo.RightTop = true;
		this.rpOrderInfo.SelectCaption = false;
		this.rpOrderInfo.ShowHeader = false;
		this.rpOrderInfo.Size = new System.Drawing.Size(393, 480);
		this.rpOrderInfo.TabIndex = 2;
		this.rpOrderInfo.Thickness = 0f;
		this.lblSeriesValue.BackColor = System.Drawing.Color.Transparent;
		this.lblSeriesValue.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblSeriesValue.ForeColor = System.Drawing.Color.Black;
		this.lblSeriesValue.Location = new System.Drawing.Point(22, 117);
		this.lblSeriesValue.Name = "lblSeriesValue";
		this.lblSeriesValue.Size = new System.Drawing.Size(328, 29);
		this.lblSeriesValue.TabIndex = 17;
		this.lblExpirationDateValue.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.lblExpirationDateValue.BackColor = System.Drawing.Color.Transparent;
		this.lblExpirationDateValue.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblExpirationDateValue.ForeColor = System.Drawing.Color.Black;
		this.lblExpirationDateValue.Location = new System.Drawing.Point(22, 440);
		this.lblExpirationDateValue.Name = "lblExpirationDateValue";
		this.lblExpirationDateValue.Size = new System.Drawing.Size(342, 29);
		this.lblExpirationDateValue.TabIndex = 14;
		this.lblProductionDateValue.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.lblProductionDateValue.BackColor = System.Drawing.Color.Transparent;
		this.lblProductionDateValue.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblProductionDateValue.ForeColor = System.Drawing.Color.Black;
		this.lblProductionDateValue.Location = new System.Drawing.Point(22, 384);
		this.lblProductionDateValue.Name = "lblProductionDateValue";
		this.lblProductionDateValue.Size = new System.Drawing.Size(328, 29);
		this.lblProductionDateValue.TabIndex = 15;
		this.lblExpirationDateCaption.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.lblExpirationDateCaption.AutoSize = true;
		this.lblExpirationDateCaption.BackColor = System.Drawing.Color.Transparent;
		this.lblExpirationDateCaption.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblExpirationDateCaption.ForeColor = System.Drawing.Color.Black;
		this.lblExpirationDateCaption.Location = new System.Drawing.Point(22, 410);
		this.lblExpirationDateCaption.Name = "lblExpirationDateCaption";
		this.lblExpirationDateCaption.Size = new System.Drawing.Size(135, 29);
		this.lblExpirationDateCaption.TabIndex = 13;
		this.lblExpirationDateCaption.Text = "Годен до:";
		this.lblProductionDateCaption.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.lblProductionDateCaption.AutoSize = true;
		this.lblProductionDateCaption.BackColor = System.Drawing.Color.Transparent;
		this.lblProductionDateCaption.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblProductionDateCaption.ForeColor = System.Drawing.Color.Black;
		this.lblProductionDateCaption.Location = new System.Drawing.Point(22, 354);
		this.lblProductionDateCaption.Name = "lblProductionDateCaption";
		this.lblProductionDateCaption.Size = new System.Drawing.Size(264, 29);
		this.lblProductionDateCaption.TabIndex = 12;
		this.lblProductionDateCaption.Text = "Дата производства:";
		this.lblProductCaption.AutoSize = true;
		this.lblProductCaption.BackColor = System.Drawing.Color.Transparent;
		this.lblProductCaption.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblProductCaption.ForeColor = System.Drawing.Color.Black;
		this.lblProductCaption.Location = new System.Drawing.Point(22, 159);
		this.lblProductCaption.Name = "lblProductCaption";
		this.lblProductCaption.Size = new System.Drawing.Size(203, 29);
		this.lblProductCaption.TabIndex = 11;
		this.lblProductCaption.Text = "Номенклатура:";
		this.lblProductHeight.AutoSize = true;
		this.lblProductHeight.BackColor = System.Drawing.Color.Transparent;
		this.lblProductHeight.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblProductHeight.ForeColor = System.Drawing.Color.Black;
		this.lblProductHeight.Location = new System.Drawing.Point(22, 290);
		this.lblProductHeight.Name = "lblProductHeight";
		this.lblProductHeight.Size = new System.Drawing.Size(238, 29);
		this.lblProductHeight.TabIndex = 18;
		this.lblProductHeight.Text = "Высота упаковки:";
		this.lblProductHeightValue.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblProductHeightValue.BackColor = System.Drawing.Color.Transparent;
		this.lblProductHeightValue.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblProductHeightValue.ForeColor = System.Drawing.Color.Black;
		this.lblProductHeightValue.Location = new System.Drawing.Point(22, 320);
		this.lblProductHeightValue.Name = "lblProductHeightValue";
		this.lblProductHeightValue.Size = new System.Drawing.Size(353, 162);
		this.lblProductHeightValue.TabIndex = 16;
		this.lblSeriesCaption.AutoSize = true;
		this.lblSeriesCaption.BackColor = System.Drawing.Color.Transparent;
		this.lblSeriesCaption.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblSeriesCaption.ForeColor = System.Drawing.Color.Black;
		this.lblSeriesCaption.Location = new System.Drawing.Point(22, 87);
		this.lblSeriesCaption.Name = "lblSeriesCaption";
		this.lblSeriesCaption.Size = new System.Drawing.Size(135, 29);
		this.lblSeriesCaption.TabIndex = 10;
		this.lblSeriesCaption.Text = "Серия №:";
		this.lblProductValue.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblProductValue.BackColor = System.Drawing.Color.Transparent;
		this.lblProductValue.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblProductValue.ForeColor = System.Drawing.Color.Black;
		this.lblProductValue.Location = new System.Drawing.Point(22, 191);
		this.lblProductValue.Name = "lblProductValue";
		this.lblProductValue.Size = new System.Drawing.Size(353, 162);
		this.lblProductValue.TabIndex = 16;
		this.rpOrderHeader.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rpOrderHeader.BackColor = System.Drawing.SystemColors.InactiveCaption;
		this.rpOrderHeader.BorderColor = System.Drawing.Color.Transparent;
		this.rpOrderHeader.Caption = null;
		this.rpOrderHeader.Controls.Add(this.rpOrderSelect);
		this.rpOrderHeader.Controls.Add(this.lblOrderCaption);
		this.rpOrderHeader.DrawCaption = false;
		this.rpOrderHeader.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpOrderHeader.HeaderHeight = 20;
		this.rpOrderHeader.LeftBottom = true;
		this.rpOrderHeader.LeftTop = true;
		this.rpOrderHeader.Location = new System.Drawing.Point(10, 10);
		this.rpOrderHeader.Name = "rpOrderHeader";
		this.rpOrderHeader.PanelBackColor = System.Drawing.Color.Black;
		this.rpOrderHeader.Radius = 24;
		this.rpOrderHeader.RightBottom = true;
		this.rpOrderHeader.RightTop = true;
		this.rpOrderHeader.SelectCaption = false;
		this.rpOrderHeader.ShowHeader = false;
		this.rpOrderHeader.Size = new System.Drawing.Size(373, 60);
		this.rpOrderHeader.TabIndex = 1;
		this.rpOrderHeader.Thickness = 0f;
		this.rpOrderSelect.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.rpOrderSelect.BackColor = System.Drawing.Color.Black;
		this.rpOrderSelect.BorderColor = System.Drawing.Color.Transparent;
		this.rpOrderSelect.Caption = null;
		this.rpOrderSelect.Controls.Add(this.lblOrderSelect);
		this.rpOrderSelect.DrawCaption = false;
		this.rpOrderSelect.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpOrderSelect.HeaderHeight = 20;
		this.rpOrderSelect.LeftBottom = true;
		this.rpOrderSelect.LeftTop = true;
		this.rpOrderSelect.Location = new System.Drawing.Point(145, 8);
		this.rpOrderSelect.Name = "rpOrderSelect";
		this.rpOrderSelect.PanelBackColor = System.Drawing.Color.FromArgb(0, 80, 255);
		this.rpOrderSelect.Radius = 24;
		this.rpOrderSelect.RightBottom = true;
		this.rpOrderSelect.RightTop = true;
		this.rpOrderSelect.SelectCaption = false;
		this.rpOrderSelect.ShowHeader = false;
		this.rpOrderSelect.Size = new System.Drawing.Size(220, 44);
		this.rpOrderSelect.TabIndex = 2;
		this.rpOrderSelect.Thickness = 0f;
		this.rpOrderSelect.Click += new System.EventHandler(this.orderSelect_Click);
		this.lblOrderSelect.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblOrderSelect.BackColor = System.Drawing.Color.Transparent;
		this.lblOrderSelect.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblOrderSelect.ForeColor = System.Drawing.Color.White;
		this.lblOrderSelect.Location = new System.Drawing.Point(0, 4);
		this.lblOrderSelect.Name = "lblOrderSelect";
		this.lblOrderSelect.Size = new System.Drawing.Size(220, 36);
		this.lblOrderSelect.TabIndex = 0;
		this.lblOrderSelect.Text = "[ выбрать ]";
		this.lblOrderSelect.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.lblOrderSelect.Click += new System.EventHandler(this.orderSelect_Click);
		this.lblOrderCaption.AutoSize = true;
		this.lblOrderCaption.BackColor = System.Drawing.Color.Black;
		this.lblOrderCaption.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblOrderCaption.ForeColor = System.Drawing.Color.White;
		this.lblOrderCaption.Location = new System.Drawing.Point(11, 13);
		this.lblOrderCaption.Name = "lblOrderCaption";
		this.lblOrderCaption.Size = new System.Drawing.Size(92, 33);
		this.lblOrderCaption.TabIndex = 0;
		this.lblOrderCaption.Text = "Заказ:";
		this.lblOrderCaption.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.lblOrderCaption_MouseDoubleClick);
		this.uiClockTimer.Enabled = true;
		this.uiClockTimer.Interval = 1000;
		this.uiClockTimer.Tick += new System.EventHandler(this.uiClockTimer_Tick);
		this.pnlMain.Controls.Add(this.pnlPagesHost);
		this.pnlMain.Controls.Add(this.pnlFooter);
		this.pnlMain.Controls.Add(this.pnlHeader);
		this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pnlMain.Location = new System.Drawing.Point(0, 0);
		this.pnlMain.Name = "pnlMain";
		this.pnlMain.Size = new System.Drawing.Size(1270, 942);
		this.pnlMain.TabIndex = 9;
		this.pnlMain.Visible = false;
		this.pnlPagesHost.Controls.Add(this.rpPagesContainer);
		this.pnlPagesHost.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pnlPagesHost.Location = new System.Drawing.Point(0, 80);
		this.pnlPagesHost.Name = "pnlPagesHost";
		this.pnlPagesHost.Size = new System.Drawing.Size(1270, 729);
		this.pnlPagesHost.TabIndex = 11;
		this.rpPagesContainer.BackColor = System.Drawing.Color.Transparent;
		this.rpPagesContainer.BorderColor = System.Drawing.Color.White;
		this.rpPagesContainer.Caption = null;
		this.rpPagesContainer.Controls.Add(this.pagedMain);
		this.rpPagesContainer.Dock = System.Windows.Forms.DockStyle.Fill;
		this.rpPagesContainer.DrawCaption = false;
		this.rpPagesContainer.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpPagesContainer.HeaderHeight = 20;
		this.rpPagesContainer.LeftBottom = true;
		this.rpPagesContainer.LeftTop = true;
		this.rpPagesContainer.Location = new System.Drawing.Point(0, 0);
		this.rpPagesContainer.Name = "rpPagesContainer";
		this.rpPagesContainer.Padding = new System.Windows.Forms.Padding(5);
		this.rpPagesContainer.PanelBackColor = System.Drawing.Color.White;
		this.rpPagesContainer.Radius = 24;
		this.rpPagesContainer.RightBottom = true;
		this.rpPagesContainer.RightTop = true;
		this.rpPagesContainer.SelectCaption = false;
		this.rpPagesContainer.ShowHeader = false;
		this.rpPagesContainer.Size = new System.Drawing.Size(1270, 729);
		this.rpPagesContainer.TabIndex = 0;
		this.rpPagesContainer.Thickness = 0f;
		this.pagedMain.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.pagedMain.Controls.Add(this.pageMain);
		this.pagedMain.Controls.Add(this.pageLogs);
		this.pagedMain.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pagedMain.Location = new System.Drawing.Point(5, 5);
		this.pagedMain.Name = "pagedMain";
		this.pagedMain.Size = new System.Drawing.Size(1260, 719);
		this.pagedMain.TabIndex = 8;
		this.pagedMain.PageChanged += new System.EventHandler<Manina.Windows.Forms.PageChangedEventArgs>(this.pagedMain_PageChanged);
		this.pageMain.Controls.Add(this.rpCamera);
		this.pageMain.Controls.Add(this.progressRecognized);
		this.pageMain.Controls.Add(this.lblRecognizedCount);
		this.pageMain.Controls.Add(this.lblBoxStatus);
		this.pageMain.Controls.Add(this.lblLayer);
		this.pageMain.Controls.Add(this.lblStatusCaption);
		this.pageMain.Controls.Add(this.lblBoxNumber);
		this.pageMain.Location = new System.Drawing.Point(0, 0);
		this.pageMain.Name = "pageMain";
		this.pageMain.Size = new System.Drawing.Size(1260, 719);
		this.pageMain.Text = "Page 1";
		this.rpCamera.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.rpCamera.BackColor = System.Drawing.Color.White;
		this.rpCamera.BorderColor = System.Drawing.Color.White;
		this.rpCamera.Caption = null;
		this.rpCamera.Controls.Add(this.pbCamera);
		this.rpCamera.DrawCaption = false;
		this.rpCamera.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpCamera.HeaderHeight = 20;
		this.rpCamera.LeftBottom = true;
		this.rpCamera.LeftTop = true;
		this.rpCamera.Location = new System.Drawing.Point(20, 117);
		this.rpCamera.Name = "rpCamera";
		this.rpCamera.Padding = new System.Windows.Forms.Padding(20);
		this.rpCamera.PanelBackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.rpCamera.Radius = 24;
		this.rpCamera.RightBottom = true;
		this.rpCamera.RightTop = true;
		this.rpCamera.SelectCaption = false;
		this.rpCamera.ShowHeader = false;
		this.rpCamera.Size = new System.Drawing.Size(1220, 589);
		this.rpCamera.TabIndex = 1;
		this.rpCamera.Thickness = 0f;
		this.pbCamera.BackColor = System.Drawing.Color.FromArgb(25, 25, 25);
		this.pbCamera.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pbCamera.Location = new System.Drawing.Point(20, 20);
		this.pbCamera.Margin = new System.Windows.Forms.Padding(30);
		this.pbCamera.Name = "pbCamera";
		this.pbCamera.Size = new System.Drawing.Size(1180, 549);
		this.pbCamera.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.pbCamera.TabIndex = 0;
		this.pbCamera.TabStop = false;
		this.pbCamera.Paint += new System.Windows.Forms.PaintEventHandler(this.pbCamera_Paint);
		this.pbCamera.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pbCamera_MouseClick);
		this.progressRecognized.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.progressRecognized.BackColor = System.Drawing.Color.White;
		this.progressRecognized.Location = new System.Drawing.Point(934, 15);
		this.progressRecognized.Name = "progressRecognized";
		this.progressRecognized.Progress = 0;
		this.progressRecognized.ProgressBackColor = System.Drawing.Color.Silver;
		this.progressRecognized.ProgressForeColor = System.Drawing.Color.FromArgb(0, 80, 255);
		this.progressRecognized.Radius = 4;
		this.progressRecognized.Size = new System.Drawing.Size(306, 47);
		this.progressRecognized.SplitNumber = 0;
		this.progressRecognized.TabIndex = 2;
		this.lblRecognizedCount.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.lblRecognizedCount.BackColor = System.Drawing.Color.White;
		this.lblRecognizedCount.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblRecognizedCount.ForeColor = System.Drawing.Color.Black;
		this.lblRecognizedCount.Location = new System.Drawing.Point(929, 78);
		this.lblRecognizedCount.Name = "lblRecognizedCount";
		this.lblRecognizedCount.Size = new System.Drawing.Size(306, 29);
		this.lblRecognizedCount.TabIndex = 5;
		this.lblRecognizedCount.Text = "Распознано: 0 из 0";
		this.lblRecognizedCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		this.lblBoxStatus.AutoSize = true;
		this.lblBoxStatus.BackColor = System.Drawing.Color.White;
		this.lblBoxStatus.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblBoxStatus.ForeColor = System.Drawing.Color.Black;
		this.lblBoxStatus.Location = new System.Drawing.Point(179, 53);
		this.lblBoxStatus.Name = "lblBoxStatus";
		this.lblBoxStatus.Size = new System.Drawing.Size(100, 29);
		this.lblBoxStatus.TabIndex = 7;
		this.lblBoxStatus.Text = "Полный";
		this.lblLayer.AutoSize = true;
		this.lblLayer.BackColor = System.Drawing.Color.White;
		this.lblLayer.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblLayer.ForeColor = System.Drawing.Color.Black;
		this.lblLayer.Location = new System.Drawing.Point(180, 17);
		this.lblLayer.Name = "lblLayer";
		this.lblLayer.Size = new System.Drawing.Size(142, 29);
		this.lblLayer.TabIndex = 4;
		this.lblLayer.Text = "Слой 0 из 0";
		this.lblStatusCaption.AutoSize = true;
		this.lblStatusCaption.BackColor = System.Drawing.Color.White;
		this.lblStatusCaption.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblStatusCaption.ForeColor = System.Drawing.Color.Black;
		this.lblStatusCaption.Location = new System.Drawing.Point(15, 53);
		this.lblStatusCaption.Name = "lblStatusCaption";
		this.lblStatusCaption.Size = new System.Drawing.Size(105, 29);
		this.lblStatusCaption.TabIndex = 6;
		this.lblStatusCaption.Text = "Статус:";
		this.lblBoxNumber.AutoSize = true;
		this.lblBoxNumber.BackColor = System.Drawing.Color.White;
		this.lblBoxNumber.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
		this.lblBoxNumber.ForeColor = System.Drawing.Color.Black;
		this.lblBoxNumber.Location = new System.Drawing.Point(15, 17);
		this.lblBoxNumber.Name = "lblBoxNumber";
		this.lblBoxNumber.Size = new System.Drawing.Size(142, 29);
		this.lblBoxNumber.TabIndex = 3;
		this.lblBoxNumber.Text = "Короб №1";
		this.pageLogs.Controls.Add(this.pnlLogsPage);
		this.pageLogs.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.pageLogs.Location = new System.Drawing.Point(0, 0);
		this.pageLogs.Name = "pageLogs";
		this.pageLogs.Padding = new System.Windows.Forms.Padding(10);
		this.pageLogs.Size = new System.Drawing.Size(1260, 719);
		this.pageLogs.Text = "Page 2";
		this.pnlLogsPage.Controls.Add(this.dgvLogs);
		this.pnlLogsPage.Controls.Add(this.rpLogsNav);
		this.pnlLogsPage.Dock = System.Windows.Forms.DockStyle.Fill;
		this.pnlLogsPage.Location = new System.Drawing.Point(0, 0);
		this.pnlLogsPage.Name = "pnlLogsPage";
		this.pnlLogsPage.Padding = new System.Windows.Forms.Padding(10);
		this.pnlLogsPage.Size = new System.Drawing.Size(1260, 719);
		this.pnlLogsPage.TabIndex = 24;
		this.dgvLogs.AllowUserToAddRows = false;
		this.dgvLogs.AllowUserToDeleteRows = false;
		this.dgvLogs.AllowUserToResizeRows = false;
		this.dgvLogs.BackgroundColor = System.Drawing.Color.White;
		this.dgvLogs.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.dgvLogs.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
		this.dgvLogs.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
		dataGridViewCellStyle25.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
		dataGridViewCellStyle25.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
		dataGridViewCellStyle25.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		dataGridViewCellStyle25.ForeColor = System.Drawing.Color.Black;
		dataGridViewCellStyle25.SelectionBackColor = System.Drawing.Color.Silver;
		dataGridViewCellStyle25.SelectionForeColor = System.Drawing.Color.Black;
		dataGridViewCellStyle25.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
		this.dgvLogs.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle25;
		this.dgvLogs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dgvLogs.Columns.AddRange(this.colId, this.colTime, this.colCode, this.colModule, this.colLevel, this.colMessage);
		dataGridViewCellStyle31.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle31.BackColor = System.Drawing.SystemColors.Window;
		dataGridViewCellStyle31.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		dataGridViewCellStyle31.ForeColor = System.Drawing.Color.White;
		dataGridViewCellStyle31.SelectionBackColor = System.Drawing.SystemColors.Highlight;
		dataGridViewCellStyle31.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
		dataGridViewCellStyle31.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
		this.dgvLogs.DefaultCellStyle = dataGridViewCellStyle31;
		this.dgvLogs.Dock = System.Windows.Forms.DockStyle.Fill;
		this.dgvLogs.EnableHeadersVisualStyles = false;
		this.dgvLogs.GridColor = System.Drawing.Color.Silver;
		this.dgvLogs.Location = new System.Drawing.Point(10, 10);
		this.dgvLogs.MultiSelect = false;
		this.dgvLogs.Name = "dgvLogs";
		this.dgvLogs.ReadOnly = true;
		this.dgvLogs.RowHeadersVisible = false;
		dataGridViewCellStyle32.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.dgvLogs.RowsDefaultCellStyle = dataGridViewCellStyle32;
		this.dgvLogs.RowTemplate.DefaultCellStyle.BackColor = System.Drawing.Color.White;
		this.dgvLogs.RowTemplate.DefaultCellStyle.Font = new System.Drawing.Font("Tahoma", 14.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.dgvLogs.RowTemplate.DefaultCellStyle.ForeColor = System.Drawing.Color.Black;
		this.dgvLogs.RowTemplate.Height = 40;
		this.dgvLogs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
		this.dgvLogs.Size = new System.Drawing.Size(1240, 609);
		this.dgvLogs.TabIndex = 22;
		this.dgvLogs.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dgvLogs_CellFormatting);
		dataGridViewCellStyle33.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
		this.colId.DefaultCellStyle = dataGridViewCellStyle33;
		this.colId.HeaderText = "№";
		this.colId.Name = "colId";
		this.colId.ReadOnly = true;
		this.colId.Visible = false;
		this.colId.Width = 200;
		dataGridViewCellStyle34.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
		this.colTime.DefaultCellStyle = dataGridViewCellStyle34;
		this.colTime.HeaderText = "Время";
		this.colTime.Name = "colTime";
		this.colTime.ReadOnly = true;
		this.colTime.Width = 200;
		dataGridViewCellStyle35.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
		this.colCode.DefaultCellStyle = dataGridViewCellStyle35;
		this.colCode.HeaderText = "Код";
		this.colCode.Name = "colCode";
		this.colCode.ReadOnly = true;
		this.colCode.Width = 150;
		dataGridViewCellStyle36.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
		this.colModule.DefaultCellStyle = dataGridViewCellStyle36;
		this.colModule.HeaderText = "Модуль";
		this.colModule.Name = "colModule";
		this.colModule.ReadOnly = true;
		this.colModule.Width = 150;
		dataGridViewCellStyle37.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
		this.colLevel.DefaultCellStyle = dataGridViewCellStyle37;
		this.colLevel.HeaderText = "Уровень";
		this.colLevel.Name = "colLevel";
		this.colLevel.ReadOnly = true;
		this.colLevel.Width = 150;
		this.colMessage.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
		this.colMessage.HeaderText = "Описание";
		this.colMessage.Name = "colMessage";
		this.colMessage.ReadOnly = true;
		this.rpLogsNav.BorderColor = System.Drawing.Color.Red;
		this.rpLogsNav.Caption = null;
		this.rpLogsNav.Controls.Add(this.btnLogsClear);
		this.rpLogsNav.Controls.Add(this.btnLogsRefresh);
		this.rpLogsNav.Controls.Add(this.btnLogsFirst);
		this.rpLogsNav.Controls.Add(this.lblLogsPage);
		this.rpLogsNav.Controls.Add(this.btnLogsPrev);
		this.rpLogsNav.Controls.Add(this.btnLogsLast);
		this.rpLogsNav.Controls.Add(this.btnLogsNext);
		this.rpLogsNav.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.rpLogsNav.DrawCaption = false;
		this.rpLogsNav.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpLogsNav.HeaderHeight = 20;
		this.rpLogsNav.LeftBottom = true;
		this.rpLogsNav.LeftTop = true;
		this.rpLogsNav.Location = new System.Drawing.Point(10, 619);
		this.rpLogsNav.Name = "rpLogsNav";
		this.rpLogsNav.PanelBackColor = System.Drawing.Color.White;
		this.rpLogsNav.Radius = 24;
		this.rpLogsNav.RightBottom = true;
		this.rpLogsNav.RightTop = true;
		this.rpLogsNav.SelectCaption = false;
		this.rpLogsNav.ShowHeader = false;
		this.rpLogsNav.Size = new System.Drawing.Size(1240, 90);
		this.rpLogsNav.TabIndex = 23;
		this.rpLogsNav.Thickness = 0f;
		this.btnLogsClear.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnLogsClear.BackColor = System.Drawing.Color.White;
		this.btnLogsClear.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnLogsClear.Enabled = false;
		this.btnLogsClear.FlatAppearance.BorderColor = System.Drawing.Color.Black;
		this.btnLogsClear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnLogsClear.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLogsClear.ForeColor = System.Drawing.Color.Black;
		this.btnLogsClear.Image = (System.Drawing.Image)resources.GetObject("btnLogsClear.Image");
		this.btnLogsClear.Location = new System.Drawing.Point(1082, 10);
		this.btnLogsClear.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
		this.btnLogsClear.Name = "btnLogsClear";
		this.btnLogsClear.Size = new System.Drawing.Size(70, 70);
		this.btnLogsClear.TabIndex = 22;
		this.btnLogsClear.Tag = "1";
		this.btnLogsClear.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnLogsClear.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnLogsClear.UseVisualStyleBackColor = false;
		this.btnLogsClear.Click += new System.EventHandler(this.btnLogsClear_Click);
		this.btnLogsRefresh.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnLogsRefresh.BackColor = System.Drawing.Color.White;
		this.btnLogsRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnLogsRefresh.FlatAppearance.BorderColor = System.Drawing.Color.Black;
		this.btnLogsRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnLogsRefresh.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLogsRefresh.ForeColor = System.Drawing.Color.Black;
		this.btnLogsRefresh.Image = (System.Drawing.Image)resources.GetObject("btnLogsRefresh.Image");
		this.btnLogsRefresh.Location = new System.Drawing.Point(1160, 10);
		this.btnLogsRefresh.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
		this.btnLogsRefresh.Name = "btnLogsRefresh";
		this.btnLogsRefresh.Size = new System.Drawing.Size(70, 70);
		this.btnLogsRefresh.TabIndex = 21;
		this.btnLogsRefresh.Tag = "1";
		this.btnLogsRefresh.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnLogsRefresh.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnLogsRefresh.UseVisualStyleBackColor = false;
		this.btnLogsRefresh.Click += new System.EventHandler(this.btnLogsRefresh_Click);
		this.btnLogsFirst.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.btnLogsFirst.BackColor = System.Drawing.Color.White;
		this.btnLogsFirst.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnLogsFirst.FlatAppearance.BorderColor = System.Drawing.Color.Black;
		this.btnLogsFirst.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnLogsFirst.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLogsFirst.ForeColor = System.Drawing.Color.Black;
		this.btnLogsFirst.Image = (System.Drawing.Image)resources.GetObject("btnLogsFirst.Image");
		this.btnLogsFirst.Location = new System.Drawing.Point(367, 10);
		this.btnLogsFirst.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
		this.btnLogsFirst.Name = "btnLogsFirst";
		this.btnLogsFirst.Size = new System.Drawing.Size(70, 70);
		this.btnLogsFirst.TabIndex = 16;
		this.btnLogsFirst.Tag = "1";
		this.btnLogsFirst.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnLogsFirst.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnLogsFirst.UseVisualStyleBackColor = false;
		this.btnLogsFirst.Click += new System.EventHandler(this.btnLogsFirst_Click);
		this.lblLogsPage.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.lblLogsPage.BackColor = System.Drawing.Color.Transparent;
		this.lblLogsPage.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.lblLogsPage.ForeColor = System.Drawing.Color.Black;
		this.lblLogsPage.Location = new System.Drawing.Point(522, 10);
		this.lblLogsPage.Name = "lblLogsPage";
		this.lblLogsPage.Size = new System.Drawing.Size(194, 70);
		this.lblLogsPage.TabIndex = 20;
		this.lblLogsPage.Text = "1 / 1";
		this.lblLogsPage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnLogsPrev.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.btnLogsPrev.BackColor = System.Drawing.Color.White;
		this.btnLogsPrev.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnLogsPrev.FlatAppearance.BorderColor = System.Drawing.Color.Black;
		this.btnLogsPrev.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnLogsPrev.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLogsPrev.ForeColor = System.Drawing.Color.Black;
		this.btnLogsPrev.Image = (System.Drawing.Image)resources.GetObject("btnLogsPrev.Image");
		this.btnLogsPrev.Location = new System.Drawing.Point(445, 10);
		this.btnLogsPrev.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
		this.btnLogsPrev.Name = "btnLogsPrev";
		this.btnLogsPrev.Size = new System.Drawing.Size(70, 70);
		this.btnLogsPrev.TabIndex = 17;
		this.btnLogsPrev.Tag = "1";
		this.btnLogsPrev.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnLogsPrev.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnLogsPrev.UseVisualStyleBackColor = false;
		this.btnLogsPrev.Click += new System.EventHandler(this.btnLogsPrev_Click);
		this.btnLogsLast.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.btnLogsLast.BackColor = System.Drawing.Color.White;
		this.btnLogsLast.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnLogsLast.FlatAppearance.BorderColor = System.Drawing.Color.Black;
		this.btnLogsLast.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnLogsLast.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLogsLast.ForeColor = System.Drawing.Color.Black;
		this.btnLogsLast.Image = (System.Drawing.Image)resources.GetObject("btnLogsLast.Image");
		this.btnLogsLast.Location = new System.Drawing.Point(797, 10);
		this.btnLogsLast.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
		this.btnLogsLast.Name = "btnLogsLast";
		this.btnLogsLast.Size = new System.Drawing.Size(70, 70);
		this.btnLogsLast.TabIndex = 19;
		this.btnLogsLast.Tag = "1";
		this.btnLogsLast.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnLogsLast.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnLogsLast.UseVisualStyleBackColor = false;
		this.btnLogsLast.Click += new System.EventHandler(this.btnLogsLast_Click);
		this.btnLogsNext.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.btnLogsNext.BackColor = System.Drawing.Color.White;
		this.btnLogsNext.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btnLogsNext.FlatAppearance.BorderColor = System.Drawing.Color.Black;
		this.btnLogsNext.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnLogsNext.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		this.btnLogsNext.ForeColor = System.Drawing.Color.Black;
		this.btnLogsNext.Image = (System.Drawing.Image)resources.GetObject("btnLogsNext.Image");
		this.btnLogsNext.Location = new System.Drawing.Point(719, 10);
		this.btnLogsNext.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
		this.btnLogsNext.Name = "btnLogsNext";
		this.btnLogsNext.Size = new System.Drawing.Size(70, 70);
		this.btnLogsNext.TabIndex = 18;
		this.btnLogsNext.Tag = "1";
		this.btnLogsNext.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnLogsNext.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnLogsNext.UseVisualStyleBackColor = false;
		this.btnLogsNext.Click += new System.EventHandler(this.btnLogsNext_Click);
		this.pnlFooter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
		this.pnlFooter.Controls.Add(this.tlpFooter);
		this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.pnlFooter.Location = new System.Drawing.Point(0, 809);
		this.pnlFooter.Name = "pnlFooter";
		this.pnlFooter.Size = new System.Drawing.Size(1270, 133);
		this.pnlFooter.TabIndex = 10;
		this.pnlFooter.Visible = false;
		this.tlpFooter.BackColor = System.Drawing.Color.Transparent;
		this.tlpFooter.ColumnCount = 2;
		this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70f));
		this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30f));
		this.tlpFooter.Controls.Add(this.roundedPanelFooter, 0, 0);
		this.tlpFooter.Controls.Add(this.roundedPanelRepeatPrint, 1, 0);
		this.tlpFooter.Dock = System.Windows.Forms.DockStyle.Fill;
		this.tlpFooter.Location = new System.Drawing.Point(0, 0);
		this.tlpFooter.Margin = new System.Windows.Forms.Padding(0);
		this.tlpFooter.Name = "tlpFooter";
		this.tlpFooter.RowCount = 1;
		this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.tlpFooter.Size = new System.Drawing.Size(1270, 133);
		this.tlpFooter.TabIndex = 0;
		this.roundedPanelFooter.BackColor = System.Drawing.Color.Transparent;
		this.roundedPanelFooter.BorderColor = System.Drawing.Color.Silver;
		this.roundedPanelFooter.Caption = null;
		this.roundedPanelFooter.Controls.Add(this.tabMain);
		this.roundedPanelFooter.Controls.Add(this.labelApplication);
		this.roundedPanelFooter.Controls.Add(this.btnSettings);
		this.roundedPanelFooter.Controls.Add(this.btnHelp);
		this.roundedPanelFooter.Controls.Add(this.btnCloseApp);
		this.roundedPanelFooter.Controls.Add(this.tabLogs);
		this.roundedPanelFooter.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanelFooter.DrawCaption = false;
		this.roundedPanelFooter.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelFooter.HeaderHeight = 20;
		this.roundedPanelFooter.LeftBottom = true;
		this.roundedPanelFooter.LeftTop = true;
		this.roundedPanelFooter.Location = new System.Drawing.Point(0, 0);
		this.roundedPanelFooter.Margin = new System.Windows.Forms.Padding(0);
		this.roundedPanelFooter.Name = "roundedPanelFooter";
		this.roundedPanelFooter.PanelBackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.roundedPanelFooter.Radius = 18;
		this.roundedPanelFooter.RightBottom = true;
		this.roundedPanelFooter.RightTop = true;
		this.roundedPanelFooter.SelectCaption = false;
		this.roundedPanelFooter.ShowHeader = false;
		this.roundedPanelFooter.Size = new System.Drawing.Size(889, 133);
		this.roundedPanelFooter.TabIndex = 16;
		this.roundedPanelFooter.Thickness = 2f;
		this.tabMain.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.tabMain.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.tabMain.BorderColor = System.Drawing.Color.White;
		this.tabMain.Caption = "Главная";
		this.tabMain.DrawCaption = true;
		this.tabMain.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.tabMain.ForeColor = System.Drawing.Color.Black;
		this.tabMain.HeaderBackColor = System.Drawing.Color.Silver;
		this.tabMain.HeaderHeight = 20;
		this.tabMain.LeftBottom = true;
		this.tabMain.LeftTop = true;
		this.tabMain.Location = new System.Drawing.Point(265, 51);
		this.tabMain.Name = "tabMain";
		this.tabMain.PanelBackColor = System.Drawing.Color.White;
		this.tabMain.Radius = 24;
		this.tabMain.RightBottom = true;
		this.tabMain.RightTop = true;
		this.tabMain.SelectCaption = false;
		this.tabMain.ShowHeader = false;
		this.tabMain.Size = new System.Drawing.Size(140, 60);
		this.tabMain.TabIndex = 10;
		this.tabMain.Thickness = 1f;
		this.tabMain.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tabMain_MouseDown);
		this.labelApplication.BackColor = System.Drawing.Color.Transparent;
		this.labelApplication.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold);
		this.labelApplication.ForeColor = System.Drawing.Color.White;
		this.labelApplication.Location = new System.Drawing.Point(21, 12);
		this.labelApplication.Name = "labelApplication";
		this.labelApplication.Size = new System.Drawing.Size(387, 29);
		this.labelApplication.TabIndex = 20;
		this.labelApplication.Text = "Приложение";
		this.btnSettings.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnSettings.BackColor = System.Drawing.Color.Black;
		this.btnSettings.BackColorHovered = System.Drawing.Color.Silver;
		this.btnSettings.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnSettings.BGColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.btnSettings.BorderColor = System.Drawing.Color.White;
		this.btnSettings.CornerRadius = 24;
		this.btnSettings.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnSettings.ForeColor = System.Drawing.Color.White;
		this.btnSettings.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnSettings.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnSettings.HoverEffect = false;
		this.btnSettings.ImageDisabled = (System.Drawing.Image)resources.GetObject("btnSettings.ImageDisabled");
		this.btnSettings.ImageEnabled = (System.Drawing.Image)resources.GetObject("btnSettings.ImageEnabled");
		this.btnSettings.ImageSize = new System.Drawing.Size(50, 50);
		this.btnSettings.LeftSideRounded = true;
		this.btnSettings.LeftTextOffset = 0;
		this.btnSettings.Location = new System.Drawing.Point(740, 51);
		this.btnSettings.Name = "btnSettings";
		this.btnSettings.Pressed = false;
		this.btnSettings.RightSideRounded = true;
		this.btnSettings.RoundedCorners = true;
		this.btnSettings.ShowBorder = true;
		this.btnSettings.Size = new System.Drawing.Size(60, 60);
		this.btnSettings.TabIndex = 5;
		this.btnSettings.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
		this.btnHelp.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnHelp.BackColor = System.Drawing.Color.Black;
		this.btnHelp.BackColorHovered = System.Drawing.Color.Silver;
		this.btnHelp.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnHelp.BGColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.btnHelp.BorderColor = System.Drawing.Color.White;
		this.btnHelp.CornerRadius = 24;
		this.btnHelp.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnHelp.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnHelp.ForeColor = System.Drawing.Color.White;
		this.btnHelp.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnHelp.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnHelp.HoverEffect = false;
		this.btnHelp.ImageDisabled = (System.Drawing.Image)resources.GetObject("btnHelp.ImageDisabled");
		this.btnHelp.ImageEnabled = (System.Drawing.Image)resources.GetObject("btnHelp.ImageEnabled");
		this.btnHelp.ImageSize = new System.Drawing.Size(55, 55);
		this.btnHelp.LeftSideRounded = true;
		this.btnHelp.LeftTextOffset = 0;
		this.btnHelp.Location = new System.Drawing.Point(810, 51);
		this.btnHelp.Name = "btnHelp";
		this.btnHelp.Pressed = false;
		this.btnHelp.RightSideRounded = true;
		this.btnHelp.RoundedCorners = true;
		this.btnHelp.ShowBorder = true;
		this.btnHelp.Size = new System.Drawing.Size(60, 60);
		this.btnHelp.TabIndex = 4;
		this.btnHelp.Text = "П";
		this.btnHelp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
		this.btnCloseApp.BackColor = System.Drawing.Color.Gray;
		this.btnCloseApp.BackColorHovered = System.Drawing.Color.Silver;
		this.btnCloseApp.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnCloseApp.BGColor = System.Drawing.SystemColors.WindowText;
		this.btnCloseApp.BorderColor = System.Drawing.Color.White;
		this.btnCloseApp.CornerRadius = 24;
		this.btnCloseApp.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnCloseApp.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnCloseApp.ForeColor = System.Drawing.Color.White;
		this.btnCloseApp.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnCloseApp.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnCloseApp.HoverEffect = false;
		this.btnCloseApp.ImageDisabled = null;
		this.btnCloseApp.ImageEnabled = null;
		this.btnCloseApp.ImageSize = new System.Drawing.Size(41, 41);
		this.btnCloseApp.LeftSideRounded = true;
		this.btnCloseApp.LeftTextOffset = 0;
		this.btnCloseApp.Location = new System.Drawing.Point(25, 51);
		this.btnCloseApp.Name = "btnCloseApp";
		this.btnCloseApp.Pressed = false;
		this.btnCloseApp.RightSideRounded = true;
		this.btnCloseApp.RoundedCorners = true;
		this.btnCloseApp.ShowBorder = true;
		this.btnCloseApp.Size = new System.Drawing.Size(154, 60);
		this.btnCloseApp.TabIndex = 17;
		this.btnCloseApp.Text = "Закрыть";
		this.btnCloseApp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnCloseApp.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnCloseApp_MouseDown);
		this.tabLogs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.tabLogs.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.tabLogs.BorderColor = System.Drawing.Color.Gray;
		this.tabLogs.Caption = "Журнал";
		this.tabLogs.DrawCaption = true;
		this.tabLogs.Enabled = false;
		this.tabLogs.Font = new System.Drawing.Font("Tahoma", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.tabLogs.ForeColor = System.Drawing.Color.Gray;
		this.tabLogs.HeaderBackColor = System.Drawing.Color.Silver;
		this.tabLogs.HeaderHeight = 20;
		this.tabLogs.LeftBottom = true;
		this.tabLogs.LeftTop = true;
		this.tabLogs.Location = new System.Drawing.Point(414, 51);
		this.tabLogs.Name = "tabLogs";
		this.tabLogs.PanelBackColor = System.Drawing.Color.Black;
		this.tabLogs.Radius = 24;
		this.tabLogs.RightBottom = true;
		this.tabLogs.RightTop = true;
		this.tabLogs.SelectCaption = false;
		this.tabLogs.ShowHeader = false;
		this.tabLogs.Size = new System.Drawing.Size(140, 60);
		this.tabLogs.TabIndex = 12;
		this.tabLogs.Thickness = 1f;
		this.tabLogs.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tabLogs_MouseDown);
		this.roundedPanelRepeatPrint.BackColor = System.Drawing.Color.Transparent;
		this.roundedPanelRepeatPrint.BorderColor = System.Drawing.Color.Silver;
		this.roundedPanelRepeatPrint.Caption = null;
		this.roundedPanelRepeatPrint.Controls.Add(this.labelRepeatPrint);
		this.roundedPanelRepeatPrint.Controls.Add(this.btnPrintManual);
		this.roundedPanelRepeatPrint.Controls.Add(this.btnPrintRecent);
		this.roundedPanelRepeatPrint.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanelRepeatPrint.DrawCaption = false;
		this.roundedPanelRepeatPrint.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelRepeatPrint.HeaderHeight = 20;
		this.roundedPanelRepeatPrint.LeftBottom = true;
		this.roundedPanelRepeatPrint.LeftTop = true;
		this.roundedPanelRepeatPrint.Location = new System.Drawing.Point(889, 0);
		this.roundedPanelRepeatPrint.Margin = new System.Windows.Forms.Padding(0);
		this.roundedPanelRepeatPrint.Name = "roundedPanelRepeatPrint";
		this.roundedPanelRepeatPrint.PanelBackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.roundedPanelRepeatPrint.Radius = 18;
		this.roundedPanelRepeatPrint.RightBottom = true;
		this.roundedPanelRepeatPrint.RightTop = true;
		this.roundedPanelRepeatPrint.SelectCaption = false;
		this.roundedPanelRepeatPrint.ShowHeader = false;
		this.roundedPanelRepeatPrint.Size = new System.Drawing.Size(381, 133);
		this.roundedPanelRepeatPrint.TabIndex = 18;
		this.roundedPanelRepeatPrint.Thickness = 2f;
		this.labelRepeatPrint.BackColor = System.Drawing.Color.Transparent;
		this.labelRepeatPrint.Font = new System.Drawing.Font("Tahoma", 12f, System.Drawing.FontStyle.Bold);
		this.labelRepeatPrint.ForeColor = System.Drawing.Color.White;
		this.labelRepeatPrint.Location = new System.Drawing.Point(13, 12);
		this.labelRepeatPrint.Name = "labelRepeatPrint";
		this.labelRepeatPrint.Size = new System.Drawing.Size(387, 29);
		this.labelRepeatPrint.TabIndex = 19;
		this.labelRepeatPrint.Text = "Повторная печать транспортной этикетки";
		this.btnPrintManual.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnPrintManual.BackColor = System.Drawing.Color.Black;
		this.btnPrintManual.BackColorHovered = System.Drawing.Color.Silver;
		this.btnPrintManual.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnPrintManual.BGColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.btnPrintManual.BorderColor = System.Drawing.Color.White;
		this.btnPrintManual.CornerRadius = 24;
		this.btnPrintManual.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnPrintManual.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnPrintManual.ForeColor = System.Drawing.Color.White;
		this.btnPrintManual.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnPrintManual.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnPrintManual.HoverEffect = false;
		this.btnPrintManual.ImageDisabled = null;
		this.btnPrintManual.ImageEnabled = null;
		this.btnPrintManual.ImageSize = new System.Drawing.Size(40, 40);
		this.btnPrintManual.LeftSideRounded = true;
		this.btnPrintManual.LeftTextOffset = 0;
		this.btnPrintManual.Location = new System.Drawing.Point(-50, 51);
		this.btnPrintManual.Name = "btnPrintManual";
		this.btnPrintManual.Pressed = false;
		this.btnPrintManual.RightSideRounded = true;
		this.btnPrintManual.RoundedCorners = true;
		this.btnPrintManual.ShowBorder = true;
		this.btnPrintManual.Size = new System.Drawing.Size(190, 60);
		this.btnPrintManual.TabIndex = 7;
		this.btnPrintManual.Text = "Ручной ввод";
		this.btnPrintManual.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnPrintManual.Click += new System.EventHandler(this.btnPrintManual_Click);
		this.btnPrintRecent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.btnPrintRecent.BackColor = System.Drawing.Color.Black;
		this.btnPrintRecent.BackColorHovered = System.Drawing.Color.Silver;
		this.btnPrintRecent.BackColorPressed = System.Drawing.Color.DarkGray;
		this.btnPrintRecent.BGColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.btnPrintRecent.BorderColor = System.Drawing.Color.White;
		this.btnPrintRecent.CornerRadius = 24;
		this.btnPrintRecent.DialogResult = System.Windows.Forms.DialogResult.None;
		this.btnPrintRecent.Font = new System.Drawing.Font("Microsoft Sans Serif", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btnPrintRecent.ForeColor = System.Drawing.Color.White;
		this.btnPrintRecent.ForeColorHovered = System.Drawing.Color.DodgerBlue;
		this.btnPrintRecent.ForeColorPressed = System.Drawing.Color.Lime;
		this.btnPrintRecent.HoverEffect = false;
		this.btnPrintRecent.ImageDisabled = null;
		this.btnPrintRecent.ImageEnabled = null;
		this.btnPrintRecent.ImageSize = new System.Drawing.Size(40, 40);
		this.btnPrintRecent.LeftSideRounded = true;
		this.btnPrintRecent.LeftTextOffset = 0;
		this.btnPrintRecent.Location = new System.Drawing.Point(150, 51);
		this.btnPrintRecent.Name = "btnPrintRecent";
		this.btnPrintRecent.Pressed = false;
		this.btnPrintRecent.RightSideRounded = true;
		this.btnPrintRecent.RoundedCorners = true;
		this.btnPrintRecent.ShowBorder = true;
		this.btnPrintRecent.Size = new System.Drawing.Size(215, 60);
		this.btnPrintRecent.TabIndex = 6;
		this.btnPrintRecent.Text = "Последние коды";
		this.btnPrintRecent.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.btnPrintRecent.Click += new System.EventHandler(this.btnPrintRecent_Click);
		this.pnlHeader.Controls.Add(this.roundedPanelHeader);
		this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
		this.pnlHeader.Location = new System.Drawing.Point(0, 0);
		this.pnlHeader.Name = "pnlHeader";
		this.pnlHeader.Size = new System.Drawing.Size(1270, 80);
		this.pnlHeader.TabIndex = 9;
		this.pnlHeader.Visible = false;
		this.roundedPanelHeader.BackColor = System.Drawing.Color.Transparent;
		this.roundedPanelHeader.BorderColor = System.Drawing.Color.Silver;
		this.roundedPanelHeader.Caption = null;
		this.roundedPanelHeader.Controls.Add(this.roundedPanelStationID);
		this.roundedPanelHeader.Controls.Add(this.rpClockContainer);
		this.roundedPanelHeader.Dock = System.Windows.Forms.DockStyle.Fill;
		this.roundedPanelHeader.DrawCaption = false;
		this.roundedPanelHeader.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelHeader.HeaderHeight = 20;
		this.roundedPanelHeader.LeftBottom = true;
		this.roundedPanelHeader.LeftTop = true;
		this.roundedPanelHeader.Location = new System.Drawing.Point(0, 0);
		this.roundedPanelHeader.Name = "roundedPanelHeader";
		this.roundedPanelHeader.PanelBackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.roundedPanelHeader.Radius = 18;
		this.roundedPanelHeader.RightBottom = true;
		this.roundedPanelHeader.RightTop = true;
		this.roundedPanelHeader.SelectCaption = false;
		this.roundedPanelHeader.ShowHeader = false;
		this.roundedPanelHeader.Size = new System.Drawing.Size(1270, 80);
		this.roundedPanelHeader.TabIndex = 18;
		this.roundedPanelHeader.Thickness = 2f;
		this.roundedPanelStationID.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.roundedPanelStationID.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.roundedPanelStationID.BorderColor = System.Drawing.Color.Black;
		this.roundedPanelStationID.Caption = "";
		this.roundedPanelStationID.Controls.Add(this.lblStationId);
		this.roundedPanelStationID.DrawCaption = false;
		this.roundedPanelStationID.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.roundedPanelStationID.HeaderBackColor = System.Drawing.Color.Silver;
		this.roundedPanelStationID.HeaderHeight = 20;
		this.roundedPanelStationID.LeftBottom = true;
		this.roundedPanelStationID.LeftTop = true;
		this.roundedPanelStationID.Location = new System.Drawing.Point(437, 11);
		this.roundedPanelStationID.Name = "roundedPanelStationID";
		this.roundedPanelStationID.PanelBackColor = System.Drawing.Color.Silver;
		this.roundedPanelStationID.Radius = 24;
		this.roundedPanelStationID.RightBottom = true;
		this.roundedPanelStationID.RightTop = true;
		this.roundedPanelStationID.SelectCaption = false;
		this.roundedPanelStationID.ShowHeader = false;
		this.roundedPanelStationID.Size = new System.Drawing.Size(808, 60);
		this.roundedPanelStationID.TabIndex = 1;
		this.roundedPanelStationID.Thickness = 0f;
		this.lblStationId.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblStationId.BackColor = System.Drawing.Color.Silver;
		this.lblStationId.ForeColor = System.Drawing.Color.Black;
		this.lblStationId.Location = new System.Drawing.Point(3, 4);
		this.lblStationId.Name = "lblStationId";
		this.lblStationId.Size = new System.Drawing.Size(796, 47);
		this.lblStationId.TabIndex = 0;
		this.lblStationId.Text = "..";
		this.lblStationId.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.rpClockContainer.BackColor = System.Drawing.Color.FromArgb(5, 37, 38);
		this.rpClockContainer.BorderColor = System.Drawing.Color.Black;
		this.rpClockContainer.Caption = "";
		this.rpClockContainer.Controls.Add(this.lblClock);
		this.rpClockContainer.DrawCaption = false;
		this.rpClockContainer.Font = new System.Drawing.Font("Tahoma", 20.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.rpClockContainer.HeaderBackColor = System.Drawing.Color.Silver;
		this.rpClockContainer.HeaderHeight = 20;
		this.rpClockContainer.LeftBottom = true;
		this.rpClockContainer.LeftTop = true;
		this.rpClockContainer.Location = new System.Drawing.Point(25, 11);
		this.rpClockContainer.Name = "rpClockContainer";
		this.rpClockContainer.PanelBackColor = System.Drawing.Color.Silver;
		this.rpClockContainer.Radius = 24;
		this.rpClockContainer.RightBottom = true;
		this.rpClockContainer.RightTop = true;
		this.rpClockContainer.SelectCaption = false;
		this.rpClockContainer.ShowHeader = false;
		this.rpClockContainer.Size = new System.Drawing.Size(393, 60);
		this.rpClockContainer.TabIndex = 0;
		this.rpClockContainer.Thickness = 0f;
		this.lblClock.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblClock.BackColor = System.Drawing.Color.Silver;
		this.lblClock.ForeColor = System.Drawing.Color.Black;
		this.lblClock.Location = new System.Drawing.Point(5, 4);
		this.lblClock.Name = "lblClock";
		this.lblClock.Size = new System.Drawing.Size(385, 47);
		this.lblClock.TabIndex = 0;
		this.lblClock.Text = "..";
		this.lblClock.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.Black;
		base.ClientSize = new System.Drawing.Size(1700, 942);
		base.Controls.Add(this.pnlMain);
		base.Controls.Add(this.pnlRight);
		this.Font = new System.Drawing.Font("Tahoma", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.Name = "Form3";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "AG";
		base.WindowState = System.Windows.Forms.FormWindowState.Maximized;
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form3_FormClosing);
		base.Load += new System.EventHandler(this.Form3_Load);
		this.pnlRight.ResumeLayout(false);
		this.roundedPanelRight.ResumeLayout(false);
		this.panelVideoControls.ResumeLayout(false);
		this.panelBoxActions.ResumeLayout(false);
		this.rpOrderInfo.ResumeLayout(false);
		this.rpOrderInfo.PerformLayout();
		this.rpOrderHeader.ResumeLayout(false);
		this.rpOrderHeader.PerformLayout();
		this.rpOrderSelect.ResumeLayout(false);
		this.pnlMain.ResumeLayout(false);
		this.pnlPagesHost.ResumeLayout(false);
		this.rpPagesContainer.ResumeLayout(false);
		this.pagedMain.ResumeLayout(false);
		this.pageMain.ResumeLayout(false);
		this.pageMain.PerformLayout();
		this.rpCamera.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.pbCamera).EndInit();
		this.pageLogs.ResumeLayout(false);
		this.pnlLogsPage.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.dgvLogs).EndInit();
		this.rpLogsNav.ResumeLayout(false);
		this.pnlFooter.ResumeLayout(false);
		this.tlpFooter.ResumeLayout(false);
		this.roundedPanelFooter.ResumeLayout(false);
		this.roundedPanelRepeatPrint.ResumeLayout(false);
		this.pnlHeader.ResumeLayout(false);
		this.roundedPanelHeader.ResumeLayout(false);
		this.roundedPanelStationID.ResumeLayout(false);
		this.rpClockContainer.ResumeLayout(false);
		base.ResumeLayout(false);
	}

	private void rpCloseApp_Click(object sender, EventArgs e)
	{
		OnCloseAppClicked(sender, e);
	}

	private void rbIncompleteLayer_Click(object sender, EventArgs e)
	{
	}

	private void rbIncompleteLayer_MouseClick(object sender, MouseEventArgs e)
	{
		OnIncompleteBoxClicked(sender, e);
	}

	private void rbStop_Click(object sender, EventArgs e)
	{
		OnStopClicked(sender, e);
	}

	private void rbStop_MouseDown(object sender, MouseEventArgs e)
	{
	}

	private void btnModeManual_Click(object sender, EventArgs e)
	{
		OnSingleFrameClicked(sender, e);
	}

	private void btnModeAuto_Click(object sender, EventArgs e)
	{
		OnRepeatLayerClicked(sender, e);
	}

	private void rbNewBox_Click(object sender, EventArgs e)
	{
		OnNewBoxClicked(sender, e);
	}

	private void rbResetErrors_Click(object sender, EventArgs e)
	{
		OnResetErrorsClicked(sender, e);
	}

	private void rbStart_Click(object sender, EventArgs e)
	{
		OnStartClicked(sender, e);
	}

	private void rbStart_MouseDown(object sender, MouseEventArgs e)
	{
	}

	private void orderSelect_Click(object sender, EventArgs e)
	{
		OnOrderSelectClicked(sender, e);
	}

	private void btnManualPrint_Click(object sender, EventArgs e)
	{
	}

	private void TryClearFramesDir()
	{
		try
		{
			string framesDir = Path.Combine(Application.StartupPath, "frames");
			if (!Directory.Exists(framesDir))
			{
				return;
			}
			FileInfo[] files = new DirectoryInfo(framesDir).GetFiles();
			foreach (FileInfo file in files)
			{
				try
				{
					file.Delete();
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private void lblOrderCaption_MouseDoubleClick(object sender, MouseEventArgs e)
	{
	}

	private void uiClockTimer_Tick(object sender, EventArgs e)
	{
		OnUiClockTick(sender, e);
	}

	private void pbCamera_Paint(object sender, PaintEventArgs e)
	{
		OnCameraPaint(sender, e);
	}

	private void pbCamera_MouseClick(object sender, MouseEventArgs e)
	{
		OnCameraMouseClick(sender, e);
	}

	private void ApplyAuthToUi()
	{
		btnLogsClear.Enabled = _isAdmin;
		tabLogs.Enabled = _isAdmin;
		tabLogs.ForeColor = (_isAdmin ? Color.White : Color.Gray);
		tabLogs.BorderColor = (_isAdmin ? Color.White : Color.Gray);
		btnPrintManual.Enabled = true;
		btnPrintManual.ForeColor = Color.White;
		btnPrintManual.BorderColor = Color.White;
	}

	private void SelectFooterTab(RoundedPanel tab)
	{
		RoundedPanel[] array = new RoundedPanel[2] { tabMain, tabLogs };
		foreach (RoundedPanel t in array)
		{
			if (t == tab)
			{
				t.ForeColor = Color.Black;
				t.PanelBackColor = Color.White;
				t.BorderColor = Color.White;
				continue;
			}
			t.PanelBackColor = Color.Black;
			if (t.Enabled)
			{
				t.ForeColor = Color.White;
				t.BorderColor = Color.White;
			}
			else
			{
				t.ForeColor = Color.Gray;
				t.BorderColor = Color.Gray;
			}
		}
	}

	private void pagedMain_PageChanged(object sender, PageChangedEventArgs e)
	{
		if (e.CurrentPage == pageLogs)
		{
			if (!tabLogs.Enabled)
			{
				pagedMain.SelectedIndex = 0;
				SelectFooterTab(tabMain);
			}
			else
			{
				SelectFooterTab(tabLogs);
				OnLogsPageShown(sender, e);
			}
		}
		else if (e.CurrentPage == pageMain)
		{
			SelectFooterTab(tabMain);
		}
	}

	private void dgvLogs_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
	{
		OnLogsCellFormatting(sender, e);
	}

	private void btnLogsClear_Click(object sender, EventArgs e)
	{
		OnLogsClearClicked(sender, e);
	}

	private void btnLogsRefresh_Click(object sender, EventArgs e)
	{
		OnLogsRefreshClicked(sender, e);
	}

	private void btnLogsFirst_Click(object sender, EventArgs e)
	{
		OnLogsFirstClicked(sender, e);
	}

	private void btnLogsPrev_Click(object sender, EventArgs e)
	{
		OnLogsPrevClicked(sender, e);
	}

	private void btnLogsNext_Click(object sender, EventArgs e)
	{
		OnLogsNextClicked(sender, e);
	}

	private void btnLogsLast_Click(object sender, EventArgs e)
	{
		OnLogsLastClicked(sender, e);
	}

	private void btnPrintManual_MouseDown(object sender, MouseEventArgs e)
	{
		if (btnPrintManual.Enabled)
		{
			OnManualPrintClicked(sender, EventArgs.Empty);
		}
	}

	private void tabLogs_MouseDown(object sender, MouseEventArgs e)
	{
		if (tabLogs.Enabled)
		{
			SelectFooterTab(tabLogs);
			pagedMain.SelectedIndex = 1;
		}
	}

	private void tabMain_MouseDown(object sender, MouseEventArgs e)
	{
		SelectFooterTab(tabMain);
		pagedMain.SelectedIndex = 0;
	}

	private void btnPrintRecent_Click(object sender, EventArgs e)
	{
		OnPrintRecentClicked(sender, e);
	}

	private void btnSettings_Click(object sender, EventArgs e)
	{
		OnSettingsClicked(sender, e);
	}

	private void Form3_FormClosing(object sender, FormClosingEventArgs e)
	{
		OnFormClosing(sender, e);
	}

	private void Form3_Load(object sender, EventArgs e)
	{
		OnFormLoad(sender, e);
	}

	private void btnCloseApp_MouseDown(object sender, MouseEventArgs e)
	{
		OnCloseAppClicked(sender, e);
	}

	private void btnPrintManual_Click(object sender, EventArgs e)
	{
		OnManualPrintClicked(sender, e);
	}

	private void btnHelp_Click(object sender, EventArgs e)
	{
		OnHelpClicked(sender, e);
	}
}
