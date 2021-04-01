using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
    class GpxFeatureClass
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public esriGeometryType GeometryType { get; set; }
        public IFields Fields { get; set; }
        public IGeometry WorkingGeometry { get; set; }

        public static readonly GpxFeatureClass[] Defaults = {
            new GpxFeatureClass {Name = "Waypoints", Path = "wpt", GeometryType = esriGeometryType.esriGeometryPoint},
            new GpxFeatureClass {Name = "RoutePoints", Path = "rtept", GeometryType = esriGeometryType.esriGeometryPoint},
            new GpxFeatureClass {Name = "TrackPoints", Path = "trkpt", GeometryType = esriGeometryType.esriGeometryPoint},
            new GpxFeatureClass {Name = "Routes", Path = "rte", GeometryType = esriGeometryType.esriGeometryPolyline},
            new GpxFeatureClass {Name = "Tracks", Path = "trk", GeometryType = esriGeometryType.esriGeometryPolyline},
            new GpxFeatureClass {Name = "ClosedRoutes", Path = "rte", GeometryType = esriGeometryType.esriGeometryPolygon},
            new GpxFeatureClass {Name = "ClosedTracks", Path = "trk", GeometryType = esriGeometryType.esriGeometryPolygon}
        };
    }
}
