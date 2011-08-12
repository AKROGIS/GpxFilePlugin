using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
	/// <summary>
    /// Provides data via the interface expected by the plug in framework.
    /// The IPlugInCursorHelper interface must be implemented by a helper class of a plug-in data source.
    /// When the cursor is created it must be positioned at the first record, so that
    /// QueryValues and QueryShape can get the data for the first record in the results.
    /// NextRecord advances the current record, so that QueryValues and QueryShape then get data for the next record.
	/// </summary>
	internal class GpxCursor : IPlugInCursorHelper
	{
        private int _currentId;
        private bool _isFinished;

        private readonly IEnumerator<XElement> _enumerator;
        private readonly int _queryId;
        private readonly IEnvelope _queryEnvelope;
        private readonly int[] _fieldMap;
        private readonly IGeometry _tempGeometry;

        /// <summary>
        /// Set up a cursor so that records can be obtained via the cursor interface
        /// </summary>
        /// <remarks>
        /// If there is no object matching the search, this method should throw.
        /// </remarks>
        /// <param name="gpx">The dataset (GPX file) to search</param>
        /// <param name="classIndex">The index to the feature types in gpx that should be searched</param>
        /// <param name="id">if id > 0, then the cursor will have a single record (with object ID = id).  If 1 > id (and env is null), then return all features</param>
        /// <param name="env">if env != null, then the cursor is limited to features within the env. If env is null (and 1 > id), then return all features.</param>
        /// <param name="fieldMap">fieldMap is used by QueryValues() to limit the fields returned.</param>
        internal GpxCursor(Gpx gpx, int classIndex, int id, IEnvelope env, int[] fieldMap)
        {
            if (gpx == null)
                throw new ArgumentNullException("gpx");
            if (classIndex < 0 || gpx.FeatureClasses.Length <= classIndex)
                throw new ArgumentOutOfRangeException("classIndex");

            _queryId = id;
            _queryEnvelope = env;
            _enumerator = gpx.GetEnumerator(gpx.FeatureClasses[classIndex]);
            _fieldMap = fieldMap;
            _tempGeometry = gpx.GetWorkingGeometry(classIndex);

            //advance cursor (based on id/env/enumerator) so error is thrown or first record is ready for reading
            NextRecord();
        }

		#region IPlugInCursorHelper Members

        /// <summary>
        /// Returns true if there are no more records to get from the cursor.
        /// IsFinished is called any time that NextRecord fails.
        /// ? I assume this allows the caller to distinguish between end of records and true failure ?
        /// </summary>
        /// <returns></returns>
        public bool IsFinished()
        {
            return _isFinished;
        }


        /// <summary>
        /// NextRecord advances the cursor helper to represent the next record in the collection.
        /// Use QueryShape and/or QueryValue after calling to get the record.
        /// </summary>
        /// <remarks>
        /// Calling this method repeatedly should get all the records without getting any record twice.
        /// This method throws when there are no more records left to get.
        /// NextRecord must raise an error if there are no more rows to fetch.
        /// </remarks>
        /// <exception>
        /// COMException - Thrown when the end of the cursor is reached, thrown only once.
        /// </exception>
		public void NextRecord()
		{
			if (_isFinished)
				return;

            //fetch all
            if (_queryId < 1 && (_queryEnvelope == null || _queryEnvelope.IsEmpty))
            {
                AdvanceCursor();
            }
            //fetch by envelope
            else if (_queryId < 1 && (_queryEnvelope != null && !_queryEnvelope.IsEmpty))
            {
                do
                {
                    AdvanceCursor();
                    QueryShape(_tempGeometry);
                } while (((IRelationalOperator)_tempGeometry).Disjoint(_queryEnvelope));
            }
            //fetch by id
            else if (_queryEnvelope == null || _queryEnvelope.IsEmpty)
            {
                //close if we've alredy returned the requested record
                if (_queryId <= _currentId)
                    CloseCursor();
                //loop until we find the requested record, or the end of the file
                while (_currentId < _queryId)
                    AdvanceCursor();
            }
            //fetch by id and envelope (not allowed by the interface)
            else 
            {
                throw new NotImplementedException();
            }
        }

	    private void AdvanceCursor()
	    {
	        _currentId++;
	        if (_enumerator.MoveNext() == false)
	            CloseCursor();
	    }

	    private void CloseCursor()
	    {
	        _isFinished = true;
	        _currentId = -1;
            // COMException with HRESULT = E_FAIL
	        throw new COMException("End of Gpx Plugin cursor", unchecked((int)0x80004005));
	    }

	    /// <summary>
        /// QueryShape uses the data in the current record to modify the provided geometry.
        /// If anything goes wrong, the geometry should be set empty. 
        /// This method should not allocate memory.
        /// For simple shapes you can reset the contents of the supplied geometry object.
        /// For data sources with complex shapes attach a shape buffer to the geometry (see IPlugInCursorHelper.QueryShape)
        /// </summary>
        /// <param name="geometry">A geometry object to be reset to contain the current shape</param>
        public void QueryShape(IGeometry geometry)
        {
            if (geometry == null || _enumerator.Current == null)
                return;

            if (_enumerator.Current == null)
            {
                geometry.SetEmpty();
                return;               
            }
 
            if (geometry is IPoint)
                BuildPoint((IPoint) geometry, _enumerator.Current);
            else
            {
                // since each polyline/polygon can be a different size, it is not easy to reuse the geometry
                // API recommends using IESRIShape.AttachBuffer.
                //For now, I will just build a new shape, and pay the inefficiency cost
                geometry.SetEmpty();
                if (geometry is IPolyline)
                    BuildPoly(geometry, _enumerator.Current, false);
                if (geometry is IPolygon)
                    BuildPoly(geometry, _enumerator.Current, true);
            }
        }


        /// <summary>
        /// Copies data from the current record into the row that is passed in.
        /// The method should get the field-set from the row buffer.
        /// The field map passed to the Fetch method determines which fields will be copied.
        /// For each field in the field set, the data should be copied only if the corresponding value in the field map is not -1.
        /// However, the shape and object ID fields should NOT be copied.
        /// The shape field is handled separately in QueryShape.
        /// The object ID cannot be set through the IRowBuffer interface.
        /// Instead, the object ID should be the return value of QueryValues.
        /// </summary>
        /// <param name="row">The IRowBuffer to be filled with data from the current record</param>
        /// <returns>The object Id of the current record</returns>
        public int QueryValues(IRowBuffer row)
        {
            if (row == null || _enumerator.Current == null)
                return -1;

            foreach (var i in _fieldMap)
            {
                if (i == -1)
                    continue;

                IField field = row.Fields.Field[i];
                switch (field.Type)
                {
                    case esriFieldType.esriFieldTypeInteger:
                        row.Value[i] = (int?) _enumerator.Current.GetElement(field.Name);
                        break;
                    case esriFieldType.esriFieldTypeDouble:
                        row.Value[i] = (double?) _enumerator.Current.GetElement(field.Name);
                        break;
                    case esriFieldType.esriFieldTypeDate:
                        row.Value[i] = (DateTime?) _enumerator.Current.GetElement(field.Name);
                        break;
                    case esriFieldType.esriFieldTypeString:
                        //FIXME - if field.Name = "link", then there may be multiple Elements, but we are only getting the first
                        var v = _enumerator.Current.GetElement(field.Name);
                        if (v != null)
                        {
                            if (field.Name == "extensions")
                                row.Value[i] = v.ToString();  //FIXME - omit the surrounding <extension> tag; only provide the child elements 
                            else if (field.Name == "link")
                                row.Value[i] = (string) v.Attribute("href");
                            else
                                row.Value[i] = v.Value;
                           
                        }
                        break;
                }
            }
            return _currentId;
        }

        #endregion

        #region Geometry construction

        private void BuildPoly(IGeometry geometry, XElement xElement, bool close)
        {
            if (xElement.Name.LocalName == "rte")
                BuildSegment("rtept", (IPointCollection) geometry, xElement, close);
            else if (xElement.Name.LocalName == "trk")
                BuildTrack((IGeometryCollection) geometry, xElement, close);
            if (!close)
                ((IZAware)geometry).ZAware = true;
        }

        private void BuildTrack(IGeometryCollection paths, XElement xElement, bool close)
        {
            foreach (var ele in xElement.GetElements("trkseg"))
            {
                IGeometry path;
                if (close)
                    path = new RingClass();
                else
                    path = new PathClass();
                path.SpatialReference = ((IGeometry)paths).SpatialReference;
                BuildSegment("trkpt", (IPointCollection) path, ele, close);
                paths.AddGeometry(path);
            }
        }

        private void BuildSegment(string pointName, IPointCollection points, XElement xElement, bool close)
        {
            //object missing = Type.Missing;
            IPoint point = new PointClass();
            foreach (var ele in xElement.GetElements(pointName))
            {
                BuildPoint(point, ele);
                points.AddPoint(point); //, ref missing, ref missing);
            }
            if (close)
                points.AddPoint(points.Point[0]); //, ref missing, ref missing);
        }

        private void BuildPoint(IPoint point, XElement ele)
        {
            var x = (double?)ele.Attribute("lon");
            var y = (double?)ele.Attribute("lat");
            if (x.HasValue && y.HasValue)
                point.PutCoords(x.Value, y.Value);
            else
                point.SetEmpty();

            //elevation
            var elev = (double?)ele.GetElement("ele");
            if (elev.HasValue)
            {
                point.Z = elev.Value;
                ((IZAware)point).ZAware = true;
            }
        }

        #endregion
	}
}
