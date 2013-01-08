// Based on ESRI sample code for Simple Point Plugin

using System;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.ADF.CATIDs;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("076B45C4-7E51-401C-B50B-68D1CCE8DDA9")]
    [ProgId("GpxPlugin.GpxFactoryHelper")]
    [ComVisible(true)]
    public sealed class GpxWorkspaceFactory : IPlugInWorkspaceFactoryHelper
    {
        #region Component (COM) Category Registration

        [ComRegisterFunction]
        public static void RegisterFunction(String regKey)
        {
            PlugInWorkspaceFactoryHelpers.Register(regKey);
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(String regKey)
        {
            PlugInWorkspaceFactoryHelpers.Unregister(regKey);
        }
        #endregion

        private const string Extension = ".gpx";
        private const string MetadataExtension = ".gpx.xml";

        #region Required members for implementing IPlugInWorkspaceFactoryHelper

        public string get_DatasetDescription(esriDatasetType datasetType)
        {
            return datasetType == esriDatasetType.esriDTFeatureDataset ? "Gpx Feature Dataset" : null;
        }

        public string get_WorkspaceDescription(bool plural)
        {
            return plural ? "GPX (Gps Exchange) Files" :
                            "GPX (Gps Exchange) File";
        }

        public bool CanSupportSQL
        {
            get { return false; }
        }

        public string DataSourceName
        {
            get { return "GpxPlugin"; }
        }

        public bool ContainsWorkspace(string parentDirectory, IFileNames fileNames)
        {
            if (fileNames == null)
                return IsWorkspace(parentDirectory);

            if (!System.IO.Directory.Exists(parentDirectory))
                return false;

            string fileName;
            while ((fileName = fileNames.Next()) != null)
            {
                if (fileNames.IsDirectory())
                    continue;

                var ext = System.IO.Path.GetExtension(fileName);
                if (ext != null && ext.Equals(Extension))
                    return true;
            }

            return false;
        }

        public UID WorkspaceFactoryTypeID
        {
            get { return new UIDClass { Value = "{0E9FECE1-C40C-44B1-9257-4F632257F340}" }; }
        }

        public bool IsWorkspace(string path)
        {
            //eventhough any folder can be a gpx workspace, only return true if the folder has gpx files
            return (System.IO.Directory.Exists(path) &&
                    System.IO.Directory.GetFiles(path, "*" + Extension).Length > 0);
        }

        public esriWorkspaceType WorkspaceType
        {
            get { return esriWorkspaceType.esriFileSystemWorkspace; }
        }

        public IPlugInWorkspaceHelper OpenWorkspace(string path)
        {
            //Any valid folder path can be a Gpx Workspace
            return System.IO.Directory.Exists(path) ? new GpxWorkspace(path) : null;
        }

        public string GetWorkspaceString(string parentDirectory, IFileNames fileNames)
        {
            if (!System.IO.Directory.Exists(parentDirectory))
                return null;

            if (fileNames == null)
                return parentDirectory;

            //claim file names by removing them from the list
            string fileName;
            bool fileFound = false;
            while ((fileName = fileNames.Next()) != null)
            {
                if (fileNames.IsDirectory())
                    continue;

                var ext = System.IO.Path.GetExtension(fileName);
                if (ext != null && ext.Equals(Extension) ||
                    fileName.EndsWith(MetadataExtension,StringComparison.InvariantCultureIgnoreCase))
                {
                    fileFound = true;
                    fileNames.Remove();
                }
            }

            return fileFound ? parentDirectory : null;
        }

        #endregion

    }
}
