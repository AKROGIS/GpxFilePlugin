using ArcGIS.Core.Data;
using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpxPluginPro
{
    public class GpxPluginTableTemplate : PluginTableTemplate
    {
        private readonly GpxFeatureClass _featureClass;
        internal GpxPluginTableTemplate(GpxFeatureClass featureClass)
        {
            if (featureClass == null)
                throw new ArgumentNullException("featureClass"); 
            _featureClass = featureClass;
        }

        public override Envelope GetExtent()
        {
            return _featureClass.Extent;
        }
        public override IReadOnlyList<PluginField> GetFields()
        {
            return _featureClass.Fields;
        }

        public override string GetName()
        {
            return _featureClass.Name;
        }

        public override int GetNativeRowCount()
        {
            return _featureClass.Rows.Count;
        }

        public override GeometryType GetShapeType()
        {
            return _featureClass.GeometryType;
        }

        public override bool IsNativeRowCountSupported()
        {
            return true;
        }

        public override PluginCursorTemplate Search(QueryFilter queryFilter)
        {
            var rows = FilterByAttribute(_featureClass.Rows, queryFilter);
            return new GpxPluginCursorTemplate(rows.ToArray());
        }

        public override PluginCursorTemplate Search(SpatialQueryFilter spatialQueryFilter)
        {
            var rows = (spatialQueryFilter.SearchOrder == SearchOrder.Attribute)
                ? FilterByGeometry(FilterByAttribute(_featureClass.Rows, spatialQueryFilter), spatialQueryFilter)
                : FilterByAttribute(FilterByGeometry(_featureClass.Rows, spatialQueryFilter), spatialQueryFilter);
            
            if (spatialQueryFilter.OutputSpatialReference != null &&
                !spatialQueryFilter.OutputSpatialReference.Equals(SpatialReferences.WGS84))
            {
                rows = Projected(rows, spatialQueryFilter.OutputSpatialReference);
            }
            return new GpxPluginCursorTemplate(rows.ToArray());
        }

        private IEnumerable<Collection<object>> FilterByAttribute(IEnumerable<Collection<object>> rows, QueryFilter queryFilter)
        {
            // This plugin does not support Where, Prefix or Postfix clauses (IsQueryLanguageSupported == false)
            // We need to support OIFields and SpatialReference
            if (queryFilter == null)
                throw new ArgumentNullException("queryFilter");
            if (!string.IsNullOrWhiteSpace(queryFilter.WhereClause))
                throw new NotImplementedException("Query Filter Where Clause");
            if (!string.IsNullOrWhiteSpace(queryFilter.PrefixClause))
                throw new NotImplementedException("Query Filter Prefix Clause");
            if (!string.IsNullOrWhiteSpace(queryFilter.PostfixClause))
                throw new NotImplementedException("Query Filter Postfix Clause");
            rows = _featureClass.Rows;
            if (queryFilter.ObjectIDs.Count > 0 && queryFilter.ObjectIDs.Count < rows.Count())
            {
                rows = FilterByOid(rows, queryFilter.ObjectIDs);
            }
            if (queryFilter.SubFields != "*")
            {
                // SubFields is an optional optimization (see https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/#topic7549.html)
                // It is faster for this provider to do nothing and return all fields
            }
            return rows;
        }

        private IEnumerable<Collection<object>> FilterByOid(IEnumerable<Collection<object>> rows, IReadOnlyList<long> oids)
        {
            var oidSet = new HashSet<long>(oids);
            var filtered = rows.Where(row => {
                var oid = (long)row[GpxFile.OidIndex];
                return oidSet.Contains(oid);
            });
            return filtered;
        }

        private IEnumerable<Collection<object>> FilterByGeometry(IEnumerable<Collection<object>> rows, SpatialQueryFilter spatialQueryFilter)
        {
            var filtered = rows.Where(row => {
                var shape = row[GpxFile.ShapeIndex] as Geometry;
                return HasRelationship(GeometryEngine.Instance, spatialQueryFilter.FilterGeometry, shape, spatialQueryFilter.SpatialRelationship);
            });
            return filtered;
        }

        private IEnumerable<Collection<object>> Projected(IEnumerable<Collection<object>> rows, SpatialReference spatialRefernce)
        {
            var projectedRows = rows.Select(row =>
            {
                var shape = row[GpxFile.ShapeIndex] as Geometry;
                shape = GeometryEngine.Instance.Project(shape, spatialRefernce);
                row[GpxFile.ShapeIndex] = shape;
                return row;
            });
            return projectedRows;
        }

        private static bool HasRelationship(IGeometryEngine engine,
                                  Geometry geom1,
                                  Geometry geom2,
                                  SpatialRelationship relationship)
        {
            switch (relationship)
            {
                case SpatialRelationship.Intersects:
                    return engine.Intersects(geom1, geom2);
                case SpatialRelationship.IndexIntersects:
                    return engine.Intersects(geom1, geom2);
                case SpatialRelationship.EnvelopeIntersects:
                    return engine.Intersects(geom1.Extent, geom2.Extent);
                case SpatialRelationship.Contains:
                    return engine.Contains(geom1, geom2);
                case SpatialRelationship.Crosses:
                    return engine.Crosses(geom1, geom2);
                case SpatialRelationship.Overlaps:
                    return engine.Overlaps(geom1, geom2);
                case SpatialRelationship.Touches:
                    return engine.Touches(geom1, geom2);
                case SpatialRelationship.Within:
                    return engine.Within(geom1, geom2);
            }
            return false;//unknown relationship
        }

    }
}
