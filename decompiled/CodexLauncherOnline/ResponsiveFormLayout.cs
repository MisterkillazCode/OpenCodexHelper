using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace CodexLauncherOnline;

internal sealed class ResponsiveFormLayout
{
	private sealed class ControlMetrics
	{
		public Rectangle Bounds { get; }

		public string FontName { get; }

		public float FontSize { get; }

		public FontStyle FontStyle
		{
			[CompilerGenerated]
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				return field;
			}
		}

		public GraphicsUnit FontUnit
		{
			[CompilerGenerated]
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				return field;
			}
		}

		public byte FontCharSet { get; }

		public int Radius { get; }

		public int AccentWidth { get; }

		public ControlMetrics(Control control)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			Bounds = control.Bounds;
			FontName = control.Font.FontFamily.Name;
			FontSize = control.Font.Size;
			FontStyle = control.Font.Style;
			FontUnit = control.Font.Unit;
			FontCharSet = control.Font.GdiCharSet;
			if (control is RoundedPanel roundedPanel)
			{
				Radius = roundedPanel.Radius;
				AccentWidth = roundedPanel.AccentWidth;
			}
		}
	}

	private const int ScreenMargin = 24;

	private readonly Form _form;

	private readonly Size _designSize;

	private readonly float _minimumScale;

	private readonly List<Control> _topLevelControls;

	private readonly Dictionary<Control, ControlMetrics> _metrics = new Dictionary<Control, ControlMetrics>();

	private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>(StringComparer.Ordinal);

	private bool _applyingLayout;

	private ResponsiveFormLayout(Form form, Size designSize, float minimumScale)
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected Obj, but got Unknown
		_form = form;
		_designSize = designSize;
		_minimumScale = minimumScale;
		_topLevelControls = ((IEnumerable)((Control)form).Controls).Cast<Control>().ToList();
		foreach (Control topLevelControl in _topLevelControls)
		{
			Capture(topLevelControl);
		}
		_form.Load += (object? _, EventArgs _) =>
		{
			FitToCurrentScreen(centerWindow: true);
		};
		((Control)_form).ClientSizeChanged += (object? _, EventArgs _) =>
		{
			ApplyLayout();
		};
		_form.DpiChanged += (object? _, DpiChangedEventArgs _) =>
		{
			if (((Control)_form).IsHandleCreated && !((Control)_form).IsDisposed)
			{
				((Control)_form).BeginInvoke((Action)(() =>
				{
					FitToCurrentScreen(centerWindow: false);
				}));
			}
		};
		((Component)(object)_form).Disposed += (object? _, EventArgs _) =>
		{
			foreach (Font value in _fontCache.Values)
			{
				value.Dispose();
			}
			_fontCache.Clear();
		};
		ApplyLayout();
	}

	public static void Attach(Form form, Size designSize, float minimumScale = 0f)
	{
		new ResponsiveFormLayout(form, designSize, minimumScale);
	}

	private void Capture(Control control)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected Obj, but got Unknown
		_metrics[control] = new ControlMetrics(control);
		foreach (Control item in (ArrangedElementCollection)control.Controls)
		{
			Control control2 = item;
			Capture(control2);
		}
	}

	private void FitToCurrentScreen(bool centerWindow)
	{
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Invalid comparison between Unknown and I4
		if (((Control)_form).IsDisposed)
		{
			return;
		}
		Rectangle workingArea = Screen.FromControl((Control)(object)_form).WorkingArea;
		int num = Math.Max(0, ((Control)_form).Width - _form.ClientSize.Width);
		int num2 = Math.Max(0, ((Control)_form).Height - _form.ClientSize.Height);
		int num3 = Math.Max(1, workingArea.Width - num - 48);
		int num4 = Math.Max(1, workingArea.Height - num2 - 48);
		float num5 = Math.Max(1f, (float)((Control)_form).DeviceDpi / 96f);
		float val = (float)num3 / (float)_designSize.Width;
		float val2 = (float)num4 / (float)_designSize.Height;
		float num6 = Math.Max(0.1f, Math.Min(num5, Math.Min(val, val2)));
		Size clientSize = new Size(Math.Max(1, (int)Math.Round((float)_designSize.Width * num6)), Math.Max(1, (int)Math.Round((float)_designSize.Height * num6)));
		UpdateMinimumSize(num, num2, num5, num6);
		if ((int)_form.WindowState == 2)
		{
			ApplyLayout();
			return;
		}
		_form.ClientSize = clientSize;
		ApplyLayout();
		if (centerWindow)
		{
			_form.StartPosition = (FormStartPosition)0;
			_form.Location = new Point(workingArea.Left + Math.Max(0, (workingArea.Width - ((Control)_form).Width) / 2), workingArea.Top + Math.Max(0, (workingArea.Height - ((Control)_form).Height) / 2));
		}
		else
		{
			_form.Location = new Point(Math.Clamp(((Control)_form).Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - ((Control)_form).Width)), Math.Clamp(((Control)_form).Top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - ((Control)_form).Height)));
		}
	}

	private void UpdateMinimumSize(int nonClientWidth, int nonClientHeight, float dpiScale, float targetScale)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I4
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		bool flag = _minimumScale <= 0f;
		if (!flag)
		{
			FormBorderStyle formBorderStyle = _form.FormBorderStyle;
			bool flag2 = (((int)formBorderStyle == 1 || (int)formBorderStyle == 3) ? true : false);
			flag = flag2;
		}
		if (!flag)
		{
			float num = Math.Min(targetScale, dpiScale * _minimumScale);
			((Control)_form).MinimumSize = new Size(Math.Max(1, (int)Math.Round((float)_designSize.Width * num) + nonClientWidth), Math.Max(1, (int)Math.Round((float)_designSize.Height * num) + nonClientHeight));
		}
	}

	private void ApplyLayout()
	{
		if (_applyingLayout || ((Control)_form).IsDisposed || _form.ClientSize.Width <= 0 || _form.ClientSize.Height <= 0)
		{
			return;
		}
		_applyingLayout = true;
		((Control)_form).SuspendLayout();
		try
		{
			float num = Math.Min((float)_form.ClientSize.Width / (float)_designSize.Width, (float)_form.ClientSize.Height / (float)_designSize.Height);
			int num2 = (int)Math.Round((float)_designSize.Width * num);
			int num3 = (int)Math.Round((float)_designSize.Height * num);
			Point offset = new Point((_form.ClientSize.Width - num2) / 2, (_form.ClientSize.Height - num3) / 2);
			float num4 = Math.Max(1f, (float)((Control)_form).DeviceDpi / 96f);
			float fontScale = num / num4;
			foreach (Control topLevelControl in _topLevelControls)
			{
				ApplyControlLayout(topLevelControl, num, fontScale, offset);
			}
		}
		finally
		{
			((Control)_form).ResumeLayout(true);
			_applyingLayout = false;
		}
	}

	private void ApplyControlLayout(Control control, float scale, float fontScale, Point offset)
	{
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected Obj, but got Unknown
		ControlMetrics controlMetrics = _metrics[control];
		if (!(control is Panel) && !(control is PictureBox))
		{
			control.Font = GetScaledFont(controlMetrics, fontScale);
		}
		Rectangle bounds = ScaleRectangle(controlMetrics.Bounds, scale);
		if ((object)control.Parent == _form)
		{
			bounds.Offset(offset);
		}
		control.Bounds = bounds;
		if (control is RoundedPanel roundedPanel)
		{
			roundedPanel.Radius = Math.Max(2, (int)Math.Round((float)controlMetrics.Radius * scale));
			roundedPanel.AccentWidth = Math.Max(0, (int)Math.Round((float)controlMetrics.AccentWidth * scale));
		}
		foreach (Control item in (ArrangedElementCollection)control.Controls)
		{
			Control control2 = item;
			ApplyControlLayout(control2, scale, fontScale, Point.Empty);
		}
	}

	private Font GetScaledFont(ControlMetrics metrics, float fontScale)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected I4, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected I4, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Expected Obj, but got Unknown
		float num = Math.Max(5f, metrics.FontSize * fontScale);
		string key = $"{metrics.FontName}|{num:F2}|{(int)metrics.FontStyle}|{(int)metrics.FontUnit}|{metrics.FontCharSet}";
		if (!_fontCache.TryGetValue(key, out Font value))
		{
			value = new Font(metrics.FontName, num, metrics.FontStyle, metrics.FontUnit, metrics.FontCharSet);
			_fontCache.Add(key, value);
		}
		return value;
	}

	private static Rectangle ScaleRectangle(Rectangle bounds, float scale)
	{
		return new Rectangle((int)Math.Round((float)bounds.X * scale), (int)Math.Round((float)bounds.Y * scale), Math.Max(1, (int)Math.Round((float)bounds.Width * scale)), Math.Max(1, (int)Math.Round((float)bounds.Height * scale)));
	}
}
