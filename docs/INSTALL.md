# Installation

> Neither extension has been installed in its host application during development (no Windows/AutoCAD/SketchUp
> was available). The steps below follow the documented Autodesk Autoloader and SketchUp extension mechanisms.
> Please report anything that does not work, including the AutoCAD command line / Ruby console output and the log
> files named below.

## AutoCAD 2025, 2026, 2027 (Windows)

1. Build (`tools/build_and_test.sh`) or take `dist/SiteDesignPro.bundle.zip`.
2. Unzip so that the folder `SiteDesignPro.bundle` is in
   `%APPDATA%\Autodesk\ApplicationPlugins\` (current user) or `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` (all users).
3. Start AutoCAD. The Autoloader loads `Contents\Win64\R25\SiteDesignPro.AutoCAD.dll` (2025/2026) or
   `Contents\Win64\R26\...` (2027). The ribbon tab **SITE DESIGN PRO** appears.
4. If Windows blocked the downloaded zip: right-click the zip → Properties → *Unblock* before extracting.
   If `SECURELOAD` is 1 or 2, add the bundle folder to *Trusted Locations* (OPTIONS → Files).

Manual load for testing: `NETLOAD` and pick the DLL matching your AutoCAD version.

Check: run `SDPABOUT` - prints version, schema version, catalog and the log path
(`%LOCALAPPDATA%\SiteDesignPro\logs\site-design-pro-<date>.log`). Then `SDPSAMPLE` builds the sample project in an empty drawing.

User catalogs: `SDPCATALOGFOLDER` opens `%APPDATA%\SiteDesignPro\catalogs`; `SDPRELOAD` reloads them.

## SketchUp 2021 or later

1. Window → Extension Manager → *Install Extension* → choose `dist/pb_site_design_pro.rbz`.
2. Menu **Extensions → Site Design Pro** and the *Site Design Pro* toolbar appear (View → Toolbars if hidden).
3. Check: *Import package* → `dist/samples/residential-complex.sdpkg`.

The log is `site_design_pro.log` in SketchUp's temp folder (*Extensions → Site Design Pro → Open log*).

## Uninstall

* AutoCAD: delete the `SiteDesignPro.bundle` folder. Drawings keep their geometry; element data remains as
  XData/Xrecords and is ignored without the plug-in.
* SketchUp: Extension Manager → Uninstall. Models keep groups, tags and attributes.
