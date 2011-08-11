// Copyright 2010 ESRI
// 
// All rights reserved under the copyright laws of the United States
// and applicable international laws, treaties, and conventions.
// 
// You may freely redistribute and use this sample code, with or
// without modification, provided you include the original copyright
// notice and use restrictions.
// 
// See the use restrictions at http://help.arcgis.com/en/sdk/10.0/usageRestrictions.htm
// 

using System;
using System.Runtime.InteropServices;
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

		private int m_iInterate = -1;

		private string m_sbuffer;
		private System.IO.StreamReader m_pStreamReader;
		private int m_iOID = -1;
		
		private System.Array m_fieldMap;
		private IFields m_fields;
		private IEnvelope m_searchEnv;
		private IGeometry m_wkGeom;
		private IPoint[] m_workPts;

		// HRESULTs definitions for COM Exception
		private const int E_FAIL = unchecked((int)0x80004005);

        public bool HasZ { get; set; }
        public bool HasM { get; set; }


        public GpxCursor(Gpx gpx, int classIndex, int id, IEnvelope env, int[] fieldMap)
        {
            if (gpx == null)
                throw new ArgumentNullException("gpx");
            if (classIndex < 0 || gpx.FeatureTypes.Length <= classIndex)
                throw new ArgumentOutOfRangeException("classIndex");


            //if id != -1, then the cursor will have a single record (with object ID = id).
            //if env != null, then the cursor is limited to features within the env.
            //if env is null and id = -1, then return all features
            //fieldMap is used by QueryValues() to limit the fields returned.

            //If there is no object with the given id, this method should fail.

        }

	    public GpxCursor(string filePath, IFields fields, int OID, 
			System.Array fieldMap, IEnvelope queryEnv, esriGeometryType geomType)	
		{
//HIGHLIGHT: 0 - Set up cursor
			_isFinished = false;
			m_pStreamReader = new System.IO.StreamReader(filePath);
			m_fields = fields;
			m_iOID = OID;
			m_fieldMap = fieldMap;
			m_searchEnv = queryEnv;
			switch (geomType)
			{
				case esriGeometryType.esriGeometryPolygon:
					m_wkGeom = new Polygon() as IGeometry;
					m_workPts = new PointClass[5];
					for (int i = 0; i < m_workPts.Length; i++)
						m_workPts[i] = new PointClass();
					break;
				case esriGeometryType.esriGeometryPolyline:
					m_wkGeom = new PolylineClass() as IGeometry;
					m_workPts = new PointClass[5];
					for (int i = 0; i < m_workPts.Length; i++)
						m_workPts[i] = new PointClass();
					break;
				
				case esriGeometryType.esriGeometryPoint:
					m_wkGeom = new PointClass() as IGeometry;
					break;
				default:	//doesn't need to set worker geometry if it is table 
					break;
			}

			//advance cursor so data is readily available
			NextRecord();
		}



		#region IPlugInCursorHelper Members

        /// <summary>
        /// Returns true if there are no more records to get from the cursor.
        /// IsFinished is called any time that NextRecord fails.
        /// </summary>
        /// <returns></returns>
        public bool IsFinished()
        {
            return _isFinished;
        }
        private bool _isFinished;


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
			if (_isFinished)	//error already thrown once
				return;

			//OID search has been performed
			if (m_iOID > -1 && m_sbuffer != null)
			{
				m_pStreamReader.Close();
				_isFinished = true;	
				throw new COMException("End of Gpx Plugin cursor", E_FAIL);
			}
			else
			{
				//HIGHLIGHT: 1.1 Next - Read the file for text
				m_sbuffer = ReadFile(m_pStreamReader, m_iOID);
				if (m_sbuffer == null)
				{
					//finish reading, close the stream reader so resources will be released
					m_pStreamReader.Close();
					_isFinished = true;	

					//HIGHLIGHT: 1.2 Next - Raise E_FAIL to notify end of cursor
					throw new COMException("End of Gpx Plugin cursor", E_FAIL);
				}
				//HIGHLIGHT: 1.3 Next - Search by envelope; or return all records and let post-filtering do 
				//the work for you (performance overhead)
				else if (m_searchEnv != null && !(m_searchEnv.IsEmpty))	
				{
					this.QueryShape(m_wkGeom);
					IRelationalOperator pRelOp = (IRelationalOperator)m_wkGeom;					
					if (!pRelOp.Disjoint((IGeometry)m_searchEnv))
						return;	//HIGHLIGHT: 1.4 Next - valid record within search geometry - stop advancing
					else
						this.NextRecord();
				}
			}
			
		}


        /// <summary>
        /// QueryShape uses the data in the current record to modify the provided geometry.
        /// If anything goes wrong, the geometry should be set empty. 
        /// This method should not allocate memory.
        /// For simple shapes you can reset the contents of the supplied geometry object.
        /// For data sources with complex shapes attach a shape buffer to the geometry (see IPlugInCursorHelper.QueryShape)
        /// </summary>
        /// <param name="geometry"></param>
        public void QueryShape(IGeometry geometry)
        {
            if (geometry == null)
                return;

            try
            {
                double x, y;
                x = Convert.ToDouble(m_sbuffer.Substring(0, 6));
                y = Convert.ToDouble(m_sbuffer.Substring(6, 6));

                #region set M and Z aware
                if (HasZ)
                    ((IZAware)geometry).ZAware = true;
                if (HasM)
                    ((IMAware)geometry).MAware = true;
                #endregion

                //HIGHLIGHT: 2.1 QueryShape - (advanced) geometry construction
                if (geometry is IPoint)
                {
                    ((IPoint)geometry).PutCoords(x, y);
                    if (HasM)
                        ((IPoint)geometry).M = m_iInterate;
                    if (HasZ)
                        ((IPoint)geometry).Z = m_iInterate * 100;
                }
                else if (geometry is IPolyline)
                    buildPolyline((IPointCollection)geometry, x, y);
                else if (geometry is IPolygon)
                    buildPolygon((IPointCollection)geometry, x, y);
                else
                    geometry.SetEmpty();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(" Error: " + ex.Message);
                geometry.SetEmpty();
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
        /// <param name="Row"></param>
        /// <returns></returns>
        public int QueryValues(IRowBuffer Row)
        {
            try
            {
                if (m_sbuffer == null)
                    return -1;

                for (int i = 0; i < m_fieldMap.GetLength(0); i++)
                {
                    //HIGHLIGHT: 2.2 QueryValues - field map interpretation
                    if (m_fieldMap.GetValue(i).Equals(-1))
                        continue;

                    IField valField = m_fields.get_Field(i);
                    char parse = m_sbuffer[m_sbuffer.Length - 1];
                    switch (valField.Type)
                    {
                        case esriFieldType.esriFieldTypeInteger:
                        case esriFieldType.esriFieldTypeDouble:
                        case esriFieldType.esriFieldTypeSmallInteger:
                        case esriFieldType.esriFieldTypeSingle:
                            Row.set_Value(i, Convert.ToInt32(parse));	//get ascii code # for the character
                            break;
                        case esriFieldType.esriFieldTypeString:
                            Row.set_Value(i, parse.ToString());
                            break;
                    }
                }
                return m_iInterate;	//HIGHLIGHT: 2.3 QueryValues - return OID
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return -1;
            }

        }

        #endregion



		#region Geometry construction

		private void buildPolygon(IPointCollection pGonColl, double x, double y)
		{
			m_workPts[0].PutCoords(x - 500, y - 500);
			m_workPts[1].PutCoords(x + 500, y - 500);
			m_workPts[2].PutCoords(x + 500, y + 500);
			m_workPts[3].PutCoords(x - 500, y + 500);
			m_workPts[4].PutCoords(x - 500, y - 500);
			try
			{
				bool add = (pGonColl.PointCount == 0);
				object missingVal = System.Reflection.Missing.Value;
					
				for (int i = 0; i< m_workPts.Length; i++)
				{
					((IZAware)m_workPts[i]).ZAware = HasZ;
					((IMAware)m_workPts[i]).MAware = HasM;

					if (HasM)
						m_workPts[i].M = i % 4;
					if (HasZ)
						m_workPts[i].Z = (i % 4) * 100;	//match start and end points
						
					if (add)
						pGonColl.AddPoint(m_workPts[i], ref missingVal, ref missingVal);	//The Add method only accepts either a before index or an after index.	
					else
						pGonColl.UpdatePoint(i, m_workPts[i]);
				}
			}

			catch (Exception Ex)
			{System.Diagnostics.Debug.WriteLine(Ex.Message);}	
			//Attempted to store an element of the incorrect type into the array.
		}

		private void buildPolyline(IPointCollection pGonColl, double x, double y)
		{
			m_workPts[0].PutCoords(x - 500, y - 500);
			m_workPts[1].PutCoords(x + 500, y - 500);
			m_workPts[2].PutCoords(x + 500, y + 500);
			m_workPts[3].PutCoords(x - 500, y + 500);
			m_workPts[4].PutCoords(x, y);

			try
			{
				bool add = (pGonColl.PointCount == 0);
			
					object missingVal = System.Reflection.Missing.Value;
					for (int i = 0; i< m_workPts.Length; i++)
					{
						((IZAware)m_workPts[i]).ZAware = HasZ;
						((IMAware)m_workPts[i]).MAware = HasM;

						if (HasM)
							m_workPts[i].M =  i;
						if (HasZ)
							m_workPts[i].Z = i * 100;
						//add it point by point - .Net IDL limitation to do batch update?
						if (add)	//pGonColl.AddPoints(5, ref m_workPts[0]);//strange error of type mismatch
							pGonColl.AddPoint(m_workPts[i], ref missingVal, ref missingVal);	//The Add method only accepts either a before index or an after index.	
						else
							pGonColl.UpdatePoint(i, m_workPts[i]);
					}

				//Can I user replace point collection or addPointcollection?
			}

			catch (Exception Ex)
			{System.Diagnostics.Debug.WriteLine(Ex.Message);}	
			//Attempted to store an element of the incorrect type into the array.
		}

		#endregion

		private string ReadFile(System.IO.StreamReader sr, int lineNumber)
		{
			m_iInterate++;
			string buffer = sr.ReadLine();

			if (buffer == null)
				return null;

			if (lineNumber > -1 && lineNumber != m_iInterate)
				buffer = ReadFile(sr, lineNumber);
			//System.Diagnostics.Debug.WriteLine(buffer);
			return buffer;
		}

	}
}
