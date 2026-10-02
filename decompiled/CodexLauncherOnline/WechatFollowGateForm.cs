using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CodexLauncherOnline;

internal sealed class WechatFollowGateForm : Form
{
	private readonly CheckBox _confirmed = new CheckBox();

	private readonly Button _continueButton = new Button();

	public WechatFollowGateForm()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected Obj, but got Unknown
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected Obj, but got Unknown
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Expected Obj, but got Unknown
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Expected Obj, but got Unknown
		//IL_0185: Expected Obj, but got Unknown
		((Control)this).Text = "关注公众号后继续";
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)0;
		((Form)this).ClientSize = new Size(520, 620);
		((Form)this).FormBorderStyle = (FormBorderStyle)4;
		((Form)this).MaximizeBox = true;
		((Form)this).MinimizeBox = false;
		((Control)this).MinimumSize = new Size(360, 430);
		((Form)this).StartPosition = (FormStartPosition)1;
		((Control)this).BackColor = Colors.Canvas;
		((Control)this).Font = new Font("Microsoft YaHei UI", 9f, (FontStyle)0, (GraphicsUnit)3);
		((Form)this).Icon = Program.LoadAssistantIcon();
		((Control)this).Controls.Add((Control)(object)CreateText("扫码关注微信公众号", new Point(40, 26), new Size(440, 34), 20f, (FontStyle)1, Colors.Ink, ((Control)this).BackColor, (HorizontalAlignment)2));
		((Control)this).Controls.Add((Control)(object)CreateText("首次使用需要完成关注验证", new Point(40, 66), new Size(440, 24), 10f, (FontStyle)0, Colors.Muted, ((Control)this).BackColor, (HorizontalAlignment)2));
		using Stream stream = typeof(Program).Assembly.GetManifestResourceStream("CodexLauncherOnline.OfficialAccountQr.jpg");
		if (stream != null)
		{
			Image val = Image.FromStream(stream);
			try
			{
				PictureBox val2 = new PictureBox
				{
					Location = new Point(95, 106),
					Size = new Size(330, 330),
					SizeMode = (PictureBoxSizeMode)4,
					BackColor = Color.White,
					Image = (Image)new Bitmap(val)
				};
				((Control)this).Controls.Add((Control)(object)val2);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		else
		{
			((Control)this).Controls.Add((Control)(object)CreateText("二维码资源缺失，请重新安装程序。", new Point(40, 250), new Size(440, 30), 11f, (FontStyle)1, Color.Firebrick, ((Control)this).BackColor, (HorizontalAlignment)2));
		}
		((Control)_confirmed).Location = new Point(74, 464);
		((Control)_confirmed).Size = new Size(372, 28);
		((Control)_confirmed).Text = "我已扫码并关注微信公众号";
		((Control)_confirmed).ForeColor = Colors.Ink;
		((Control)_confirmed).BackColor = ((Control)this).BackColor;
		((ButtonBase)_confirmed).FlatStyle = (FlatStyle)2;
		((ButtonBase)_confirmed).UseVisualStyleBackColor = false;
		_confirmed.CheckedChanged += (object? _, EventArgs _) =>
		{
			((Control)_continueButton).Enabled = _confirmed.Checked;
		};
		((Control)this).Controls.Add((Control)(object)_confirmed);
		((Control)_continueButton).Location = new Point(150, 514);
		((Control)_continueButton).Size = new Size(220, 42);
		((Control)_continueButton).Text = "验证并进入 Codex 助手";
		((Control)_continueButton).Enabled = false;
		((Control)_continueButton).BackColor = Colors.Primary;
		((Control)_continueButton).ForeColor = Color.White;
		((ButtonBase)_continueButton).FlatStyle = (FlatStyle)0;
		((ButtonBase)_continueButton).UseVisualStyleBackColor = false;
		((ButtonBase)_continueButton).FlatAppearance.BorderSize = 0;
		((Control)_continueButton).Cursor = Cursors.Hand;
		((Control)_continueButton).Click += (object? _, EventArgs _) =>
		{
			((Form)this).DialogResult = (DialogResult)1;
		};
		((Control)this).Controls.Add((Control)(object)_continueButton);
		((Form)this).AcceptButton = (IButtonControl)(object)_continueButton;
		((Form)this).CancelButton = (IButtonControl)new Button
		{
			DialogResult = (DialogResult)2
		};
		ResponsiveFormLayout.Attach((Form)(object)this, new Size(520, 620), 0.68f);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		((Form)this).OnHandleCreated(e);
		Program.ApplyTaskbarIcon(((Control)this).Handle, ((Form)this).Icon);
	}

	private static TextBox CreateText(string text, Point location, Size size, float fontSize, FontStyle style, Color color, Color backColor, HorizontalAlignment alignment)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected Obj, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected Obj, but got Unknown
		return new TextBox
		{
			Text = text,
			Location = location,
			Size = size,
			BorderStyle = (BorderStyle)0,
			ReadOnly = true,
			TabStop = false,
			BackColor = backColor,
			ForeColor = color,
			Font = new Font("Microsoft YaHei UI", fontSize, style, (GraphicsUnit)3),
			TextAlign = alignment
		};
	}
}
