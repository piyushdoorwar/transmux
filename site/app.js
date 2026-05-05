const repo = "piyushdoorwar/transmux";

const linuxLink = document.querySelector("#linuxDownloadLink");
const windowsLink = document.querySelector("#windowsDownloadLink");
const heroDownloadLink = document.querySelector("#downloadLink");

heroDownloadLink.href = "#download";

function enableDownload(link, url) {
  link.href = url;
  link.classList.remove("disabled");
  link.removeAttribute("aria-disabled");
}

function linuxAsset(release) {
  return release.assets.find((asset) => /_amd64\.deb$/i.test(asset.name));
}

function windowsAsset(release) {
  return release.assets.find((asset) => /win-x64.*_setup\.exe$/i.test(asset.name));
}

function latestAssetWithInstaller(releases, findAsset) {
  for (const release of releases) {
    const asset = findAsset(release);
    if (asset?.browser_download_url) return asset;
  }
  return null;
}

async function hydrateDownloadLinks() {
  try {
    const response = await fetch(`https://api.github.com/repos/${repo}/releases?per_page=100`, {
      headers: { Accept: "application/vnd.github+json" },
    });
    if (!response.ok) return;

    const releases = await response.json();
    const stableReleases = releases
      .filter((item) => !item.draft && !item.prerelease && item.assets?.length)
      .sort((a, b) => new Date(b.published_at) - new Date(a.published_at));

    const linux = latestAssetWithInstaller(stableReleases, linuxAsset);
    const windows = latestAssetWithInstaller(stableReleases, windowsAsset);

    if (linux?.browser_download_url) {
      enableDownload(linuxLink, linux.browser_download_url);
    }

    if (windows?.browser_download_url) {
      enableDownload(windowsLink, windows.browser_download_url);
    }
  } catch {
    // Keep the buttons disabled if GitHub is unreachable or matching assets are absent.
  }
}

hydrateDownloadLinks();

// ── Scroll reveal ─────────────────────────────────────────────────────────
(function () {
  const obs = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("revealed");
          obs.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.1 }
  );
  document.querySelectorAll("[data-reveal]").forEach((el) => obs.observe(el));
})();
