using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Aggregation.Properties;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				resourceMan = new ResourceManager("Aggregation.Properties.Resources", typeof(Resources).Assembly);
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static byte[] autoDisabled => (byte[])ResourceManager.GetObject("autoDisabled", resourceCulture);

	internal static byte[] autoEnabled => (byte[])ResourceManager.GetObject("autoEnabled", resourceCulture);

	internal static byte[] boxActive => (byte[])ResourceManager.GetObject("boxActive", resourceCulture);

	internal static byte[] boxHoverIcon => (byte[])ResourceManager.GetObject("boxHoverIcon", resourceCulture);

	internal static byte[] boxIcon => (byte[])ResourceManager.GetObject("boxIcon", resourceCulture);

	internal static byte[] boxInactive => (byte[])ResourceManager.GetObject("boxInactive", resourceCulture);

	internal static byte[] camActiveIcon => (byte[])ResourceManager.GetObject("camActiveIcon", resourceCulture);

	internal static byte[] camHoverIcon => (byte[])ResourceManager.GetObject("camHoverIcon", resourceCulture);

	internal static byte[] camInactiveIcon => (byte[])ResourceManager.GetObject("camInactiveIcon", resourceCulture);

	internal static byte[] cycleHoverIcon => (byte[])ResourceManager.GetObject("cycleHoverIcon", resourceCulture);

	internal static byte[] cycleIcon => (byte[])ResourceManager.GetObject("cycleIcon", resourceCulture);

	internal static byte[] exitHoverIcon => (byte[])ResourceManager.GetObject("exitHoverIcon", resourceCulture);

	internal static byte[] exitHoverIcon2 => (byte[])ResourceManager.GetObject("exitHoverIcon2", resourceCulture);

	internal static byte[] exitIcon => (byte[])ResourceManager.GetObject("exitIcon", resourceCulture);

	internal static byte[] exitIcon2 => (byte[])ResourceManager.GetObject("exitIcon2", resourceCulture);

	internal static byte[] historyActiveIcon => (byte[])ResourceManager.GetObject("historyActiveIcon", resourceCulture);

	internal static byte[] historyInactiveIcon => (byte[])ResourceManager.GetObject("historyInactiveIcon", resourceCulture);

	internal static byte[] loadDisabled => (byte[])ResourceManager.GetObject("loadDisabled", resourceCulture);

	internal static byte[] loadEnabled => (byte[])ResourceManager.GetObject("loadEnabled", resourceCulture);

	internal static byte[] logsErrorIcon => (byte[])ResourceManager.GetObject("logsErrorIcon", resourceCulture);

	internal static byte[] logsHoverIcon => (byte[])ResourceManager.GetObject("logsHoverIcon", resourceCulture);

	internal static byte[] logsIcon => (byte[])ResourceManager.GetObject("logsIcon", resourceCulture);

	internal static byte[] logsInactiveIcon => (byte[])ResourceManager.GetObject("logsInactiveIcon", resourceCulture);

	internal static byte[] manualDisabled => (byte[])ResourceManager.GetObject("manualDisabled", resourceCulture);

	internal static byte[] manualEnabled => (byte[])ResourceManager.GetObject("manualEnabled", resourceCulture);

	internal static byte[] newLayerHoverIcon => (byte[])ResourceManager.GetObject("newLayerHoverIcon", resourceCulture);

	internal static byte[] newLayerIcon => (byte[])ResourceManager.GetObject("newLayerIcon", resourceCulture);

	internal static byte[] playIcon => (byte[])ResourceManager.GetObject("playIcon", resourceCulture);

	internal static byte[] playInactiveIcon => (byte[])ResourceManager.GetObject("playInactiveIcon", resourceCulture);

	internal static byte[] settingsHoverIcon => (byte[])ResourceManager.GetObject("settingsHoverIcon", resourceCulture);

	internal static byte[] settingsIcon => (byte[])ResourceManager.GetObject("settingsIcon", resourceCulture);

	internal static byte[] stopIcon => (byte[])ResourceManager.GetObject("stopIcon", resourceCulture);

	internal Resources()
	{
	}
}
