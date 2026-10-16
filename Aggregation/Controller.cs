using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AG.Entities;
using Aggregation.Exceptions;
using Aggregation.LensV2;
using Aggregation.Settings;
using Baumer;
using ComputarLens;
using DBLogger;
using DeviceInterfaces.Camera;
using DeviceInterfaces.L3;
using DeviceInterfaces.Lens;
using DeviceInterfaces.Module;
using DeviceInterfaces.Recognizer;
using L3Service;
using NLog;
using Recognizer2;

namespace Aggregation;

public class Controller : IDisposable
{
	private static readonly Logger logger = LogManager.GetCurrentClassLogger();

	private readonly AppSettingsManager _config;

	private volatile bool _isDisposed;

	private readonly object _disposeLock = new object();

	private readonly object _lensOpLock = new object();

	private volatile ILensController _lens;

	private volatile IImageSource _cam;

	private volatile IDatamatrixRecognizer _datamatrixRecognizer;

	private const double CmToMm = 10.0;

	private readonly int _lensRetryCount;

	private readonly int _lensRetryDelayMs;

	private readonly ushort _focusHomeAddr;

	private readonly ushort _zoomHomeAddr;

	private readonly ushort _irisHomeAddr;

	private readonly int _focusCalibrationInterval;

	private readonly BoxWorkflowConfig _boxWorkflowConfig;

	private readonly string _printerName;

	private readonly int _printerCopiesDefault;

	private readonly bool _useServerTemplate;

	private readonly string _equipmentName;

	private readonly string _applicationInfo;

	private readonly LensLookup _lensLookup;

	private readonly bool _useLookup;

	private readonly IL3Service _l3Service;

	private readonly DeviceInterfaces.Module.ILogger _dbLogger;

	private readonly CodeCacheManager _codeCacheManager;

	private readonly AggregationManager _aggregationManager;

	private readonly string _localPath;

	private volatile Order _currentOrder;

	private volatile bool _work;

	public AppSettingsManager Config => _config;

	public BoxWorkflowConfig BoxWorkflowConfig => _boxWorkflowConfig;

	public int PrinterCopiesDefault => _printerCopiesDefault;

	public bool UseServerTemplate => _useServerTemplate;

	public string EquipmentName => _equipmentName;

	public string ApplicationInfo => _applicationInfo;

	public bool UseLookupTable => _useLookup;

	public LensLookup LensLookup => _lensLookup;

	public bool IsLookupAvailable => _useLookup;

	public bool AdminMode { get; set; }

	public Order CurrentOrder
	{
		get
		{
			return _currentOrder;
		}
		set
		{
			_currentOrder = value;
		}
	}

	public bool IsCameraConnected
	{
		get
		{
			if (_cam != null)
			{
				return _cam.IsConnected();
			}
			return false;
		}
	}

	public bool IsLensConnected
	{
		get
		{
			if (_lens != null)
			{
				return _lens.IsConnected();
			}
			return false;
		}
	}

	public string CameraName
	{
		get
		{
			if (_cam == null)
			{
				return null;
			}
			return _cam.GetDeviceName();
		}
	}

	public string LensName
	{
		get
		{
			if (_lens == null)
			{
				return null;
			}
			return _lens.GetDeviceName();
		}
	}

	public int FocusCalibrationInterval => _focusCalibrationInterval;

	public event ImageReceivedHandler OnImageReceived;

	public event EventHandler OnCloseRequest;

	public Controller(string localPath)
	{
		_localPath = localPath;
		logger.Trace("Controller initializing...");
		_config = new AppSettingsManager
		{
			WriteChecksum = true,
			EnforceChecksum = false,
			AutoBackup = true
		};
		_lensRetryCount = ClampInt(_config.GetInt("lens_retry_count", 3), 1, 10);
		_lensRetryDelayMs = ClampInt(_config.GetInt("lens_retry_delay_ms", 300), 50, 2000);
		_focusHomeAddr = _config.GetUShort("lens_focus_home", 0);
		_zoomHomeAddr = _config.GetUShort("lens_zoom_home", 0);
		_irisHomeAddr = _config.GetUShort("lens_iris_home", 0);
		_focusCalibrationInterval = ClampInt(_config.GetInt("lens_focus_calibration_interval", 10), 0, 1000);
		string lookupPath = FindLookupFile();
		_lensLookup = LensLookup.Load(lookupPath);
		_useLookup = _lensLookup.HasData;
		if (_useLookup)
		{
			(double, double)? range = _lensLookup.GetHeightRange();
			logger.Info($"LensLookup: ENABLED, {_lensLookup.Count} entries, height range {range?.Item1:F0}-{range?.Item2:F0}mm");
		}
		else
		{
			logger.Error("══════════════════════════════════════════════════════════");
			logger.Error("  LENS LOOKUP TABLE NOT FOUND (lens.lookup)");
			logger.Error("  Automatic layer focusing WILL NOT WORK.");
			logger.Error("  Place lens.lookup file in application directory.");
			logger.Error("══════════════════════════════════════════════════════════");
		}
		try
		{
			logger.Trace("Initializing Baumer camera...");
			BaumerLXGSeries cam = new BaumerLXGSeries();
			cam.Init();
			cam.OnImageReceived += Camera_OnImageReceived;
			_cam = cam;
			logger.Info("Camera initialized successfully");
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Camera initialization failed");
			_cam = null;
		}
		try
		{
			logger.Trace("Initializing Computar lens...");
			Computar lens = new Computar();
			lens.Init();
			_lens = lens;
			logger.Info("Lens initialized: " + lens.GetDeviceName());
		}
		catch (Exception exception2)
		{
			logger.Error(exception2, "Lens initialization failed");
			_lens = null;
		}
		try
		{
			_datamatrixRecognizer = new RecognizerV2();
			logger.Info("Recognizer initialized");
		}
		catch (Exception exception3)
		{
			logger.Error(exception3, "Recognizer initialization failed");
			_datamatrixRecognizer = null;
		}
		string l3Url = _config.Get("l3_url", "");
		string l3Login = _config.Get("l3_login", "");
		string l3Password = _config.Get("l3_password", "");
		string l3Token = _config.Get("l3_token", "");
		string l3EquipId = _config.Get("l3_equip_id", "");
		string l3Timeout = _config.Get("l3_server_timeout", "6");
		_l3Service = new TraceWayService(l3Url, l3Login, l3Password, l3Token, l3EquipId, l3Timeout);
		_dbLogger = new SQLiteLogger();
		_codeCacheManager = new CodeCacheManager();
		_aggregationManager = new AggregationManager();
		_printerName = _config.Get("PRINTER_name", "");
		_printerCopiesDefault = ClampInt(_config.GetInt("PRINTER_copies_default", 1), 1, 10);
		string templateSource = _config.Get("PRINTER_template_source", "server");
		_useServerTemplate = templateSource.Equals("server", StringComparison.OrdinalIgnoreCase);
		_equipmentName = _config.Get("EQUIPMENT_name", "");
		_applicationInfo = _config.Get("application_info", "");
		_boxWorkflowConfig = new BoxWorkflowConfig
		{
			AutoStartNextBox = _config.GetBool("box_workflow_auto_start")
		};
		LogStartupConfig();
		logger.Info("Controller initialization complete");
	}

	private static int ClampInt(int value, int min, int max)
	{
		return Math.Max(min, Math.Min(max, value));
	}

	private static double ClampDouble(double value, double min, double max)
	{
		return Math.Max(min, Math.Min(max, value));
	}

	private string FindLookupFile()
	{
		string[] paths = new string[4]
		{
			Path.Combine(_localPath, "lens.lookup"),
			Path.Combine(_localPath, "Settings", "lens.lookup"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lens.lookup"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "lens.lookup")
		};
		string[] array = paths;
		foreach (string path in array)
		{
			if (File.Exists(path))
			{
				logger.Info("LensLookup: Found at '" + path + "'");
				return path;
			}
		}
		logger.Warn("LensLookup: File not found in any location");
		return paths[0];
	}

	private void LogStartupConfig()
	{
		logger.Info("=== Controller Configuration ===");
		logger.Info("  Application Info: " + _applicationInfo);
		logger.Info("  Equipment: " + _equipmentName);
		logger.Info($"  Lens - Focus home: {_focusHomeAddr}, Zoom home: {_zoomHomeAddr}, Iris home: {_irisHomeAddr}");
		logger.Info($"  Lens - Focus calibration interval: {_focusCalibrationInterval} boxes");
		logger.Info($"  Lens - Retry: {_lensRetryCount}x, delay {_lensRetryDelayMs}ms");
		logger.Info($"  Lookup - Enabled: {_useLookup}, Entries: {_lensLookup.Count}");
		logger.Info($"  Printer: {_printerName}, Copies: {_printerCopiesDefault}");
		logger.Info("  Template source: " + (_useServerTemplate ? "server" : "local"));
		logger.Info("  Camera: " + (IsCameraConnected ? "connected" : "not connected"));
		logger.Info("  Lens: " + (IsLensConnected ? "connected" : "not connected"));
		logger.Info("================================");
	}

	private void Camera_OnImageReceived(Image image)
	{
		if (_isDisposed)
		{
			image?.Dispose();
			return;
		}
		try
		{
			OnImageReceived?.Invoke(image);
		}
		catch (Exception exception)
		{
			logger.Warn(exception, "OnImageReceived handler threw exception");
			image?.Dispose();
		}
	}

	private LensOperationResult ExecuteLensOperationWithRetry(Func<int> operation, string operationName)
	{
		ILensController lens = _lens;
		int lastErrorCode = -1;
		string lastErrorText = null;
		for (int attempt = 1; attempt <= _lensRetryCount; attempt++)
		{
			if (_isDisposed)
			{
				return LensOperationResult.Disposed(attempt);
			}
			try
			{
				int result;
				lock (_lensOpLock)
				{
					if (_isDisposed)
					{
						return LensOperationResult.Disposed(attempt);
					}
					result = operation();
				}
				if (result == 0)
				{
					if (attempt > 1)
					{
						logger.Info($"{operationName}: Succeeded on attempt {attempt}/{_lensRetryCount}");
					}
					return LensOperationResult.Ok(attempt);
				}
				lastErrorCode = result;
				lastErrorText = lens?.GetErrorText(result) ?? $"Unknown error {result}";
				logger.Warn($"{operationName}: Attempt {attempt}/{_lensRetryCount} failed - {lastErrorText}");
			}
			catch (ObjectDisposedException)
			{
				return LensOperationResult.Disposed(attempt);
			}
			catch (Exception ex2)
			{
				lastErrorCode = -1;
				lastErrorText = ex2.Message;
				logger.Warn(ex2, $"{operationName}: Attempt {attempt}/{_lensRetryCount} threw exception");
			}
			if (attempt < _lensRetryCount)
			{
				Thread.Sleep(_lensRetryDelayMs);
			}
		}
		logger.Error($"{operationName}: All {_lensRetryCount} attempts failed. Last error: {lastErrorText}");
		return LensOperationResult.Fail(lastErrorCode, _lensRetryCount, lastErrorText);
	}

	private bool TryGetFocusForLayer(int layerNumber, out ushort focusAddr)
	{
		focusAddr = 0;
		Order order = _currentOrder;
		if (order == null || order.LayerHeight <= 0 || layerNumber <= 0)
		{
			logger.Warn($"TryGetFocusForLayer: Invalid parameters - order={order != null}, layerHeight={order?.LayerHeight}, layer={layerNumber}");
			return false;
		}
		if (!_useLookup)
		{
			logger.Error("TryGetFocusForLayer: Lookup table not available - lens.lookup file missing?");
			return false;
		}
		double heightMm = (double)order.LayerHeight * 10.0 * (double)layerNumber;
		if (!_lensLookup.TryGetFocus(heightMm, out focusAddr))
		{
			logger.Error($"TryGetFocusForLayer: Lookup failed for height={heightMm:F1}mm");
			return false;
		}
		logger.Info($"Focus: Layer {layerNumber}, height={heightMm:F1}mm -> {focusAddr}");
		return true;
	}

	private bool TryGetFocusForHeight(double heightMm, out ushort focusAddr)
	{
		focusAddr = 0;
		if (heightMm < 0.0)
		{
			heightMm = 0.0;
		}
		if (!_useLookup)
		{
			logger.Error("TryGetFocusForHeight: Lookup table not available - lens.lookup file missing?");
			return false;
		}
		return _lensLookup.TryGetFocus(heightMm, out focusAddr);
	}

	internal LensOperationResult FocusInitWithRetry()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		return ExecuteLensOperationWithRetry(() => lens.FocusInit(), "FocusInit");
	}

	internal LensOperationResult FocusLayerWithRetry(int layerNumber)
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		if (!TryGetFocusForLayer(layerNumber, out var targetPos))
		{
			return LensOperationResult.Fail(-10, 0, "Cannot calculate focus position");
		}
		return ExecuteLensOperationWithRetry(() => lens.FocusTo(targetPos), $"FocusLayer({layerNumber})");
	}

	internal LensOperationResult FocusToAddressWithRetry(ushort address)
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		logger.Info($"FocusToAddressWithRetry: {address}");
		return ExecuteLensOperationWithRetry(() => lens.FocusTo(address), $"FocusToAddress({address})");
	}

	internal LensOperationResult FocusToHeightWithRetry(double heightMm)
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		if (!TryGetFocusForHeight(heightMm, out var targetPos))
		{
			return LensOperationResult.Fail(-11, 0, $"Cannot calculate focus for height {heightMm}mm");
		}
		return ExecuteLensOperationWithRetry(() => lens.FocusTo(targetPos), $"FocusToHeight({heightMm:F1}mm)");
	}

	internal LensOperationResult FocusHomeWithRetry()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		if (!_useLookup || !_lensLookup.TryGetFocus(0.0, out var targetPos))
		{
			if (_focusHomeAddr <= 0)
			{
				return LensOperationResult.Fail(-3, 0, "Focus home not configured");
			}
			targetPos = _focusHomeAddr;
		}
		return ExecuteLensOperationWithRetry(() => lens.FocusTo(targetPos), "FocusHome");
	}

	internal LensOperationResult ZoomInitWithRetry()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		return ExecuteLensOperationWithRetry(() => lens.ZoomInit(), "ZoomInit");
	}

	internal LensOperationResult ZoomHomeWithRetry()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		if (_zoomHomeAddr == 0)
		{
			return LensOperationResult.Fail(-3, 0, "Zoom home not configured");
		}
		return ExecuteLensOperationWithRetry(() => lens.ZoomTo(_zoomHomeAddr), "ZoomHome");
	}

	internal LensOperationResult IrisInitWithRetry()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		return ExecuteLensOperationWithRetry(() => lens.IrisInit(), "IrisInit");
	}

	internal LensOperationResult IrisHomeWithRetry()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return LensOperationResult.LensNotAvailable("Lens not available");
		}
		if (_irisHomeAddr == 0)
		{
			return LensOperationResult.Fail(-3, 0, "Iris home not configured");
		}
		return ExecuteLensOperationWithRetry(() => lens.IrisTo(_irisHomeAddr), "IrisHome");
	}

	internal int focusInit()
	{
		if (!FocusInitWithRetry().Success)
		{
			return -1;
		}
		return 0;
	}

	internal void focusLayer(int layer)
	{
		FocusLayerWithRetry(layer);
	}

	internal void focusHome()
	{
		FocusHomeWithRetry();
	}

	internal void zoomHome()
	{
		ZoomHomeWithRetry();
	}

	internal void irisHome()
	{
		IrisHomeWithRetry();
	}

	internal int focusCurrentPos()
	{
		ILensController lens = _lens;
		if (lens == null || !lens.IsConnected())
		{
			return 0;
		}
		lock (_lensOpLock)
		{
			try
			{
				return lens.GetFocusPos();
			}
			catch
			{
				return 0;
			}
		}
	}

	public Image GetSingleImage()
	{
		IImageSource cam = _cam;
		if (cam == null || !cam.IsConnected())
		{
			return null;
		}
		try
		{
			return cam.GetFrame();
		}
		catch (Exception exception)
		{
			logger.Error(exception, "GetFrame failed");
			return null;
		}
	}

	public void StartImageReceiving()
	{
		InfoMessage("СТАРТ: онлайн-распознавание");
		_work = true;
		_cam?.StartImageReceiving();
	}

	public void StopImageReceiving()
	{
		InfoMessage("СТОП: онлайн-распознавание");
		_work = false;
		_cam?.StopImageReceiving();
	}

	public bool CamHasInvalidSettings()
	{
		return (_cam as BaumerLXGSeries)?.isInvalidSettings() ?? false;
	}

	public void SetCameraCanvasHandle(IntPtr handle)
	{
	}

	public async Task<List<RecognitionResult>> RecognizeDatamatrixAsync(Image img)
	{
		IDatamatrixRecognizer recognizer = _datamatrixRecognizer;
		if (recognizer == null)
		{
			throw new InvalidOperationException("Recognizer not initialized");
		}
		try
		{
			return await recognizer.RecognizeAsync(img).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (ServiceUnavailableException)
		{
			throw new RecognizeServiceUnavailableException();
		}
	}

	public void NewBox()
	{
		InfoMessage("Начат новый короб");
	}

	public void InfoMessage(string msg, string module = "", string code = "")
	{
		try
		{
			_dbLogger?.Info(DateTime.Now, msg, module, code);
		}
		catch
		{
		}
	}

	public void TraceMessage(string msg, string module = "", string code = "")
	{
		try
		{
			_dbLogger?.Trace(DateTime.Now, msg, module, code);
		}
		catch
		{
		}
	}

	public void ErrorMessage(string msg, string module, string code)
	{
		try
		{
			_dbLogger?.Error(DateTime.Now, msg, module, code);
		}
		catch
		{
		}
	}

	public async Task<List<LogEvent>> GetLogsByPageAsync(int page)
	{
		return await Task.Run(() =>
		{
			try
			{
				return _dbLogger?.GetEventsByPage(page) ?? new List<LogEvent>();
			}
			catch
			{
				return new List<LogEvent>();
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<int> GetLogsCountAsync()
	{
		return await Task.Run(() =>
		{
			try
			{
				return _dbLogger?.GetEventsCount() ?? 0;
			}
			catch
			{
				return 0;
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public void ClearAllLogs()
	{
		try
		{
			_dbLogger?.ClearAllLogs();
		}
		catch
		{
		}
	}

	public async Task<List<LogEvent>> GetRecentLogsAsync(int count)
	{
		return await Task.Run(() =>
		{
			try
			{
				return _dbLogger?.GetRecentEvents(count) ?? new List<LogEvent>();
			}
			catch
			{
				return new List<LogEvent>();
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<string> GetGroupCode(List<string> codes)
	{
		return await _l3Service.GetAggregationCode(codes).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<string> CheckCodes(List<string> codes)
	{
		Order order = _currentOrder;
		if (order == null)
		{
			throw new InvalidOperationException("No order selected");
		}
		return await _l3Service.CheckCodes(order.Id, codes).ConfigureAwait(continueOnCapturedContext: false);
	}

	public Task<(bool SsccExists, string Message)> GetCodeInfo(string code)
	{
		return _l3Service.GetCodeInfo(code);
	}

	internal async Task<OrdersPage> GetOrdersFromL3Async()
	{
		return await _l3Service.GetOrdersPage().ConfigureAwait(continueOnCapturedContext: false);
	}

	internal void CacheCodes(Dictionary<int, Dictionary<string, RecognitionResult>> resultsDictionary)
	{
		if (resultsDictionary == null)
		{
			return;
		}
		try
		{
			foreach (KeyValuePair<int, Dictionary<string, RecognitionResult>> layerKvp in resultsDictionary)
			{
				if (layerKvp.Value == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, RecognitionResult> codeKvp in layerKvp.Value)
				{
					_codeCacheManager?.AddToCache(DateTime.Now, codeKvp.Key);
				}
			}
			InfoMessage("Коды сохранены в кэш.");
		}
		catch (Exception exception)
		{
			logger.Warn(exception, "CacheCodes failed");
		}
	}

	internal void Aggregate(string groupCode, Dictionary<int, Dictionary<string, RecognitionResult>> codes)
	{
		_aggregationManager?.Add(DateTime.Now, groupCode, codes);
		InfoMessage("Получен групповой код: " + groupCode);
	}

	public AggregationEntity GetBoxInfo(int id)
	{
		return _aggregationManager?.GetById(id);
	}

	internal void ClearAllArchive()
	{
		_aggregationManager?.ClearAll();
	}

	internal async Task<List<AggregationEntity>> GetLastNGroupCodes(int n)
	{
		return await Task.Run(() =>
		{
			try
			{
				return _aggregationManager?.GetLastN(n) ?? new List<AggregationEntity>();
			}
			catch
			{
				return new List<AggregationEntity>();
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<List<AggregationEntity>> GetAggregationsByPageAsync(int page)
	{
		return await Task.Run(() =>
		{
			try
			{
				return _aggregationManager?.GetByPage(page) ?? new List<AggregationEntity>();
			}
			catch
			{
				return new List<AggregationEntity>();
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<int> GetHistoryCountAsync()
	{
		return await Task.Run(() =>
		{
			try
			{
				return _aggregationManager?.GetCount() ?? 0;
			}
			catch
			{
				return 0;
			}
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<(string TemplateData, string TemplateName, bool FromServer)> GetTemplateForCodeAsync(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return (TemplateData: null, TemplateName: null, FromServer: false);
		}
		try
		{
			(string, string, Order) result = await _l3Service.GetTemplateByCode(code).ConfigureAwait(continueOnCapturedContext: false);
			if (!string.IsNullOrWhiteSpace(result.Item2))
			{
				return (TemplateData: result.Item2, TemplateName: result.Item1, FromServer: true);
			}
		}
		catch (Exception exception)
		{
			logger.Warn(exception, "GetTemplateByCode failed");
		}
		return (TemplateData: null, TemplateName: null, FromServer: false);
	}

	public string GetDefaultTemplate()
	{
		try
		{
			string path = Path.Combine(_localPath, "template.txt");
			if (File.Exists(path))
			{
				return File.ReadAllText(path);
			}
		}
		catch (Exception exception)
		{
			logger.Warn(exception, "GetDefaultTemplate failed");
		}
		return null;
	}

	public bool SaveDefaultTemplate(string content)
	{
		try
		{
			File.WriteAllText(Path.Combine(_localPath, "template.txt"), content ?? string.Empty);
			return true;
		}
		catch (Exception exception)
		{
			logger.Error(exception, "SaveDefaultTemplate failed");
			return false;
		}
	}

	public static bool IsTemplateValid(string templateData)
	{
		if (!string.IsNullOrWhiteSpace(templateData))
		{
			return templateData.Contains("{{CODE}}");
		}
		return false;
	}

	internal bool printCode(string code, string templateData, out string error, int copies = 1)
	{
		error = null;
		if (_config.GetBool("debug_printer_bypass"))
		{
			InfoMessage("DEBUG: Printer bypass - " + code, "DEBUG", "PRINTER");
			return true;
		}
		if (!IsTemplateValid(templateData))
		{
			error = "Template invalid";
			return false;
		}
		int cnt = Math.Max(1, Math.Min(10, copies));
		for (int i = 0; i < cnt; i++)
		{
			if (!RawPrinterHelper.TrySendStringToPrinter(code, templateData, out error, _printerName))
			{
				ErrorMessage("Print error: " + error, "PRINT", "002");
				return false;
			}
		}
		return true;
	}

	internal bool printCode(string code, out string error, int copies = 1)
	{
		error = null;
		Order order = _currentOrder;
		string templateData = ((!_useServerTemplate || order == null || string.IsNullOrWhiteSpace(order.TemplateData)) ? GetDefaultTemplate() : order.TemplateData);
		if (string.IsNullOrWhiteSpace(templateData))
		{
			error = "No template found";
			return false;
		}
		return printCode(code, templateData, out error, copies);
	}

	internal bool printCode(string code, out string error)
	{
		return printCode(code, out error, 1);
	}

	internal void Close()
	{
		OnCloseRequest?.Invoke(this, EventArgs.Empty);
	}

	internal void GlobalStop()
	{
		_work = false;
		IImageSource cam = _cam;
		if (cam != null)
		{
			try
			{
				cam.OnImageReceived -= Camera_OnImageReceived;
			}
			catch
			{
			}
			try
			{
				cam.StopImageReceiving();
			}
			catch
			{
			}
			try
			{
				cam.Finish();
			}
			catch
			{
			}
		}
	}

	public void Free()
	{
		Dispose();
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_isDisposed)
		{
			return;
		}
		lock (_disposeLock)
		{
			if (_isDisposed)
			{
				return;
			}
			_isDisposed = true;
		}
		logger.Info("Controller disposing...");
		IImageSource cam = _cam;
		_cam = null;
		if (cam != null)
		{
			try
			{
				cam.OnImageReceived -= Camera_OnImageReceived;
			}
			catch
			{
			}
			try
			{
				cam.StopImageReceiving();
			}
			catch
			{
			}
			try
			{
				cam.Finish();
			}
			catch
			{
			}
		}
		ILensController lens = _lens;
		_lens = null;
		IDisposable disposableLens = lens;
		if (disposableLens != null)
		{
			try
			{
				disposableLens.Dispose();
			}
			catch
			{
			}
		}
		try
		{
			_config?.Dispose();
		}
		catch
		{
		}
		if (disposing)
		{
			logger.Info("Controller disposed");
		}
	}

	~Controller()
	{
		Dispose(disposing: false);
	}
}
