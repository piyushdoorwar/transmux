/*
 * Releases page: reads ../releases.json (written by CI from the GitHub
 * releases API) and renders each version with its platform downloads.
 */
(function () {
  "use strict";

  const ICONS = "../assets/icons.svg";
  const PER_PAGE = 10;

  let all = [];
  let page = 1;
  let os = "all";
  let stableOnly = true;

  const $ = (id) => document.getElementById(id);
  const loadingEl = $("rel-loading");
  const errorEl = $("rel-error");
  const emptyEl = $("rel-empty");
  const listEl = $("rel-list");
  const pager = $("pager");
  const prevBtn = $("page-prev");
  const nextBtn = $("page-next");
  const pageLabel = $("page-label");
  const stableToggle = $("stableOnly");
  const osButtons = document.querySelectorAll("[data-os]");

  const esc = (s) =>
    String(s ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");

  const icon = (name) => `<svg class="ic" aria-hidden="true"><use href="${ICONS}#i-${name}" /></svg>`;

  // ---- Asset matching -----------------------------------------------------
  const assets = (r) => (Array.isArray(r.assets) ? r.assets : []);
  const linuxAsset = (r) => assets(r).find((a) => /_amd64\.deb$/i.test(a.name));
  const windowsAsset = (r) =>
    assets(r).find((a) => /win-x64.*_setup\.exe$/i.test(a.name)) ??
    assets(r).find((a) => /win-x64\.exe$/i.test(a.name)) ??
    assets(r).find((a) => /win-x64\.zip$/i.test(a.name));
  const macosAsset = () => null;

  const PLATFORMS = [
    { os: "linux", label: "Linux", icon: "ubuntu", find: linuxAsset },
    { os: "windows", label: "Windows", icon: "windows", find: windowsAsset },
    { os: "macos", label: "macOS", icon: "apple", find: macosAsset },
  ];

  const hasOs = (r) => os === "all" || !!PLATFORMS.find((p) => p.os === os)?.find(r);

  function formatDate(iso) {
    const d = new Date(iso);
    return isNaN(d) ? "" : d.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
  }

  function timeAgo(iso) {
    const seconds = Math.floor((Date.now() - new Date(iso)) / 1000);
    if (!Number.isFinite(seconds)) return "";
    if (seconds < 60) return "just now";
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days}d ago`;
    const months = Math.floor(days / 30);
    if (months < 12) return `${months}mo ago`;
    return `${Math.floor(months / 12)}y ago`;
  }

  function downloadButton(platform, asset) {
    const ext = "." + asset.name.split(".").pop().toLowerCase();
    return `<a class="btn btn-secondary" href="${esc(asset.browser_download_url)}" download title="Download ${esc(asset.name)}">
      ${icon(platform.icon)}<span>${platform.label}</span><small>${esc(ext)}</small>
    </a>`;
  }

  function renderRelease(r, latestId) {
    const isLatest = r.id === latestId;
    const downloads = PLATFORMS.filter((p) => os === "all" || p.os === os)
      .map((p) => {
        const asset = p.find(r);
        return asset ? downloadButton(p, asset) : "";
      })
      .join("");

    return `<article class="release${isLatest ? " latest" : ""}">
      <div class="release-head">
        <h3>${esc(r.tag_name)}</h3>
        ${isLatest ? '<span class="badge badge-latest">Latest</span>' : ""}
        ${r.prerelease ? '<span class="badge badge-pre">Pre-release</span>' : ""}
        <time class="release-date" datetime="${esc(r.published_at)}">${esc(timeAgo(r.published_at))} · ${esc(formatDate(r.published_at))}</time>
      </div>
      <div class="release-body">
        ${downloads ? `<div class="release-downloads">${downloads}</div>` : ""}
        <div class="release-links">
          <a href="${esc(r.html_url)}" rel="noreferrer">${icon("github")}Release on GitHub</a>
        </div>
      </div>
    </article>`;
  }

  function render() {
    const filtered = all.filter((r) => hasOs(r) && (!stableOnly || !r.prerelease));
    const latestStable = filtered.find((r) => !r.prerelease);

    if (!filtered.length) {
      listEl.innerHTML = "";
      emptyEl.classList.remove("hidden");
      pager.hidden = true;
      return;
    }
    emptyEl.classList.add("hidden");

    const pages = Math.ceil(filtered.length / PER_PAGE);
    page = Math.min(Math.max(page, 1), pages);
    const slice = filtered.slice((page - 1) * PER_PAGE, page * PER_PAGE);
    listEl.innerHTML = slice.map((r) => renderRelease(r, latestStable?.id)).join("");

    pager.hidden = pages <= 1;
    pageLabel.textContent = `Page ${page} of ${pages}`;
    prevBtn.disabled = page <= 1;
    nextBtn.disabled = page >= pages;
  }

  const jumpToList = () => window.scrollTo(0, listEl.offsetTop - 120);

  osButtons.forEach((btn) => {
    btn.addEventListener("click", () => {
      os = btn.dataset.os;
      osButtons.forEach((b) => b.setAttribute("aria-pressed", String(b === btn)));
      page = 1;
      render();
    });
  });
  stableToggle.addEventListener("change", () => {
    stableOnly = stableToggle.checked;
    page = 1;
    render();
  });
  prevBtn.addEventListener("click", () => {
    page -= 1;
    render();
    jumpToList();
  });
  nextBtn.addEventListener("click", () => {
    page += 1;
    render();
    jumpToList();
  });

  (async function init() {
    try {
      const res = await fetch("../releases.json", { cache: "no-cache" });
      if (!res.ok) throw new Error(`Release manifest returned ${res.status}`);
      const data = await res.json();
      all = (Array.isArray(data) ? data : [])
        .filter((r) => !r.draft)
        .sort((a, b) => new Date(b.published_at) - new Date(a.published_at));
      loadingEl.classList.add("hidden");
      render();
    } catch {
      loadingEl.classList.add("hidden");
      errorEl.classList.remove("hidden");
    }
  })();
})();
