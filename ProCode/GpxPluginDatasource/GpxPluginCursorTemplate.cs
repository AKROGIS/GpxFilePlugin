using ArcGIS.Core.Data;
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
    public class GpxPluginCursorTemplate : PluginCursorTemplate
    {
        private readonly IReadOnlyList<IReadOnlyList<object>> _rows;
        private int _current = -1;

        internal GpxPluginCursorTemplate(IReadOnlyList<IReadOnlyList<object>> rows)
        {
            _rows = rows;
        }

        public override PluginRow GetCurrentRow()
        {
            if (_current < 0 || _current >= _rows.Count) { return null; }
            return new PluginRow(_rows[_current]);
        }

        public override bool MoveNext()
        {
            _current += 1; 
            return _current < _rows.Count;
        }
    }
}
