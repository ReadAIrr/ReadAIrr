# Announcement: Retirement of ReadAIrr

We would like to announce that the [Readarr project](<https://github.com/Readarr/Readarr>) has been retired. This difficult decision was made due to a combination of factors: the project's metadata has become unusable, we no longer have the time to remake or repair it, and the community effort to transition to using Open Library as the source has stalled without much progress.

Third-party metadata mirrors exist, but as we're not involved with them at all, we cannot provide support for them. Use of them is entirely at your own risk. The most popular mirror appears to be [rreading-glasses](<https://github.com/blampe/rreading-glasses>).

Without anyone to take over ReadAIrr development, we expect it to wither away, so we still encourage you to seek alternatives to ReadAIrr.

## Key Points:
- **Effective Immediately**: The retirement takes effect immediately. Please stay tuned for any possible further communications.
- **Support Window**: We will provide support during a brief transition period to help with troubleshooting non metadata related issues.
- **Alternative Solutions**: Users are encouraged to explore and adopt any other possible solutions as alternatives to ReadAIrr.
- **Opportunities for Revival**: We are open to someone taking over and revitalizing the project. If you are interested, please get in touch.
- **Gratitude**: We extend our deepest gratitude to all the contributors and community members who supported ReadAIrr over the years.

Thank you for being part of the ReadAIrr journey. For any inquiries or assistance during this transition, please contact our team.

Sincerely,  
The Servarr Team

# ReadAIrr

[![Build Status](https://dev.azure.com/Readarr/Readarr/_apis/build/status/Readarr.Readarr?branchName=develop)](https://dev.azure.com/Readarr/Readarr/_build/latest?definitionId=1&branchName=develop)
[![Translated](https://translate.servarr.com/widgets/servarr/-/readarr/svg-badge.svg)](https://translate.servarr.com/engage/readarr/?utm_source=widget)
[![Docker Pulls](https://img.shields.io/docker/pulls/hotio/readarr)](https://wiki.servarr.com/readarr/installation#docker)
[![Donors on Open Collective](https://opencollective.com/Readarr/backers/badge.svg)](#backers)
[![Sponsors on Open Collective](https://opencollective.com/Readarr/sponsors/badge.svg)](#sponsors)
[![Mega Sponsors on Open Collective](https://opencollective.com/Readarr/megasponsors/badge.svg)](#mega-sponsors)

### ReadAIrr is currently in beta testing and is generally still in a work in progress. Features may be broken, incomplete, or cause spontaneous combustion

ReadAIrr is an ebook and audiobook collection manager for Usenet and BitTorrent users. It can monitor multiple RSS feeds for new books from your favorite authors and will grab, sort, and rename them.
Note that only one type of a given book is supported. If you want both an audiobook and ebook of a given book you will need multiple instances.

## Major Features Include

* Can watch for better quality of the ebooks and audiobooks you have and do an automatic upgrade. *e.g. from PDF to AZW3*
* Support for major platforms: Windows, Linux, macOS, Raspberry Pi, etc.
* Automatically detects new books
* Can scan your existing library and download any missing books
* Automatic failed download handling will try another release if one fails
* Manual search so you can pick any release or to see why a release was not downloaded automatically
* Advanced customization for profiles, such that ReadAIrr will always download the copy you want
* Fully configurable book renaming
* SABnzbd, NZBGet, QBittorrent, Deluge, rTorrent, Transmission, uTorrent, and other download clients are supported and integrated
* Full integration with Calibre (add to library, conversion) (Requires Calibre Content Server)
* And a beautiful UI

## Support

[![Wiki](https://img.shields.io/badge/servarr-wiki-181717.svg?maxAge=60)](https://wiki.servarr.com/readarr)
[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://readarr.com/discord)

Note: GitHub Issues are for Bugs and Feature Requests Only

[![GitHub - Bugs and Feature Requests Only](https://img.shields.io/badge/github-issues-red.svg?maxAge=60)](https://github.com/Readarr/Readarr/issues)

## Contributors & Developers

[API Documentation](https://readarr.com/docs/api/)

This project exists thanks to all the people who contribute.
- [Contribute (GitHub)](CONTRIBUTING.md)
- [Contribution (Wiki Article)](https://wiki.servarr.com/readarr/contributing)

[![Contributors List](https://opencollective.com/Readarr/contributors.svg?width=890&button=false)](https://github.com/Readarr/Readarr/graphs/contributors)

## Backers

Thank you to all our backers! 🙏 [Become a backer](https://opencollective.com/Readarr#backer)

[![Backers List](https://opencollective.com/Readarr/backers.svg?width=890)](https://opencollective.com/Readarr#backer)

## Sponsors

Support this project by becoming a sponsor. Your logo will show up here with a link to your website. [Become a sponsor](https://opencollective.com/readarr#sponsor)

[![Sponsors List](https://opencollective.com/Readarr/sponsors.svg?width=890)](https://opencollective.com/readarr#sponsor)

## Mega Sponsors

[![Mega Sponsors List](https://opencollective.com/Readarr/tiers/mega-sponsor.svg?width=890)](https://opencollective.com/readarr#mega-sponsor)

## DigitalOcean

This project is also supported by DigitalOcean
<p>
  <a href="https://www.digitalocean.com/">
    <img src="https://opensource.nyc3.cdn.digitaloceanspaces.com/attribution/assets/SVG/DO_Logo_horizontal_blue.svg" width="201px">
  </a>
</p>

## Docker Development Environment

ReadAIrr includes a comprehensive Docker development environment with support for network storage (SMB/NFS) integration.

### Quick Start with Docker

```bash
# Copy and configure environment
cp .env.local.example .env.local
# Edit .env.local with your settings

# Start development environment
docker-compose -f docker-compose.dev.yml up -d

# Access ReadAIrr
open http://localhost:8246
```

### Network Storage Support

The Docker environment supports mounting remote network shares directly:

- **SMB/CIFS**: Windows shares, Samba, NAS devices
- **NFS**: Linux/Unix network file systems
- **Multiple Shares**: Configure primary and secondary mount points
- **Persistent Storage**: All configuration and data persists across restarts

Example configuration in `.env.local`:
```bash
# Enable network storage
NETWORK_STORAGE_ENABLED=true

# SMB share configuration
SMB_SHARE_PATH=//nas.local/books
SMB_USERNAME=your-username
SMB_PASSWORD=your-password

# NFS share configuration  
NFS_SHARE_PATH=192.168.1.100:/volume1/books
```

For detailed network storage setup, see: [Network Storage Guide](docs/NETWORK_STORAGE.md)

### Development Features

- **Live Reload**: Source code changes reflected immediately
- **Persistent Data**: Configuration, downloads, and media persist across restarts
- **Health Checks**: Automatic service monitoring
- **Resource Limits**: Configurable CPU and memory constraints
- **Test Scripts**: Validation tools for network storage setup

### Testing Network Storage

```bash
# Test your network storage configuration
./scripts/test-network-storage.sh

# View container logs
docker-compose -f docker-compose.dev.yml logs -f readairr-dev

# Access container shell
docker-compose -f docker-compose.dev.yml exec readairr-dev bash
```

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2010-2022
