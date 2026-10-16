using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NLog;

namespace Aggregation.LensV2;

public class LensLookup
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	private readonly List<(double Height, ushort Focus)> _data = new List<(double, ushort)>();

	public int Count => _data.Count;

	public bool HasData => _data.Count > 0;

	public static LensLookup Load(string filePath)
	{
		LensLookup lookup = new LensLookup();
		if (!File.Exists(filePath))
		{
			Logger.Warn("LensLookup: File not found: " + filePath);
			return lookup;
		}
		try
		{
			int lineNum = 0;
			string[] array = File.ReadAllLines(filePath);
			foreach (string obj in array)
			{
				lineNum++;
				string line = obj.Trim();
				if (!string.IsNullOrEmpty(line) && line[0] != '#' && !line.StartsWith("//"))
				{
					string[] parts = line.Split(new char[3] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
					double height;
					ushort focus;
					if (parts.Length < 2)
					{
						Logger.Warn($"LensLookup: Line {lineNum} invalid format: '{line}'");
					}
					else if (!TryParseDouble(parts[0].Trim(), out height))
					{
						Logger.Warn($"LensLookup: Line {lineNum} invalid height: '{parts[0]}'");
					}
					else if (!ushort.TryParse(parts[1].Trim(), out focus))
					{
						Logger.Warn($"LensLookup: Line {lineNum} invalid focus: '{parts[1]}'");
					}
					else
					{
						lookup._data.Add((height, focus));
					}
				}
			}
			lookup._data.Sort(((double Height, ushort Focus) a, (double Height, ushort Focus) b) => a.Height.CompareTo(b.Height));
			Logger.Info($"LensLookup: Loaded {lookup.Count} entries from '{Path.GetFileName(filePath)}'");
			if (lookup.Count > 0)
			{
				(double, double)? range = lookup.GetHeightRange();
				Logger.Info($"LensLookup: Height range [{range?.Item1:F0} - {range?.Item2:F0}] mm");
				foreach (var entry in lookup._data)
				{
					Logger.Debug($"LensLookup:   {entry.Height:F0}mm → {entry.Focus}");
				}
			}
		}
		catch (Exception exception)
		{
			Logger.Error(exception, "LensLookup: Failed to load '" + filePath + "'");
		}
		return lookup;
	}

	public bool TryGetFocus(double heightMm, out ushort focusAddr)
	{
		focusAddr = 0;
		if (_data.Count == 0)
		{
			Logger.Error("LensLookup: No data available");
			return false;
		}
		if (double.IsNaN(heightMm) || double.IsInfinity(heightMm))
		{
			Logger.Error($"LensLookup: Invalid height value: {heightMm}");
			return false;
		}
		if (heightMm < 0.0)
		{
			heightMm = 0.0;
		}
		double minHeight = _data[0].Height;
		double maxHeight = _data[_data.Count - 1].Height;
		if (_data.Count == 1)
		{
			focusAddr = _data[0].Focus;
			Logger.Warn("LensLookup: [SINGLE-POINT] Only 1 calibration point");
			Logger.Warn($"LensLookup:   Requested: {heightMm:F1}mm → Using constant: {focusAddr}");
			return true;
		}
		int lower = -1;
		int upper = -1;
		for (int i = 0; i < _data.Count; i++)
		{
			if (_data[i].Height <= heightMm)
			{
				lower = i;
			}
			if (_data[i].Height >= heightMm && upper < 0)
			{
				upper = i;
			}
		}
		bool extrapolated = false;
		if (lower < 0)
		{
			lower = 0;
			upper = 1;
			extrapolated = true;
			double distance = minHeight - heightMm;
			Logger.Warn("┌─────────────────────────────────────────────────────────────");
			Logger.Warn("│ LensLookup: [EXTRAPOLATION BELOW]");
			Logger.Warn($"│   Requested height: {heightMm:F1}mm");
			Logger.Warn($"│   Calibrated range: [{minHeight:F1} - {maxHeight:F1}]mm");
			Logger.Warn($"│   Distance below min: {distance:F1}mm");
			Logger.Warn($"│   Using: [{_data[0].Height:F1}mm → {_data[0].Focus}] " + $"and [{_data[1].Height:F1}mm → {_data[1].Focus}]");
			Logger.Warn("└─────────────────────────────────────────────────────────────");
		}
		else if (upper < 0)
		{
			lower = _data.Count - 2;
			upper = _data.Count - 1;
			extrapolated = true;
			double distance2 = heightMm - maxHeight;
			Logger.Warn("┌─────────────────────────────────────────────────────────────");
			Logger.Warn("│ LensLookup: [EXTRAPOLATION ABOVE]");
			Logger.Warn($"│   Requested height: {heightMm:F1}mm");
			Logger.Warn($"│   Calibrated range: [{minHeight:F1} - {maxHeight:F1}]mm");
			Logger.Warn($"│   Distance above max: {distance2:F1}mm");
			Logger.Warn($"│   Using: [{_data[lower].Height:F1}mm → {_data[lower].Focus}] " + $"and [{_data[upper].Height:F1}mm → {_data[upper].Focus}]");
			Logger.Warn("└─────────────────────────────────────────────────────────────");
		}
		else if (lower == upper)
		{
			focusAddr = _data[lower].Focus;
			Logger.Info($"LensLookup: [EXACT] {heightMm:F1}mm → {focusAddr}");
			return true;
		}
		(double, ushort) p1 = _data[lower];
		(double, ushort) p2 = _data[upper];
		double deltaH = p2.Item1 - p1.Item1;
		if (Math.Abs(deltaH) < 0.001)
		{
			focusAddr = p1.Item2;
			return true;
		}
		double t = (heightMm - p1.Item1) / deltaH;
		double focusInterp = (double)(int)p1.Item2 + t * (double)(p2.Item2 - p1.Item2);
		bool wasClamped = false;
		if (focusInterp < 0.0)
		{
			focusInterp = 0.0;
			wasClamped = true;
		}
		else if (focusInterp > 65535.0)
		{
			focusInterp = 65535.0;
			wasClamped = true;
		}
		focusAddr = (ushort)Math.Round(focusInterp);
		if (extrapolated)
		{
			Logger.Warn($"LensLookup: [EXTRAPOLATION RESULT] {heightMm:F1}mm → {focusAddr} (t={t:F3})" + (wasClamped ? " [CLAMPED]" : ""));
		}
		else
		{
			Logger.Info($"LensLookup: [INTERPOLATED] {heightMm:F1}mm → {focusAddr}");
			Logger.Debug($"LensLookup:   Between [{p1.Item1:F1}mm → {p1.Item2}] and [{p2.Item1:F1}mm → {p2.Item2}]");
			Logger.Debug($"LensLookup:   Position: {t * 100.0:F1}% (t={t:F3})");
		}
		return true;
	}

	public (double Min, double Max)? GetHeightRange()
	{
		if (_data.Count == 0)
		{
			return null;
		}
		return (_data[0].Height, _data[_data.Count - 1].Height);
	}

	public bool IsInCalibratedRange(double heightMm)
	{
		if (_data.Count < 2)
		{
			return false;
		}
		if (heightMm >= _data[0].Height)
		{
			return heightMm <= _data[_data.Count - 1].Height;
		}
		return false;
	}

	private static bool TryParseDouble(string s, out double result)
	{
		if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
		{
			return true;
		}
		return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
	}
}
