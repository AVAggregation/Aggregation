using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using NLog;

namespace Aggregation.Settings;

public class AppSettingsManager : IDisposable
{
	private static readonly Logger _log = LogManager.GetCurrentClassLogger();

	private const string CHECKSUM_KEY = "__checksum__";

	private const int MAX_BACKUPS = 10;

	private readonly string _configPath;

	private readonly string _backupDir;

	private readonly object _lock = new object();

	private Dictionary<string, string> _cache;

	private bool _disposed;

	public bool WriteChecksum { get; set; } = true;

	public bool EnforceChecksum { get; set; }

	public bool AutoBackup { get; set; } = true;

	public AppSettingsManager(string configPath = null)
	{
		_configPath = configPath ?? AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
		_backupDir = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "config_backups");
		Directory.CreateDirectory(_backupDir);
		Reload();
		_log.Info("AppSettingsManager: " + _configPath);
	}

	public string Get(string key, string defaultValue = null)
	{
		lock (_lock)
		{
			string val;
			return _cache.TryGetValue(key, out val) ? val : defaultValue;
		}
	}

	public int GetInt(string key, int defaultValue = 0)
	{
		if (!int.TryParse(Get(key), out var v))
		{
			return defaultValue;
		}
		return v;
	}

	public ushort GetUShort(string key, ushort defaultValue = 0)
	{
		if (!ushort.TryParse(Get(key), out var v))
		{
			return defaultValue;
		}
		return v;
	}

	public double GetDouble(string key, double defaultValue = 0.0)
	{
		if (!double.TryParse(Get(key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
		{
			return defaultValue;
		}
		return v;
	}

	public bool GetBool(string key, bool defaultValue = false)
	{
		if (!bool.TryParse(Get(key), out var v))
		{
			return defaultValue;
		}
		return v;
	}

	public IReadOnlyDictionary<string, string> GetAll()
	{
		lock (_lock)
		{
			return _cache.Where((KeyValuePair<string, string> kv) => !kv.Key.StartsWith("__")).ToDictionary((KeyValuePair<string, string> kv) => kv.Key, (KeyValuePair<string, string> kv) => kv.Value);
		}
	}

	public void Set(string key, string value)
	{
		if (key.StartsWith("__"))
		{
			throw new ArgumentException("Keys mit '__' sind reserviert");
		}
		lock (_lock)
		{
			_cache[key] = value ?? "";
		}
	}

	public void Set(string key, int value)
	{
		Set(key, value.ToString());
	}

	public void Set(string key, ushort value)
	{
		Set(key, value.ToString());
	}

	public void Set(string key, double value)
	{
		Set(key, value.ToString(CultureInfo.InvariantCulture));
	}

	public void Set(string key, bool value)
	{
		Set(key, value.ToString().ToLower());
	}

	public void Save()
	{
		lock (_lock)
		{
			if (AutoBackup)
			{
				CreateBackup("auto");
			}
			XDocument doc = XDocument.Load(_configPath);
			XElement appSettings = doc.Root.Element("appSettings");
			if (appSettings == null)
			{
				appSettings = new XElement("appSettings");
				doc.Root.Add(appSettings);
			}
			foreach (KeyValuePair<string, string> kv in _cache.Where((KeyValuePair<string, string> x) => !x.Key.StartsWith("__")))
			{
				SetXmlValue(appSettings, kv.Key, kv.Value);
			}
			if (WriteChecksum)
			{
				string checksum = CalculateChecksum(appSettings);
				SetXmlValue(appSettings, "__checksum__", checksum);
				_cache["__checksum__"] = checksum;
			}
			string temp = _configPath + ".tmp";
			doc.Save(temp);
			File.Replace(temp, _configPath, null);
			_log.Info("Config gespeichert (" + _cache.Count + " Einträge)");
		}
	}

	public void Reload()
	{
		lock (_lock)
		{
			_cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(_configPath))
			{
				_log.Warn("Config nicht gefunden: " + _configPath);
				return;
			}
			XElement appSettings = XDocument.Load(_configPath).Root.Element("appSettings");
			if (appSettings == null)
			{
				return;
			}
			foreach (XElement el in appSettings.Elements("add"))
			{
				string key = ((el.Attribute("key") != null) ? el.Attribute("key").Value : null);
				string val = ((el.Attribute("value") != null) ? el.Attribute("value").Value : null);
				if (!string.IsNullOrEmpty(key))
				{
					_cache[key] = val ?? "";
				}
			}
			if (_cache.TryGetValue("__checksum__", out var stored) && !string.IsNullOrEmpty(stored))
			{
				string current = CalculateChecksum(appSettings);
				if (stored != current)
				{
					string msg = "Config-Checksum stimmt nicht überein (externe Änderung?)";
					if (EnforceChecksum)
					{
						throw new InvalidOperationException(msg);
					}
					_log.Warn(msg);
				}
			}
			_log.Debug("Config geladen (" + _cache.Count + " Einträge)");
		}
	}

	public void UpdateChecksum()
	{
		lock (_lock)
		{
			XDocument doc = XDocument.Load(_configPath);
			XElement appSettings = doc.Root.Element("appSettings");
			if (appSettings != null)
			{
				string checksum = CalculateChecksum(appSettings);
				SetXmlValue(appSettings, "__checksum__", checksum);
				string temp = _configPath + ".tmp";
				doc.Save(temp);
				File.Replace(temp, _configPath, null);
				_cache["__checksum__"] = checksum;
				_log.Info("Checksum aktualisiert");
			}
		}
	}

	public void RemoveChecksum()
	{
		lock (_lock)
		{
			XDocument xDocument = XDocument.Load(_configPath);
			xDocument.Root.Element("appSettings")?.Elements("add").FirstOrDefault((XElement e) => e.Attribute("key") != null && e.Attribute("key").Value == "__checksum__")?.Remove();
			string temp = _configPath + ".tmp";
			xDocument.Save(temp);
			File.Replace(temp, _configPath, null);
			_cache.Remove("__checksum__");
			_log.Info("Checksum entfernt");
		}
	}

	private string CalculateChecksum(XElement appSettings)
	{
		IEnumerable<string> data = from e in appSettings.Elements("add")
			select new
			{
				Key = ((e.Attribute("key") != null) ? e.Attribute("key").Value : ""),
				Val = ((e.Attribute("value") != null) ? e.Attribute("value").Value : "")
			} into x
			where !x.Key.StartsWith("__")
			orderby x.Key
			select x.Key + "=" + x.Val;
		using SHA256 sha = SHA256.Create();
		return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", data)))).Substring(0, 16);
	}

	public string CreateBackup(string reason = null)
	{
		string name = string.Format("config_{0}_{1}.xml", DateTime.Now.ToString("yyyyMMdd_HHmmss"), reason ?? "manual");
		string path = Path.Combine(_backupDir, name);
		File.Copy(_configPath, path, overwrite: true);
		_log.Info("Backup: " + name);
		CleanupBackups();
		return path;
	}

	public List<string> GetBackups()
	{
		return (from f in Directory.GetFiles(_backupDir, "config_*.xml")
			orderby f descending
			select f).ToList();
	}

	public bool RestoreBackup(string path)
	{
		if (!File.Exists(path))
		{
			_log.Error("Backup nicht gefunden: " + path);
			return false;
		}
		try
		{
			XDocument.Load(path);
			CreateBackup("pre-restore");
			File.Copy(path, _configPath, overwrite: true);
			Reload();
			_log.Info("Backup wiederhergestellt: " + Path.GetFileName(path));
			return true;
		}
		catch (Exception exception)
		{
			_log.Error(exception, "Restore fehlgeschlagen");
			return false;
		}
	}

	private void CleanupBackups()
	{
		foreach (string f in GetBackups().Skip(10))
		{
			try
			{
				File.Delete(f);
			}
			catch
			{
			}
		}
	}

	private static void SetXmlValue(XElement appSettings, string key, string value)
	{
		XElement el = appSettings.Elements("add").FirstOrDefault((XElement e) => e.Attribute("key") != null && e.Attribute("key").Value == key);
		if (el != null)
		{
			el.SetAttributeValue("value", value);
			return;
		}
		appSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
	}

	public void Dispose()
	{
		_disposed = true;
		GC.SuppressFinalize(this);
	}
}
