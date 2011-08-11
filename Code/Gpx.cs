using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
    class Gpx
    {
        //private XNamespace gpx10 = "http://www.topografix.com/GPX/1/0";
        //private XNamespace gpx11 = "http://www.topografix.com/GPX/1/1";

        private const string GpxRootElement = "gpx";

        private readonly string _path;
        private bool _loaded;
        private XElement _xele;
        private XNamespace _ns;

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

        internal void Load()
        {
            try
            {
                _xele = XElement.Load(_path);
                _ns = _xele.GetDefaultNamespace();
                if (_xele.Name != _ns + GpxRootElement)
                    _xele = null;
            }
            catch (Exception)
            {
                _xele = null;
            }
            _loaded = true;
        }

        private XElement Root
        {
            get
            {
                if (!_loaded)
                    Load();
                return _xele;
            }
        }



        internal FeatureType[] FeatureTypes
        {
            get
            {
                if (Root == null)
                    return new FeatureType[0];
                return _featureTypes ?? (_featureTypes = GetFeatureTypes());
            }
        }
        private FeatureType[] _featureTypes;

        private FeatureType[] GetFeatureTypes()
        {
            return FeatureType.Defaults.Where(t => Root.Descendants(_ns + t.Path).FirstOrDefault() != null).ToArray();
        }



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
            //A GPX file has a single optional metadata/bounds tag
            XElement boundsElement = Root.Descendants(_ns + "bounds").FirstOrDefault();
            IEnvelope bounds = null;
            if (boundsElement != null)
                bounds = BoundsFromMetadata(boundsElement);
            return bounds ?? BoundsFromFileScan();
        }

        private IEnvelope BoundsFromMetadata(XElement boundsElement)
        {
            var xmin = (double?)boundsElement.Attribute("minlon");
            var xmax = (double?)boundsElement.Attribute("maxlon");
            var ymin = (double?)boundsElement.Attribute("minlat");
            var ymax = (double?)boundsElement.Attribute("maxlat");

            if (!xmin.HasValue || !xmax.HasValue || !ymin.HasValue || !ymax.HasValue)
                return null;
            if (xmin < -180 || ymin < -90 || xmax > 180 || ymax > 90 || xmin > xmax || ymin > ymax)
                return null;

            IEnvelope bounds = new EnvelopeClass { SpatialReference = SpatialReference };
            bounds.PutCoords((double)xmin,(double)ymin,(double)xmax,(double)ymax);
            return bounds;
        }

        private IEnvelope BoundsFromFileScan()
        {
            double xmin = double.MaxValue,
                   ymin = double.MaxValue,
                   xmax = double.MinValue,
                   ymax = double.MinValue;

            var ptNodes = Root.Descendants().Where(e => e.Name == _ns + "wpt" || e.Name == _ns + "trkpt" || e.Name == _ns + "rtept");
            var coords = from e in ptNodes
                         select new { X = (double?)e.Attribute("lon"), Y = (double?)e.Attribute("lat") };

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


        private ISpatialReference SpatialReference
        {
            get
            {
                if (_sr == null)
                {
                    //All GPX file are in WGS84
                    ISpatialReferenceFactory2 factory = new SpatialReferenceEnvironmentClass();
                    _sr = factory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);
                }
                return _sr;
            }
        }
        private ISpatialReference _sr;



        internal IFields GetFields(int classIndex)
        {
            if (classIndex < 0 || FeatureTypes.Length <= classIndex)
                return null;
            if (FeatureTypes[classIndex].Fields == null)
                FeatureTypes[classIndex].Fields = BuildField(FeatureTypes[classIndex]);
            return FeatureTypes[classIndex].Fields;
        }

        private IFields BuildField(FeatureType type)
        {
            var search = Root.Descendants(_ns + type.Path);

            IFieldEdit fieldEdit;
            IFields fields;
            IFieldsEdit fieldsEdit;
            IObjectClassDescription fcDesc;
            fcDesc = new FeatureClassDescriptionClass();

            fields = fcDesc.RequiredFields;
            fieldsEdit = (IFieldsEdit)fields;

            fieldEdit = new FieldClass();
            fieldEdit.Length_2 = 1;
            fieldEdit.Name_2 = "ColumnOne";
            fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            fieldsEdit.AddField((IField)fieldEdit);

            //HIGHLIGHT: Add extra int column
            fieldEdit = new FieldClass();
            fieldEdit.Name_2 = "Extra";
            fieldEdit.Type_2 = esriFieldType.esriFieldTypeInteger;
            fieldsEdit.AddField((IField)fieldEdit);

            IField field = fields.get_Field(fields.FindField("Shape"));
            fieldEdit = (IFieldEdit)field;
            IGeometryDefEdit geomDefEdit = (IGeometryDefEdit)field.GeometryDef;
            geomDefEdit.GeometryType_2 = type.Type;
            ISpatialReference shapeSRef = this.SpatialReference;

            //M/Z
            geomDefEdit.HasM_2 = false;
            geomDefEdit.HasZ_2 = false;

            geomDefEdit.SpatialReference_2 = shapeSRef;

            return fields;
        }
    }
}
