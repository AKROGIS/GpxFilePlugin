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
            return new GpxPluginCursorTemplate(rows.ToArray());
        }

        private IEnumerable<Collection<object>> FilterByAttribute(IEnumerable<Collection<object>> rows, QueryFilter queryFilter)
        {
            // queryFilter will never be null; it should never have a WhereClause
            // We need to support OIFields and SpatialReference
            if (queryFilter.WhereClause != null || queryFilter.PrefixClause != null || queryFilter.PostfixClause != null)
                throw new NotImplementedException("Where/Prefix/PostfixClause");
            rows = _featureClass.Rows;
            if (queryFilter.ObjectIDs != null)
            {
                //TODO: Implement
                throw new NotImplementedException("Filter search by OID");
                //rows = filterByOID(rows, queryFilter.ObjectIDs);
            }
            if (queryFilter.SubFields != null)
            {
                //TODO: Implement
                throw new NotImplementedException("Filter search by Subfield");
                //rows = TransformRows(rows, queryFilter.SubFields);
            }

            if (queryFilter.OutputSpatialReference != null)
            {
                //TODO: Implement
                throw new NotImplementedException("Project search");
                //rows = TransformRows(rows, queryFilter.SubFields);
            }
            return rows;
        }

        private IEnumerable<Collection<object>> FilterByGeometry(IEnumerable<Collection<object>> rows, SpatialQueryFilter spatialQueryFilter)
        {
            //TODO: Implement
            throw new NotImplementedException("Spatial Query");
            // return rows;
        }

    }
}
