using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodexLauncherOnline;

internal static class Program
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	private struct PropertyKey(Guid formatId, uint propertyId)
	{
		public Guid FormatId = formatId;

		public uint PropertyId = propertyId;
	}

	[StructLayout(LayoutKind.Explicit, Size = 16)]
	private struct PropVariant
	{
		[FieldOffset(0)]
		public ushort VariantType;

		[FieldOffset(8)]
		public nint PointerValue;

		public static PropVariant FromString(string value)
		{
			return new PropVariant
			{
				VariantType = 31,
				PointerValue = Marshal.StringToCoTaskMemUni(value)
			};
		}

		public void Clear()
		{
			PropVariantClear(ref this);
		}
	}

	[ComImport]
	[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IPropertyStore
	{
		[PreserveSig]
		int GetCount(out uint propertyCount);

		[PreserveSig]
		int GetAt(uint propertyIndex, out PropertyKey key);

		[PreserveSig]
		int GetValue(ref PropertyKey key, out PropVariant value);

		[PreserveSig]
		int SetValue(ref PropertyKey key, ref PropVariant value);

		[PreserveSig]
		int Commit();
	}

	private const string AssistantAppUserModelId = "ApiNexus.CodexAssistant.2.0";

	private static readonly Guid AppUserModelPropertySet = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

	private static readonly PropertyKey RelaunchIconResourceKey = new PropertyKey(AppUserModelPropertySet, 3u);

	private static readonly PropertyKey AppUserModelIdKey = new PropertyKey(AppUserModelPropertySet, 5u);

	internal static Icon LoadAssistantIcon()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected Obj, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected Obj, but got Unknown
		using Stream stream = typeof(Program).Assembly.GetManifestResourceStream("CodexLauncherOnline20.AppIcon.ico") ?? throw new InvalidOperationException("内置程序图标资源不可用。");
		Icon val = new Icon(stream);
		try
		{
			return (Icon)val.Clone();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static string EnsureAssistantIconFile()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "Codex助手2.0-App.ico");
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		using Stream stream = typeof(Program).Assembly.GetManifestResourceStream("CodexLauncherOnline20.AppIcon.ico") ?? throw new InvalidOperationException("内置程序图标资源不可用。");
		using FileStream destination = File.Create(text);
		stream.CopyTo(destination);
		return text;
	}

	internal static void ConfigureTaskbarIdentity()
	{
		try
		{
			SetCurrentProcessExplicitAppUserModelID("ApiNexus.CodexAssistant.2.0");
			EnsureAssistantIconFile();
			SHChangeNotify(8192u, 5u, Application.ExecutablePath, IntPtr.Zero);
		}
		catch
		{
		}
	}

	internal static void ApplyTaskbarIcon(nint windowHandle, Icon icon)
	{
		try
		{
			string text = EnsureAssistantIconFile();
			SendMessage(windowHandle, 128u, IntPtr.Zero, icon.Handle);
			SendMessage(windowHandle, 128u, new IntPtr(1), icon.Handle);
			Guid interfaceId = typeof(IPropertyStore).GUID;
			if (SHGetPropertyStoreForWindow(windowHandle, ref interfaceId, out IPropertyStore propertyStore) != 0)
			{
				return;
			}
			try
			{
				PropVariant value = PropVariant.FromString(text + ",0");
				PropVariant value2 = PropVariant.FromString("ApiNexus.CodexAssistant.2.0");
				try
				{
					PropertyKey key = RelaunchIconResourceKey;
					propertyStore.SetValue(ref key, ref value);
					PropertyKey key2 = AppUserModelIdKey;
					propertyStore.SetValue(ref key2, ref value2);
				}
				finally
				{
					value.Clear();
					value2.Clear();
				}
			}
			finally
			{
				Marshal.FinalReleaseComObject(propertyStore);
			}
		}
		catch
		{
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

	[DllImport("shell32.dll")]
	private static extern int SHGetPropertyStoreForWindow(nint windowHandle, ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint windowHandle, uint message, nint wParam, nint lParam);

	[DllImport("ole32.dll")]
	private static extern int PropVariantClear(ref PropVariant propVariant);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	internal static extern void SHChangeNotify(uint eventId, uint flags, string path, nint secondPath);

	[STAThread]
	private static void Main(string[] args)
	{
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Invalid comparison between Unknown and I4
		if (args.Any((string argument) => string.Equals(argument, "--shortcut-launch", StringComparison.OrdinalIgnoreCase)))
		{
			LauncherForm.RunShortcutLauncherAsync().GetAwaiter().GetResult();
			return;
		}
		if (args.Any((string argument) => string.Equals(argument, "--watcher", StringComparison.OrdinalIgnoreCase)))
		{
			LauncherForm.RunPersistentWatcherAsync().GetAwaiter().GetResult();
			return;
		}
		if (args.Any((string argument) => string.Equals(argument, "--background-launch", StringComparison.OrdinalIgnoreCase)))
		{
			LauncherForm.RunPersistentLauncherAsync().GetAwaiter().GetResult();
			return;
		}
		if (args.Any((string argument) => string.Equals(argument, "--debug-port", StringComparison.OrdinalIgnoreCase)))
		{
			LauncherForm.RunShortcutLauncherAsync().GetAwaiter().GetResult();
			return;
		}
		ConfigureTaskbarIdentity();
		ApplicationConfiguration.Initialize();
		LauncherForm.RefreshExistingShortcutIcons();
		WechatFollowGateForm wechatFollowGateForm = new WechatFollowGateForm();
		try
		{
			if ((int)((Form)wechatFollowGateForm).ShowDialog() != 1)
			{
				return;
			}
		}
		finally
		{
			((IDisposable)(object)wechatFollowGateForm)?.Dispose();
		}
		Application.Run((Form)(object)new LauncherForm());
	}
}
