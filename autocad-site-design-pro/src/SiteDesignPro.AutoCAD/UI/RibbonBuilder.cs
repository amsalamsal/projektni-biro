using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>Executes the command string stored in a ribbon item's CommandParameter.</summary>
    internal sealed class RibbonCommandHandler : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            string? macro = (parameter as RibbonCommandItem)?.CommandParameter as string;
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (macro != null && doc != null)
            {
                doc.SendStringToExecute(macro, true, false, true);
            }
        }
    }

    /// <summary>
    /// Builds the VANJSKO UREĐENJE PRO ribbon tab. Every button runs a real command; element-type buttons are
    /// generated from the catalog (so user catalog types appear automatically).
    /// </summary>
    internal static class RibbonBuilder
    {
        public const string TabId = "SITE_DESIGN_PRO_TAB";
        private static readonly RibbonCommandHandler Handler = new RibbonCommandHandler();

        public static void Create()
        {
            RibbonControl? ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
            {
                return;
            }

            RibbonTab? old = ribbon.Tabs.FirstOrDefault(t => t.Id == TabId);
            if (old != null)
            {
                ribbon.Tabs.Remove(old);
            }

            var tab = new RibbonTab { Title = "VANJSKO UREĐENJE PRO", Id = TabId };
            ElementCatalog cat = AcadEnv.Catalog;

            tab.Panels.Add(Panel("Projekt",
                Large("Novi projekat", "SDPNEW", "new", "Postavke projekta: naziv, jedinice, ishodište, sjever, razmjera."),
                Stack(Small("Postavke", "SDPSETTINGS", "settings", "Postavke projekta: jedinice, ishodište, sjever, razmjera oznaka, numeracija."),
                      Small("Slojevi", "SDPLAYERS", "layers", "Napravi, popravi ili provjeri standard slojeva proširenja (izvorni slojevi se ne mijenjaju)."),
                      Small("Biblioteka", "SDPBROWSER", "browser", "Paleta: biblioteka elemenata, svojstva i rezultati provjere.")),
                Break(),
                Small("Primjer projekta", "SDPSAMPLE", "sample", "Primjer stambenog kompleksa i/ili primjer izvornog crteža za učenje toka rada.")));

            tab.Panels.Add(Panel("Uvoz DWG-a",
                Large("Mapiranje slojeva", "SDPMAPIRANJE", "layers", "Povežite slojeve izvornog crteža s ulogama (parcela, objekti, kote...). Nazivi slojeva se ne mijenjaju."),
                Stack(Small("Provjera crteža", "SDPPROVJERADWG", "validate", "Izvještaj o ulaznom crtežu: jedinice, parcela, objekti, kote, tačke terena. Ništa se ne izmišlja."),
                      Small("Preuzmi objekte", "SDPPREUZMI", "convert", "Pretvori mapiranu parcelu i tlocrte objekata u elemente. Oznake objekata samo uz potvrdu."),
                      Small("Pretvori krive", "SDPCONVERT", "convert", "Pretvori odabrane polilinije, linije, lukove ili krugove u elemente.")),
                Stack(CategorySplits(cat, new[] { "site", "buildings" }).ToArray())));

            tab.Panels.Add(Panel("Georeferenciranje",
                Large("Kontrolne tačke", "SDPGEOREF", "coord", "Georeferenciranje preko kontrolnih tačaka (jedna tačka daje samo položaj). Bilježi se historija transformacija."),
                Stack(Small("Izvoz KML", "SDPKML", "export", "KML za Google Earth – samo za orijentaciju, nije geodetska podloga."),
                      Small("Koordinate", "SDPCOORD", "coord", "Oznaka koordinata Y / X."),
                      Small("Sjever", "SDPNORTH", "north", "Strelica sjevera prema stvarnom sjeveru projekta."))));

            tab.Panels.Add(Panel("Teren i kote",
                Large("Nulta kota", "SDPDATUM", "spotlevel", "Nulta kota objekta (±0,00), kota partera i visinski datum. Spuštanje saobraćajnica zadaje korisnik."),
                Stack(Small("Dodijeli kotu", "SDPKOTA", "spotlevel", "Kota samo za odabrani objekat, s pregledom prije primjene."),
                      Small("Kote iz oznaka", "SDPKOTEIZOZNAKA", "label", "Prijedlog kota iz tekstualnih oznaka; prikaz veza, pa potvrda. Nejasne oznake se ne pogađaju."),
                      Small("Visinska kota", "SDPSPOTLEVEL", "spotlevel", "Oznaka relativne i apsolutne kote.")),
                Stack(Small("Model terena", "SDPTEREN", "slope", "Teren iz tačaka, izohipsi i prelomnica; izvještaj kada podataka nema dovoljno."),
                      Small("Profil", "SDPPROFIL", "section", "Uzdužni profil terena duž linije."),
                      Small("Analiza terena", "SDPANALIZATERENA", "slope", "Nagibi, lokalne depresije, kubature prema koti. Orijentaciono, nije inženjerski proračun.")),
                Small("Strelica nagiba", "SDPSLOPE", "slope", "Strelica nagiba iz kota tačaka.")));

            AddCategoryPanel(tab, cat, "Parter i popločanje", new[] { "surfaces", "walls" },
                Small("Površina", "SDPDRAW surface.concrete_paving\n", "draw", "Površina iz polilinije, pravougaonika ili odabrane granice."),
                Small("Oznake površina", "SDPAREALABEL", "area", "Šifra, vrsta i površina."));
            AddCategoryPanel(tab, cat, "Zelenilo", new[] { "landscape" },
                Small("Drvored", "SDPPATHARRAY", "patharray", "Ponavljanje stabala duž putanje na zadanom razmaku."),
                Small("Raster sadnje", "SDPGRID", "plantgrid", "Popuna granice rasterom sadnje (kvadratni ili smaknuti)."));
            AddCategoryPanel(tab, cat, "Saobraćaj i parking", new[] { "roads", "cycling" },
                Small("Niz parking mjesta", "SDPPARKING", "parking", "Niz parking mjesta uz rub prolaza."),
                Small("Oznake širina", "SDPWIDTHLABEL", "width", "Oznake širina saobraćajnica i staza."),
                Small("Stalci duž staze", "SDPPATHARRAY", "patharray", "Ponavljanje stalaka duž staze."));
            AddCategoryPanel(tab, cat, "Infrastruktura", new[] { "utilities", "water", "sustainability", "climate" });
            AddCategoryPanel(tab, cat, "Rasvjeta", new[] { "lighting" },
                Small("Svjetiljke duž puta", "SDPPATHARRAY", "patharray", "Ponavljanje svjetiljki duž puta ili staze."),
                Small("Pokrivenost (ilustr.)", "SDPLIGHTCOVER", "lightcover", "Ilustrativni krugovi pokrivenosti; nije fotometrijski proračun."));
            AddCategoryPanel(tab, cat, "Urbani mobilijar", new[] { "furniture", "structures", "decorative", "information" },
                Small("Raspored duž staze", "SDPPATHARRAY", "patharray", "Raspored klupa ili mobilijara duž staze."));

            tab.Panels.Add(Panel("3D modeli",
                Large("Uredi element", "SDPEDIT", "edit", "Parametri, oznaka i svojstva odabranih elemenata. 3D model u SketchUpu se gradi iz ovih parametara."),
                Stack(Small("Zone sigurnosti", "SDPCLEARANCE", "clearance", "Zone servisa, sigurnosti i podizanja iz vrijednosti elementa."),
                      Small("Odabir", "SDPSELECT", "select", "Odabir elemenata po vrsti, kategoriji, oznaci, id-u ili materijalu."),
                      Small("Korisnički katalozi", "SDPCATALOGFOLDER", "catalog", "Folder za korisničke vrste elemenata, slojeve i materijale."))));

            tab.Panels.Add(Panel("SketchUp razmjena",
                Large("Izvoz paketa", "SDPEXPORT", "export", "Provjera i zapis .sdpkg paketa za SketchUp (elementi, kote, georeferenca, teren)."),
                Stack(Small("Primijeni izmjene", "SDPAPPLYCHANGES", "apply", "Pregled i primjena izmjena iz SketchUpa (parametri, pomjereni elementi)."),
                      Small("Provjera prije izvoza", "SDPVALIDATE", "validate", "Sve provjere prije izvoza."))));

            tab.Panels.Add(Panel("Količinski obračun",
                Large("Tabele", "SDPSCHEDULE", "schedule", "Količinske tabele kao AutoCAD tabele (mjereno i procijenjeno ~ odvojeno)."),
                Stack(Small("Izvoz CSV", "SDPSCHEDULECSV", "csv", "Sve tabele u CSV (;) i JSON."),
                      Small("Količine", "SDPQUANTITIES", "quantities", "Količine po vrsti u komandnoj liniji."))));

            tab.Panels.Add(Panel("Nacrti i oznake",
                Large("Oznaka", "SDPLABEL", "label", "Oznake elemenata s brojem i ključnim podacima (multileader)."),
                Stack(Small("Numeracija", "SDPNUMBER", "number", "Dodjela brojeva ili prenumeracija sjever–jug, zapad–istok."),
                      Small("Legenda", "SDPLEGEND", "legend", "Legenda vrsta elemenata u crtežu."),
                      Small("Sastavnica", "SDPTITLEBLOCK", "titleblock", "Okvir lista i sastavnica u razmjeri projekta.")),
                Break(),
                Stack(Small("Razmjernik", "SDPSCALEBAR", "scalebar", "Grafički razmjernik."),
                      Small("Presjek", "SDPSECTION", "section", "Linija presjeka s oznakama."),
                      Small("Detalj", "SDPDETAIL", "detail", "Oznaka detalja.")),
                Stack(Small("Rasterske ose", "SDPGRIDLINES", "grid", "Ose s oznakama."),
                      Small("Oblak revizije", "SDPREVCLOUD", "revcloud", "Oblak revizije s napomenom."))));

            tab.Panels.Add(Panel("Provjera projekta",
                Large("Provjeri projekat", "SDPVALIDATE", "validate", "Geometrija, podaci, kolizije, izlazak iz parcele, kote saobraćajnica. Upozorenja softvera, ne potvrda propisa."),
                Stack(Small("Provjera crteža", "SDPPROVJERADWG", "validate", "Ponovna provjera ulaznog DWG-a."),
                      Small("Revizija", "SDPAUDIT", "audit", "Popravak identifikatora kopiranih objekata i izvještaj o problemima."))));

            tab.Panels.Add(Panel("Postavke i pomoć",
                Large("Pomoć", "SDPPOMOC", "about", "Uputstvo za korištenje na bosanskom jeziku."),
                Stack(Small("Sigurnosna kopija", "SDPKOPIJA", "export", "Provjerena kopija zadnje sačuvane verzije crteža."),
                      Small("Postavke kopija", "SDPKOPIJEPOSTAVKE", "settings", "Automatske kopije prije većih operacija, broj i starost kopija."),
                      Small("Oporavak", "SDPOPORAVAK", "apply", "Prekinute operacije i otvaranje kopije kao novog crteža.")),
                Stack(Small("Učitaj katalog", "SDPRELOAD", "reload", "Ponovno učitavanje kataloga s korisničkim katalozima."),
                      Small("O programu", "SDPABOUT", "about", "Verzija, katalog i log datoteka."))));

            ribbon.Tabs.Add(tab);
            Log.Info("Ribbon tab created with " + tab.Panels.Count + " panels.");
        }

        /// <summary>Panel with element types of one or more catalog categories, grouped into split buttons by subcategory.</summary>
        private static void AddCategoryPanel(RibbonTab tab, ElementCatalog cat, string title, string[] categories, params RibbonItem[] tools)
        {
            var items = new List<RibbonItem>
            {
                Large("Pregled", "SDPBROWSECAT " + categories[0] + "\n", categories[0], "Svi elementi kategorije u paleti: " + string.Join(", ", categories.Select(c => CategoryName(cat, c))) + "."),
            };
            List<RibbonItem> splits = CategorySplits(cat, categories);
            for (int i = 0; i < splits.Count; i += 3)
            {
                items.Add(Stack(splits.Skip(i).Take(3).ToArray()));
                if (i == 0 && splits.Count > 6)
                {
                    // keep the panel compact: the remaining subcategories go to the slide-out
                    items.Add(Break());
                }
            }

            for (int i = 0; i < tools.Length; i += 3)
            {
                items.Add(Stack(tools.Skip(i).Take(3).ToArray()));
            }

            tab.Panels.Add(Panel(title, items.ToArray()));
        }

        private static List<RibbonItem> CategorySplits(ElementCatalog cat, string[] categories)
        {
            var result = new List<RibbonItem>();
            foreach (string category in categories)
            {
                string name = CategoryName(cat, category);
                List<ElementType> types = cat.Types.Where(t => t.Category == category || t.ExtraPanels.Contains(name)).ToList();
                result.AddRange(types.GroupBy(t => t.Subcategory.Length > 0 ? t.Subcategory : name)
                    .Select(g => (RibbonItem)Split(g.Key, category, g.ToList())));
            }

            return result;
        }

        private static string CategoryName(ElementCatalog cat, string id) => cat.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? id;

        private static RibbonPanel Panel(string title, params RibbonItem[] items)
        {
            var source = new RibbonPanelSource { Title = title };
            foreach (RibbonItem i in items)
            {
                source.Items.Add(i);
            }

            return new RibbonPanel { Source = source };
        }

        private static RibbonItem Break() => new RibbonPanelBreak();

        private static RibbonButton Button(string text, string macro, string icon, string tip, RibbonItemSize size)
        {
            string command = macro.EndsWith("\n", StringComparison.Ordinal) ? "_" + macro : "_" + macro + " ";
            return new RibbonButton
            {
                Text = text,
                ShowText = true,
                ShowImage = true,
                Size = size,
                Orientation = size == RibbonItemSize.Large ? System.Windows.Controls.Orientation.Vertical : System.Windows.Controls.Orientation.Horizontal,
                Image = Icons.Get(icon, 16),
                LargeImage = Icons.Get(icon, 32),
                CommandParameter = command,
                CommandHandler = Handler,
                ToolTip = new RibbonToolTip { Title = text, Content = tip, Command = macro.Split(' ', '\n')[0] },
                Id = "SDP_" + macro.Split(' ', '\n')[0] + "_" + text.Replace(" ", string.Empty),
            };
        }

        private static RibbonButton Large(string text, string macro, string icon, string tip) => Button(text, macro, icon, tip, RibbonItemSize.Large);

        private static RibbonButton Small(string text, string macro, string icon, string tip) => Button(text, macro, icon, tip, RibbonItemSize.Standard);

        private static RibbonRowPanel Stack(params RibbonItem[] items)
        {
            var row = new RibbonRowPanel();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    row.Items.Add(new RibbonRowBreak());
                }

                row.Items.Add(items[i]);
            }

            return row;
        }

        private static RibbonSplitButton Split(string title, string icon, List<ElementType> types)
        {
            var split = new RibbonSplitButton
            {
                Text = title,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Standard,
                Image = Icons.Get(icon, 16),
                LargeImage = Icons.Get(icon, 32),
                IsSplit = true,
                IsSynchronizedWithCurrentItem = true,
                ListStyle = RibbonSplitButtonListStyle.List,
            };
            foreach (ElementType t in types)
            {
                string command = t.Geometry == Core.Geometry.GeometryKind.Point ? "SDPPLACE " : "SDPDRAW ";
                RibbonButton b = Small(t.Name, command + t.Id + "\n", icon, PlacementTip(t));
                split.Items.Add(b);
            }

            return split;
        }

        private static string PlacementTip(ElementType t) => t.Name + " (" + t.Id + "). " + t.Builder.Description +
            (t.Geometry == Core.Geometry.GeometryKind.Point ? " Odaberite tačku umetanja i rotaciju." : " Nacrtajte poliliniju ili pravougaonik, odaberite granicu ili postojeće krive.");
    }
}
