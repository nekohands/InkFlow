(() => {
  const storageKey = "inkflow.reader.preferences.v1";
  const defaults = { theme: "system", fontSize: 100, lineHeight: 180 };
  const root = document.documentElement;
  const dialog = document.getElementById("reader-settings");
  const theme = document.getElementById("reader-theme");
  const fontSize = document.getElementById("reader-font-size");
  const lineHeight = document.getElementById("reader-line-height");
  const fontSizeOutput = document.getElementById("reader-font-size-output");
  const lineHeightOutput = document.getElementById("reader-line-height-output");
  const status = document.getElementById("reader-settings-status");
  let lastTrigger = null;
  let preferenceSyncTimer = null;

  const clamp = (value, min, max, fallback) => {
    const number = Number(value);
    return Number.isFinite(number) ? Math.min(max, Math.max(min, number)) : fallback;
  };

  const readPreferences = () => {
    try {
      const stored = JSON.parse(localStorage.getItem(storageKey) || "null");
      return {
        theme: ["system", "light", "sepia", "dark"].includes(stored?.theme) ? stored.theme : defaults.theme,
        fontSize: clamp(stored?.fontSize, 90, 140, defaults.fontSize),
        lineHeight: clamp(stored?.lineHeight, 150, 230, defaults.lineHeight)
      };
    } catch {
      return { ...defaults };
    }
  };

  let preferences = readPreferences();
  const normaliseTheme = (value) => {
    const candidate = String(value || defaults.theme).toLowerCase();
    return ["system", "light", "sepia", "dark"].includes(candidate) ? candidate : defaults.theme;
  };

  const applyPreferences = (announce) => {
    root.dataset.readerTheme = preferences.theme;
    root.style.setProperty("--reader-font-size", `${preferences.fontSize}%`);
    root.style.setProperty("--reader-line-height", `${preferences.lineHeight / 100}`);
    if (theme) theme.value = preferences.theme;
    if (fontSize) fontSize.value = preferences.fontSize;
    if (lineHeight) lineHeight.value = preferences.lineHeight;
    if (fontSizeOutput) fontSizeOutput.value = `${preferences.fontSize}%`;
    if (lineHeightOutput) lineHeightOutput.value = `${(preferences.lineHeight / 100).toFixed(2)}×`;
    if (announce && status) status.textContent = "阅读设置已保存到本设备";
  };

  const savePreferences = () => {
    try { localStorage.setItem(storageKey, JSON.stringify(preferences)); } catch { /* private mode may deny storage */ }
  };

  const syncPreferencesFromServer = async () => {
    const client = window.InkFlowReader;
    if (!client?.isSignedIn()) return;
    const response = await client.apiFetch("/api/v1/me/reading/preferences");
    if (!response?.ok) return;
    const remote = await response.json().catch(() => null);
    if (!remote) return;
    preferences = {
      theme: normaliseTheme(remote.theme),
      fontSize: clamp(remote.fontSizePercent, 90, 140, defaults.fontSize),
      lineHeight: clamp(remote.lineHeightPercent, 150, 230, defaults.lineHeight)
    };
    savePreferences();
    applyPreferences(false);
  };

  const syncPreferencesToServer = async () => {
    const client = window.InkFlowReader;
    if (!client?.isSignedIn()) return;
    const response = await client.apiFetch("/api/v1/me/reading/preferences", {
      method: "PUT",
      body: JSON.stringify({
        fontSizePercent: preferences.fontSize,
        lineHeightPercent: preferences.lineHeight,
        theme: preferences.theme
      })
    });
    if (response?.ok && status) status.textContent = "阅读设置已同步";
  };

  const schedulePreferencesSync = () => {
    window.clearTimeout(preferenceSyncTimer);
    preferenceSyncTimer = window.setTimeout(() => void syncPreferencesToServer(), 350);
  };

  const openDialog = (trigger) => {
    lastTrigger = trigger;
    applyPreferences(false);
    if (dialog && typeof dialog.showModal === "function") dialog.showModal();
    else if (dialog) dialog.setAttribute("open", "");
  };

  const closeDialog = () => {
    if (!dialog) return;
    if (typeof dialog.close === "function" && dialog.open) dialog.close();
    else dialog.removeAttribute("open");
    lastTrigger?.focus();
  };

  document.querySelectorAll("[data-open-reader-settings]").forEach((trigger) => {
    trigger.addEventListener("click", () => openDialog(trigger));
  });
  document.getElementById("reader-settings-close")?.addEventListener("click", closeDialog);
  dialog?.addEventListener("close", () => lastTrigger?.focus());

  const updateFromControls = () => {
    preferences = {
      theme: theme?.value || defaults.theme,
      fontSize: clamp(fontSize?.value, 90, 140, defaults.fontSize),
      lineHeight: clamp(lineHeight?.value, 150, 230, defaults.lineHeight)
    };
    savePreferences();
    applyPreferences(true);
    schedulePreferencesSync();
  };
  document.getElementById("reader-settings-form")?.addEventListener("input", updateFromControls);
  document.getElementById("reader-settings-form")?.addEventListener("change", updateFromControls);

  const progress = document.getElementById("reading-progress");
  const updateProgress = () => {
    if (!progress) return;
    const maximum = document.documentElement.scrollHeight - window.innerHeight;
    const percentage = maximum <= 0 ? 100 : Math.round((window.scrollY / maximum) * 100);
    progress.style.width = `${Math.min(100, Math.max(0, percentage))}%`;
    progress.setAttribute("aria-valuenow", percentage);
  };
  window.addEventListener("scroll", updateProgress, { passive: true });
  window.addEventListener("resize", updateProgress);

  applyPreferences(false);
  updateProgress();
  void syncPreferencesFromServer();
})();
