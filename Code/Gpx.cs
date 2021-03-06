﻿//TODO - Create a cache indexed by OID (file is scanned (number of records)+1 times to fill data table) 
//       If we cache a file, can we efficiently create a lightweight envelope for each element to optimize IPlugInDatasetHelper::FetchByEnvelope
//       How will a cache mess with the dynamic nature of datasources (i.e. file should be recanned on screen redraws)


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
    //This class stores a pathname to a Gpx file.
    //It reads the file at the path lazily and only once.
    //If it fails for any reason, or the file is invalid,
    //it acts as through the file is empty.  No exception is thrown. 
    //If the file appears empty, but you want to try again,
    //then create a new object with the same path.
    class Gpx
    {
        private readonly string _path;
        private bool _loaded;
        private XElement _xmlRoot;
        private XNamespace _xmlNamespace;

        internal Gpx(string path)
        {
            _path = path;
        }

        internal string Name
        {
            get { return System.IO.Path.GetFileNameWithoutExtension(_path); }
        }

        internal string Path
        {
            get { return _path; }
        }

        // This is the intended access point to the data in the file
        // always check for null before using
        private XElement Root
        {
            get
            {
                if (!_loaded)
                    Load();
                return _xmlRoot;
            }
        }

        //Load should only be called once.  
        private void Load()
        {
            try
            {
#if NOHACK 
                //_xmlRoot = XElement.Load(_path);
                //System.Diagnostics.Trace.TraceInformation("Loaded file " + _path);

#else //define the namespace 'gpxx:' in order to read old DNR Garmin files which use this undeclared namespace

                //TODO - loop on XmlExceptions, adding any other undeclared namespaces
                var nt = new NameTable();
                var nsmgr = new XmlNamespaceManager(nt);
                nsmgr.AddNamespace("gpxx", "urn:ignore");
                var pc = new XmlParserContext(null, nsmgr, null, XmlSpace.None);
                using (var stream = new System.IO.FileStream(_path, System.IO.FileMode.Open))
                {
                    var tr = new XmlTextReader(stream, XmlNodeType.Document, pc);
                    _xmlRoot = XElement.Load(tr);
                    System.Diagnostics.Trace.TraceInformation("Loaded file " + _path);
                }
#endif

                _xmlNamespace = _xmlRoot.GetDefaultNamespace();
                if (!IsValidGpxFile)
                    _xmlRoot = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                _xmlRoot = null;
            }
            _loaded = true; //if we failed, do not keep trying.
        }

        private bool IsValidGpxFile
        {
            get
            {
                const string gpxRootElement = "gpx";
                const string gpx10 = "http://www.topografix.com/GPX/1/0";
                const string gpx11 = "http://www.topografix.com/GPX/1/1";

                return _xmlRoot.Name.LocalName == gpxRootElement &&
                       (_xmlNamespace.NamespaceName == gpx10 ||
                        _xmlNamespace.NamespaceName == gpx11);
            }
        }


        internal IEnumerator<XElement> GetEnumerator(GpxFeatureClass type)
        {
            return Root.Descendants().Where(e => e.Name == _xmlNamespace + type.Path).GetEnumerator();
        }

        // A feature dataset has only one spatial reference, not one for each feature class
        private ISpatialReference SpatialReference
        {
            get
            {
                if (_sr == null)
                {
                    //All GPX file are in WGS84
                    ISpatialReferenceFactory3 factory = new SpatialReferenceEnvironmentClass();
                    _sr = (ISpatialReference3)factory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);
                    _sr.VerticalCoordinateSystem =
                        factory.CreateVerticalCoordinateSystem((int)esriSRVerticalCSType.esriSRVertCS_WGS1984);
                }
                return _sr;
            }
        }
        private ISpatialReference3 _sr;

        //This is an array of data for each feature class in the feature dataset
        internal GpxFeatureClass[] FeatureClasses
        {
            get
            {
                if (Root == null)
                    return new GpxFeatureClass[0];
                return _featureTypes ?? (_featureTypes = GetFeatureTypes());
            }
        }
        private GpxFeatureClass[] _featureTypes;

        private GpxFeatureClass[] GetFeatureTypes()
        {
            return GpxFeatureClass.Defaults.Where(t => Root.Descendants(_xmlNamespace + t.Path).FirstOrDefault() != null).ToArray();
        }


        // A feature dataset has only one bounding envelope (union of all feature classes), not one for each feature class
        internal IEnvelope Bounds
        {
            get
            {
                if (Root == null)
                    return null;
                return _bounds ?? (_bounds = GetBounds());
            }
        }
        private IEnvelope _bounds;

        private IEnvelope GetBounds()
        {
            //A GPX 1.0 has a single optional bounds tag
            //A GPX 1.1 file has a single optional metadata/bounds tag
            XElement boundsElement = Root.Descendants(_xmlNamespace + "bounds").FirstOrDefault();
            IEnvelope bounds = null;
            if (boundsElement != null)
                bounds = BoundsFromMetadata(boundsElement);
            return bounds ?? BoundsFromFileScan();
        }

        private IEnvelope BoundsFromMetadata(XElement boundsElement)
        {
            double? xmin = GetSafeDoubleAttribute(boundsElement, "minlon");
            double? xmax = GetSafeDoubleAttribute(boundsElement, "maxlon");
            double? ymin = GetSafeDoubleAttribute(boundsElement, "minlat");
            double? ymax = GetSafeDoubleAttribute(boundsElement, "maxlat");

            if (!xmin.HasValue || !xmax.HasValue || !ymin.HasValue || !ymax.HasValue)
                return null;
            if (xmin < -180 || ymin < -90 || xmax > 180 || ymax > 90 || xmin > xmax || ymin > ymax)
                return null;

            IEnvelope bounds = new EnvelopeClass { SpatialReference = SpatialReference };
            bounds.PutCoords((double)xmin, (double)ymin, (double)xmax, (double)ymax);
            return bounds;
        }

        private IEnvelope BoundsFromFileScan()
        {
            double xmin = double.MaxValue,
                   ymin = double.MaxValue,
                   xmax = double.MinValue,
                   ymax = double.MinValue;

            var ptNodes = Root.Descendants().Where(e => e.Name == _xmlNamespace + "wpt" || e.Name == _xmlNamespace + "trkpt" || e.Name == _xmlNamespace + "rtept");
            var coords = from e in ptNodes
                         select new { X = GetSafeDoubleAttribute(e, "lon"), Y = GetSafeDoubleAttribute(e, "lat") };

            foreach (var coord in coords.Where(coord => coord.X.HasValue && coord.Y.HasValue))
            {
                if (coord.X < xmin) xmin = coord.X.Value;
                if (coord.X > xmax) xmax = coord.X.Value;
                if (coord.Y < ymin) ymin = coord.Y.Value;
                if (coord.Y > ymax) ymax = coord.Y.Value;
            }
            if (xmin < -180 || ymin < -90 || xmax > 180 || ymax > 90 || xmin > xmax || ymin > ymax)
                return null;

            IEnvelope bounds = new EnvelopeClass { SpatialReference = SpatialReference };
            bounds.PutCoords(xmin, ymin, xmax, ymax);
            return bounds;
        }

        private static double? GetSafeDoubleAttribute(XElement element, string name)
        {
            try
            {
                return (double?)element.Attribute(name);
            }
            catch (FormatException)
            {
                return null;
            }
        }


        internal IGeometry GetWorkingGeometry(int classIndex)
        {
            if (classIndex < 0 || FeatureClasses.Length <= classIndex)
                return null;
            return FeatureClasses[classIndex].WorkingGeometry ??
                   (FeatureClasses[classIndex].WorkingGeometry = BuildWorkingGeometry(FeatureClasses[classIndex]));
        }

        private IGeometry BuildWorkingGeometry(GpxFeatureClass featureClass)
        {
            IGeometry result = null;

            if (featureClass.GeometryType == esriGeometryType.esriGeometryPoint)
                result = new PointClass();
            if (featureClass.GeometryType == esriGeometryType.esriGeometryPolyline)
                result = new PolylineClass();
            if (featureClass.GeometryType == esriGeometryType.esriGeometryPolygon)
                result = new PolygonClass();

            if (result == null)
                throw new ArgumentException("Geometry type must be Point/Polyline/Polygon", "featureClass");

            result.SpatialReference = SpatialReference;
            return result;
        }

        internal IFields GetFields(int classIndex)
        {
            if (classIndex < 0 || FeatureClasses.Length <= classIndex)
                return null;
            return FeatureClasses[classIndex].Fields ??
                   (FeatureClasses[classIndex].Fields = BuildFields(FeatureClasses[classIndex]));
        }


        //This is the full GPX 1.1 schema.
        //Option 1 - Add all schema defined fields, even if they are not used
        //TODO - Option 2 - Scan file and only use fields used in this file.
        //var search = Root.Descendants(_ns + type.Path);

        private IFields BuildFields(GpxFeatureClass featureClass)
        {
            var description = new FeatureClassDescriptionClass();
            var fields = (IFieldsEdit)description.RequiredFields;
            IFieldEdit field = new FieldClass();

            //Geometry
            var geometryIndex = fields.FindField(description.ShapeFieldName);
            var geometry = (IGeometryDefEdit)fields.Field[geometryIndex].GeometryDef;
            geometry.GeometryType_2 = featureClass.GeometryType;
            geometry.SpatialReference_2 = SpatialReference;
            geometry.HasM_2 = false;
            geometry.HasZ_2 = true;  //FIXME - this may be false.

            //Common Fields
            field.Name_2 = "name";
            field.AliasName_2 = "Name";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            // ReSharper disable UseObjectOrCollectionInitializer
            //These COM classes do not support object initializers
            field = new FieldClass();
            field.Name_2 = "cmt";
            field.AliasName_2 = "Comment";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            field = new FieldClass();
            field.Name_2 = "desc";
            field.AliasName_2 = "Description";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            field = new FieldClass();
            field.Name_2 = "src";
            field.AliasName_2 = "Source";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            field = new FieldClass();
            field.Name_2 = "link";
            field.AliasName_2 = "Hyperlink";
            //field.Type_2 = esriFieldType.esriFieldTypeXML;
            //XML datatype is not fully supported in Desktop (i.e. no value is displayed, and export to shapefile will fail)
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            //GPX 1.0
            field = new FieldClass();
            field.Name_2 = "url";
            field.AliasName_2 = "Url link";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            //GPX 1.0
            field = new FieldClass();
            field.Name_2 = "urlname";
            field.AliasName_2 = "Url Name";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            field = new FieldClass();
            field.Name_2 = "type";
            field.AliasName_2 = "Type";
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            field = new FieldClass();
            field.Name_2 = "extensions";
            //field.Type_2 = esriFieldType.esriFieldTypeXML;
            field.Type_2 = esriFieldType.esriFieldTypeString;
            fields.AddField(field);

            //Only for Routes and Tracks
            if (featureClass.Path == "rte" || featureClass.Path == "trk")
            {
                field = new FieldClass();
                field.Name_2 = "number";
                field.AliasName_2 = featureClass.Path == "rte" ? "Route Number" : "Track Number";
                field.Type_2 = esriFieldType.esriFieldTypeInteger;
                fields.AddField(field);
            }

            //Only for Points
            if (featureClass.Path == "wpt" || featureClass.Path == "trkpt" || featureClass.Path == "rtept")
            {
                field = new FieldClass();
                field.Name_2 = "ele";
                field.AliasName_2 = "Elevation";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "time";
                field.AliasName_2 = "Time (UTC)";
                field.Type_2 = esriFieldType.esriFieldTypeDate;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "magvar";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "geoidheight";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "sym";
                field.AliasName_2 = "Symbol";
                field.Type_2 = esriFieldType.esriFieldTypeString;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "fix";
                field.AliasName_2 = "GpsFix";
                field.Type_2 = esriFieldType.esriFieldTypeString;
                field.Length_2 = 4;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "sat";
                field.AliasName_2 = "Satellites";
                field.Type_2 = esriFieldType.esriFieldTypeInteger;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "hdop";
                field.AliasName_2 = "HDOP";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "vdop";
                field.AliasName_2 = "VDOP";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "pdop";
                field.AliasName_2 = "PDOP";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "ageofdgpsdata";
                field.Type_2 = esriFieldType.esriFieldTypeDouble;
                fields.AddField(field);

                field = new FieldClass();
                field.Name_2 = "dgpsid";
                field.Type_2 = esriFieldType.esriFieldTypeInteger;
                fields.AddField(field);
                // ReSharper restore UseObjectOrCollectionInitializer
            }

            return fields;
        }

        //private Dictionary<string,int> ElementList(GpxFeatureClass featureClass)
        //{
        //    var result = new Dictionary<string, int>();
        //    var elements = Root.Descendants().Where(e => e.Name == _xmlNamespace + featureClass.Path);
        //    foreach (var element in elements)
        //    {
        //        bool newElement = true;
        //        foreach (var subelement in element.Elements())
        //        {
        //            PutElementInDict(result, subelement, newElement);
        //            newElement = false;
        //        }
        //    }
        //    return result;
        //}

        //private void PutElementInDict(Dictionary<string, int> dict, XElement subelement, bool newElement)
        //{
        //    int linkCount = 0;
        //    string name = subelement.Name.LocalName;
        //    if (name == "link")
        //    {
        //        if (newElement)
        //            linkCount = 1;
        //        else
        //            linkCount++;
        //    }
        //    else if (name == "extensions")
        //    {

        //    }
        //    else
        //    {
        //        dict[name] = 1;
        //    }

        //}
    }
}
