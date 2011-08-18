using System;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
    internal class GpxDataset : IPlugInDatasetHelper, IPlugInDatasetInfo, IPlugInFileOperations
    {
        private const string Extension = ".gpx";
        private const string XmlExtension = ".xml";
        private readonly Gpx _gpx;

        public GpxDataset(string workspace, string dataset)
        {
            _gpx = new Gpx(System.IO.Path.Combine(workspace, dataset + Extension));
        }



        #region IPlugInDatasetHelper Members

        public IEnvelope Bounds
        {
            get { return _gpx.Bounds; }
        }

        public int get_OIDFieldIndex(int classIndex)
        {
            return 0;
        }

        public int get_ShapeFieldIndex(int classIndex)
        {
            return 1;
        }

        public IFields get_Fields(int classIndex)
        {
            return _gpx.GetFields(classIndex);
        }

        public int ClassCount
        {
            get { return _gpx.FeatureClasses.Length; }
        }

        public string get_ClassName(int classIndex)
        {
            if (classIndex < 0 || ClassCount <= classIndex)
                return "Undefined";
            return _gpx.FeatureClasses[classIndex].Name;
        }

        public int get_ClassIndex(string name)
        {
            for (int i = 0; i < _gpx.FeatureClasses.Length; i++)
                if (name == _gpx.FeatureClasses[i].Name)
                    return i;
            return -1;
        }

        public IPlugInCursorHelper FetchAll(int classIndex, string whereClause, object fieldMap)
        {
            //The where clause will always be null (since IPlugInWorkspaceFactoryHelper::CanSupportSQL returns false); so we ignore it.
            return new GpxCursor(_gpx, classIndex, -1, null, (int[])fieldMap);
        }

        public IPlugInCursorHelper FetchByID(int classIndex, int id, object fieldMap)
        {
            return new GpxCursor(_gpx, classIndex, id, null, (int[])fieldMap);
        }

        public IPlugInCursorHelper FetchByEnvelope(int classIndex, IEnvelope env, bool strictSearch, string whereClause, object fieldMap)
        {
            //Data sources that don't use indexes or blocks may ignore strictSearch
            return new GpxCursor(_gpx, classIndex, -1, env, (int[])fieldMap);
        }

        #endregion




        #region IPlugInDatasetInfo Members

        /// <summary>
        /// Returns the dataset type of this dataset. Determines what kind of icon the dataset will have in ArcCatalog
        /// </summary>
        public esriDatasetType DatasetType
        {
            get { return esriDatasetType.esriDTFeatureDataset; }
        }

        /// <summary>
        /// Returns the geometry type of this dataset.
        /// Determines which feature class icon the dataset will have in ArcCatalog.
        /// Only called if the dataset type is feature class. Therefore it is never called for this plugin.
        /// </summary>
        public esriGeometryType GeometryType
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The name of the dataset within the workspace.
        /// This is the name that will show up in ArcCatalog, and that will be passed to the OpenDataset method of the workspace.
        /// </summary>
        public string LocalDatasetName
        {
            get { return _gpx.Name; }
        }

        /// <summary>
        /// The name of the dataset's shape field.
        /// Only called if dataset type is feature class and geometry type is not null.
        /// Used to construct the FeatureClassName.
        /// </summary>
        public string ShapeFieldName
        {
            get { return "Shape"; }
        }

        #endregion




        #region IPlugInFileOperations Members

        public bool CanCopy()
        {
            return true;
        }

        public bool CanDelete()
        {
            return true;
        }

        public bool CanRename()
        {
            return true;
        }

        public void Copy(string copyName, IWorkspace copyWorkspace)
        {
            string newPath = System.IO.Path.Combine(copyWorkspace.PathName, copyName + Extension);
            System.IO.File.Copy(_gpx.Path, newPath);
            try
            {
                System.IO.File.Copy(_gpx.Path + XmlExtension, newPath + XmlExtension);
            }
            catch (System.IO.FileNotFoundException) {}
        }

        public void Delete()
        {
            System.IO.File.Delete(_gpx.Path);
            System.IO.File.Delete(_gpx.Path + XmlExtension);  //does not throw if file does not exist
            //if caller tries to use this datasource now it will throw
        }

        public string Rename(string name)
        {
            string newPath = _gpx.Path.Replace(_gpx.Name, name);
            System.IO.File.Move(_gpx.Path, newPath);
            try
            {
                System.IO.File.Move(_gpx.Path + XmlExtension, newPath + XmlExtension);
            }
            catch (System.IO.FileNotFoundException) { }
            // _gpx now references a non-existant file.
            // OK, because this dataset is never used again.
            return name;
        }

        #endregion

    }
}
