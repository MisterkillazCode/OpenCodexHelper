using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexLauncherOnline;

internal sealed class RoundedPanel : Panel
{
	public int Radius { get; set; } = 14;

	public Color FillColor { get; set; } = Color.White;

	public Color BorderColor { get; set; } = Color.FromArgb(225, 232, 244);

	public Color AccentColor { get; set; } = Color.Transparent;

	public int AccentWidth { get; set; }

	public RoundedPanel()
	{
		((Control)this).DoubleBuffered = true;
		((Control)this).BackColor = Color.Transparent;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected Obj, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected Obj, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected Obj, but got Unknown
		((Control)this).OnPaint(e);
		e.Graphics.SmoothingMode = (SmoothingMode)4;
		GraphicsPath val = CreateRoundRect(new Rectangle(0, 0, ((Control)this).Width - 1, ((Control)this).Height - 1), Radius);
		try
		{
			SolidBrush val2 = new SolidBrush(FillColor);
			try
			{
				Pen val3 = new Pen(BorderColor, 1f);
				try
				{
					e.Graphics.FillPath((Brush)(object)val2, val);
					e.Graphics.DrawPath(val3, val);
					if (AccentWidth <= 0 || !(AccentColor != Color.Transparent))
					{
						return;
					}
					SolidBrush val4 = new SolidBrush(AccentColor);
					try
					{
						GraphicsPath val5 = CreateRoundRect(new Rectangle(0, 0, ((Control)this).Width - 1, ((Control)this).Height - 1), Radius);
						try
						{
							e.Graphics.SetClip(val5);
							e.Graphics.FillRectangle((Brush)(object)val4, 0, 0, AccentWidth, ((Control)this).Height);
							e.Graphics.ResetClip();
						}
						finally
						{
							((IDisposable)val5)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)val4)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static GraphicsPath CreateRoundRect(Rectangle bounds, int radius)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected Obj, but got Unknown
		GraphicsPath val = new GraphicsPath();
		int num = radius * 2;
		val.AddArc(bounds.Left, bounds.Top, num, num, 180f, 90f);
		val.AddArc(bounds.Right - num, bounds.Top, num, num, 270f, 90f);
		val.AddArc(bounds.Right - num, bounds.Bottom - num, num, num, 0f, 90f);
		val.AddArc(bounds.Left, bounds.Bottom - num, num, num, 90f, 90f);
		val.CloseFigure();
		return val;
	}
}
