# SavegameBunker user guide (Zensical)

Two independent [Zensical](https://zensical.org/) projects, one per language, cross-linked via
each project's `extra.alternate` so the built-in language switcher works. Each project also ships
Zensical's built-in light/dark theme toggle — no extra setup needed.

```
docs/userguide/
  en/   English site (zensical.toml + docs/)
  de/   German site (zensical.toml + docs/)
  index.html  # redirect for the web deploy
  overrides/  # shared theme overrides
```

## Build

```powershell
pip install zensical  # CI installs the latest release too
cd docs/userguide/en
python -m zensical build
cd ../de
python -m zensical build
```

Each command writes that language's static site to `site/` next to its `zensical.toml`.

## Preview locally

```powershell
cd docs/userguide/en
python -m zensical serve
```

Serves the English site with live reload. Run the same in `de/` on a different port to preview
both at once.

## Publishing both languages together

The language switcher expects both sites to live under `/en/` and `/de/` of the same host. When
deploying, copy `en/site/` to `<host-root>/en/`, `de/site/` to `<host-root>/de/`, and
`index.html` (the root redirect to `en/`, English being the default) to `<host-root>/index.html`.
The release workflow builds both sites and ships them inside the release zip as the offline help
(`help/en/`, `help/de/`). [`pages.yml`](../../.github/workflows/pages.yml) builds the same two sites
on every push to `main` that touches `docs/userguide/` and publishes them to GitHub Pages at
<https://argusloom.github.io/SavegameBunker/>, with `index.html` as the root redirect. Because every
link is relative (`use_directory_urls = false`), the same build works under the Pages sub-path and
from `file://`.
