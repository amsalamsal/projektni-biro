using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>
    /// Right click on a selected element: "Vanjsko uređenje PRO" with Uredi element (all its data in one window),
    /// Uredi ulicu (for a street or road), Oznaka and Sinhronizacija. The items are greyed out when nothing of the
    /// add-in is selected, so the menu never gets in the way of AutoCAD's own commands.
    /// </summary>
    internal static class ElementContextMenu
    {
        private static ContextMenuExtension? menu;

        public static void Attach()
        {
            if (menu != null)
            {
                return;
            }

            menu = new ContextMenuExtension { Title = "Vanjsko uređenje PRO" };
            MenuItem edit = Item("Uredi element…", "_SDPEDIT ");
            MenuItem street = Item("Uredi ulicu…", "_SDPULICAUREDI ");
            MenuItem label = Item("Oznaka", "_SDPLABEL ");
            menu.MenuItems.Add(edit);
            menu.MenuItems.Add(street);
            menu.MenuItems.Add(label);
            menu.Popup += (s, e) => Log.Guard("Meni desnog klika", () =>
            {
                (bool any, bool road) = Selected();
                edit.Enabled = any;
                label.Enabled = any;
                street.Enabled = road;
            });
            AcApp.AddObjectContextMenuExtension(RXObject.GetClass(typeof(Entity)), menu);
        }

        private static MenuItem Item(string text, string macro)
        {
            var item = new MenuItem(text);
            item.Click += (s, e) => Log.Guard(text, () => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(macro, true, false, false));
            return item;
        }

        /// <summary>Whether the selection holds an element of the add-in, and whether one of them is a street or road.</summary>
        private static (bool Any, bool Road) Selected()
        {
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return (false, false);
            }

            PromptSelectionResult sel = doc.Editor.SelectImplied();
            if (sel.Status != PromptStatus.OK)
            {
                return (false, false);
            }

            bool any = false, road = false;
            using (Transaction tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in sel.Value.GetObjectIds())
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent) || !ElementStore.IsManaged(ent))
                    {
                        continue;
                    }

                    any = true;
                    string? type = ElementStore.ReadKey(ent)?.Type;
                    if (type == Core.Roads.RoadElements.StreetType || (type != null && Core.Roads.RoadElements.IsNetworkRoad(AcadEnv.Catalog.FindType(type))))
                    {
                        road = true;
                    }
                }

                tr.Commit();
            }

            return (any, road);
        }
    }
}
