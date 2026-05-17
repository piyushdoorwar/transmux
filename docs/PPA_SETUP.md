# Ubuntu PPA Setup Guide

This guide explains how to publish Transmux to Ubuntu Personal Package Archive (PPA).

## Prerequisites

- ✅ Ubuntu Launchpad account
- ✅ GPG key registered on Launchpad
- ✅ PPA created at `ppa:piyushdoorwar/transmux`
- ✅ GPG key passphrase

## GitHub Secrets Required

Set these secrets in your GitHub repository settings:

### 1. **GPG_PRIVATE_KEY**
Your GPG private key in ASCII-armored format.

Export it:
```bash
gpg --armor --export-secret-keys BD927A711D65E714F7AC835B74934BB1EE4B3D81 > private_key.asc
```

Then:
- Go to **GitHub Repo** → **Settings** → **Secrets and variables** → **Actions**
- Click **New repository secret**
- Name: `GPG_PRIVATE_KEY`
- Value: Paste the contents of `private_key.asc`

⚠️ **Keep this secret safe!** Never commit it to git.

### 2. **GPG_PASSPHRASE**
Your GPG key's passphrase.

- Go to **GitHub Repo** → **Settings** → **Secrets and variables** → **Actions**
- Click **New repository secret**
- Name: `GPG_PASSPHRASE`
- Value: Your GPG passphrase

⚠️ This allows GitHub Actions to sign packages. Use a strong passphrase.

## How It Works

When you push a tag:
```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow:
1. **Extracts version** from tag
2. **Updates `debian/changelog`** with new version
3. **Builds source package** (`.dsc`, `.tar.gz`, `.orig.tar.gz`)
4. **Signs** with your GPG key
5. **Uploads to PPA** using `dput`
6. **Launchpad builds** for multiple Ubuntu versions

## Installation for Users

Once published, users can install with:
```bash
sudo add-apt-repository ppa:piyushdoorwar/transmux
sudo apt update
sudo apt install transmux
```

## Troubleshooting

### "Failed to authenticate to PPA"
- Ensure your Launchpad SSH key is set up
- Check that your GPG key is registered on Launchpad

### "debsign: gpg failed to sign"
- Verify `GPG_PASSPHRASE` is correct
- Test locally: `gpg --sign --armor test.txt`

### "Package already exists"
- PPAs don't allow re-uploading the same version
- Increment the version in `debian/changelog` and push again

## Manual Testing Locally

Test building the source package:
```bash
dpkg-buildpackage -S -d -us -uc
```

Sign it:
```bash
debsign -k BD927A711D65E714F7AC835B74934BB1EE4B3D81 transmux_*.changes
```

Upload manually:
```bash
dput ppa:piyushdoorwar/transmux transmux_*.changes
```

**Note:** Your PPA is at: https://launchpad.net/~piyushdoorwar/+archive/ubuntu/transmux

## Ubuntu Versions Supported

By default, the workflow targets **jammy** (Ubuntu 22.04). To support more versions:
1. Edit `.github/workflows/release.yml`
2. Modify the `dch` command to include additional distributions
3. Or set up separate builds for each version

Example for multiple versions:
```bash
dch -v "${VERSION}-0piyushdoorwar1" --distribution "focal jammy noble" "Release version ${VERSION}"
```

## Your PPA

- **PPA Page:** https://launchpad.net/~piyushdoorwar/+archive/ubuntu/transmux
- **GitHub Repo:** https://github.com/piyushdoorwar/transmux
- **Launchpad Project:** https://launchpad.net/transmux

## References

- [Launchpad PPA Help](https://help.launchpad.net/Packaging/PPA)
- [Creating Debian Packages](https://wiki.debian.org/Packaging)
- [dput Documentation](https://manpages.ubuntu.com/manpages/jammy/man1/dput.1.html)
