using System;
using System.IO;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
    /// <summary>
    /// GpxWorkspace is a filesystem folder (usually with *.gpx files within).
    /// </summary>
    internal class GpxWorkspace : IPlugInWorkspaceHelper, IPlugInMetadataPath
    {
        private const string GpxFilter = "*.gpx";
        private const string MetadataExtension = ".gpx.xml";

        private readonly string _workspacePath;

        // The constructor should only be called by the Factory's OpenWorkspace() method
        // which verified that the path was valid just before the workspace was created.
        // since this defines the workspace, we have no choice but to assume it is valid.
        internal GpxWorkspace(string workspacePath)
        {
            _workspacePath = workspacePath;
        }

        #region IPlugInWorkspaceHelper Members

        public bool OIDIsRecordNumber
        {
            get { return true; }
        }


        public IArray get_DatasetNames(esriDatasetType datasetType)
        {
            IArray datasets = new ArrayClass();

            if (datasetType == esriDatasetType.esriDTAny || datasetType != esriDatasetType.esriDTFeatureDataset)
            {
                // Since the state of the filesystem is beyond our control, 
                // GetFiles() could throw.  If it does, we return an empty array.
                try
                {
                    foreach (var file in Directory.GetFiles(_workspacePath, GpxFilter))
                    {
                        string localName = Path.GetFileNameWithoutExtension(file);
                        if (!String.IsNullOrEmpty(localName))
                        {
                            datasets.Add(new GpxDataset(_workspacePath, localName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is UnauthorizedAccessException)
                        System.Diagnostics.Trace.TraceError(ex.Message);
                    else
                        throw;
                }
            }
            return datasets;
        }

        //I can't cache the datasets I create in get_DatasetNames for use in OpenDataset,
        //because ArcGIS will create one workspace for getting the names,
        //and then another for opening the dataset.  Bizare.
        //Fixing this issue by having the Factory cache the workspaces, and only calling
        //new when the path is different, doesn't work, because there is no way to know
        //when the user is doing a 'refresh', and wants the objects to be recreated.

        public IPlugInDatasetHelper OpenDataset(string localName)
        {
            return new GpxDataset(_workspacePath, localName);
        }

        public INativeType get_NativeType(esriDatasetType datasetType, string localName)
        {
            return null;
        }

        public bool RowCountIsCalculated
        {
            get { return true; }
        }

        #endregion

        #region IPlugInMetadataPath Members
        //Must implement IPlugInMetadataPath so export data in arcmap works correctly

        public string get_MetadataPath(string localName)
        {
            //caller is responsible for checking for IO exceptions during use.
            return Path.Combine(_workspacePath, localName + MetadataExtension);
        }

        #endregion
    }
}
