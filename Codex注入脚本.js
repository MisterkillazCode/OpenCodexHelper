(() => {
  const navId = "codex-assistant-20-nav";
  const pageId = "codex-assistant-20-page";
  const pageClass = "codex-assistant-20-page";
  const styleId = "codex-assistant-20-style";
  const imageNavId = "codex-imagegen-test-nav";
  const imagePageId = "codex-imagegen-test-page";
  const buttonText = "充值";
  const remoteServiceUrl = "https://wzyp.cn/item/bjbyqd";
  const localizationUrl = "https://wzyp.cn/item/zm1ofy";
  const videoUrl = "https://www.bilibili.com/video/BV1BHN26GETP";
  const topupUrl = "https://apinexus.dpdns.org/console/topup";

  function closePage() {
    document.getElementById(pageId)?.remove();
    document.getElementById(navId)?.querySelector("button")?.setAttribute("aria-current", "false");
  }

  function closeImagePage() {
    document.getElementById(imagePageId)?.remove();
    document.getElementById(imageNavId)?.querySelector("button")?.setAttribute("aria-current", "false");
  }

  function positionPage(page) {
    const sidebar = document.querySelector("aside.app-shell-left-panel");
    const rect = sidebar?.getBoundingClientRect?.();
    const left = rect && rect.width > 0 ? Math.max(0, rect.right) : 0;
    page.style.left = `${left}px`;
  }

  function openPage() {
    closeImagePage();
    closePage();
    const page = document.createElement("div");
    page.id = pageId;
    page.className = pageClass;
    page.setAttribute("role", "dialog");
    page.setAttribute("aria-modal", "true");
    page.setAttribute("aria-label", "充值");
    Object.assign(page.style, {
      position: "fixed", left: "0", top: "0", bottom: "0", width: "min(420px, 100vw)",
      zIndex: "2147483000", background: "var(--bg-primary, #fff)",
      color: "var(--text-primary, #171717)", borderRight: "1px solid rgba(0,0,0,.12)",
      boxShadow: "8px 0 24px rgba(0,0,0,.14)", fontFamily: "inherit", overflow: "auto"
    });
    page.innerHTML = `
      <div style="height:100%;display:flex;flex-direction:column">
        <header style="display:flex;align-items:center;padding:18px 20px;border-bottom:1px solid rgba(0,0,0,.10);font-weight:700;font-size:18px">
          <span>充值</span>
        </header>
        <div class="codex-assistant-20-actions">
          <button type="button" class="codex-assistant-20-topup" data-action="topup">充值</button>
          <button type="button" class="codex-assistant-20-secondary" data-action="remote">远程服务</button>
          <button type="button" class="codex-assistant-20-secondary" data-action="localization">中文汉化</button>
          <button type="button" class="codex-assistant-20-secondary" data-action="video">视频教程</button>
          <button type="button" class="codex-assistant-20-close" data-action="close">关闭</button>
        </div>
      </div>`;
    let style = document.getElementById(styleId);
    if (!style) {
      style = document.createElement("style");
      style.id = styleId;
      style.textContent = `
        #${pageId} .codex-assistant-20-actions { padding: 22px 20px; display: grid; gap: 12px; }
        #${pageId} button { width: 100%; box-sizing: border-box; border-radius: 12px; font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; transition: transform .15s ease, box-shadow .15s ease, background .15s ease; }
        #${pageId} button:hover { transform: translateY(-1px); }
        #${pageId} .codex-assistant-20-topup { padding: 15px 16px; border: 0; color: #fff; font-size: 17px; font-weight: 700; letter-spacing: .08em; background: linear-gradient(135deg, #ff8a1f 0%, #ff3d68 100%); box-shadow: 0 8px 18px rgba(255, 89, 72, .28); }
        #${pageId} .codex-assistant-20-topup:hover { box-shadow: 0 10px 22px rgba(255, 89, 72, .38); }
        #${pageId} .codex-assistant-20-secondary { padding: 12px 14px; border: 1px solid rgba(99, 115, 145, .24); color: var(--text-primary, #273142); background: var(--bg-secondary, #f6f8fc); }
        #${pageId} .codex-assistant-20-secondary:hover { background: var(--bg-hover, #edf2fb); }
        #${pageId} .codex-assistant-20-close { margin-top: 4px; padding: 10px 14px; border: 1px solid rgba(99, 115, 145, .32); color: var(--text-secondary, #64748b); background: transparent; }
      `;
      document.head?.appendChild(style);
    }
    positionPage(page);
    page.addEventListener("click", (event) => {
      const target = event.target instanceof Element ? event.target : event.target?.parentElement;
      const action = target?.closest("[data-action]")?.getAttribute("data-action");
      if (action === "close") {
        event.preventDefault();
        event.stopPropagation();
        closePage();
        return;
      }
      if (action === "remote") { window.open(remoteServiceUrl, "_blank", "noopener,noreferrer"); closePage(); return; }
      if (action === "localization") { window.open(localizationUrl, "_blank", "noopener,noreferrer"); closePage(); return; }
      if (action === "video") { window.open(videoUrl, "_blank", "noopener,noreferrer"); closePage(); return; }
      if (action === "topup") { window.open(topupUrl, "_blank", "noopener,noreferrer"); closePage(); return; }
    }, true);
    document.body.appendChild(page);
    document.getElementById(navId)?.querySelector("button")?.setAttribute("aria-current", "page");
  }

  function installSidebarNavigation() {
    const navigation = document.querySelector("aside.app-shell-left-panel nav[role=navigation], nav[role=navigation]");
    if (!navigation) return false;
    const navButtons = Array.from(navigation.querySelectorAll("button"));
    const pluginButton = navButtons.find((button) => /^(插件|Plugins)$/i.test((button.getAttribute("aria-label") || button.textContent || "").replace(/\\s+/g, " ").trim()));
    const insertionButton = pluginButton || navButtons.find((button) => /^(新对话|New chat|已安排|Scheduled|拉取请求|Pull requests)$/i.test((button.getAttribute("aria-label") || button.textContent || "").replace(/\\s+/g, " ").trim())) || navButtons[0];
    const parent = insertionButton?.parentElement || navigation;
    let wrapper = document.getElementById(navId);
    if (!wrapper || wrapper.parentElement !== parent) {
      wrapper?.remove();
      wrapper = document.createElement("div");
      wrapper.id = navId;
      wrapper.dataset.codexAssistant20 = "true";
      const button = (insertionButton || document.createElement("button")).cloneNode(true);
      button.type = "button";
      button.removeAttribute("disabled");
      button.removeAttribute("aria-disabled");
      button.removeAttribute("data-state");
      button.setAttribute("aria-label", buttonText);
      button.textContent = "";
      button.innerHTML = `<span aria-hidden="true" style="font-size:16px;line-height:1">✦</span><span class="truncate">${buttonText}</span>`;
      button.addEventListener("click", (event) => { event.preventDefault(); event.stopPropagation(); openPage(); }, true);
      wrapper.appendChild(button);
      if (insertionButton?.nextSibling) parent.insertBefore(wrapper, insertionButton.nextSibling); else parent.appendChild(wrapper);
    }

    let imageWrapper = document.getElementById(imageNavId);
    if (!imageWrapper || imageWrapper.parentElement !== parent) {
      imageWrapper?.remove();
      imageWrapper = document.createElement("div");
      imageWrapper.id = imageNavId;
      imageWrapper.dataset.codexImagegenTest = "true";
      const imageButton = (insertionButton || document.createElement("button")).cloneNode(true);
      imageButton.type = "button";
      imageButton.removeAttribute("disabled");
      imageButton.removeAttribute("aria-disabled");
      imageButton.removeAttribute("data-state");
      imageButton.setAttribute("aria-label", "生图");
      imageButton.textContent = "";
      imageButton.innerHTML = `<span aria-hidden="true" style="font-size:16px;line-height:1">✦</span><span class="truncate">生图</span>`;
      imageButton.addEventListener("click", async (event) => {
        event.preventDefault(); event.stopPropagation();
        window.__assistantOpenImageSite = true;
      }, true);
      imageWrapper.appendChild(imageButton);
    }
    if (imageWrapper.parentElement !== parent) parent.appendChild(imageWrapper);
    if (wrapper.nextSibling !== imageWrapper) parent.insertBefore(imageWrapper, wrapper.nextSibling);
    return true;
  }

  const wasInstalled = window.__codexAssistant20InstalledVersion === "204-close-actions";
  if (!wasInstalled) {
    window.__codexAssistant20Observer?.disconnect();
    window.__codexAssistant20Observer = null;
    document.getElementById(imageNavId)?.remove();
    window.__codexAssistant20InstalledVersion = "204-close-actions";
    document.getElementById(pageId)?.remove();
    document.querySelectorAll(`.${pageClass}`).forEach((node) => node.remove());
    document.getElementById(navId)?.remove();
  }

  if (!window.__codexAssistant20Observer && document.documentElement) {
    const observer = new MutationObserver(() => {
      try { installSidebarNavigation(); } catch {}
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
    window.__codexAssistant20Observer = observer;
  }

  if (!window.__codexAssistant20RetryTimer) {
    const retryTimer = setInterval(() => {
      try {
        if (installSidebarNavigation() || !document.documentElement) {
          clearInterval(retryTimer);
          window.__codexAssistant20RetryTimer = null;
        }
      } catch {}
    }, 300);
    window.__codexAssistant20RetryTimer = retryTimer;
    setTimeout(() => {
      clearInterval(retryTimer);
      if (window.__codexAssistant20RetryTimer === retryTimer) window.__codexAssistant20RetryTimer = null;
    }, 15000);
  }

  try { installSidebarNavigation(); } catch {}
  if (!window.__codexAssistant20ResizeBound) {
    window.__codexAssistant20ResizeBound = true;
    window.addEventListener("resize", () => {
      const page = document.getElementById(pageId);
      if (page) positionPage(page);
    });
  }
  return true;
})();
(() => {
const openExternal = window.__assistantOpenImageSite === true;
window.__assistantOpenImageSite = false;
return { openExternal };
})()