using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpxPluginPro
{
    class GpxFeatureClass
    {
        public Envelope Extent { get; set; }
        public Collection<PluginField> Fields { get; set; }
        public GeometryType GeometryType { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public Collection<Collection<object>> Rows { get; set; }

        public static readonly GpxFeatureClass[] Defaults = {
            new GpxFeatureClass {Name = "Waypoints", Path = "wpt", GeometryType = GeometryType.Point},
            new GpxFeatureClass {Name = "RoutePoints", Path = "rtept", GeometryType = GeometryType.Point},
            new GpxFeatureClass {Name = "TrackPoints", Path = "trkpt", GeometryType = GeometryType.Point},
            new GpxFeatureClass {Name = "Routes", Path = "rte", GeometryType = GeometryType.Polyline},
            new GpxFeatureClass {Name = "Tracks", Path = "trk", GeometryType = GeometryType.Polyline},
            new GpxFeatureClass {Name = "ClosedRoutes", Path = "rte", GeometryType = GeometryType.Polygon},
            new GpxFeatureClass {Name = "ClosedTracks", Path = "trk", GeometryType = GeometryType.Polygon}
        };
    }

}
