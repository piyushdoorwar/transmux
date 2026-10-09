/*
 * Transmux site behaviour: mobile menu, copy buttons, scroll reveal,
 * download links from releases.json with OS detection, and the converter mock.
 */
(function () {
  "use strict";

  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ---- Mobile navigation ------------------------------------------------
  const topbar = document.querySelector(".topbar");
  const toggle = document.querySelector(".nav-toggle");
  if (topbar && toggle) {
    const setOpen = (open) => {
      topbar.classList.toggle("open", open);
      toggle.setAttribute("aria-expanded", String(open));
      toggle.setAttribute("aria-label", open ? "Close menu" : "Open menu");
    };
    toggle.addEventListener("click", () => setOpen(!topbar.classList.contains("open")));
    topbar.querySelectorAll(".nav a").forEach((a) => a.addEventListener("click", () => setOpen(false)));
    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape") setOpen(false);
    });
  }

  // ---- Copy buttons -----------------------------------------------------
  async function copyText(text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      const ta = document.createElement("textarea");
      ta.value = text;
      ta.setAttribute("readonly", "");
      ta.style.position = "fixed";
      ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      let ok = false;
      try {
        ok = document.execCommand("copy");
      } catch {
        ok = false;
      }
      ta.remove();
      return ok;
    }
  }

  document.addEventListener("click", async (event) => {
    const btn = event.target.closest(".copy-btn");
    if (!btn) return;
    const text = btn.dataset.copy ?? btn.closest(".cmd")?.querySelector("code")?.innerText ?? "";
    const ok = await copyText(text);
    const label = btn.querySelector("span");
    btn.classList.toggle("done", ok);
    if (label) label.textContent = ok ? "Copied" : "Press Ctrl+C";
    clearTimeout(btn._timer);
    btn._timer = setTimeout(() => {
      btn.classList.remove("done");
      if (label) label.textContent = "Copy";
    }, 1800);
  });

  // ---- Scroll reveal ----------------------------------------------------
  const revealEls = document.querySelectorAll("[data-reveal]");
  if ("IntersectionObserver" in window) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          // Elements above the viewport (after an anchor jump or reload) are shown as well.
          if (entry.isIntersecting || entry.boundingClientRect.top < 0) {
            entry.target.classList.add("in");
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.08, rootMargin: "0px 0px -40px 0px" }
    );
    revealEls.forEach((el) => observer.observe(el));
  } else {
    revealEls.forEach((el) => el.classList.add("in"));
  }

  // ---- Downloads --------------------------------------------------------
  // CI writes releases.json from the GitHub releases API during the Pages deploy.
  const linuxLink = document.getElementById("linuxDownloadLink");
  const windowsLink = document.getElementById("windowsDownloadLink");
  const heroLink = document.getElementById("downloadLink");
  const heroLabel = document.getElementById("downloadLabel");

  function detectOs() {
    const ua = `${navigator.userAgent || ""} ${navigator.userAgentData?.platform || navigator.platform || ""}`;
    if (/android|cros/i.test(ua)) return null;
    if (/windows|win32|win64/i.test(ua)) return "windows";
    if (/linux|x11|ubuntu|debian/i.test(ua)) return "linux";
    return null;
  }

  const linuxAsset = (release) => release.assets.find((a) => /_amd64\.deb$/i.test(a.name));
  const windowsAsset = (release) => release.assets.find((a) => /win-x64.*_setup\.exe$/i.test(a.name));

  function latestAssetWithInstaller(releases, findAsset) {
    for (const release of releases) {
      const asset = findAsset(release);
      if (asset?.browser_download_url) return asset;
    }
    return null;
  }

  function enableDownload(link, url) {
    link.href = url;
    link.removeAttribute("aria-disabled");
  }

  async function hydrateDownloads() {
    if (!linuxLink || !windowsLink) return;
    const os = detectOs();

    if (os) {
      const card = document.getElementById(`platform-${os}`);
      card?.classList.add("detected");
      card?.querySelector("[data-detected]")?.classList.remove("hidden");
    }

    try {
      const response = await fetch("releases.json", { cache: "no-cache" });
      if (!response.ok) return;
      const releases = await response.json();
      const stable = (Array.isArray(releases) ? releases : [])
        .filter((r) => !r.draft && !r.prerelease && r.assets?.length)
        .sort((a, b) => new Date(b.published_at) - new Date(a.published_at));

      const linux = latestAssetWithInstaller(stable, linuxAsset);
      const windows = latestAssetWithInstaller(stable, windowsAsset);
      if (linux) enableDownload(linuxLink, linux.browser_download_url);
      if (windows) enableDownload(windowsLink, windows.browser_download_url);

      // The hero button downloads straight away when the visitor's OS has a build.
      const mine = os === "linux" ? linux : os === "windows" ? windows : null;
      if (heroLink && heroLabel && mine) {
        heroLink.href = mine.browser_download_url;
        heroLabel.textContent = os === "linux" ? "Download for Linux" : "Download for Windows";
      }
    } catch {
      // Keep the buttons disabled if the manifest is unreachable or has no matching assets.
    }
  }
  hydrateDownloads();

  // ---- Converter mock: looping progress and the mode switch -------------
  const mock = document.getElementById("mock");
  if (mock) {
    const bar = document.getElementById("progBar");
    const pct = document.getElementById("progPercent");
    const label = document.getElementById("progLabel");
    const speed = document.getElementById("progSpeed");
    const eta = document.getElementById("progEta");
    const box = document.getElementById("mockProgress");
    const mode = document.getElementById("mockMode");
    let fast = true;

    const fmt = (s) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;

    const paint = (p) => {
      const percent = Math.max(1, Math.round(p * 100));
      const total = fast ? 180 : 420;
      bar.style.width = percent + "%";
      pct.textContent = percent + "%";
      eta.textContent = "ETA " + fmt(Math.max(0, Math.round(total * (1 - p))));
      speed.textContent = fast ? "3.4x" : "1.6x";
    };

    const finish = () => {
      box.classList.add("done");
      bar.style.width = "100%";
      pct.textContent = "100%";
      label.textContent = "Conversion complete";
      eta.textContent = "Saved to ~/Videos";
    };

    mode?.querySelectorAll("button").forEach((btn, i) => {
      btn.addEventListener("click", () => {
        fast = i === 0;
        mode.querySelectorAll("button").forEach((b, j) => b.setAttribute("aria-pressed", String(j === i)));
      });
    });

    if (reduceMotion) {
      paint(0.38);
    } else {
      const duration = 14000;
      let start = performance.now();
      let holdUntil = 0;
      const tick = (now) => {
        if (holdUntil) {
          if (now >= holdUntil) {
            holdUntil = 0;
            start = now;
            box.classList.remove("done");
            label.textContent = "Converting";
          }
        } else {
          const t = Math.min((now - start) / duration, 1);
          const eased = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
          if (t >= 1) {
            finish();
            holdUntil = now + 2600;
          } else {
            paint(eased);
          }
        }
        requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
    }
  }
})();
