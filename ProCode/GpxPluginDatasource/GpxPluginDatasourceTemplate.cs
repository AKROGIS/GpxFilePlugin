using ArcGIS.Core.Data;
using ArcGIS.Core.Data.PluginDatastore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GpxPluginPro
{
    public class GpxPluginDatasourceTemplate : PluginDatasourceTemplate
    {

        private Dictionary<string, GpxFeatureClass> _tables = null;

        public override string GetDatasourceDescription(bool inPluralForm)
        {
            return inPluralForm ? "GPX (GPS Exchange) Files" : "GPX (GPS Exchange) File";
        }

        public override string GetDatasetDescription(DatasetType datasetType)
        {
            return datasetType == DatasetType.FeatureClass ? "GPX Feature Class" : null;
        }

        public override bool IsQueryLanguageSupported()
        {
            return false;
        }

        public override bool CanOpen(Uri connectionPath)
        {
            return GpxFile.HasCorrectExtension(connectionPath);
        }

        public override void Open(Uri connectionPath)
        {
            _tables = (new GpxFile(connectionPath)).GetFeatureClasses();
        }

        public override void Close()
        {
            _tables = null;
        }

        public override IReadOnlyList<string> GetTableNames()
        {
            return _tables.Keys.ToArray();
        }

        public override PluginTableTemplate OpenTable(string name)
        {
            
            return new GpxPluginTableTemplate(_tables[name]);
        }

    }
}
