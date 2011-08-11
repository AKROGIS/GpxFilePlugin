using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
        class FeatureType
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public esriGeometryType Type { get; set; }
            public IFields Fields  { get; set; }

            public static readonly FeatureType[] Defaults = {
	                                   new FeatureType {Name = "Waypoints", Path = "wpt", Type = esriGeometryType.esriGeometryPoint},
	                                   new FeatureType {Name = "RoutePoints", Path = "rtept", Type = esriGeometryType.esriGeometryPoint},
	                                   new FeatureType {Name = "TrackPoints", Path = "trkpt", Type = esriGeometryType.esriGeometryPoint},
	                                   new FeatureType {Name = "Routes", Path = "rte", Type = esriGeometryType.esriGeometryPolyline},
	                                   new FeatureType {Name = "Tracks", Path = "trk", Type = esriGeometryType.esriGeometryPolyline},
	                                   new FeatureType {Name = "ClosedRoutes", Path = "rte", Type = esriGeometryType.esriGeometryPolygon},
	                                   new FeatureType {Name = "ClosedTracks", Path = "trk", Type = esriGeometryType.esriGeometryPolygon}
	                               };
        }



}
