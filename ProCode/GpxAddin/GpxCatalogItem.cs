using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ESRI.ArcGIS.ItemIndex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GpxAddin
{
    internal class GpxCatalogItem : CustomItemBase
    {
        protected GpxCatalogItem() : base()
        {
        }

        protected GpxCatalogItem(ItemInfoValue iiv) : base(FlipBrowseDialogOnly(iiv))
        {
        }

        private static ItemInfoValue FlipBrowseDialogOnly(ItemInfoValue iiv)
        {
            iiv.browseDialogOnly = "FALSE";
            return iiv;
        }
        //Overload for use in your container create item
        //public GpxCatalogItem(string name, string catalogPath, string typeID, string containerTypeID) :
        //  base(name, catalogPath, typeID, containerTypeID)
        //{
        //}

        public override ImageSource LargeImage
        {
            get
            {
                var largeImg = new BitmapImage(new Uri(@"pack://application:,,,/GpxAddin;component/Images/gpx32.png"));
                return largeImg;
            }
        }

        public override Task<ImageSource> SmallImage
        {
            get
            {
                var smallImage = new BitmapImage(new Uri(@"pack://application:,,,/GpxAddin;component/Images/gpx16.png"));
                if (smallImage == null) throw new ArgumentException("SmallImage for CustomItem doesn't exist");
                return Task.FromResult(smallImage as ImageSource);
            }
        }

        public override bool IsContainer => false;

        //TODO: Fetch is required if <b>IsContainer</b> = <b>true</b>
        //public override void Fetch()
        //    {
        //TODO Retrieve your child items
        //TODO child items must also derive from CustomItemBase
        //this.AddRangeToChildren(children);
        //   }
    }
    internal class ShowItemNameGpxCatalogItem : Button
    {
        protected override void OnClick()
        {
            var catalog = Project.GetCatalogPane();
            var items = catalog.SelectedItems;
            var item = items.OfType<GpxCatalogItem>().FirstOrDefault();
            if (item == null)
                return;
            //MessageBox.Show($"Selected Custom Item: {item.Name}");
            QueuedTask.Run(() =>
            {
                try
                {
                    ArcGIS.Core.Geometry.Envelope zoomToEnv = null;
                    var connection = new PluginDatasourceConnectionPath("GpxPlugin_Datasource",
                         new Uri(item.Path, UriKind.Absolute));
                    using (var pluginGpx = new PluginDatastore(connection))
                    {
                        // because a GPX file can have several "feature classes" they are added to a group layer
                        // MapView.Active may be a 2D map or a 3D scene
                        var groupLayer = LayerFactory.Instance.CreateGroupLayer(MapView.Active.Map, 0, Path.GetFileNameWithoutExtension(item.Path));
                        foreach (var tn in pluginGpx.GetTableNames())
                        {
                            using (var table = pluginGpx.OpenTable(tn))
                            {
                                //Add feature class to the group layer
                                LayerFactory.Instance.CreateFeatureLayer((FeatureClass)table, groupLayer);
                                zoomToEnv = ((FeatureClass)table).GetExtent().Clone() as ArcGIS.Core.Geometry.Envelope;
                            }
                        }
                    }
                    if (zoomToEnv != null) MapView.Active.ZoomToAsync(zoomToEnv);
                }
                catch
                {
                    // If there is an exception loading the GPX file, The exception comes the plugin loader and isn't helpful.
                    MessageBox.Show($"{item.Name} is not a valid GPX file.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
        }
    }
}
