using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexLauncherOnline;

internal sealed class LauncherForm : Form
{
	private sealed record UpdateManifest(string Version, string DownloadUrl, string? Sha256, string? Notes);

	private sealed record CdpTarget(string Title, string Url, string? WebSocketDebuggerUrl);

	private sealed record PackagedCodexApp(string AppUserModelId, string PackageDirectory);

	[ComImport]
	[Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
	private sealed class ApplicationActivationManager
	{
	}

	[ComImport]
	[Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IApplicationActivationManager
	{
		[PreserveSig]
		int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
	}

	private sealed class ApiKeyValidationException : Exception
	{
		public ApiKeyValidationException(string message)
			: base(message)
		{
		}
	}

	private sealed record ModelCatalogEntry(string Slug, string DisplayName, string Description, string DefaultReasoningLevel, int Priority, string[] SupportedReasoningLevels);

	private static readonly Version CurrentVersion = new Version(2, 1, 1);

	private const string VideoUrl = "https://www.bilibili.com/video/BV1BHN26GETP";

	private const string RemoteServiceUrl = "https://wzyp.cn/item/bjbyqd";

	private const string ChineseLocalizationUrl = "https://wzyp.cn/item/zm1ofy";

	private const string KeyUrl = "https://apinexus.dpdns.org";

	private const string AnnouncementUrl = "https://apinexus.dpdns.org/codex-launcher/announcement.txt";

	private const string UpdateManifestUrl = "http://156.238.239.133/codex-launcher/update.json";

	private const string DefaultAnnouncement = "售后QQ群：1048665530";

	private const string CodexProviderId = "custom";

	private const string ApiBaseUrl = "https://apinexus.dpdns.org/v1";

	private const string ModelCatalogFileName = "codex-launcher-model-catalog.json";

	private const int CodexDebugPort = 9229;

	private const string WatcherRunValueName = "CodexAssistant20Watcher";

	private const string WatcherMutexName = "Local\\CodexAssistant20Watcher";

	private const string WatcherShortcutName = "Codex助手启动器.lnk";

	private const int WatcherPollMilliseconds = 3000;

	private static readonly HttpClient CdpHttpClient = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(2.0)
	};

	private const string CodexAssistantInjectionScript = "(() => {\r\n  const navId = \"codex-assistant-20-nav\";\r\n  const pageId = \"codex-assistant-20-page\";\r\n  const pageClass = \"codex-assistant-20-page\";\r\n  const styleId = \"codex-assistant-20-style\";\r\n  const imageNavId = \"codex-imagegen-test-nav\";\r\n  const imagePageId = \"codex-imagegen-test-page\";\r\n  const buttonText = \"充值\";\r\n  const remoteServiceUrl = \"https://wzyp.cn/item/bjbyqd\";\r\n  const localizationUrl = \"https://wzyp.cn/item/zm1ofy\";\r\n  const videoUrl = \"https://www.bilibili.com/video/BV1BHN26GETP\";\r\n  const topupUrl = \"https://apinexus.dpdns.org/console/topup\";\r\n\r\n  function closePage() {\r\n    document.getElementById(pageId)?.remove();\r\n    document.getElementById(navId)?.querySelector(\"button\")?.setAttribute(\"aria-current\", \"false\");\r\n  }\r\n\r\n  function closeImagePage() {\r\n    document.getElementById(imagePageId)?.remove();\r\n    document.getElementById(imageNavId)?.querySelector(\"button\")?.setAttribute(\"aria-current\", \"false\");\r\n  }\r\n\r\n  function positionPage(page) {\r\n    const sidebar = document.querySelector(\"aside.app-shell-left-panel\");\r\n    const rect = sidebar?.getBoundingClientRect?.();\r\n    const left = rect && rect.width > 0 ? Math.max(0, rect.right) : 0;\r\n    page.style.left = `${left}px`;\r\n  }\r\n\r\n  function openPage() {\r\n    closeImagePage();\r\n    closePage();\r\n    const page = document.createElement(\"div\");\r\n    page.id = pageId;\r\n    page.className = pageClass;\r\n    page.setAttribute(\"role\", \"dialog\");\r\n    page.setAttribute(\"aria-modal\", \"true\");\r\n    page.setAttribute(\"aria-label\", \"充值\");\r\n    Object.assign(page.style, {\r\n      position: \"fixed\", left: \"0\", top: \"0\", bottom: \"0\", width: \"min(420px, 100vw)\",\r\n      zIndex: \"2147483000\", background: \"var(--bg-primary, #fff)\",\r\n      color: \"var(--text-primary, #171717)\", borderRight: \"1px solid rgba(0,0,0,.12)\",\r\n      boxShadow: \"8px 0 24px rgba(0,0,0,.14)\", fontFamily: \"inherit\", overflow: \"auto\"\r\n    });\r\n    page.innerHTML = `\r\n      <div style=\"height:100%;display:flex;flex-direction:column\">\r\n        <header style=\"display:flex;align-items:center;padding:18px 20px;border-bottom:1px solid rgba(0,0,0,.10);font-weight:700;font-size:18px\">\r\n          <span>充值</span>\r\n        </header>\r\n        <div class=\"codex-assistant-20-actions\">\r\n          <button type=\"button\" class=\"codex-assistant-20-topup\" data-action=\"topup\">充值</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-secondary\" data-action=\"remote\">远程服务</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-secondary\" data-action=\"localization\">中文汉化</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-secondary\" data-action=\"video\">视频教程</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-close\" data-action=\"close\">关闭</button>\r\n        </div>\r\n      </div>`;\r\n    let style = document.getElementById(styleId);\r\n    if (!style) {\r\n      style = document.createElement(\"style\");\r\n      style.id = styleId;\r\n      style.textContent = `\r\n        #${pageId} .codex-assistant-20-actions { padding: 22px 20px; display: grid; gap: 12px; }\r\n        #${pageId} button { width: 100%; box-sizing: border-box; border-radius: 12px; font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; transition: transform .15s ease, box-shadow .15s ease, background .15s ease; }\r\n        #${pageId} button:hover { transform: translateY(-1px); }\r\n        #${pageId} .codex-assistant-20-topup { padding: 15px 16px; border: 0; color: #fff; font-size: 17px; font-weight: 700; letter-spacing: .08em; background: linear-gradient(135deg, #ff8a1f 0%, #ff3d68 100%); box-shadow: 0 8px 18px rgba(255, 89, 72, .28); }\r\n        #${pageId} .codex-assistant-20-topup:hover { box-shadow: 0 10px 22px rgba(255, 89, 72, .38); }\r\n        #${pageId} .codex-assistant-20-secondary { padding: 12px 14px; border: 1px solid rgba(99, 115, 145, .24); color: var(--text-primary, #273142); background: var(--bg-secondary, #f6f8fc); }\r\n        #${pageId} .codex-assistant-20-secondary:hover { background: var(--bg-hover, #edf2fb); }\r\n        #${pageId} .codex-assistant-20-close { margin-top: 4px; padding: 10px 14px; border: 1px solid rgba(99, 115, 145, .32); color: var(--text-secondary, #64748b); background: transparent; }\r\n      `;\r\n      document.head?.appendChild(style);\r\n    }\r\n    positionPage(page);\r\n    page.addEventListener(\"click\", (event) => {\r\n      const target = event.target instanceof Element ? event.target : event.target?.parentElement;\r\n      const action = target?.closest(\"[data-action]\")?.getAttribute(\"data-action\");\r\n      if (action === \"close\") {\r\n        event.preventDefault();\r\n        event.stopPropagation();\r\n        closePage();\r\n        return;\r\n      }\r\n      if (action === \"remote\") { window.open(remoteServiceUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n      if (action === \"localization\") { window.open(localizationUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n      if (action === \"video\") { window.open(videoUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n      if (action === \"topup\") { window.open(topupUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n    }, true);\r\n    document.body.appendChild(page);\r\n    document.getElementById(navId)?.querySelector(\"button\")?.setAttribute(\"aria-current\", \"page\");\r\n  }\r\n\r\n  function installSidebarNavigation() {\r\n    const navigation = document.querySelector(\"aside.app-shell-left-panel nav[role=navigation], nav[role=navigation]\");\r\n    if (!navigation) return false;\r\n    const navButtons = Array.from(navigation.querySelectorAll(\"button\"));\r\n    const pluginButton = navButtons.find((button) => /^(插件|Plugins)$/i.test((button.getAttribute(\"aria-label\") || button.textContent || \"\").replace(/\\\\s+/g, \" \").trim()));\r\n    const insertionButton = pluginButton || navButtons.find((button) => /^(新对话|New chat|已安排|Scheduled|拉取请求|Pull requests)$/i.test((button.getAttribute(\"aria-label\") || button.textContent || \"\").replace(/\\\\s+/g, \" \").trim())) || navButtons[0];\r\n    const parent = insertionButton?.parentElement || navigation;\r\n    let wrapper = document.getElementById(navId);\r\n    if (!wrapper || wrapper.parentElement !== parent) {\r\n      wrapper?.remove();\r\n      wrapper = document.createElement(\"div\");\r\n      wrapper.id = navId;\r\n      wrapper.dataset.codexAssistant20 = \"true\";\r\n      const button = (insertionButton || document.createElement(\"button\")).cloneNode(true);\r\n      button.type = \"button\";\r\n      button.removeAttribute(\"disabled\");\r\n      button.removeAttribute(\"aria-disabled\");\r\n      button.removeAttribute(\"data-state\");\r\n      button.setAttribute(\"aria-label\", buttonText);\r\n      button.textContent = \"\";\r\n      button.innerHTML = `<span aria-hidden=\"true\" style=\"font-size:16px;line-height:1\">✦</span><span class=\"truncate\">${buttonText}</span>`;\r\n      button.addEventListener(\"click\", (event) => { event.preventDefault(); event.stopPropagation(); openPage(); }, true);\r\n      wrapper.appendChild(button);\r\n      if (insertionButton?.nextSibling) parent.insertBefore(wrapper, insertionButton.nextSibling); else parent.appendChild(wrapper);\r\n    }\r\n\r\n    let imageWrapper = document.getElementById(imageNavId);\r\n    if (!imageWrapper || imageWrapper.parentElement !== parent) {\r\n      imageWrapper?.remove();\r\n      imageWrapper = document.createElement(\"div\");\r\n      imageWrapper.id = imageNavId;\r\n      imageWrapper.dataset.codexImagegenTest = \"true\";\r\n      const imageButton = (insertionButton || document.createElement(\"button\")).cloneNode(true);\r\n      imageButton.type = \"button\";\r\n      imageButton.removeAttribute(\"disabled\");\r\n      imageButton.removeAttribute(\"aria-disabled\");\r\n      imageButton.removeAttribute(\"data-state\");\r\n      imageButton.setAttribute(\"aria-label\", \"生图\");\r\n      imageButton.textContent = \"\";\r\n      imageButton.innerHTML = `<span aria-hidden=\"true\" style=\"font-size:16px;line-height:1\">✦</span><span class=\"truncate\">生图</span>`;\r\n      imageButton.addEventListener(\"click\", async (event) => {\r\n        event.preventDefault(); event.stopPropagation();\r\n        window.__assistantOpenImageSite = true;\r\n      }, true);\r\n      imageWrapper.appendChild(imageButton);\r\n    }\r\n    if (imageWrapper.parentElement !== parent) parent.appendChild(imageWrapper);\r\n    if (wrapper.nextSibling !== imageWrapper) parent.insertBefore(imageWrapper, wrapper.nextSibling);\r\n    return true;\r\n  }\r\n\r\n  const wasInstalled = window.__codexAssistant20InstalledVersion === \"204-close-actions\";\n  if (!wasInstalled) {\r\n    window.__codexAssistant20Observer?.disconnect();\r\n    window.__codexAssistant20Observer = null;\r\n    document.getElementById(imageNavId)?.remove();\r\n    window.__codexAssistant20InstalledVersion = \"204-close-actions\";\n    document.getElementById(pageId)?.remove();\r\n    document.querySelectorAll(`.${pageClass}`).forEach((node) => node.remove());\r\n    document.getElementById(navId)?.remove();\r\n  }\r\n\r\n  if (!window.__codexAssistant20Observer && document.documentElement) {\r\n    const observer = new MutationObserver(() => {\r\n      try { installSidebarNavigation(); } catch {}\r\n    });\r\n    observer.observe(document.documentElement, { childList: true, subtree: true });\r\n    window.__codexAssistant20Observer = observer;\r\n  }\r\n\r\n  if (!window.__codexAssistant20RetryTimer) {\r\n    const retryTimer = setInterval(() => {\r\n      try {\r\n        if (installSidebarNavigation() || !document.documentElement) {\r\n          clearInterval(retryTimer);\r\n          window.__codexAssistant20RetryTimer = null;\r\n        }\r\n      } catch {}\r\n    }, 300);\r\n    window.__codexAssistant20RetryTimer = retryTimer;\r\n    setTimeout(() => {\r\n      clearInterval(retryTimer);\r\n      if (window.__codexAssistant20RetryTimer === retryTimer) window.__codexAssistant20RetryTimer = null;\r\n    }, 15000);\r\n  }\r\n\r\n  try { installSidebarNavigation(); } catch {}\r\n  if (!window.__codexAssistant20ResizeBound) {\r\n    window.__codexAssistant20ResizeBound = true;\r\n    window.addEventListener(\"resize\", () => {\r\n      const page = document.getElementById(pageId);\r\n      if (page) positionPage(page);\r\n    });\r\n  }\r\n  return true;\r\n})()";

	private static readonly ModelCatalogEntry[] CatalogModels = new ModelCatalogEntry[7]
	{
		new ModelCatalogEntry("gpt-6.1-sol", "GPT-6.1-Sol", "Near-Astra performance for complex work at a lower cost.", "medium", 0, new string[5] { "low", "medium", "high", "xhigh", "max" }),
		new ModelCatalogEntry("gpt-6-astra", "GPT-6-Astra", "Our most capable model for complex, demanding work.", "low", 1, new string[6] { "low", "medium", "high", "xhigh", "max", "ultra" }),
		new ModelCatalogEntry("gpt-5.6-sol", "GPT-5.6-Sol", "Latest frontier agentic coding model.", "low", 6, new string[5] { "low", "medium", "high", "xhigh", "max" }),
		new ModelCatalogEntry("gpt-6-sol", "GPT-6-Sol", "Workhorse model for coding and everyday work.", "medium", 2, new string[6] { "low", "medium", "high", "xhigh", "max", "ultra" }),
		new ModelCatalogEntry("gpt-5.6-terra", "GPT-5.6-Terra", "Balanced agentic coding model for everyday work.", "medium", 7, new string[5] { "low", "medium", "high", "xhigh", "max" }),
		new ModelCatalogEntry("gpt-5.6-luna", "GPT-5.6-Luna", "Fast and affordable agentic coding model.", "medium", 8, new string[5] { "low", "medium", "high", "xhigh", "max" }),
		new ModelCatalogEntry("gpt-5.5", "GPT-5.5", "Frontier model for complex coding, research, and real-world work.", "medium", 12, new string[4] { "low", "medium", "high", "xhigh" })
	};

	private static readonly string[] AvailableModels = CatalogModels.Select((ModelCatalogEntry model) => model.Slug).ToArray();

	private static readonly JsonSerializerOptions ModelCatalogJsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly Regex TopLevelAssignmentRegex = new Regex("^(?<key>[A-Za-z0-9_\\-]+)\\s*=", RegexOptions.Compiled);

	private static readonly HashSet<string> ManagedTopLevelKeys = new HashSet<string>(StringComparer.Ordinal) { "model_provider", "model", "review_model", "model_reasoning_effort", "model_catalog_json" };

	private readonly TextBox _keyInput = new TextBox();

	private readonly ComboBox _modelSelect = new ComboBox();

	private readonly TextBox _announcementBox = new TextBox();

	private readonly TextBox _statusText = new TextBox();

	private const uint CoInitApartmentThreaded = 2u;

	private const uint ProcessQueryLimitedInformation = 4096u;

	private const int CoInitSuccess = 0;

	private const int CoInitAlreadyInitialized = 1;

	private const int RpcEChangedMode = -2147417850;

	private static void LogWatcher(string message, Exception? exception = null)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
			Directory.CreateDirectory(text);
			string text2 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
			if (exception != null)
			{
				text2 = text2 + " | " + exception.GetType().Name + ": " + exception.Message;
			}
			File.AppendAllText(Path.Combine(text, "codex-assistant-20.log"), text2 + Environment.NewLine, Encoding.UTF8);
		}
		catch
		{
		}
	}

	public LauncherForm()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected Obj, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected Obj, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected Obj, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected Obj, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Expected Obj, but got Unknown
		StopPreviousWatcherInstances();
		((Control)this).Text = "Codex 助手 2.1.1";
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)0;
		((Form)this).ClientSize = new Size(900, 660);
		((Form)this).FormBorderStyle = (FormBorderStyle)4;
		((Form)this).MaximizeBox = true;
		((Form)this).MinimizeBox = true;
		((Form)this).StartPosition = (FormStartPosition)1;
		((Control)this).BackColor = Colors.Canvas;
		((Control)this).Font = new Font("Microsoft YaHei UI", 9f, (FontStyle)0, (GraphicsUnit)3);
		((Form)this).Icon = Program.LoadAssistantIcon();
		((Control)this).Controls.Add((Control)(object)CreateText("Codex 助手 2.1.1", new Point(48, 26), new Size(300, 36), 22f, (FontStyle)1, Colors.Ink, ((Control)this).BackColor, (HorizontalAlignment)0));
		RoundedPanel roundedPanel = CreateCard(new Point(48, 96), new Size(804, 104));
		roundedPanel.AccentColor = Colors.Primary;
		roundedPanel.AccentWidth = 4;
		((Control)roundedPanel).Controls.Add((Control)(object)CreateText("公告", new Point(24, 18), new Size(70, 22), 10f, (FontStyle)1, Colors.Primary, roundedPanel.FillColor, (HorizontalAlignment)0));
		((Control)_announcementBox).Location = new Point(94, 14);
		((Control)_announcementBox).Size = new Size(678, 72);
		((TextBoxBase)_announcementBox).Multiline = true;
		((TextBoxBase)_announcementBox).ReadOnly = true;
		((Control)_announcementBox).TabStop = false;
		((TextBoxBase)_announcementBox).BorderStyle = (BorderStyle)0;
		((Control)_announcementBox).BackColor = roundedPanel.FillColor;
		((Control)_announcementBox).ForeColor = Colors.Ink;
		_announcementBox.ScrollBars = (ScrollBars)0;
		((Control)_announcementBox).Font = new Font("Microsoft YaHei UI", 12f, (FontStyle)0, (GraphicsUnit)3);
		((Control)_announcementBox).Text = FormatAnnouncement("售后QQ群：1048665530");
		((Control)roundedPanel).Controls.Add((Control)(object)_announcementBox);
		((Control)this).Controls.Add((Control)(object)roundedPanel);
		RoundedPanel roundedPanel2 = CreateCard(new Point(48, 230), new Size(380, 210));
		roundedPanel2.AccentColor = Colors.Secondary;
		roundedPanel2.AccentWidth = 4;
		((Control)roundedPanel2).Controls.Add((Control)(object)CreateText("准备流程", new Point(24, 22), new Size(150, 26), 12f, (FontStyle)1, Colors.Ink, roundedPanel2.FillColor, (HorizontalAlignment)0));
		((Control)roundedPanel2).Controls.Add((Control)(object)CreateText("按顺序完成三步即可启动。", new Point(24, 50), new Size(260, 22), 9f, (FontStyle)0, Colors.Muted, roundedPanel2.FillColor, (HorizontalAlignment)0));
		AddStep((Control)(object)roundedPanel2, 1, 88, "下载并安装 Codex 桌面版");
		AddStep((Control)(object)roundedPanel2, 2, 124, "前往apinexus.dpdns.org获取密钥");
		AddStep((Control)(object)roundedPanel2, 3, 160, "填入密钥后点击开启 Codex");
		((Control)this).Controls.Add((Control)(object)roundedPanel2);
		RoundedPanel roundedPanel3 = CreateCard(new Point(456, 230), new Size(396, 210));
		roundedPanel3.AccentColor = Color.FromArgb(35, 150, 130);
		roundedPanel3.AccentWidth = 4;
		((Control)roundedPanel3).Controls.Add((Control)(object)CreateText("配置 Codex", new Point(24, 22), new Size(160, 26), 12f, (FontStyle)1, Colors.Ink, roundedPanel3.FillColor, (HorizontalAlignment)0));
		((Control)roundedPanel3).Controls.Add((Control)(object)CreateText("密钥", new Point(24, 62), new Size(58, 22), 9f, (FontStyle)1, Colors.Muted, roundedPanel3.FillColor, (HorizontalAlignment)0));
		((Control)_keyInput).Location = new Point(24, 86);
		((Control)_keyInput).Size = new Size(228, 28);
		((TextBoxBase)_keyInput).BorderStyle = (BorderStyle)1;
		((Control)_keyInput).BackColor = Color.White;
		((Control)_keyInput).ForeColor = Colors.Ink;
		((Control)roundedPanel3).Controls.Add((Control)(object)_keyInput);
		LoadSavedApiKey();
		((Control)roundedPanel3).Controls.Add((Control)(object)CreateText("模型", new Point(272, 62), new Size(58, 22), 9f, (FontStyle)1, Colors.Muted, roundedPanel3.FillColor, (HorizontalAlignment)0));
		((Control)_modelSelect).Location = new Point(272, 86);
		((Control)_modelSelect).Size = new Size(98, 28);
		_modelSelect.DropDownStyle = (ComboBoxStyle)2;
		_modelSelect.Items.AddRange(AvailableModels.Cast<object>().ToArray());
		_modelSelect.SelectedItem = "gpt-6-sol";
		((Control)roundedPanel3).Controls.Add((Control)(object)_modelSelect);
		Button val = CreateButton("获取密钥", new Point(24, 142), new Size(150, 38), Colors.Secondary, Color.White);
		((Control)val).Click += (object? _, EventArgs _) =>
		{
			OpenUrl("https://apinexus.dpdns.org");
		};
		((Control)roundedPanel3).Controls.Add((Control)(object)val);
		Button val2 = CreateButton("开启 Codex", new Point(190, 142), new Size(180, 38), Colors.Primary, Color.White);
		((Control)val2).Click += async (object? _, EventArgs _) =>
		{
			await ConfigureCodexAsync(((Control)_keyInput).Text, SelectedModel());
		};
		((Control)roundedPanel3).Controls.Add((Control)(object)val2);
		((Control)this).Controls.Add((Control)(object)roundedPanel3);
		RoundedPanel roundedPanel4 = CreateCard(new Point(48, 470), new Size(804, 112));
		roundedPanel4.AccentColor = Color.FromArgb(230, 148, 63);
		roundedPanel4.AccentWidth = 4;
		((Control)roundedPanel4).Controls.Add((Control)(object)CreateText("活动信息", new Point(24, 20), new Size(100, 24), 11f, (FontStyle)1, Colors.Ink, roundedPanel4.FillColor, (HorizontalAlignment)0));
		((Control)roundedPanel4).Controls.Add((Control)(object)CreateText("B站搜：我不是皮皮奇，三连关注后可免费获取兑换码。", new Point(24, 50), new Size(380, 22), 9.5f, (FontStyle)0, Colors.Ink, roundedPanel4.FillColor, (HorizontalAlignment)0));
		((Control)roundedPanel4).Controls.Add((Control)(object)CreateText("视频链接：https://www.bilibili.com/video/BV1BHN26GETP", new Point(24, 76), new Size(380, 20), 8.5f, (FontStyle)0, Colors.Muted, roundedPanel4.FillColor, (HorizontalAlignment)0));
		GradientButton gradientButton = CreateGradientButton("远程服务", new Point(420, 36), new Size(108, 40), Colors.JumpStart, Colors.JumpEnd, Color.White);
		((Control)gradientButton).Click += (object? _, EventArgs _) =>
		{
			OpenUrl("https://wzyp.cn/item/bjbyqd");
		};
		((Control)roundedPanel4).Controls.Add((Control)(object)gradientButton);
		GradientButton gradientButton2 = CreateGradientButton("中文汉化", new Point(536, 36), new Size(108, 40), Colors.JumpStart, Colors.JumpEnd, Color.White);
		((Control)gradientButton2).Click += (object? _, EventArgs _) =>
		{
			OpenUrl("https://wzyp.cn/item/zm1ofy");
		};
		((Control)roundedPanel4).Controls.Add((Control)(object)gradientButton2);
		GradientButton gradientButton3 = CreateGradientButton("视频教程", new Point(652, 36), new Size(116, 40), Colors.JumpStart, Colors.JumpEnd, Color.White);
		((Control)gradientButton3).Click += (object? _, EventArgs _) =>
		{
			OpenUrl("https://www.bilibili.com/video/BV1BHN26GETP");
		};
		((Control)roundedPanel4).Controls.Add((Control)(object)gradientButton3);
		((Control)this).Controls.Add((Control)(object)roundedPanel4);
		((Control)_statusText).Location = new Point(48, 614);
		((Control)_statusText).Size = new Size(630, 24);
		((TextBoxBase)_statusText).BorderStyle = (BorderStyle)0;
		((TextBoxBase)_statusText).ReadOnly = true;
		((Control)_statusText).TabStop = false;
		((Control)_statusText).BackColor = ((Control)this).BackColor;
		((Control)_statusText).ForeColor = Colors.Muted;
		((Control)_statusText).Text = "公告服务器：https://apinexus.dpdns.org/codex-launcher/announcement.txt";
		((Control)this).Controls.Add((Control)(object)_statusText);
		Button val3 = CreateButton("检查更新", new Point(700, 608), new Size(152, 32), Colors.Secondary, Color.White);
		((Control)val3).Click += async (object? _, EventArgs _) =>
		{
			await CheckForUpdatesAsync(showUpToDate: true);
		};
		((Control)this).Controls.Add((Control)(object)val3);
		ResponsiveFormLayout.Attach((Form)(object)this, new Size(900, 660), 0.65f);
		((Form)this).Shown += async (object? _, EventArgs _) =>
		{
			await LoadAnnouncementAsync();
		};
		((Form)this).Shown += async (object? _, EventArgs _) =>
		{
			await CheckForUpdatesAsync(showUpToDate: false);
		};
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		((Form)this).OnHandleCreated(e);
		Program.ApplyTaskbarIcon(((Control)this).Handle, ((Form)this).Icon);
	}

	private string SelectedModel()
	{
		return _modelSelect.SelectedItem?.ToString() ?? "gpt-6-sol";
	}

	private async Task CheckForUpdatesAsync(bool showUpToDate)
	{
		_ = 8;
		try
		{
			using HttpClient client = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(8.0)
			};
			UpdateManifest manifest = JsonSerializer.Deserialize<UpdateManifest>(await client.GetStringAsync("http://156.238.239.133/codex-launcher/update.json"), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if ((object)manifest == null || !Version.TryParse(manifest.Version, out Version result) || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
			{
				throw new InvalidOperationException("更新清单格式无效。");
			}
			if (result <= CurrentVersion)
			{
				if (showUpToDate)
				{
					MessageBox.Show((IWin32Window)(object)this, $"当前已是最新版本（{CurrentVersion}）。", "检查更新", (MessageBoxButtons)0, (MessageBoxIcon)64);
				}
				return;
			}
			string text = (string.IsNullOrWhiteSpace(manifest.Notes) ? $"发现 Codex 助手 {result}，是否现在更新？" : $"发现 Codex 助手 {result}。\n\n{manifest.Notes}\n\n是否现在更新？");
			if ((int)MessageBox.Show((IWin32Window)(object)this, text, "发现新版本", (MessageBoxButtons)4, (MessageBoxIcon)64) != 6)
			{
				return;
			}
			string tempPath = Path.Combine(Path.GetTempPath(), $"CodexAssistant-{result}.exe");
			using (HttpResponseMessage response = await client.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();
				await using Stream source = await response.Content.ReadAsStreamAsync();
				await using FileStream target = File.Create(tempPath);
				await source.CopyToAsync(target);
			}
			if (!string.IsNullOrWhiteSpace(manifest.Sha256))
			{
				await using FileStream target = File.OpenRead(tempPath);
				if (!string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(target)), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(tempPath);
					throw new InvalidOperationException("更新文件校验失败，已取消安装。");
				}
			}
			string executablePath = Application.ExecutablePath;
			string scriptPath = Path.Combine(Path.GetTempPath(), $"CodexAssistant-update-{Guid.NewGuid():N}.ps1");
			string contents = BuildUpdateScript(tempPath, executablePath, Environment.ProcessId);
			await File.WriteAllTextAsync(scriptPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			StopPreviousWatcherInstances();
			Process.Start(new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
			((Form)this).Close();
		}
		catch (Exception ex)
		{
			if (showUpToDate)
			{
				MessageBox.Show((IWin32Window)(object)this, "检查更新失败：" + ex.Message, "检查更新", (MessageBoxButtons)0, (MessageBoxIcon)48);
			}
		}
	}

	private static string BuildUpdateScript(string source, string destination, int processId)
	{
		return $"$ErrorActionPreference = 'Stop'\n$updateSource = {Literal(source)}\n$updateDestination = {Literal(destination)}\ntry {{\n    Wait-Process -Id {processId} -Timeout 60 -ErrorAction SilentlyContinue\n    $updated = $false\n    for ($attempt = 0; $attempt -lt 30; $attempt++) {{\n        try {{\n            Copy-Item -LiteralPath $updateSource -Destination $updateDestination -Force\n            $updated = $true\n            break\n        }} catch {{ Start-Sleep -Milliseconds 500 }}\n    }}\n    if (-not $updated) {{ throw '无法替换旧程序，请手动下载并替换。' }}\n    Start-Process -FilePath $updateDestination -WorkingDirectory (Split-Path -LiteralPath $updateDestination)\n}} catch {{\n    Add-Type -AssemblyName System.Windows.Forms\n    [System.Windows.Forms.MessageBox]::Show(\"更新未完成：\" + $_.Exception.Message + \"`n新版本已下载到：\" + $updateSource, 'Codex 助手更新') | Out-Null\n}} finally {{\n    Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue\n}}";
		static string Literal(string value)
		{
			return "'" + value.Replace("'", "''") + "'";
		}
	}

	private static string FormatAnnouncement(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return "暂无公告。";
		}
		IEnumerable<string> values = (from line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
			select line.TrimEnd() into line
			where line.Length > 0
			select line).Take(3);
		return string.Join(Environment.NewLine, values);
	}

	private async Task LoadAnnouncementAsync()
	{
		try
		{
			using HttpClient client = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(8.0)
			};
			string text = await client.GetStringAsync("https://apinexus.dpdns.org/codex-launcher/announcement.txt");
			((Control)_announcementBox).Text = FormatAnnouncement(text);
			((Control)_statusText).Text = $"公告已更新：{DateTime.Now:HH:mm:ss}";
		}
		catch (Exception ex)
		{
			((Control)_announcementBox).Text = FormatAnnouncement("售后QQ群：1048665530");
			((Control)_statusText).Text = "公告服务器连接失败，已使用默认公告：" + ex.Message;
		}
	}

	private static RoundedPanel CreateCard(Point location, Size size)
	{
		RoundedPanel roundedPanel = new RoundedPanel();
		((Control)roundedPanel).Location = location;
		((Control)roundedPanel).Size = size;
		roundedPanel.Radius = 8;
		roundedPanel.FillColor = Color.White;
		roundedPanel.BorderColor = Colors.Border;
		return roundedPanel;
	}

	private static TextBox CreateText(string text, Point location, Size size, float fontSize, FontStyle style, Color color, Color backColor, HorizontalAlignment alignment = (HorizontalAlignment)0)
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

	private static Button CreateButton(string text, Point location, Size size, Color backColor, Color foreColor)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected Obj, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected Obj, but got Unknown
		Button val = new Button
		{
			Text = text,
			Location = location,
			Size = size,
			BackColor = backColor,
			ForeColor = foreColor,
			FlatStyle = (FlatStyle)0,
			Cursor = Cursors.Hand,
			Font = new Font("Microsoft YaHei UI", 9f, (FontStyle)1, (GraphicsUnit)3)
		};
		((ButtonBase)val).FlatAppearance.BorderSize = 0;
		((ButtonBase)val).FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.08f);
		((ButtonBase)val).FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.08f);
		return val;
	}

	private static GradientButton CreateGradientButton(string text, Point location, Size size, Color startColor, Color endColor, Color foreColor)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected Obj, but got Unknown
		GradientButton gradientButton = new GradientButton();
		((Control)gradientButton).Text = text;
		((Control)gradientButton).Location = location;
		((Control)gradientButton).Size = size;
		gradientButton.StartColor = startColor;
		gradientButton.EndColor = endColor;
		((Control)gradientButton).ForeColor = foreColor;
		((Control)gradientButton).Cursor = Cursors.Hand;
		((Control)gradientButton).Font = new Font("Microsoft YaHei UI", 9f, (FontStyle)1, (GraphicsUnit)3);
		return gradientButton;
	}

	private static void AddStep(Control parent, int number, int y, string text)
	{
		RoundedPanel roundedPanel = new RoundedPanel();
		((Control)roundedPanel).Location = new Point(24, y - 3);
		((Control)roundedPanel).Size = new Size(26, 26);
		roundedPanel.Radius = 8;
		roundedPanel.FillColor = Color.FromArgb(232, 240, 255);
		roundedPanel.BorderColor = Color.FromArgb(198, 215, 248);
		RoundedPanel roundedPanel2 = roundedPanel;
		((Control)roundedPanel2).Controls.Add((Control)(object)CreateText(number.ToString(), new Point(0, 5), new Size(26, 16), 8f, (FontStyle)1, Colors.AccentDark, roundedPanel2.FillColor, (HorizontalAlignment)2));
		parent.Controls.Add((Control)(object)roundedPanel2);
		Color backColor = ((parent is RoundedPanel roundedPanel3) ? roundedPanel3.FillColor : Color.White);
		parent.Controls.Add((Control)(object)CreateText(text, new Point(62, y), new Size(280, 22), 9f, (FontStyle)0, Colors.Ink, backColor, (HorizontalAlignment)0));
	}

	private static void OpenUrl(string url)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = url,
			UseShellExecute = true
		});
	}

	private void LoadSavedApiKey()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json");
			if (File.Exists(path))
			{
				string text = (JsonNode.Parse(File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)))?.AsObject())?["OPENAI_API_KEY"]?.GetValue<string>();
				if (!string.IsNullOrWhiteSpace(text))
				{
					((Control)_keyInput).Text = text;
					((TextBoxBase)_keyInput).SelectionStart = ((TextBoxBase)_keyInput).TextLength;
				}
			}
		}
		catch
		{
		}
	}

	private static string BuildImageInjectionScript()
	{
		return "(() => {\r\n  const navId = \"codex-assistant-20-nav\";\r\n  const pageId = \"codex-assistant-20-page\";\r\n  const pageClass = \"codex-assistant-20-page\";\r\n  const styleId = \"codex-assistant-20-style\";\r\n  const imageNavId = \"codex-imagegen-test-nav\";\r\n  const imagePageId = \"codex-imagegen-test-page\";\r\n  const buttonText = \"充值\";\r\n  const remoteServiceUrl = \"https://wzyp.cn/item/bjbyqd\";\r\n  const localizationUrl = \"https://wzyp.cn/item/zm1ofy\";\r\n  const videoUrl = \"https://www.bilibili.com/video/BV1BHN26GETP\";\r\n  const topupUrl = \"https://apinexus.dpdns.org/console/topup\";\r\n\r\n  function closePage() {\r\n    document.getElementById(pageId)?.remove();\r\n    document.getElementById(navId)?.querySelector(\"button\")?.setAttribute(\"aria-current\", \"false\");\r\n  }\r\n\r\n  function closeImagePage() {\r\n    document.getElementById(imagePageId)?.remove();\r\n    document.getElementById(imageNavId)?.querySelector(\"button\")?.setAttribute(\"aria-current\", \"false\");\r\n  }\r\n\r\n  function positionPage(page) {\r\n    const sidebar = document.querySelector(\"aside.app-shell-left-panel\");\r\n    const rect = sidebar?.getBoundingClientRect?.();\r\n    const left = rect && rect.width > 0 ? Math.max(0, rect.right) : 0;\r\n    page.style.left = `${left}px`;\r\n  }\r\n\r\n  function openPage() {\r\n    closeImagePage();\r\n    closePage();\r\n    const page = document.createElement(\"div\");\r\n    page.id = pageId;\r\n    page.className = pageClass;\r\n    page.setAttribute(\"role\", \"dialog\");\r\n    page.setAttribute(\"aria-modal\", \"true\");\r\n    page.setAttribute(\"aria-label\", \"充值\");\r\n    Object.assign(page.style, {\r\n      position: \"fixed\", left: \"0\", top: \"0\", bottom: \"0\", width: \"min(420px, 100vw)\",\r\n      zIndex: \"2147483000\", background: \"var(--bg-primary, #fff)\",\r\n      color: \"var(--text-primary, #171717)\", borderRight: \"1px solid rgba(0,0,0,.12)\",\r\n      boxShadow: \"8px 0 24px rgba(0,0,0,.14)\", fontFamily: \"inherit\", overflow: \"auto\"\r\n    });\r\n    page.innerHTML = `\r\n      <div style=\"height:100%;display:flex;flex-direction:column\">\r\n        <header style=\"display:flex;align-items:center;padding:18px 20px;border-bottom:1px solid rgba(0,0,0,.10);font-weight:700;font-size:18px\">\r\n          <span>充值</span>\r\n        </header>\r\n        <div class=\"codex-assistant-20-actions\">\r\n          <button type=\"button\" class=\"codex-assistant-20-topup\" data-action=\"topup\">充值</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-secondary\" data-action=\"remote\">远程服务</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-secondary\" data-action=\"localization\">中文汉化</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-secondary\" data-action=\"video\">视频教程</button>\r\n          <button type=\"button\" class=\"codex-assistant-20-close\" data-action=\"close\">关闭</button>\r\n        </div>\r\n      </div>`;\r\n    let style = document.getElementById(styleId);\r\n    if (!style) {\r\n      style = document.createElement(\"style\");\r\n      style.id = styleId;\r\n      style.textContent = `\r\n        #${pageId} .codex-assistant-20-actions { padding: 22px 20px; display: grid; gap: 12px; }\r\n        #${pageId} button { width: 100%; box-sizing: border-box; border-radius: 12px; font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; transition: transform .15s ease, box-shadow .15s ease, background .15s ease; }\r\n        #${pageId} button:hover { transform: translateY(-1px); }\r\n        #${pageId} .codex-assistant-20-topup { padding: 15px 16px; border: 0; color: #fff; font-size: 17px; font-weight: 700; letter-spacing: .08em; background: linear-gradient(135deg, #ff8a1f 0%, #ff3d68 100%); box-shadow: 0 8px 18px rgba(255, 89, 72, .28); }\r\n        #${pageId} .codex-assistant-20-topup:hover { box-shadow: 0 10px 22px rgba(255, 89, 72, .38); }\r\n        #${pageId} .codex-assistant-20-secondary { padding: 12px 14px; border: 1px solid rgba(99, 115, 145, .24); color: var(--text-primary, #273142); background: var(--bg-secondary, #f6f8fc); }\r\n        #${pageId} .codex-assistant-20-secondary:hover { background: var(--bg-hover, #edf2fb); }\r\n        #${pageId} .codex-assistant-20-close { margin-top: 4px; padding: 10px 14px; border: 1px solid rgba(99, 115, 145, .32); color: var(--text-secondary, #64748b); background: transparent; }\r\n      `;\r\n      document.head?.appendChild(style);\r\n    }\r\n    positionPage(page);\r\n    page.addEventListener(\"click\", (event) => {\r\n      const target = event.target instanceof Element ? event.target : event.target?.parentElement;\r\n      const action = target?.closest(\"[data-action]\")?.getAttribute(\"data-action\");\r\n      if (action === \"close\") {\r\n        event.preventDefault();\r\n        event.stopPropagation();\r\n        closePage();\r\n        return;\r\n      }\r\n      if (action === \"remote\") { window.open(remoteServiceUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n      if (action === \"localization\") { window.open(localizationUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n      if (action === \"video\") { window.open(videoUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n      if (action === \"topup\") { window.open(topupUrl, \"_blank\", \"noopener,noreferrer\"); closePage(); return; }\n    }, true);\r\n    document.body.appendChild(page);\r\n    document.getElementById(navId)?.querySelector(\"button\")?.setAttribute(\"aria-current\", \"page\");\r\n  }\r\n\r\n  function installSidebarNavigation() {\r\n    const navigation = document.querySelector(\"aside.app-shell-left-panel nav[role=navigation], nav[role=navigation]\");\r\n    if (!navigation) return false;\r\n    const navButtons = Array.from(navigation.querySelectorAll(\"button\"));\r\n    const pluginButton = navButtons.find((button) => /^(插件|Plugins)$/i.test((button.getAttribute(\"aria-label\") || button.textContent || \"\").replace(/\\\\s+/g, \" \").trim()));\r\n    const insertionButton = pluginButton || navButtons.find((button) => /^(新对话|New chat|已安排|Scheduled|拉取请求|Pull requests)$/i.test((button.getAttribute(\"aria-label\") || button.textContent || \"\").replace(/\\\\s+/g, \" \").trim())) || navButtons[0];\r\n    const parent = insertionButton?.parentElement || navigation;\r\n    let wrapper = document.getElementById(navId);\r\n    if (!wrapper || wrapper.parentElement !== parent) {\r\n      wrapper?.remove();\r\n      wrapper = document.createElement(\"div\");\r\n      wrapper.id = navId;\r\n      wrapper.dataset.codexAssistant20 = \"true\";\r\n      const button = (insertionButton || document.createElement(\"button\")).cloneNode(true);\r\n      button.type = \"button\";\r\n      button.removeAttribute(\"disabled\");\r\n      button.removeAttribute(\"aria-disabled\");\r\n      button.removeAttribute(\"data-state\");\r\n      button.setAttribute(\"aria-label\", buttonText);\r\n      button.textContent = \"\";\r\n      button.innerHTML = `<span aria-hidden=\"true\" style=\"font-size:16px;line-height:1\">✦</span><span class=\"truncate\">${buttonText}</span>`;\r\n      button.addEventListener(\"click\", (event) => { event.preventDefault(); event.stopPropagation(); openPage(); }, true);\r\n      wrapper.appendChild(button);\r\n      if (insertionButton?.nextSibling) parent.insertBefore(wrapper, insertionButton.nextSibling); else parent.appendChild(wrapper);\r\n    }\r\n\r\n    let imageWrapper = document.getElementById(imageNavId);\r\n    if (!imageWrapper || imageWrapper.parentElement !== parent) {\r\n      imageWrapper?.remove();\r\n      imageWrapper = document.createElement(\"div\");\r\n      imageWrapper.id = imageNavId;\r\n      imageWrapper.dataset.codexImagegenTest = \"true\";\r\n      const imageButton = (insertionButton || document.createElement(\"button\")).cloneNode(true);\r\n      imageButton.type = \"button\";\r\n      imageButton.removeAttribute(\"disabled\");\r\n      imageButton.removeAttribute(\"aria-disabled\");\r\n      imageButton.removeAttribute(\"data-state\");\r\n      imageButton.setAttribute(\"aria-label\", \"生图\");\r\n      imageButton.textContent = \"\";\r\n      imageButton.innerHTML = `<span aria-hidden=\"true\" style=\"font-size:16px;line-height:1\">✦</span><span class=\"truncate\">生图</span>`;\r\n      imageButton.addEventListener(\"click\", async (event) => {\r\n        event.preventDefault(); event.stopPropagation();\r\n        window.__assistantOpenImageSite = true;\r\n      }, true);\r\n      imageWrapper.appendChild(imageButton);\r\n    }\r\n    if (imageWrapper.parentElement !== parent) parent.appendChild(imageWrapper);\r\n    if (wrapper.nextSibling !== imageWrapper) parent.insertBefore(imageWrapper, wrapper.nextSibling);\r\n    return true;\r\n  }\r\n\r\n  const wasInstalled = window.__codexAssistant20InstalledVersion === \"204-close-actions\";\n  if (!wasInstalled) {\r\n    window.__codexAssistant20Observer?.disconnect();\r\n    window.__codexAssistant20Observer = null;\r\n    document.getElementById(imageNavId)?.remove();\r\n    window.__codexAssistant20InstalledVersion = \"204-close-actions\";\n    document.getElementById(pageId)?.remove();\r\n    document.querySelectorAll(`.${pageClass}`).forEach((node) => node.remove());\r\n    document.getElementById(navId)?.remove();\r\n  }\r\n\r\n  if (!window.__codexAssistant20Observer && document.documentElement) {\r\n    const observer = new MutationObserver(() => {\r\n      try { installSidebarNavigation(); } catch {}\r\n    });\r\n    observer.observe(document.documentElement, { childList: true, subtree: true });\r\n    window.__codexAssistant20Observer = observer;\r\n  }\r\n\r\n  if (!window.__codexAssistant20RetryTimer) {\r\n    const retryTimer = setInterval(() => {\r\n      try {\r\n        if (installSidebarNavigation() || !document.documentElement) {\r\n          clearInterval(retryTimer);\r\n          window.__codexAssistant20RetryTimer = null;\r\n        }\r\n      } catch {}\r\n    }, 300);\r\n    window.__codexAssistant20RetryTimer = retryTimer;\r\n    setTimeout(() => {\r\n      clearInterval(retryTimer);\r\n      if (window.__codexAssistant20RetryTimer === retryTimer) window.__codexAssistant20RetryTimer = null;\r\n    }, 15000);\r\n  }\r\n\r\n  try { installSidebarNavigation(); } catch {}\r\n  if (!window.__codexAssistant20ResizeBound) {\r\n    window.__codexAssistant20ResizeBound = true;\r\n    window.addEventListener(\"resize\", () => {\r\n      const page = document.getElementById(pageId);\r\n      if (page) positionPage(page);\r\n    });\r\n  }\r\n  return true;\r\n})();\n(() => {\r\nconst openExternal = window.__assistantOpenImageSite === true;\r\nwindow.__assistantOpenImageSite = false;\r\nreturn { openExternal };\r\n})()";
	}

	internal static async Task RunPersistentWatcherAsync()
	{
		await RunPersistentLauncherCoreAsync(launchOnStart: false, showLaunchErrors: false);
	}

	internal static async Task RunPersistentLauncherAsync()
	{
		await RunPersistentLauncherCoreAsync(launchOnStart: true, showLaunchErrors: false);
	}

	internal static async Task RunShortcutLauncherAsync()
	{
		await RunPersistentLauncherCoreAsync(launchOnStart: true, showLaunchErrors: true);
	}

	private static async Task RunPersistentLauncherCoreAsync(bool launchOnStart, bool showLaunchErrors)
	{
		StopPreviousWatcherInstances();
		using Mutex mutex = new Mutex(initiallyOwned: false, "Local\\CodexAssistant20Watcher");
		bool flag;
		try
		{
			flag = mutex.WaitOne(0);
		}
		catch (AbandonedMutexException)
		{
			flag = true;
		}
		if (!flag & launchOnStart)
		{
			for (int attempt = 0; attempt < 12; attempt++)
			{
				if (flag)
				{
					break;
				}
				await Task.Delay(1000);
				try
				{
					flag = mutex.WaitOne(0);
				}
				catch (AbandonedMutexException)
				{
					flag = true;
				}
			}
		}
		if (!flag)
		{
			if (showLaunchErrors)
			{
				MessageBox.Show("ChatGPT 启动器正在运行，请稍等几秒后重试。", "ChatGPT", (MessageBoxButtons)0, (MessageBoxIcon)64);
			}
			return;
		}
		try
		{
			LogWatcher($"watcher started; pid={Environment.ProcessId}; launchOnStart={launchOnStart}");
			bool codexSessionObserved = false;
			int attempt = 0;
			if (launchOnStart)
			{
				try
				{
					await LaunchCodexAndInjectAsync();
					codexSessionObserved = true;
					LogWatcher("initial Codex launch and injection completed");
				}
				catch (Exception ex3)
				{
					codexSessionObserved = true;
					LogWatcher("initial Codex launch failed", ex3);
					if (showLaunchErrors)
					{
						MessageBox.Show("无法正常启动或嵌入 ChatGPT：\n" + ex3.Message + "\n\n诊断日志：%USERPROFILE%\\.codex\\codex-assistant-20.log", "ChatGPT 启动失败", (MessageBoxButtons)0, (MessageBoxIcon)16);
					}
				}
			}
			while (true)
			{
				try
				{
					List<CdpTarget> list = await GetCdpTargetsAsync();
					if (list.Count > 0)
					{
						codexSessionObserved = true;
						attempt = 0;
						await InjectCodexTargetsAsync(list);
					}
					else if (codexSessionObserved && !HasRunningCodexDesktopProcess())
					{
						attempt++;
						if (attempt >= 3)
						{
							LogWatcher("Codex process is no longer running; watcher exiting");
							break;
						}
					}
					else if (codexSessionObserved)
					{
						attempt = 0;
					}
				}
				catch (Exception exception)
				{
					LogWatcher("watcher poll failed", exception);
				}
				await Task.Delay(3000);
			}
		}
		finally
		{
			try
			{
				mutex.ReleaseMutex();
			}
			catch
			{
			}
		}
	}

	private static void InstallPersistentWatcher()
	{
		try
		{
			StopPreviousWatcherInstances();
			string executablePath = Application.ExecutablePath;
			string iconPath = EnsureShortcutIcon();
			string value = "\"" + executablePath + "\" --background-launch";
			using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
			{
				registryKey?.DeleteValue("CodexAssistant19Watcher", throwOnMissingValue: false);
				registryKey?.SetValue("CodexAssistant20Watcher", value, RegistryValueKind.String);
			}
			RemoveDuplicateStartupShortcut();
			CreateLauncherShortcut(executablePath, "--shortcut-launch", iconPath);
			Process.Start(new ProcessStartInfo
			{
				FileName = executablePath,
				Arguments = "--background-launch",
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
		}
		catch
		{
		}
	}

	private static void RemoveDuplicateStartupShortcut()
	{
		DeleteIfExists(Path.Combine(Path.Combine(new string[6]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"Microsoft",
			"Windows",
			"Start Menu",
			"Programs",
			"Startup"
		}), "Codex助手启动器.lnk"));
	}

	private static void CreateLauncherShortcut(string executable, string arguments, string iconPath)
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		DeleteIfExists(Path.Combine(folderPath, "Codex助手启动 Codex.lnk"));
		CreateShortcutFile(Path.Combine(folderPath, "ChatGPT.lnk"), executable, arguments, iconPath);
		string text = Path.Combine(new string[5]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"Microsoft",
			"Windows",
			"Start Menu",
			"Programs"
		});
		Directory.CreateDirectory(text);
		DeleteIfExists(Path.Combine(text, "Codex助手启动 Codex.lnk"));
		CreateShortcutFile(Path.Combine(text, "ChatGPT.lnk"), executable, arguments, iconPath);
	}

	private static string EnsureShortcutIcon()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "Codex助手2.0-ChatGPT-black.ico");
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		using Stream stream = typeof(Program).Assembly.GetManifestResourceStream("CodexLauncherOnline20.ChatGpt.ico") ?? throw new InvalidOperationException("内置快捷方式图标资源不可用。");
		using FileStream destination = File.Create(text);
		stream.CopyTo(destination);
		return text;
	}

	internal static void RefreshExistingShortcutIcons()
	{
		try
		{
			string text = EnsureShortcutIcon();
			string[] source = new string[2]
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChatGPT.lnk"),
				Path.Combine(new string[6]
				{
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"Microsoft",
					"Windows",
					"Start Menu",
					"Programs",
					"ChatGPT.lnk"
				})
			};
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			if ((object)typeFromProgID == null)
			{
				return;
			}
			dynamic val = Activator.CreateInstance(typeFromProgID);
			foreach (string item in source.Where(File.Exists))
			{
				dynamic val2 = val.CreateShortcut(item);
				dynamic val3 = Convert.ToString(val2.TargetPath) ?? string.Empty;
				dynamic fileNameWithoutExtension = Path.GetFileNameWithoutExtension(val3);
				if ((!fileNameWithoutExtension.StartsWith("Codex助手", StringComparison.OrdinalIgnoreCase)))
				{
					Marshal.FinalReleaseComObject(val2);
					continue;
				}
				val2.TargetPath = Application.ExecutablePath;
				val2.Arguments = "--shortcut-launch";
				val2.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory;
				val2.IconLocation = text + ",0";
				val2.Save();
				Program.SHChangeNotify(8192u, 5u, item, IntPtr.Zero);
				try
				{
					Marshal.FinalReleaseComObject(val2);
				}
				catch
				{
				}
			}
			try
			{
				Marshal.FinalReleaseComObject(val);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static void DeleteIfExists(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static void CreateShortcutFile(string shortcutPath, string executable, string arguments, string iconPath)
	{
		Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
		if ((object)typeFromProgID == null)
		{
			return;
		}
		dynamic val = Activator.CreateInstance(typeFromProgID);
		dynamic val2 = val.CreateShortcut(shortcutPath);
		val2.TargetPath = executable;
		val2.Arguments = arguments;
		val2.WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory;
		val2.Description = "ChatGPT";
		val2.IconLocation = iconPath + ",0";
		val2.WindowStyle = 7;
		val2.Save();
		Program.SHChangeNotify(8192u, 5u, shortcutPath, IntPtr.Zero);
		try
		{
			Marshal.FinalReleaseComObject(val2);
			Marshal.FinalReleaseComObject(val);
		}
		catch
		{
		}
	}

	private static void StopPreviousWatcherInstances()
	{
		int processId = Environment.ProcessId;
		string fullPath = Path.GetFullPath(Application.ExecutablePath);
		foreach (string item in new string[2]
		{
			Path.GetFileNameWithoutExtension(fullPath),
			"Codex助手1.9"
		}.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			Process[] processesByName = Process.GetProcessesByName(item);
			foreach (Process process in processesByName)
			{
				try
				{
					if (process.Id != processId && process.MainWindowHandle == IntPtr.Zero)
					{
						string text = TryGetProcessPath(process);
						string a = (string.IsNullOrWhiteSpace(text) ? string.Empty : Path.GetFileNameWithoutExtension(text));
						if (string.Equals(text, fullPath, StringComparison.OrdinalIgnoreCase) || string.Equals(a, "Codex助手1.9", StringComparison.OrdinalIgnoreCase))
						{
							process.Kill(entireProcessTree: true);
						}
					}
				}
				catch (InvalidOperationException)
				{
				}
				catch (Win32Exception)
				{
				}
				finally
				{
					process.Dispose();
				}
			}
		}
	}

	private static bool HasRunningPackagedCodex(string packageDirectory)
	{
		Process[] processesByName = Process.GetProcessesByName("ChatGPT");
		foreach (Process process in processesByName)
		{
			try
			{
				string text = TryGetProcessPath(process);
				if (!string.IsNullOrWhiteSpace(text) && text.StartsWith(packageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			finally
			{
				process.Dispose();
			}
		}
		return false;
	}

	private static bool HasRunningCodexExecutable(string executablePath)
	{
		string fullPath = Path.GetFullPath(executablePath);
		Process[] processesByName = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullPath));
		foreach (Process process in processesByName)
		{
			try
			{
				if (string.Equals(TryGetProcessPath(process), fullPath, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			finally
			{
				process.Dispose();
			}
		}
		return false;
	}

	private static bool HasRunningCodexDesktopProcess()
	{
		string[] array = new string[2] { "ChatGPT", "Codex" };
		for (int i = 0; i < array.Length; i++)
		{
			Process[] processesByName = Process.GetProcessesByName(array[i]);
			foreach (Process process in processesByName)
			{
				try
				{
					string text = TryGetProcessPath(process);
					if (string.IsNullOrWhiteSpace(text))
					{
						return true;
					}
					if (text.Contains("\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) || text.Contains("\\Codex\\", StringComparison.OrdinalIgnoreCase) || text.EndsWith("\\Codex.exe", StringComparison.OrdinalIgnoreCase) || text.EndsWith("\\ChatGPT.exe", StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
				finally
				{
					process.Dispose();
				}
			}
		}
		return false;
	}

	private static async Task ConfigureCodexAsync(string apiKey, string model)
	{
		apiKey = apiKey.Trim();
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			MessageBox.Show("请先填写密钥。", "Codex 助手", (MessageBoxButtons)0, (MessageBoxIcon)48);
			return;
		}
		try
		{
			await ValidateApiKeyAsync(apiKey);
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
			string path = Path.Combine(text, "config.toml");
			string path2 = Path.Combine(text, "auth.json");
			string path3 = Path.Combine(text, "codex-launcher-model-catalog.json");
			Directory.CreateDirectory(text);
			BackupIfExists(path);
			BackupIfExists(path2);
			BackupIfExists(path3);
			string oldConfig = (File.Exists(path) ? File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)) : string.Empty);
			WriteUtf8NoBom(path, BuildCodexConfig(oldConfig, model));
			WriteUtf8NoBom(path2, BuildAuthJson(apiKey));
			WriteUtf8NoBom(path3, BuildModelCatalogJson());
			try
			{
				StopPreviousWatcherInstances();
				await LaunchCodexAndInjectAsync(forceRestart: true);
				InstallPersistentWatcher();
				MessageBox.Show("配置成功，重启 Codex 生效！", "Codex 助手", (MessageBoxButtons)0, (MessageBoxIcon)64);
			}
			catch
			{
				InstallPersistentWatcher();
				MessageBox.Show("配置成功，重新打开 Codex 生效！", "Codex 助手", (MessageBoxButtons)0, (MessageBoxIcon)64);
			}
		}
		catch (ApiKeyValidationException)
		{
			MessageBox.Show("密钥错误（密钥不是兑换码）", "Codex 助手", (MessageBoxButtons)0, (MessageBoxIcon)16);
		}
		catch (Exception ex2)
		{
			MessageBox.Show("配置失败：\n" + ex2.Message, "Codex 助手", (MessageBoxButtons)0, (MessageBoxIcon)16);
		}
	}

	private static async Task LaunchCodexAndInjectAsync(bool forceRestart = false)
	{
		List<CdpTarget> list = await GetCdpTargetsAsync();
		if (forceRestart)
		{
			PackagedCodexApp packagedCodexApp = FindCodexPackagedApp();
			if ((object)packagedCodexApp != null)
			{
				await StopRunningPackagedCodexAsync(packagedCodexApp.PackageDirectory);
				list = new List<CdpTarget>();
			}
		}
		if (list.Count == 0)
		{
			PackagedCodexApp packagedApp = FindCodexPackagedApp();
			if ((object)packagedApp != null)
			{
				await StopRunningPackagedCodexAsync(packagedApp.PackageDirectory);
				ActivatePackagedCodex(packagedApp.AppUserModelId);
			}
			else
			{
				string text = FindCodexExecutable();
				if (text == null)
				{
					throw new FileNotFoundException("未找到 Codex 桌面版，请先安装 Codex。", "Codex.exe");
				}
				if (Process.Start(new ProcessStartInfo
				{
					FileName = text,
					WorkingDirectory = (Path.GetDirectoryName(text) ?? Environment.CurrentDirectory),
					UseShellExecute = false,
					CreateNoWindow = true,
					Arguments = $"--remote-debugging-port={9229} --remote-allow-origins=http://127.0.0.1:{9229}"
				}) == null)
				{
					throw new InvalidOperationException("无法启动 Codex 桌面版。");
				}
			}
			list = await WaitForCdpTargetsAsync();
		}
		await InjectCodexTargetsAsync(list);
	}

	private static PackagedCodexApp? FindCodexPackagedApp()
	{
		PackagedCodexApp packagedCodexApp = FindRegisteredCodexPackage();
		if ((object)packagedCodexApp != null)
		{
			return packagedCodexApp;
		}
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
		try
		{
			if (!Directory.Exists(path))
			{
				return null;
			}
			DirectoryInfo directoryInfo = (from path2 in Directory.EnumerateDirectories(path, "OpenAI.Codex_*", SearchOption.TopDirectoryOnly)
				select new DirectoryInfo(path2) into info
				where File.Exists(Path.Combine(info.FullName, "app", "ChatGPT.exe"))
				orderby ParseCodexPackageVersion(info.Name) descending, info.LastWriteTimeUtc descending
				select info).FirstOrDefault();
			if (directoryInfo != null)
			{
				string[] array = directoryInfo.Name.Split('_', StringSplitOptions.RemoveEmptyEntries);
				if (array.Length < 2)
				{
					return null;
				}
				return new PackagedCodexApp(array[0] + "_" + array[^1] + "!App", directoryInfo.FullName);
			}
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
		return null;
	}

	private static PackagedCodexApp? FindRegisteredCodexPackage()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "powershell.exe",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "$pkg = Get-AppxPackage -Name OpenAI.Codex | Sort-Object {[version]$_.Version} -Descending | Select-Object -First 1; if ($null -ne $pkg) { [pscustomobject]@{ PackageFamilyName = $pkg.PackageFamilyName; InstallLocation = $pkg.InstallLocation } | ConvertTo-Json -Compress }" }
			});
			if (process == null)
			{
				return null;
			}
			Task<string> task = process.StandardOutput.ReadToEndAsync();
			Task<string> task2 = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(8000))
			{
				process.Kill(entireProcessTree: true);
				LogWatcher("Get-AppxPackage timed out while locating Codex");
				return null;
			}
			Task.WaitAll(new Task[2] { task, task2 }, 1000);
			string text = (task.IsCompletedSuccessfully ? task.Result.Trim() : string.Empty);
			if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(text))
			{
				string text2 = (task2.IsCompletedSuccessfully ? task2.Result.Trim() : string.Empty);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					LogWatcher("Get-AppxPackage failed: " + text2);
				}
				return null;
			}
			JsonObject jsonObject = JsonNode.Parse(text)?.AsObject();
			string text3 = jsonObject?["PackageFamilyName"]?.GetValue<string>();
			string text4 = jsonObject?["InstallLocation"]?.GetValue<string>();
			if (string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4))
			{
				return null;
			}
			return new PackagedCodexApp(text3 + "!App", text4);
		}
		catch (Exception exception)
		{
			LogWatcher("registered Codex package lookup failed", exception);
			return null;
		}
	}

	private static Version ParseCodexPackageVersion(string packageName)
	{
		string[] array = packageName.Split('_', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length <= 1 || !Version.TryParse(array[1], out Version result))
		{
			return new Version(0, 0);
		}
		return result;
	}

	private static async Task StopRunningPackagedCodexAsync(string packageDirectory)
	{
		List<Process> processes = new List<Process>();
		Process[] processesByName = Process.GetProcessesByName("ChatGPT");
		foreach (Process process in processesByName)
		{
			string text = TryGetProcessPath(process);
			if (!string.IsNullOrWhiteSpace(text) && text.StartsWith(packageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				processes.Add(process);
			}
			else
			{
				process.Dispose();
			}
		}
		if (processes.Count == 0)
		{
			return;
		}
		try
		{
			foreach (Process item in processes.Where((Process process2) => process2.MainWindowHandle != IntPtr.Zero))
			{
				try
				{
					item.CloseMainWindow();
				}
				catch (InvalidOperationException)
				{
				}
			}
			for (int attempt = 0; attempt < 40; attempt++)
			{
				if (processes.All(HasProcessExited))
				{
					return;
				}
				await Task.Delay(250);
			}
			foreach (Process item2 in processes.Where((Process process2) => !HasProcessExited(process2)))
			{
				try
				{
					item2.Kill(entireProcessTree: true);
				}
				catch (InvalidOperationException)
				{
				}
				catch (Win32Exception)
				{
				}
			}
			for (int attempt = 0; attempt < 20; attempt++)
			{
				if (!processes.Any((Process process2) => !HasProcessExited(process2)))
				{
					break;
				}
				await Task.Delay(250);
			}
		}
		finally
		{
			foreach (Process item3 in processes)
			{
				item3.Dispose();
			}
		}
	}

	private static bool HasProcessExited(Process process)
	{
		try
		{
			return process.HasExited;
		}
		catch (InvalidOperationException)
		{
			return true;
		}
	}

	private static string? TryGetProcessPath(Process process)
	{
		try
		{
			string text = process.MainModule?.FileName;
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		catch (Win32Exception)
		{
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		nint num = OpenProcess(4096u, inheritHandle: false, process.Id);
		if (num == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			int size = 32768;
			StringBuilder stringBuilder = new StringBuilder(size);
			return QueryFullProcessImageName(num, 0u, stringBuilder, ref size) ? stringBuilder.ToString() : null;
		}
		finally
		{
			CloseHandle(num);
		}
	}

	private static Process? ActivatePackagedCodex(string appUserModelId)
	{
		string arguments = $"--remote-debugging-port={9229} --remote-allow-origins=http://127.0.0.1:{9229}";
		int num = CoInitializeEx(IntPtr.Zero, 2u);
		bool flag = (uint)num <= 1u;
		bool flag2 = flag;
		try
		{
			if (num != 0 && num != 1 && num != -2147417850)
			{
				Marshal.ThrowExceptionForHR(num);
			}
			int num2 = ((IApplicationActivationManager)(object)new ApplicationActivationManager()).ActivateApplication(appUserModelId, arguments, 0u, out var processId);
			if (num2 != 0)
			{
				Marshal.ThrowExceptionForHR(num2);
			}
			try
			{
				return Process.GetProcessById((int)processId);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}
		finally
		{
			if (flag2)
			{
				CoUninitialize();
			}
		}
	}

	private static string? FindCodexExecutable()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		List<(string Path, int Priority)> candidates = new List<(string, int)>();
		AddCandidate(Path.Combine(folderPath, "OpenAI", "Codex", "Codex.exe"), 3);
		AddCandidate(Path.Combine(folderPath, "Programs", "Codex", "Codex.exe"), 3);
		AddCandidate(Path.Combine(folderPath, "Programs", "Codex", "ChatGPT.exe"), 4);
		AddCandidate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Codex", "Codex.exe"), 3);
		AddCandidate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenAI", "Codex", "Codex.exe"), 3);
		AddCandidate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenAI", "Codex", "ChatGPT.exe"), 4);
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
		try
		{
			if (Directory.Exists(path))
			{
				foreach (string item in Directory.EnumerateDirectories(path, "OpenAI.Codex_*", SearchOption.TopDirectoryOnly))
				{
					AddCandidate(Path.Combine(item, "app", "ChatGPT.exe"), 10);
				}
			}
		}
		catch (UnauthorizedAccessException)
		{
		}
		return (from @group in candidates.Where(((string Path, int Priority) candidate) => File.Exists(candidate.Path)).GroupBy(((string Path, int Priority) candidate) => candidate.Path, StringComparer.OrdinalIgnoreCase)
			select @group.OrderByDescending(((string Path, int Priority) candidate) => candidate.Priority).First() into candidate
			select (candidate: candidate, File: new FileInfo(candidate.Path)) into item
			orderby item.candidate.Priority descending, item.File.LastWriteTimeUtc descending
			select item.File.FullName).FirstOrDefault();
		void AddCandidate(string item, int priority)
		{
			candidates.Add((item, priority));
		}
	}

	private static async Task<List<CdpTarget>> GetCdpTargetsAsync()
	{
		_ = 1;
		try
		{
			using HttpResponseMessage response = await CdpHttpClient.GetAsync($"http://127.0.0.1:{9229}/json/list");
			if (!response.IsSuccessStatusCode)
			{
				return new List<CdpTarget>();
			}
			return (JsonNode.Parse(await response.Content.ReadAsStringAsync())?.AsArray())?.OfType<JsonObject>().Where((JsonObject node) => string.Equals(node["type"]?.GetValue<string>(), "page", StringComparison.OrdinalIgnoreCase)).Select((JsonObject node) => new CdpTarget(node["title"]?.GetValue<string>() ?? string.Empty, node["url"]?.GetValue<string>() ?? string.Empty, node["webSocketDebuggerUrl"]?.GetValue<string>()))
				.Where((CdpTarget target) => !string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
				.ToList() ?? new List<CdpTarget>();
		}
		catch (HttpRequestException)
		{
			return new List<CdpTarget>();
		}
		catch (TaskCanceledException)
		{
			return new List<CdpTarget>();
		}
	}

	private static async Task<List<CdpTarget>> WaitForCdpTargetsAsync()
	{
		for (int attempt = 0; attempt < 40; attempt++)
		{
			List<CdpTarget> list = await GetCdpTargetsAsync();
			if (list.Count > 0)
			{
				return list;
			}
			await Task.Delay(500);
		}
		throw new TimeoutException("等待 Codex 页面加载超时，请关闭已有 Codex 实例后重试。");
	}

	private static async Task<bool> InjectCodexTargetsAsync(IEnumerable<CdpTarget> targets)
	{
		bool injected = false;
		foreach (CdpTarget target in targets.Where((CdpTarget item) => !string.IsNullOrWhiteSpace(item.WebSocketDebuggerUrl)))
		{
			try
			{
				await InjectCodexAssistantAsync(target.WebSocketDebuggerUrl);
				injected = true;
			}
			catch (Exception exception)
			{
				LogWatcher("target injection failed: title=" + target.Title + "; url=" + target.Url, exception);
			}
		}
		if (!injected)
		{
			throw new InvalidOperationException("没有任何 Codex 页面完成注入。");
		}
		return true;
	}

	private static async Task InjectCodexAssistantAsync(string webSocketDebuggerUrl)
	{
		using ClientWebSocket socket = new ClientWebSocket();
		using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
		await socket.ConnectAsync(new Uri(webSocketDebuggerUrl), cancellation.Token);
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(new
		{
			id = 1,
			method = "Runtime.evaluate",
			@params = new
			{
				expression = BuildImageInjectionScript(),
				awaitPromise = true,
				returnByValue = true
			}
		});
		await socket.SendAsync(array, WebSocketMessageType.Text, endOfMessage: true, cancellation.Token);
		byte[] buffer = new byte[8192];
		JsonObject response = null;
		for (int messageAttempt = 0; messageAttempt < 40; messageAttempt++)
		{
			using MemoryStream responseStream = new MemoryStream();
			WebSocketReceiveResult webSocketReceiveResult;
			do
			{
				webSocketReceiveResult = await socket.ReceiveAsync(buffer, cancellation.Token);
				if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
				{
					throw new InvalidOperationException("Codex CDP 连接被关闭。");
				}
				responseStream.Write(buffer, 0, webSocketReceiveResult.Count);
			}
			while (!webSocketReceiveResult.EndOfMessage);
			JsonObject jsonObject = JsonNode.Parse(responseStream.ToArray())?.AsObject();
			if (jsonObject != null && jsonObject["id"]?.GetValue<int>() == 1)
			{
				response = jsonObject;
				break;
			}
		}
		if (response == null)
		{
			throw new TimeoutException("等待 Codex CDP 注入响应超时。");
		}
		JsonNode? jsonNode = response?["result"]?["result"]?["value"];
		if (jsonNode != null && jsonNode["openExternal"]?.GetValue<bool>() == true)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "https://apinexus.top",
				UseShellExecute = true
			});
		}
		JsonNode jsonNode2 = response?["error"];
		if (jsonNode2 != null)
		{
			throw new InvalidOperationException("Codex 页面注入失败：" + jsonNode2.ToJsonString());
		}
		JsonNode jsonNode3 = response?["result"]?["exceptionDetails"];
		if (jsonNode3 != null)
		{
			throw new InvalidOperationException("Codex 页面脚本异常：" + jsonNode3.ToJsonString());
		}
	}

	[DllImport("ole32.dll")]
	private static extern int CoInitializeEx(nint reserved, uint coInit);

	[DllImport("ole32.dll")]
	private static extern void CoUninitialize();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryFullProcessImageName(nint processHandle, uint flags, StringBuilder executableName, ref int size);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint handle);

	private static async Task ValidateApiKeyAsync(string apiKey)
	{
		using HttpClient client = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(15.0)
		};
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://apinexus.dpdns.org/v1/models");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		using HttpResponseMessage response = await client.SendAsync(request);
		if (response.IsSuccessStatusCode)
		{
			return;
		}
		string text = await response.Content.ReadAsStringAsync();
		string value = ((text.Length > 500) ? text.Substring(0, 500) : text);
		throw new ApiKeyValidationException($"HTTP {(int)response.StatusCode}: {value}");
	}

	private static string BuildCodexConfig(string oldConfig, string model)
	{
		string text = RemoveLauncherManagedConfig(oldConfig);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("model_provider = \"custom\"");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("model = \"");
		handler.AppendFormatted(EscapeTomlString(model));
		handler.AppendLiteral("\"");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
		handler.AppendLiteral("review_model = \"");
		handler.AppendFormatted(EscapeTomlString(model));
		handler.AppendLiteral("\"");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("model_reasoning_effort = \"medium\"");
		stringBuilder.AppendLine("model_catalog_json = \"codex-launcher-model-catalog.json\"");
		stringBuilder.AppendLine();
		if (!string.IsNullOrWhiteSpace(text))
		{
			stringBuilder.AppendLine(text.Trim());
			stringBuilder.AppendLine();
		}
		stringBuilder.AppendLine("[model_providers.custom]");
		stringBuilder.AppendLine("name = \"API Nexus\"");
		stringBuilder.AppendLine("base_url = \"https://apinexus.dpdns.org/v1\"");
		stringBuilder.AppendLine("wire_api = \"responses\"");
		stringBuilder.AppendLine("requires_openai_auth = true");
		return stringBuilder.ToString();
	}

	private static string BuildAuthJson(string apiKey)
	{
		return "{\r\n  \"OPENAI_API_KEY\": \"" + EscapeJsonString(apiKey) + "\"\r\n}";
	}

	private static string RemoveLauncherManagedConfig(string config)
	{
		string[] array = config.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
		List<string> list = new List<string>();
		string text = string.Empty;
		bool flag = false;
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			string line = text2.Trim();
			if (TryReadTableHeader(line, out string tableName))
			{
				text = tableName;
				flag = string.Equals(tableName, "model_providers.custom", StringComparison.Ordinal);
				if (!flag)
				{
					list.Add(text2);
				}
			}
			else if (!flag && (text.Length != 0 || !IsManagedTopLevelAssignment(line)))
			{
				list.Add(text2);
			}
		}
		return string.Join(Environment.NewLine, list).Trim();
	}

	private static bool TryReadTableHeader(string line, out string tableName)
	{
		tableName = string.Empty;
		if (line.Length >= 3 && line[0] == '[')
		{
			if (line[line.Length - 1] == ']' && !line.StartsWith("[[", StringComparison.Ordinal))
			{
				tableName = line.Substring(1, line.Length - 1 - 1).Trim();
				return tableName.Length > 0;
			}
		}
		return false;
	}

	private static bool IsManagedTopLevelAssignment(string line)
	{
		if (line.Length == 0 || line.StartsWith('#'))
		{
			return false;
		}
		Match match = TopLevelAssignmentRegex.Match(line);
		if (match.Success)
		{
			return ManagedTopLevelKeys.Contains(match.Groups["key"].Value);
		}
		return false;
	}

	private static string BuildModelCatalogJson()
	{
		return JsonSerializer.Serialize(new
		{
			models = CatalogModels.Select((ModelCatalogEntry model) => new
			{
				slug = model.Slug,
				display_name = model.DisplayName,
				description = model.Description,
				base_instructions = "You are Codex, a coding agent. You and the user share the same workspace and collaborate to achieve the user's goals.",
				default_reasoning_level = model.DefaultReasoningLevel,
				supported_reasoning_levels = model.SupportedReasoningLevels.Select((string effort) => new
				{
					effort = effort,
					description = ReasoningDescription(effort)
				}).ToArray(),
				shell_type = "unified_exec",
				visibility = "list",
				supported_in_api = true,
				priority = model.Priority,
				supports_reasoning_summaries = true,
				default_reasoning_summary = "none",
				support_verbosity = true,
				default_verbosity = "low",
				apply_patch_tool_type = "freeform",
				web_search_tool_type = "text_and_image",
				truncation_policy = new
				{
					mode = "tokens",
					limit = 10000
				},
				supports_parallel_tool_calls = true,
				supports_image_detail_original = true,
				context_window = 1000000,
				max_context_window = 1000000,
				effective_context_window_percent = 95,
				experimental_supported_tools = Array.Empty<string>(),
				input_modalities = new string[2] { "text", "image" },
				supports_search_tool = true,
				additional_speed_tiers = Array.Empty<string>(),
				service_tiers = Array.Empty<string>(),
				availability_nux = (object)null,
				upgrade = (object)null
			}).ToArray()
		}, ModelCatalogJsonOptions);
	}

	private static string ReasoningDescription(string effort)
	{
		return effort switch
		{
			"low" => "Fast responses with lighter reasoning", 
			"medium" => "Balances speed and reasoning depth for everyday tasks", 
			"high" => "Greater reasoning depth for complex problems", 
			"xhigh" => "Extra high reasoning depth for complex problems", 
			"max" => "Maximum reasoning depth for the hardest problems", 
			"ultra" => "Maximum reasoning with automatic task delegation", 
			_ => effort, 
		};
	}

	private static string EscapeTomlString(string value)
	{
		return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\b", "\\b", StringComparison.Ordinal)
			.Replace("\t", "\\t", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\f", "\\f", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal);
	}

	private static string EscapeJsonString(string value)
	{
		return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\b", "\\b", StringComparison.Ordinal)
			.Replace("\t", "\\t", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\f", "\\f", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal);
	}

	private static void BackupIfExists(string path)
	{
		if (File.Exists(path))
		{
			string destFileName = $"{path}.bak-codex-launcher-{DateTime.Now:yyyyMMdd-HHmmss}";
			File.Copy(path, destFileName, overwrite: false);
		}
	}

	private static void WriteUtf8NoBom(string path, string content)
	{
		string text = $"{path}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}";
		File.WriteAllText(text, content, Utf8NoBom);
		File.Move(text, path, overwrite: true);
	}
}
