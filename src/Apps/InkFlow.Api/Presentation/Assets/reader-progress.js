(() => {
  const client = window.InkFlowReader;
  const bookId = "{{BOOK_ID}}";
  const chapterId = "{{CHAPTER_ID}}";
  const syncStatus = document.getElementById("reader-sync-status");
  const paragraphs = [...document.querySelectorAll(".reader-content__body p")];
  let saveTimer = null;

  const metrics = () => {
    const maximum = document.documentElement.scrollHeight - window.innerHeight;
    const progressPercent = maximum <= 0 ? 100 : Math.round((window.scrollY / maximum) * 100);
    const paragraphIndex = paragraphs.findIndex((paragraph) => paragraph.getBoundingClientRect().bottom >= window.innerHeight * 0.25);
    return {
      progressPercent: Math.min(100, Math.max(0, progressPercent)),
      paragraphIndex: Math.max(0, paragraphIndex)
    };
  };

  const save = async () => {
    if (!client?.isSignedIn()) return;
    const values = metrics();
    const response = await client.apiFetch(`/api/v1/me/reading/progress/${bookId}`, {
      method: "PUT",
      body: JSON.stringify({ chapterId, paragraphIndex: values.paragraphIndex, progressPercent: values.progressPercent })
    });
    if (response?.ok && syncStatus) syncStatus.textContent = "已同步";
  };

  const scheduleSave = () => {
    if (!client?.isSignedIn()) return;
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => void save(), 850);
  };

  const restore = async () => {
    if (!client?.isSignedIn()) return;
    const response = await client.apiFetch(`/api/v1/me/reading/progress/${bookId}`);
    if (!response?.ok) return;
    const saved = await response.json().catch(() => null);
    if (!saved || saved.chapterId !== chapterId || Number(saved.progressPercent) <= 0) return;
    await new Promise((resolve) => window.requestAnimationFrame(resolve));
    window.scrollTo({
      top: Math.max(0, document.documentElement.scrollHeight - window.innerHeight) * Math.min(100, Number(saved.progressPercent)) / 100,
      behavior: "auto"
    });
  };

  const initialise = async () => {
    if (!client?.isSignedIn()) return;
    await restore();
    await new Promise((resolve) => window.requestAnimationFrame(resolve));
    await save();
  };

  window.addEventListener("scroll", scheduleSave, { passive: true });
  window.addEventListener("pagehide", () => {
    if (saveTimer !== null) void save();
  });
  void initialise();
})();
