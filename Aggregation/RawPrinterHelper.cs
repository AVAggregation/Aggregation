using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace Aggregation;

public class RawPrinterHelper
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public class DOCINFOW
	{
		[MarshalAs(UnmanagedType.LPWStr)]
		public string pDocName;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string pOutputFile;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string pDataType;
	}

	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "OpenPrinterW", SetLastError = true)]
	public static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	public static extern bool ClosePrinter(IntPtr hPrinter);

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "StartDocPrinterW", SetLastError = true)]
	public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOW di);

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	public static extern bool EndDocPrinter(IntPtr hPrinter);

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	public static extern bool StartPagePrinter(IntPtr hPrinter);

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	public static extern bool EndPagePrinter(IntPtr hPrinter);

	[DllImport("winspool.Drv", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

	public static bool TrySendStringToPrinter(string code, string templateData, out string error, string printerName)
	{
		error = null;
		if (string.IsNullOrWhiteSpace(templateData))
		{
			error = "Шаблон пустой.";
			Logger.Error("RawPrinter: templateData is empty");
			return false;
		}
		if (!templateData.Contains("{{CODE}}"))
		{
			error = "Шаблон не содержит {{CODE}}.";
			Logger.Error("RawPrinter: template missing {{CODE}}");
			return false;
		}
		try
		{
			string s = templateData.Replace("{{CODE}}", code);
			if (string.IsNullOrWhiteSpace(printerName))
			{
				error = "Не задано имя принтера.";
				Logger.Error("RawPrinter: printerName is empty");
				return false;
			}
			byte[] bytes = Encoding.Default.GetBytes(s);
			int length = bytes.Length;
			Logger.Info($"RawPrinter: start print. printer='{printerName}', bytes={length}");
			IntPtr pBytes = Marshal.AllocCoTaskMem(length);
			Marshal.Copy(bytes, 0, pBytes, length);
			try
			{
				DOCINFOW di = new DOCINFOW
				{
					pDocName = "RAW Label Print",
					pDataType = "RAW"
				};
				if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero) || hPrinter == IntPtr.Zero)
				{
					int err = Marshal.GetLastWin32Error();
					error = $"Принтер \"{printerName}\" недоступен. Win32={err}";
					Logger.Error($"RawPrinter: OpenPrinter failed. win32={err}");
					return false;
				}
				try
				{
					if (!StartDocPrinter(hPrinter, 1, di))
					{
						int err2 = Marshal.GetLastWin32Error();
						error = $"Ошибка печати: StartDocPrinter. Win32={err2}";
						return false;
					}
					try
					{
						if (!StartPagePrinter(hPrinter))
						{
							int err3 = Marshal.GetLastWin32Error();
							error = $"Ошибка печати: StartPagePrinter. Win32={err3}";
							return false;
						}
						if (!WritePrinter(hPrinter, pBytes, length, out var written))
						{
							int err4 = Marshal.GetLastWin32Error();
							error = $"Ошибка печати: WritePrinter. Win32={err4}";
							return false;
						}
						Logger.Info($"RawPrinter: ok. bytes={length}, written={written}");
						EndPagePrinter(hPrinter);
						return true;
					}
					finally
					{
						EndDocPrinter(hPrinter);
					}
				}
				finally
				{
					ClosePrinter(hPrinter);
				}
			}
			finally
			{
				Marshal.FreeCoTaskMem(pBytes);
			}
		}
		catch (Exception ex)
		{
			error = ex.Message;
			Logger.Error(ex, "RawPrinter exception");
			return false;
		}
	}

	public static bool TrySendStringToPrinter(string code, out string error, string printerName)
	{
		error = null;
		try
		{
			string templateData = File.ReadAllText("template.txt");
			return TrySendStringToPrinter(code, templateData, out error, printerName);
		}
		catch (Exception ex)
		{
			error = "Template-Datei nicht gefunden: " + ex.Message;
			return false;
		}
	}
}
