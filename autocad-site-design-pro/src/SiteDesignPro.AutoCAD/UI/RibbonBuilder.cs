using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;

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
            if (macro == null)
            {
                return;
            }

            // "hint:<type id>|<command>": the element type is handed to the command directly, not typed in
            if (macro.StartsWith(RibbonBuilder.HintPrefix, StringComparison.Ordinal))
            {
                int bar = macro.IndexOf('|');
                SitePalette.Run(macro.Substring(bar + 1), macro.Substring(RibbonBuilder.HintPrefix.Length, bar - RibbonBuilder.HintPrefix.Length));
            }
            else
            {
                SitePalette.Run(macro);
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


        /// <summary>Command parameter prefix of buttons that hand an element type to the command.</summary>
        public const string HintPrefix = "hint:";
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

            var tab = new RibbonTab { Title = "VANJSKO UREĐENJE PRO", Id = TabId, Description = "Vanjsko uređenje PRO – autor " + Author };
            ElementCatalog cat = AcadEnv.Catalog;

            // Six panels in the order of the work; rarely used tools are under Pomoć → Napredno.
            tab.Panels.Add(Panel("Projekat",
                Large("Novi projekat", "SDPNEW", "new", "Naziv, jedinice, mjerilo i nulta kota u jednom prozoru."),
                Stack(Small("Prvi koraci", "SDPPRVIKORACI", "steps", "Redoslijed rada kao lista: svaki korak pokreće svoju naredbu i štiklira se kad je urađen."),
                      Small("Nulta kota", "SDPDATUM", "spotlevel", "Nulta kota objekta (±0,00), kota partera i visinski datum."),
                      Small("Postavke", "SDPSETTINGS", "settings", "Postavke projekta: jedinice, ishodište, sjever, mjerilo, numeracija.")),
                Stack(Commands("Uvoz DWG-a", "import", "Rad na postojećem crtežu (geodetska podloga, arhitekta).",
                          Small("Mapiranje slojeva", "SDPMAPIRANJE", "layers", "Povežite slojeve izvornog crteža s ulogama (parcela, objekti, kote...)."),
                          Small("Provjera crteža", "SDPPROVJERADWG", "validate", "Izvještaj o ulaznom crtežu: jedinice, parcela, objekti, kote."),
                          Small("Preuzmi objekte", "SDPPREUZMI", "convert", "Mapiranu parcelu i tlocrte objekata pretvara u elemente."),
                          Small("Pretvori krive", "SDPCONVERT", "convert", "Odabrane polilinije, linije, lukove ili krugove pretvara u elemente.")),
                      Small("Primjer projekta", "SDPSAMPLE", "sample", "Nacrta primjer projekta za učenje."),
                      Small("Slojevi", "SDPLAYERS", "layers", "Napravi, popravi ili provjeri standard slojeva."))));

            tab.Panels.Add(Panel("Crtanje",
                Large("Biblioteka", "SDPBROWSER", "browser", "Svi elementi s pretragom; dvostruki klik postavlja ili crta element."),
                Large("Ulica", "SDPULICA", "street", "Ulica iz osovine: jednosmjerna ili dvosmjerna, 1–3 trake, trotoari i biciklističke staze, ivičnjaci, linije traka i strelice. Sve ulice i ceste se spajaju bez preklapanja, uglovi raskrsnica se zaobljuju."),
                Large("Kružni tok", "SDPKRUZNITOK", "roundabout", "Cijeli kružni tok iz jedne tačke: prsten, ostrvo, ivičnjaci, razdjelna ostrva, prelazi, strelice, znakovi i krakovi."),
                Stack(Types(cat, "Parcela i objekti", "site", "site", "buildings"),
                      Types(cat, "Parter i ograde", "surfaces", "surfaces", "walls"),
                      Types(cat, "Ceste i biciklisti", "roads", "roads", "cycling")),
                Stack(Types(cat, "Zelenilo", "landscape", "landscape"),
                      Types(cat, "Rasvjeta", "lighting", "lighting"),
                      Types(cat, "Mobilijar i oprema", "furniture", "furniture", "structures", "decorative", "information")),
                Stack(Types(cat, "Voda i instalacije", "utilities", "water", "utilities", "sustainability", "climate"),
                      Small("Duž linije", "SDPPATHARRAY", "patharray", "Drvored, svjetiljke, klupe ili stalci duž linije na zadanom razmaku."),
                      Small("Raster sadnje", "SDPGRID", "plantgrid", "Popuna površine sadnjom.")),
                Stack(Small("Uredi ulicu", "SDPULICAUREDI", "edit", "Trake, širine, trotoari, staze, radijus i strelice ulice; obična cesta tako postaje ulica."),
                      Small("Pješački prijelaz", "SDPPJESACKIPRIJELAZ", "roads", "Bijele pruge 0,50 m s razmakom 0,50 m preko svih traka; na ulici se sam okrene i uzme trake."),
                      Small("Osvježi ceste", "SDPCESTEOSVJEZI", "reload", "Ponovo crta kolovoze, ivičnjake, trotoare i oznake svih ulica i cesta (radi se i samo).")),
                Stack(Small("Niz parking mjesta", "SDPPARKING", "parking", "Red parking mjesta uz ivicu prolaza."),
                      Small("Pretvori krive", "SDPCONVERT", "convert", "Postojeće linije ili krugove pretvara u elemente."))));

            tab.Panels.Add(Panel("Izmjena",
                Large("Uredi element", "SDPEDIT", "edit", "Širina, visina i ostali podaci odabranih elemenata. Isto se može i u paleti, kartica Svojstva."),
                Stack(Small("Odabir", "SDPSELECT", "select", "Odabir svih elemenata iste vrste, kategorije, oznake ili materijala."),
                      Small("Zone sigurnosti", "SDPCLEARANCE", "clearance", "Zone servisa i sigurnosti oko opreme."),
                      Small("Popravi oznake", "SDPAUDIT", "audit", "Nove oznake za kopirane elemente i izvještaj o problemima."))));

            tab.Panels.Add(Panel("Dokumentacija",
                Large("Dokumentuj list", "SDPDOKUMENTUJ", "sheet", "Brojevi, oznake, površine, širine, legenda, tabele, sjever, razmjernik i okvir lista odjednom; veličina lista se bira sama."),
                Stack(Small("Oznaka", "SDPLABEL", "label", "Oznake odabranih elemenata."),
                      Small("Numeracija", "SDPNUMBER", "number", "Brojevi elemenata od sjevera prema jugu."),
                      Small("Legenda", "SDPLEGEND", "legend", "Legenda vrsta elemenata.")),
                Stack(Small("Tabele", "SDPSCHEDULE", "schedule", "Količinske tabele."),
                      Small("Sastavnica", "SDPTITLEBLOCK", "titleblock", "Okvir lista i sastavnica."),
                      Small("Izvoz CSV", "SDPSCHEDULECSV", "csv", "Tabele za Excel.")),
                Large("Prezentacija u boji", "SDPPREZENTACIJA", "decorative", "Plan u boji za investitora: materijali, zelenilo, voda, krošnje i sjene. Ponovni klik osvježava boje.")));

            tab.Panels.Add(Panel("Provjera i izvoz",
                Large("Provjeri projekat", "SDPVALIDATE", "validate", "Greške, kolizije, izlazak iz parcele; rezultat u paleti, grupisano."),
                Large("Izvoz paketa", "SDPEXPORT", "export", "Paket .sdpkg za 3D model u SketchUpu."),
                Stack(Small("Uvoz paketa", "SDPUVOZPAKETA", "import", "Učitava .sdpkg paket u crtež."),
                      Small("Primijeni izmjene", "SDPAPPLYCHANGES", "apply", "Izmjene iz SketchUpa, s pregledom prije primjene."),
                      Small("Količine", "SDPQUANTITIES", "quantities", "Količine po vrsti u komandnoj liniji.")),
                Stack(Small("Urbanistički pokazatelji", "SDPPOKAZATELJI", "area", "Kiz, Kis, zelenilo, vodopropusne površine i parking prema zahtjevima regulacionog plana."),
                      Small("Prohodnost vozila", "SDPPROHODNOST", "clearance", "Putanja vatrogasnog vozila, kamiona za smeće, dostavnog ili putničkog vozila: krivine i udari u prepreke."))));

            tab.Panels.Add(Panel("Pomoć",
                Large("Pomoć", "SDPPOMOC", "about", "Uputstvo za korištenje na bosanskom jeziku."),
                LargeCommands("Napredno", "advanced", "Alati koji se rjeđe koriste: georeferenciranje, teren, kote, posebne oznake, sigurnosne kopije.",
                    Small("Kontrolne tačke (georeferenca)", "SDPGEOREF", "coord", "Georeferenciranje preko kontrolnih tačaka."),
                    Small("Izvoz KML (Google Earth)", "SDPKML", "export", "KML samo za orijentaciju."),
                    Small("Koordinate", "SDPCOORD", "coord", "Oznaka koordinata Y / X."),
                    Small("Strelica sjevera", "SDPNORTH", "north", "Strelica prema stvarnom sjeveru."),
                    Small("Razmjernik", "SDPSCALEBAR", "scalebar", "Grafički razmjernik."),
                    Small("Dodijeli kotu", "SDPKOTA", "spotlevel", "Kota za odabrani objekat."),
                    Small("Kote iz oznaka", "SDPKOTEIZOZNAKA", "label", "Prijedlog kota iz tekstualnih oznaka."),
                    Small("Visinska kota", "SDPSPOTLEVEL", "spotlevel", "Oznaka relativne i apsolutne kote."),
                    Small("Strelica nagiba", "SDPSLOPE", "slope", "Strelica nagiba iz kota."),
                    Small("Model terena", "SDPTEREN", "slope", "Teren iz tačaka i izohipsi."),
                    Small("Profil terena", "SDPPROFIL", "section", "Uzdužni profil duž linije."),
                    Small("Analiza terena", "SDPANALIZATERENA", "slope", "Nagibi, depresije, kubature (orijentaciono)."),
                    Small("Oznake površina", "SDPAREALABEL", "area", "Šifra, vrsta i površina."),
                    Small("Oznake širina", "SDPWIDTHLABEL", "width", "Širine cesta i staza."),
                    Small("Pokrivenost rasvjete", "SDPLIGHTCOVER", "lightcover", "Ilustrativni krugovi; nije fotometrija."),
                    Small("Presjek", "SDPSECTION", "section", "Linija presjeka."),
                    Small("Detalj", "SDPDETAIL", "detail", "Oznaka detalja."),
                    Small("Rasterske ose", "SDPGRIDLINES", "grid", "Ose s oznakama."),
                    Small("Oblak revizije", "SDPREVCLOUD", "revcloud", "Oblak revizije s napomenom."),
                    Small("Pregled simbola", "SDPSIMBOLI", "legend", "List sa svim simbolima elemenata i njihovim nazivima."),
                    Small("Korisnički katalozi", "SDPCATALOGFOLDER", "catalog", "Folder za vlastite vrste elemenata."),
                    Small("Učitaj katalog", "SDPRELOAD", "reload", "Ponovno učitavanje kataloga."),
                    Small("Sigurnosna kopija", "SDPKOPIJA", "export", "Provjerena kopija sačuvanog crteža."),
                    Small("Postavke kopija", "SDPKOPIJEPOSTAVKE", "settings", "Automatske kopije prije većih operacija."),
                    Small("Oporavak", "SDPOPORAVAK", "apply", "Prekinute operacije i otvaranje kopije."),
                    Small("O programu", "SDPABOUT", "about", "Verzija, katalog i dnevnik.")),
                Stack(Label("Vanjsko uređenje PRO"), Label("Autor: " + Author), Label("© 2026"))));

            ribbon.Tabs.Add(tab);
            Log.Info("Ribbon tab created with " + tab.Panels.Count + " panels.");
        }
        }

        /// <summary>Drop-down of all element types of the given categories; picking one starts placing or drawing it.</summary>
        private static RibbonSplitButton Types(ElementCatalog cat, string title, string icon, params string[] categories)
        {
            var names = categories.Select(c => CategoryName(cat, c)).ToList();
            List<ElementType> types = cat.PlaceableTypes.Where(t => categories.Contains(t.Category) || t.ExtraPanels.Any(names.Contains)).ToList();
            var split = new RibbonSplitButton
            {
                Text = title,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Standard,
                Image = Icons.Get(icon, 16),
                LargeImage = Icons.Get(icon, 32),
                IsSplit = false,
                IsSynchronizedWithCurrentItem = false,
                ListStyle = RibbonSplitButtonListStyle.List,
                ToolTip = new RibbonToolTip { Title = title, Content = "Odaberite element s liste: tačkasti se postavlja klikom, linijski i površinski se crtaju." },
            };
            foreach (ElementType t in types.OrderBy(t => t.Subcategory).ThenBy(t => t.Name))
            {
                string command = t.Geometry == Core.Geometry.GeometryKind.Point ? "_SDPPLACE " : "_SDPDRAW ";
                RibbonButton b = Small(t.Name, command, t.Category, PlacementTip(t));
                b.CommandParameter = HintPrefix + t.Id + "|" + command;
                b.Id = "SDP_TYPE_" + t.Id;
                split.Items.Add(b);
            }

            return split;
        }

        /// <summary>Small drop-down grouping related commands.</summary>
        private static RibbonSplitButton Commands(string title, string icon, string tip, params RibbonButton[] buttons) => CommandList(title, icon, tip, RibbonItemSize.Standard, buttons);

        /// <summary>Large drop-down grouping related commands.</summary>
        private static RibbonSplitButton LargeCommands(string title, string icon, string tip, params RibbonButton[] buttons) => CommandList(title, icon, tip, RibbonItemSize.Large, buttons);

        private static RibbonSplitButton CommandList(string title, string icon, string tip, RibbonItemSize size, RibbonButton[] buttons)
        {
            var split = new RibbonSplitButton
            {
                Text = title,
                ShowText = true,
                ShowImage = true,
                Size = size,
                Orientation = size == RibbonItemSize.Large ? System.Windows.Controls.Orientation.Vertical : System.Windows.Controls.Orientation.Horizontal,
                Image = Icons.Get(icon, 16),
                LargeImage = Icons.Get(icon, 32),
                IsSplit = false,
                IsSynchronizedWithCurrentItem = false,
                ListStyle = RibbonSplitButtonListStyle.List,
                ToolTip = new RibbonToolTip { Title = title, Content = tip },
            };
            foreach (RibbonButton b in buttons)
            {
                b.Id = "SDP_" + title.Replace(" ", string.Empty) + "_" + b.Id;
                split.Items.Add(b);
            }

            return split;
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

        /// <summary>Author credit shown on the ribbon and by SDPABOUT.</summary>
        internal const string Author = "Amsal Aganspahić";

        private static RibbonLabel Label(string text) => new RibbonLabel { Text = text };

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

        private static string PlacementTip(ElementType t) => t.Name + " (" + t.Id + "). " + t.Builder.Description +
            (t.Geometry == Core.Geometry.GeometryKind.Point ? " Odaberite tačku umetanja i rotaciju." : " Nacrtajte poliliniju ili pravougaonik, odaberite granicu ili postojeće krive.");
    }
}
