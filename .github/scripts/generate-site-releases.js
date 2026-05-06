const fs = require("node:fs/promises");
const path = require("node:path");

const repo = process.env.GITHUB_REPOSITORY || "piyushdoorwar/transmux";
const token = process.env.GITHUB_TOKEN;
const outputPath = path.resolve(process.cwd(), "site/releases.json");

async function githubFetch(url) {
  const headers = {
    Accept: "application/vnd.github+json",
    "User-Agent": "Transmux-Site-Release-Manifest",
    "X-GitHub-Api-Version": "2022-11-28",
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(url, { headers });
  if (!response.ok) {
    throw new Error(`GitHub API ${response.status}: ${await response.text()}`);
  }

  return response.json();
}

async function fetchReleases() {
  const releases = [];
  let page = 1;

  while (true) {
    const batch = await githubFetch(
      `https://api.github.com/repos/${repo}/releases?per_page=100&page=${page}`
    );

    if (!batch.length) break;
    releases.push(...batch);
    if (batch.length < 100) break;
    page += 1;
  }

  return releases
    .filter((release) => !release.draft)
    .sort((a, b) => new Date(b.published_at) - new Date(a.published_at))
    .map((release) => ({
      id: release.id,
      tag_name: release.tag_name,
      prerelease: release.prerelease,
      published_at: release.published_at,
      html_url: release.html_url,
      assets: (release.assets || []).map((asset) => ({
        name: asset.name,
        browser_download_url: asset.browser_download_url,
      })),
    }));
}

async function main() {
  const releases = await fetchReleases();
  await fs.writeFile(outputPath, `${JSON.stringify(releases, null, 2)}\n`);
  console.log(`Wrote ${releases.length} releases to ${outputPath}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
