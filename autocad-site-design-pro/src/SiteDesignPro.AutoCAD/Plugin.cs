using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using SiteDesignPro.AutoCAD.Commands;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.AutoCAD.UI;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(SiteDesignPro.AutoCAD.Plugin))]
[assembly: CommandClass(typeof(ProjectCommands))]
[assembly: CommandClass(typeof(PlacementCommands))]
[assembly: CommandClass(typeof(DocumentationCommands))]
[assembly: CommandClass(typeof(ExchangeCommands))]
[assembly: CommandClass(typeof(PaletteCommands))]

namespace SiteDesignPro.AutoCAD
{
    /// <summary>
    /// Extension entry point: creates the ribbon tab (now, or when the ribbon becomes available), rebuilds it
    /// after workspace or theme changes, and keeps the properties palette in sync with the selection.
    /// </summary>
    public sealed class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                Log.Info("Site Design Pro " + AcadEnv.Version + " loading in AutoCAD " + AcApp.Version + " (.NET " + Environment.Version + ")");
                _ = AcadEnv.Catalog;
                if (ComponentManager.Ribbon != null)
                {
                    RibbonBuilder.Create();
                }
                else
                {
                    ComponentManager.ItemInitialized += OnItemInitialized;
                }

                AcApp.SystemVariableChanged += OnSystemVariableChanged;
                AcApp.DocumentManager.DocumentCreated += (s, e) => Hook(e.Document);
                foreach (Document doc in AcApp.DocumentManager)
                {
                    Hook(doc);
                }

                AcadEnv.Message("Site Design Pro " + AcadEnv.Version + " loaded: SITE DESIGN PRO ribbon tab, palette SDPBROWSER, start with SDPNEW or SDPSAMPLE.");
            }
            catch (System.Exception ex)
            {
                // Initialization must never take AutoCAD down; the failure is logged with details.
                Log.Error("Site Design Pro failed to initialise", ex);
            }
        }

        public void Terminate()
        {
            Log.Info("Site Design Pro unloading");
        }

        private static void Hook(Document doc)
        {
            doc.ImpliedSelectionChanged += (s, e) => SitePalette.SelectionChanged();
        }

        private static void OnItemInitialized(object? sender, RibbonItemEventArgs e)
        {
            if (ComponentManager.Ribbon != null)
            {
                ComponentManager.ItemInitialized -= OnItemInitialized;
                RibbonBuilder.Create();
            }
        }

        private static void OnSystemVariableChanged(object? sender, Autodesk.AutoCAD.ApplicationServices.SystemVariableChangedEventArgs e)
        {
            if (string.Equals(e.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase) || string.Equals(e.Name, "COLORTHEME", StringComparison.OrdinalIgnoreCase))
            {
                Icons.ClearCache();
                RibbonBuilder.Create();
            }
        }
    }
}
