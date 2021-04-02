using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
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
            //TODO: Define Fields, Rows, and Extent
            featureClass.Extent = GetBounds();
            return featureClass;
        }

        private Envelope GetBounds()
        {
            //A GPX 1.0 has a single optional bounds tag
            //A GPX 1.1 file has a single optional metadata/bounds tag
            XElement boundsElement = _xmlRoot.Descendants(_xmlNamespace + "bounds").FirstOrDefault();
            return (boundsElement != null)
                ? BoundsFromMetadata(boundsElement)
                : BoundsFromFileScan();
        }

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
            double xmin = double.MaxValue,
                   ymin = double.MaxValue,
                   xmax = double.MinValue,
                   ymax = double.MinValue;

            var ptNodes = _xmlRoot.Descendants().Where(e => e.Name == _xmlNamespace + "wpt" || e.Name == _xmlNamespace + "trkpt" || e.Name == _xmlNamespace + "rtept");
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

    }
}
