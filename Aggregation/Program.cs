using System;
using System.Threading;
using System.Windows.Forms;
using Aggregation.Forms;

namespace Aggregation;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		Application.ThreadException += Application_ThreadException;
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new Form3());
	}

	private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
	{
		Console.WriteLine(e.Exception.ToString());
	}

	private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		Console.WriteLine(e.ExceptionObject.ToString());
	}
}
