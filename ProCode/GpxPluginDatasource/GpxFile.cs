using ArcGIS.Core.Data;
using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace GpxPluginPro
{
    class GpxFile
    {
        private static readonly string _gpxExtension = ".gpx";
        private readonly Uri _path;
        private XElement _xmlRoot;
        private XNamespace _xmlNamespace;

        internal static bool HasCorrectExtension(Uri uri)
        {
            var builder = new UriBuilder(uri);
            return builder.Path.EndsWith(_gpxExtension, StringComparison.InvariantCultureIgnoreCase);
        }

        internal GpxFile(Uri path)
        {
            if (path == null) { throw new ArgumentNullException("path"); }
            _path = path;
        }

        internal Dictionary<string, GpxFeatureClass> GetFeatureClasses()
        {
            Load();
            var featureClasses = GpxFeatureClass.Defaults.Where(t => _xmlRoot.Descendants(_xmlNamespace + t.Path).FirstOrDefault() != null);
            var tables = new Dictionary<string, GpxFeatureClass>();
            foreach (var featureClass in featureClasses)
            {
                tables[featureClass.Name] = Fix(featureClass);
            }
            return tables;
        }

        private void Load()
        {
            //define the namespace 'gpxx:' in order to read old DNR Garmin files which use this undeclared namespace

            //TODO - loop on XmlExceptions, adding any other undeclared namespaces
            var nt = new NameTable();
            var nsmgr = new XmlNamespaceManager(nt);
            nsmgr.AddNamespace("gpxx", "urn:ignore");
            var pc = new XmlParserContext(null, nsmgr, null, XmlSpace.None);
            // Will work with URI of file://, http:// and https:// and ftp://
            using (var response = WebRequest.Create(_path).GetResponse())
            using (var stream = response.GetResponseStream())
            {
                var tr = new XmlTextReader(stream, XmlNodeType.Document, pc);
                _xmlRoot = XElement.Load(tr);
                System.Diagnostics.Trace.TraceInformation("Loaded file " + _path);
            }
            _xmlNamespace = _xmlRoot.GetDefaultNamespace();
            if (!IsValidGpxFile)
                throw new InvalidOperationException("Bad GPX file contents");
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

        private GpxFeatureClass Fix(GpxFeatureClass featureClass)
        {
            //TODO: Define Rows
            featureClass.Extent = GetBounds();
            featureClass.Fields = BuildFields(featureClass);
            // Fields must be set first in order to ensure Row's values match the field list.
            featureClass.Rows = GetRows(featureClass);
            return featureClass;
        }

        #region Extents

        //FIXME: HasZ, hasM is determined by the extent envelope.  Current implementation does not allow Z (GPX has no M or ID)
        //Use Builder with two MapPoints with a Z value defined
        //MapPoint pt = MapPointBuilder.CreateMapPoint(x,y,z,sr);
        private Envelope GetBounds()
        {
            //A GPX 1.0 has a single optional bounds tag
            //A GPX 1.1 file has a single optional metadata/bounds tag
            if (_bounds == null)
            {
                XElement boundsElement = _xmlRoot.Descendants(_xmlNamespace + "bounds").FirstOrDefault();
                _bounds = (boundsElement != null)
                    ? BoundsFromMetadata(boundsElement)
                    : BoundsFromFileScan();
            }
            return _bounds;
        }
        // Cache the results, because it will be the same for all feature classes (one per file)
        // TODO: Consider ignoringing the metadata and always doing a file scan.
        // TODO: Limit the file scan to just the elments for a specific feature class.
        private Envelope _bounds;

        private Envelope BoundsFromMetadata(XElement boundsElement)
        {
            double? xmin = GetSafeDoubleAttribute(boundsElement, "minlon");
            double? xmax = GetSafeDoubleAttribute(boundsElement, "maxlon");
            double? ymin = GetSafeDoubleAttribute(boundsElement, "minlat");
            double? ymax = GetSafeDoubleAttribute(boundsElement, "maxlat");

            if (!xmin.HasValue || !xmax.HasValue || !ymin.HasValue || !ymax.HasValue)
                return null;
            if (xmin < -180 || ymin < -90 || xmax > 180 || ymax > 90 || xmin > xmax || ymin > ymax)
                return null;
            return EnvelopeBuilder.CreateEnvelope((double)xmin, (double)ymin, (double)xmax, (double)ymax, SpatialReferences.WGS84);

        }

        private Envelope BoundsFromFileScan()
        {
            double xmin = 180.0,
                   ymin = 90.0,
                   xmax = -180.0,
                   ymax = -90.0;

            var ptNodes = _xmlRoot.Descendants().Where(e => e.Name == _xmlNamespace + "wpt" || e.Name == _xmlNamespace + "trkpt" || e.Name == _xmlNamespace + "rtept");
            var coords = from e in ptNodes
                         select new { X = GetSafeDoubleAttribute(e, "lon"), Y = GetSafeDoubleAttribute(e, "lat") };

            foreach (var coord in coords.Where(
                //Ignore pair if either is invalid; i.e. dont process a valid X if the Y is invalid. 
                coord => (coord.X.HasValue && coord.X.Value >= -180.0 && coord.X.Value <= 180.0) &&
                          coord.Y.HasValue && coord.Y.Value >= -90.0 && coord.Y.Value <= 90.0))
            {
                if (coord.X.Value < xmin) xmin = coord.X.Value;
                if (coord.X.Value > xmax) xmax = coord.X.Value;
                if (coord.Y.Value < ymin) ymin = coord.Y.Value;
                if (coord.Y.Value > ymax) ymax = coord.Y.Value;
            }
            // In case we did not see any valid values
            if (xmax < xmin)
            {
                xmin = -180.0; xmax = 180.0;
            }
            if (ymax < ymin)
            {
                ymin = -90.0; ymax = 90.0;
            }
            return EnvelopeBuilder.CreateEnvelope(xmin, ymin, xmax, ymax, SpatialReferences.WGS84);
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

        #endregion

        #region Fields

        //This is the full GPX 1.1 schema.
        //Option 1 - Add all schema defined fields, even if they are not used (return null for unused fields)
        //TODO - Option 2 - Scan file and only use fields used in this file.
        //var search = Root.Descendants(_ns + type.Path);

        //IMPORTANT: Other code in this plugin assume that the OID is field 0, and the Shape is field 1 (ArcGIS doesn't care about the ordering)
        //IMPORTANT: the field.Name (Except OID and SHAPE) must be the exact same as the XML element name to look up the correct attribute value 

        private Collection<PluginField> BuildFields(GpxFeatureClass featureClass)
        {
            var fields = new Collection<PluginField>();

            //ObjectID
            fields.Add(new PluginField
            {
                Name = "ObjectID",
                AliasName = "Object ID",
                FieldType = FieldType.OID
            });
            //Geometry
            fields.Add(new PluginField
            {
                Name = "SHAPE",
                AliasName = "Shape",
                FieldType = FieldType.Geometry
            });
            //Common Fields
            fields.Add(new PluginField
            {
                Name = "name",
                AliasName = "Name",
                FieldType = FieldType.String
            });
            fields.Add(new PluginField
            {
                Name = "cmt",
                AliasName = "Comment",
                FieldType = FieldType.String
            });
            fields.Add(new PluginField
            {
                Name = "desc",
                AliasName = "Description",
                FieldType = FieldType.String
            });
            fields.Add(new PluginField
            {
                Name = "src",
                AliasName = "Source",
                FieldType = FieldType.String
            });
            fields.Add(new PluginField
            {
                Name = "link",
                AliasName = "Hyperlink",
                //field.FieldType = FieldType.XML;
                //XML datatype is not fully supported in Desktop (i.e. no value is displayed, and export to shapefile will fail)
                //TODO: Test XML data type in Pro
                FieldType = FieldType.String
            });
            //GPX 1.0
            fields.Add(new PluginField
            {
                Name = "url",
                AliasName = "Url link",
                FieldType = FieldType.String
            });
            //GPX 1.0
            fields.Add(new PluginField
            {
                Name = "urlname",
                AliasName = "Url Name",
                FieldType = FieldType.String
            });
            fields.Add(new PluginField
            {
                Name = "type",
                AliasName = "Type",
                FieldType = FieldType.String
            });
            fields.Add(new PluginField
            {
                Name = "extensions",
                AliasName = "XML Extensions",
                //field.FieldType = FieldType.XML;
                //XML datatype is not fully supported in Desktop (i.e. no value is displayed, and export to shapefile will fail)
                //TODO: Test XML data type in Pro
                FieldType = FieldType.String
            });

            //Only for Routes and Tracks
            if (featureClass.Path == "rte" || featureClass.Path == "trk")
            {
                fields.Add(new PluginField
                {
                    Name = "number",
                    AliasName = featureClass.Path == "rte" ? "Route Number" : "Track Number",
                    FieldType = FieldType.Integer
                });
            }

            //Only for Points
            if (featureClass.Path == "wpt" || featureClass.Path == "trkpt" || featureClass.Path == "rtept")
            {
                fields.Add(new PluginField
                {
                    Name = "ele",
                    AliasName = "Elevation",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "time",
                    AliasName = "Time (UTC)",
                    FieldType = FieldType.Date
                });
                fields.Add(new PluginField
                {
                    Name = "magvar",
                    AliasName = "Magnetic Variation",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "geoidheight",
                    AliasName = "GeoID Height",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "sym",
                    AliasName = "Symbol",
                    FieldType = FieldType.String
                });
                fields.Add(new PluginField
                {
                    Name = "fix",
                    AliasName = "GpsFix",
                    FieldType = FieldType.String
                });
                fields.Add(new PluginField
                {
                    Name = "sat",
                    AliasName = "Satellites",
                    FieldType = FieldType.Integer
                });
                fields.Add(new PluginField
                {
                    Name = "hdop",
                    AliasName = "HDOP",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "vdop",
                    AliasName = "VDOP",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "pdop",
                    AliasName = "PDOP",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "ageofdgpsdata",
                    AliasName = "ageofdgps Data",
                    FieldType = FieldType.Double
                });
                fields.Add(new PluginField
                {
                    Name = "dgpsid",
                    AliasName = "DGPS ID",
                    FieldType = FieldType.Integer
                });
            }
            return fields;
        }

        #endregion

        #region Rows

        private Collection<Collection<object>> GetRows(GpxFeatureClass featureClass)
        {
            var rows = new Collection<Collection<object>>();
            var records = _xmlRoot.Descendants().Where(e => e.Name == _xmlNamespace + featureClass.Path);
            var oid = 1;
            foreach (var record in records)
            {
                rows.Add(GetValues(oid, record, featureClass));
                oid += 1;
            }
            return rows;
        }

        private Collection<object> GetValues(int oid, XElement row, GpxFeatureClass featureClass)
        {
            var values = new Collection<object>();
            foreach (var field in featureClass.Fields)
            {
                switch (field.FieldType)
                {
                    case FieldType.OID:
                        values.Add(oid);
                        break;
                    case FieldType.Geometry:
                        values.Add(GetGeometry(row, featureClass));
                        break;
                    case FieldType.Integer:
                        values.Add(GetSafeIntElement(row, field.Name));
                        break;
                    case FieldType.Double:
                        values.Add(GetSafeDoubleElement(row, field.Name));
                        break;
                    case FieldType.String:
                    case FieldType.XML:
                        values.Add(GetStringElement(row, field.Name));
                        break;
                    case FieldType.Date:
                        values.Add(GetSafeDateTimeElement(row, field.Name));
                        break;
                    case FieldType.Single:
                    case FieldType.Blob:
                    case FieldType.SmallInteger:
                    case FieldType.Raster:
                    case FieldType.GUID:
                    case FieldType.GlobalID:
                    default:
                        throw new NotImplementedException();
                }
            }
            return values;
        }

        private Geometry GetGeometry(XElement element, GpxFeatureClass featureClass)
        {
            switch (featureClass.GeometryType)
            {
                case GeometryType.Point:
                    return BuildPoint(element);
                case GeometryType.Polyline:
                    return BuildPolyline(element);
                case GeometryType.Polygon:
                    return BuildPolygon(element);
                case GeometryType.Unknown:
                case GeometryType.Envelope:
                case GeometryType.Multipoint:
                case GeometryType.Multipatch:
                case GeometryType.GeometryBag:
                default:
                    throw new NotImplementedException();
            }
        }

        private static string GetStringElement(XElement element, string name)
        {
            var v = element.GetElement(name);
            if (v == null) { return null; }
            //FIXME - omit the surrounding <extension> tag; only provide the child elements
            if (name == "extensions") { return v.ToString(); }
            if (name == "link") { return (string)v.Attribute("href"); }
            return v.Value;
        }

        private static double? GetSafeDoubleElement(XElement element, string name)
        {
            try
            {
                return (double?)element.GetElement(name);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static int? GetSafeIntElement(XElement element, string name)
        {
            try
            {
                return (int?)element.GetElement(name);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static DateTime? GetSafeDateTimeElement(XElement element, string name)
        {
            try
            {
                return (DateTime?)element.GetElement(name);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        #region Geometry construction

        private Polygon BuildPolygon(XElement xElement)
        {
            if (xElement.Name.LocalName == "rte")
                return PolygonBuilder.CreatePolygon(BuildSegment("rtept", xElement));
            if (xElement.Name.LocalName == "trk")
            {
                var polygons = BuildTracks(xElement).Select(p => PolygonBuilder.CreatePolygon(p));
                return PolygonBuilder.CreatePolygon(polygons);
            }
            return null;
        }

        private Polyline BuildPolyline(XElement xElement)
        {
            if (xElement.Name.LocalName == "rte")
                return BuildSegment("rtept", xElement);
            if (xElement.Name.LocalName == "trk")
                return PolylineBuilder.CreatePolyline(BuildTracks(xElement));
            return null;
        }

        private Collection<Polyline> BuildTracks(XElement xElement)
        {
            var tracks = new Collection<Polyline>();
            foreach (var ele in xElement.GetElements("trkseg"))
            {
                tracks.Add(BuildSegment("trkpt", ele));
            }
            return tracks;
        }

        private Polyline BuildSegment(string pointName, XElement xElement)
        {
            var points = new Collection<MapPoint>();
            foreach (var ele in xElement.GetElements(pointName))
            {
                points.Add(BuildPoint(ele));
            }
            return PolylineBuilder.CreatePolyline(points);
        }

        private MapPoint BuildPoint(XElement ele)
        {
            double? x = GetSafeDoubleAttribute(ele, "lon");
            double? y = GetSafeDoubleAttribute(ele, "lat");
            double? z = GetSafeDoubleElement(ele, "ele");
            if (!x.HasValue || y.HasValue)
            {
                MapPointBuilder.CreateMapPoint(SpatialReferences.WGS84);
            }
            if (!z.HasValue)
            {
                MapPointBuilder.CreateMapPoint(x.Value, y.Value, SpatialReferences.WGS84);
            }
            return MapPointBuilder.CreateMapPoint(x.Value, y.Value, z.Value, SpatialReferences.WGS84);
        }

        #endregion

        #endregion
    }

    static class XmlToLinqExtensions
    {
        public static XElement GetElement(this XElement element, string name)
        {
            return element.Element(element.GetDefaultNamespace() + name);
        }

        public static IEnumerable<XElement> GetElements(this XElement element, string name)
        {
            return element.Elements(element.GetDefaultNamespace() + name);
        }
    }

}
