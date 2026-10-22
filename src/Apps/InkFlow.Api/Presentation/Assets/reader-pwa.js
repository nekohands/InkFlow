(() => {
  const installButton = document.getElementById("reader-install");
  let deferredPrompt = null;
  const standalone = window.matchMedia?.("(display-mode: standalone)")?.matches || window.navigator.standalone === true;

  if (installButton && !standalone) {
    window.addEventListener("beforeinstallprompt", (event) => {
      event.preventDefault();
      deferredPrompt = event;
      installButton.hidden = false;
    });
    installButton.addEventListener("click", async () => {
      if (!deferredPrompt) return;
      await deferredPrompt.prompt();
      deferredPrompt = null;
      installButton.hidden = true;
    });
    window.addEventListener("appinstalled", () => {
      deferredPrompt = null;
      installButton.hidden = true;
    });
  }

  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("/reader/sw.js", { scope: "/reader/" }).catch(() => { /* offline enhancement is optional */ });
  }
})();
