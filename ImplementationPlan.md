Try to improve the Cover art download

Improve readme to be more user friendly for a user --- also could improve the page --- - Recommended: migrate user docs to a static-site generator with real URLs (VitePress, Astro Starlight, or MkDocs Material), keep developer docs in a separate section, and add a proper landing page.

Create a demo video

“Add to Steam” feature

signed installer + runtime bootstrap

self-contained portable build.

- Recommended: per-user install under `%LocalAppData%\SimpleLauncher`, optional custom folder, no admin unless the runtime must be installed machine-wide.

- Uninstaller must offer to keep user data (default: keep).

### D8 — Code-signing path (individual developer in Brazil, OSS + commercial)

- The project is GPLv3 and the Windows/Linux builds are free/open source. Recommended split:
  - **Windows OSS builds:** apply to **SignPath Foundation** ($0). It signs qualifying FOSS projects with a CA-backed OV-level certificate; the publisher shown to users is **“SignPath Foundation”**, not your name. Apply in week 1; approval can take weeks to months, so do not block the release on it.
  - **Always, even before approval:** publish `SHA256SUMS.txt`, optionally GPG-sign it, and document the SmartScreen “More info → Run anyway” path.
  - **If the publisher name must be yours:** use **Certum Open Source** (≈€49–69/year depending on cloud vs card/reader, “Open Source Developer: <name>”, strictly non-commercial — check donation/sponsorship rules and taxes) or an **Individual Validation (IV)** certificate (≈US$130–300/year plus a hardware token or cloud HSM). From Brazil, prefer a **cloud HSM** (e.g. SSL.com eSigner) over a shipped USB token to avoid customs and delays.

  - **Microsoft Store/MSIX:** individual developer registration is currently **free** (2026; verify), and Microsoft signs Store-delivered packages. This is a distribution channel, not a replacement for signing the installer/portable artifacts; MSIX packaging and Store policy review are extra work. Consider it later.
  
  - **Linux:** no paid CA. GPG-sign the checksums/AppImage; Flathub/Snapcraft handle repository trust. Use `osslsigncode` or `jsign` for Windows signing from Linux CI if needed.
  
  - **macOS:** only if/when macOS ships. Apple Developer Program US$99/year plus notarization; out of scope now.

- [ ] **P0-13** Publish `docs/privacy.md` and link it from README, About, and the landing page.

- [ ] **P1-01** Build an installer prototype (Inno Setup) that detects/installs .NET 11 and installs the current framework-dependent payload.

> Policy and cost matrix: **Appendix A**. Recommended: **SignPath Foundation** for the OSS Windows builds, `SHA256SUMS.txt` + optional GPG from day one, and a paid certificate only if the publisher name must be the author. Apply to SignPath in week 1; do not block the P0 release on approval.

- [ ] **P1-14** Apply to SignPath Foundation: public repo, GPLv3, build instructions, release history, CI workflow, and contact details. Record the application date and expected lead time.

- [ ] **P1-18a** Evaluate MSIX/Microsoft Store as a later distribution channel (individual registration is currently free; Microsoft signs Store packages); document packaging effort and Store policy risk.

- [ ] **P1-18b** Linux: GPG-sign the release checksums/AppImage; publish the public key and verification steps. Flathub/Snap trust is handled by the store.

- [ ] **P1-18c** macOS: record the future requirement (Apple Developer Program US$99/year + notarization); do not implement now.

- [ ] **P1-19** Create a winget manifest for `PureLogicCode.SimpleLauncher`:
  - installer type and silent switches;
  - per-user scope;
  - `.NET Desktop Runtime 11` dependency only if the payload stays framework-dependent.
- [ ] **P1-20** Submit a PR to `microsoft/winget-pkgs`; verify `winget install PureLogicCode.SimpleLauncher` on a clean VM.
- [ ] **P1-21** Add the winget command to README, landing page, and release notes.
- [ ] **P1-22** Optional: Scoop and Chocolatey manifests.


- [ ] **P1-33** UI: replace the first-run Yes/No + Easy Mode dialog with the wizard in **both** apps. Reuse `EasyModeWindow`/`EasyModeViewModel` where possible; add Back/Next/Skip, progress, cancel, and error states.

- [ ] **P1-72** Add an honest comparison table vs LaunchBox, ES-DE/RetroBat, and Playnite with a “last verified” date.

- [ ] **P1-73** Add install one-liners: winget, Steam Deck guide.

- [ ] **P1-74** Add FAQ: runtime, Dokan/FUSE, privacy,save data location.


- [ ] **P1-80** Add a real landing page at the GitHub Pages root: hero, 20-second demo, OS-detected download, screenshots, three feature cards, FAQ, docs link, GitHub link.

- [ ] **P1-81** Add SEO essentials: title/description, canonical URL, sitemap, robots, Open Graph/Twitter image, structured data.

- [ ] **P1-82** Migrate user docs to real URLs (D5); keep developer docs in a separate section; preserve redirects from old hash URLs where practical.

- [ ] **P1-84** Add privacy-friendly analytics and a download-click event.

-- [ ] **P1-91** Create a 15–20 second muted GIF/MP4 for the README and landing page (< 5 MB).

- [ ] **P1-93** Use the assets in README, landing page, GitHub release notes, AlternativeTo, and the wiki.

- [ ] **P1-94** Optional: create a YouTube channel, upload the demo, embed it.

**Acceptance:** landing page has real URLs, an OG preview, and one clear download; README explains the product and install in under ten seconds; demo GIF/video is used in at least three places.

**Acceptance:** Steam Deck guide works end-to-end;

- [ ] **P2-21** YouTube outreach: Retro Game Corps, Phawx, Mr. Sujano, ETA Prime, Fan The Deck. Personalized messages with the 60-second demo and a download link.
- [ ] **P2-22** Reddit: r/emulation, r/EmulationOnPC, r/SteamDeck, r/retrogaming. Be transparent (“I’m the developer”), follow each community’s self-promotion rules, and answer comments for 24 hours.

- [ ] **M-09** Uninstaller behavior: user data kept by default; document cleanup.

| Signing approval or certificate lead time | Unsigned release or launch delay | Apply to SignPath Foundation in week 1; ship checksums + SmartScreen guide; buy a certificate only if needed |

| Certificate cost/eligibility in Brazil | Budget or publisher-name surprises | Use the free OSS program; prefer cloud HSM; Azure individual signing is US/Canada-only, so plan around it; keep OSS/commercial identities separate |

| Self-contained dual-app merge conflicts | Broken payload or large refactor | Prefer the installer path for P0; spike self-contained separately; do not rush |

| Scope creep (new systems/emulators) | Delays the growth work | Freeze new integrations for 90 days; refer requests to the backlog |

## Appendix A — Code-signing options and costs (Brazil, individual developer)

> Prices and eligibility are approximate as of November 2026. Verify with the provider before purchasing. This project is GPLv3; **SignPath Foundation** is the recommended first stop for the free builds.

| Option | Platform | Approx. cost | Publisher shown | SmartScreen / trust | Notes |
|---|---|---|---|---|---|
| **SignPath Foundation** | Windows | **$0** | `SignPath Foundation` | CA-backed OV-level signature; reputation still accrues | Free for qualifying FOSS; publisher is the foundation, not you; approval can take weeks to months |
| **Certum Open Source** | Windows | **≈€49–69/year** (cloud vs card/reader; taxes may apply) | `Open Source Developer: <name>` | OV signature; reputation accrues | Strict non-commercial/open-source terms; check donations/sponsorships |
| **Individual Validation (IV) certificate** (SSL.com/Sectigo/resellers) | Windows | **≈US$130–300/year** + hardware token (≈US$90–130) or cloud HSM (≈US$10–20/month) | Your legal name | OV signature; reputation accrues | For commercial apps or own-name publishing; from Brazil prefer cloud HSM over a shipped token |
| **Azure Artifact Signing** (formerly Trusted Signing) | Windows | **≈US$9.99/month** | Your organization/name | Microsoft cloud signing; SmartScreen reputation | Individuals only in the US/Canada; organizations only in 12 regions that do **not** include Brazil |
| **Microsoft Store / MSIX** | Windows | **$0** for individual accounts (2026; verify) | Store/publisher | Microsoft signs Store packages | Distribution channel, not a replacement for signing installer/portable; MSIX + policy review |
| **Sigstore** | Any | **$0** | OIDC identity / transparency log | No native SmartScreen root trust | Good for provenance/transparency; not a consumer SmartScreen solution |
| **GPG** | Linux | **$0** | Your key | Package/repo trust | Sign checksums/AppImage; Flathub/Snap handle store trust |
| **Apple Developer Program** | macOS | **US$99/year** | Your name/team | Gatekeeper + notarization | Only when macOS ships; out of scope now |

1. **Now:** apply to **SignPath Foundation** (free, publisher “SignPath Foundation”). Approval can take weeks to months; in parallel, publish `SHA256SUMS.txt` and SmartScreen instructions.
2. **If the publisher name must be yours:** budget **Certum Open Source** for the GPLv3 builds (≈€49–69/year) or an **IV certificate** for commercial builds. From Brazil, choose a cloud HSM to avoid international shipping, customs, and import taxes.
3. **Do not plan on Azure Artifact Signing:** individuals must be in the US or Canada, and organizations must be in one of 12 eligible regions that do not include Brazil.
4. **Microsoft Store/MSIX:** individual registration is currently free (2026); it signs Store-delivered packages but does not sign the GitHub installer or portable zip. Treat it as a later distribution experiment.
5. **Linux:** sign checksums/AppImage with GPG and publish the key. Flathub/Snap provide repository trust.

### A.2 Brazil-specific notes

- You can apply as an **individual**; a CNPJ/company is not required for SignPath Foundation or Certum Open Source.
- Prefer **cloud HSM / remote signing** (e.g. SSL.com eSigner) over a physical USB token: faster, no customs, no import tax, and it works from GitHub Actions.
- Verify whether **donations/sponsorships** affect the “non-commercial” terms of an open-source certificate; if in doubt, use SignPath Foundation or a standard IV certificate.
- Keep **OSS and commercial signing identities separate**; do not sign commercial builds with the OSS certificate.
- Microsoft Store individual registration is currently free (2026) and does not require a CNPJ; company accounts may also have free registration now. Verify the current terms and any payout/tax requirements if you ever sell through the Store.
