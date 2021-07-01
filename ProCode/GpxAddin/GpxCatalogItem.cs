using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
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
                var largeImg = new BitmapImage(new Uri(@"pack://application:,,,/GpxAddin;component/Images/BexDog32.png"));
                return largeImg;
            }
        }

        public override Task<ImageSource> SmallImage
        {
            get
            {
                var smallImage = new BitmapImage(new Uri(@"pack://application:,,,/GpxAddin;component/Images/BexDog16.png"));
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
            MessageBox.Show($"Selected Custom Item: {item.Name}");
        }
    }
}
