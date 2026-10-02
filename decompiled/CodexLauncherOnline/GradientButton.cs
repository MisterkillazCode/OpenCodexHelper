using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexLauncherOnline;

internal sealed class GradientButton : Button
{
	private bool _hovered;

	private bool _pressed;

	public Color StartColor { get; set; } = Color.FromArgb(110, 103, 255);

	public Color EndColor { get; set; } = Color.FromArgb(42, 146, 239);

	public GradientButton()
	{
		((ButtonBase)this).FlatStyle = (FlatStyle)0;
		((ButtonBase)this).FlatAppearance.BorderSize = 0;
		((ButtonBase)this).UseVisualStyleBackColor = false;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hovered = true;
		((Control)this).Invalidate();
		((Button)this).OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hovered = false;
		_pressed = false;
		((Control)this).Invalidate();
		((Button)this).OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs mevent)
	{
		_pressed = true;
		((Control)this).Invalidate();
		((ButtonBase)this).OnMouseDown(mevent);
	}

	protected override void OnMouseUp(MouseEventArgs mevent)
	{
		_pressed = false;
		((Control)this).Invalidate();
		((Button)this).OnMouseUp(mevent);
	}

	protected override void OnPaint(PaintEventArgs pevent)
	{
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Expected Obj, but got Unknown
		pevent.Graphics.SmoothingMode = (SmoothingMode)4;
		Graphics graphics = pevent.Graphics;
		Control parent = ((Control)this).Parent;
		graphics.Clear((parent != null) ? parent.BackColor : Color.White);
		Rectangle rectangle = new Rectangle(0, 0, ((Control)this).Width - 1, ((Control)this).Height - 1);
		GraphicsPath val = CreateRoundRect(rectangle, 7);
		try
		{
			Color color;
			if (_pressed)
			{
				color = ControlPaint.Dark(StartColor, 0.12f);
			}
			else
			{
				color = (_hovered ? ControlPaint.Light(StartColor, 0.08f) : StartColor);
			}
			Color color2;
			if (_pressed)
			{
				color2 = ControlPaint.Dark(EndColor, 0.12f);
			}
			else
			{
				color2 = (_hovered ? ControlPaint.Light(EndColor, 0.08f) : EndColor);
			}
			LinearGradientBrush val2 = new LinearGradientBrush(rectangle, color, color2, 35f);
			try
			{
				pevent.Graphics.FillPath((Brush)(object)val2, val);
				TextRenderer.DrawText((IDeviceContext)(object)pevent.Graphics, ((Control)this).Text, ((Control)this).Font, rectangle, ((Control)this).ForeColor, (TextFormatFlags)32773);
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
