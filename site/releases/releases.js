/* releases.js - powers the /releases/ page */

(function () {
  const REPO = "piyushdoorwar/lumyn-media-player";
  const PER_PAGE = 10;

  // ── State ──────────────────────────────────────────────────────────────────
  let allReleases = [];   // all non-draft releases fetched so far
  let currentOS  = "all";
  let currentPage = 1;
  let stableOnly  = true;

  // ── DOM refs ───────────────────────────────────────────────────────────────
  const loadingEl = document.getElementById("releases-loading");
  const errorEl   = document.getElementById("releases-error");
  const emptyEl   = document.getElementById("releases-empty");
  const itemsEl   = document.getElementById("releases-items");
  const pagination = document.getElementById("pagination");
  const prevBtn   = document.getElementById("page-prev");
  const nextBtn   = document.getElementById("page-next");
  const pageLabel = document.getElementById("page-label");
  const osTabs          = document.querySelectorAll(".os-tab");
  const stableToggle    = document.getElementById("stableOnlyToggle");

  // ── Fetch all releases from GitHub (traverse pages) ───────────────────────
  async function fetchAllReleases() {
    const results = [];
    let page = 1;
    while (true) {
      const res = await fetch(
        `https://api.github.com/repos/${REPO}/releases?per_page=100&page=${page}`,
        { headers: { Accept: "application/vnd.github+json" } }
      );
      if (!res.ok) throw new Error(`GitHub API ${res.status}`);
      const batch = await res.json();
      if (!batch.length) break;
      results.push(...batch.filter(r => !r.draft));
      if (batch.length < 100) break;
      page++;
    }
    // Newest first (GitHub returns newest first, but be explicit)
    results.sort((a, b) => new Date(b.published_at) - new Date(a.published_at));
    return results;
  }

  // ── Asset helpers ──────────────────────────────────────────────────────────
  function linuxAsset(release) {
    return release.assets.find(a => /_amd64\.deb$/i.test(a.name));
  }
  function windowsAsset(release) {
    return (
      release.assets.find(a => /win-x64.*_setup\.exe$/i.test(a.name)) ??
      release.assets.find(a => /win-x64\.exe$/i.test(a.name)) ??
      release.assets.find(a => /win-x64\.zip$/i.test(a.name))
    );
  }
  function macosAsset(release) {
    return (
      release.assets.find(a => /macos-arm64\.dmg$/i.test(a.name)) ??
      release.assets.find(a => /macos-x64\.dmg$/i.test(a.name)) ??
      release.assets.find(a => /osx.*\.dmg$/i.test(a.name)) ??
      release.assets.find(a => /macos-arm64\.zip$/i.test(a.name)) ??
      release.assets.find(a => /macos-x64\.zip$/i.test(a.name)) ??
      release.assets.find(a => /osx.*\.zip$/i.test(a.name))
    );
  }

  function hasOsAsset(release, os) {
    if (os === "all") return true;
    if (os === "linux")   return !!linuxAsset(release);
    if (os === "windows") return !!windowsAsset(release);
    if (os === "macos")   return !!macosAsset(release);
    return true;
  }

  // ── Date formatting ────────────────────────────────────────────────────────
  function formatDate(iso) {
    const d = new Date(iso);
    return d.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
  }

  function timeAgo(iso) {
    const seconds = Math.floor((Date.now() - new Date(iso)) / 1000);
    if (seconds < 60)   return "just now";
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60)   return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24)     return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 30)      return `${days}d ago`;
    const months = Math.floor(days / 30);
    if (months < 12)    return `${months}mo ago`;
    return `${Math.floor(months / 12)}y ago`;
  }

  // ── Render ─────────────────────────────────────────────────────────────────
  function renderPage() {
    const filtered = allReleases.filter(r => hasOsAsset(r, currentOS) && (!stableOnly || !r.prerelease));
    const latestStable = filtered.find(r => !r.prerelease);

    if (filtered.length === 0) {
      itemsEl.innerHTML = "";
      emptyEl.classList.remove("hidden");
      pagination.hidden = true;
      return;
    }
    emptyEl.classList.add("hidden");

    const totalPages = Math.ceil(filtered.length / PER_PAGE);
    currentPage = Math.min(currentPage, totalPages);
    const start = (currentPage - 1) * PER_PAGE;
    const page  = filtered.slice(start, start + PER_PAGE);

    itemsEl.innerHTML = page.map((release, idx) => {
      const isLatest  = latestStable?.id === release.id;
      const linux   = linuxAsset(release);
      const windows = windowsAsset(release);
      const macos   = macosAsset(release);

      const tagName   = release.tag_name;
      const published = release.published_at;

      const showLinux   = currentOS === "all" || currentOS === "linux";
      const showWindows = currentOS === "all" || currentOS === "windows";
      const showMacos   = currentOS === "all" || currentOS === "macos";

      function dlBtn(asset, imgSrc) {
        if (!asset) return "";
        const ext = asset.name.split(".").pop().toLowerCase();
        const label = ext === "exe" ? ".exe" : ext === "dmg" ? ".dmg" : ext === "deb" ? ".deb" : "." + ext;
        return `<a class="button secondary release-dl-btn" href="${escHtml(asset.browser_download_url)}" download title="Download ${escHtml(asset.name)}">
          <img src="${imgSrc}" alt="" /><span>${label}</span>
        </a>`;
      }

      const downloads = [
        showLinux   ? dlBtn(linux,   "../assets/ubuntu.svg")  : "",
        showWindows ? dlBtn(windows, "../assets/windows.svg") : "",
        showMacos   ? dlBtn(macos,   "../assets/apple.svg")   : "",
      ].join("");

      return `<article class="release-item">
        <div class="release-meta">
          <div class="release-tag-row">
            <span class="release-version">${escHtml(tagName)}</span>
            ${isLatest ? '<span class="badge-latest">Latest</span>' : ""}
            ${release.prerelease ? '<span class="badge-pre">Pre-release</span>' : ""}
          </div>
          <time class="release-date" datetime="${escHtml(published)}" title="${formatDate(published)}">${timeAgo(published)} · ${formatDate(published)}</time>
        </div>
        <div class="release-downloads">
          ${downloads || `<a class="release-gh-link" href="${escHtml(release.html_url)}" rel="noreferrer">View on GitHub →</a>`}
        </div>
      </article>`;
    }).join("");

    // Pagination controls
    pagination.hidden = totalPages <= 1;
    pageLabel.textContent = `Page ${currentPage} of ${totalPages}`;
    prevBtn.disabled = currentPage <= 1;
    nextBtn.disabled = currentPage >= totalPages;
  }

  function escHtml(str) {
    return String(str)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  // ── Event listeners ────────────────────────────────────────────────────────
  osTabs.forEach(tab => {
    tab.addEventListener("click", () => {
      osTabs.forEach(t => { t.classList.remove("active"); t.setAttribute("aria-selected", "false"); });
      tab.classList.add("active");
      tab.setAttribute("aria-selected", "true");
      currentOS = tab.dataset.os;
      currentPage = 1;
      renderPage();
    });
  });

  prevBtn.addEventListener("click", () => { if (currentPage > 1) { currentPage--; renderPage(); window.scrollTo(0, 0); } });
  nextBtn.addEventListener("click", () => { currentPage++; renderPage(); window.scrollTo(0, 0); });

  stableToggle.addEventListener("change", () => {
    stableOnly = stableToggle.checked;
    currentPage = 1;
    renderPage();
  });

  // ── Init ───────────────────────────────────────────────────────────────────
  (async function init() {
    try {
      allReleases = await fetchAllReleases();
      loadingEl.classList.add("hidden");
      renderPage();
    } catch {
      loadingEl.classList.add("hidden");
      errorEl.classList.remove("hidden");
    }
  })();
})();
