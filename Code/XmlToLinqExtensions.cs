using System.Collections.Generic;

using System.Xml.Linq;

namespace NPS.AKRO.ArcGIS.GpxPlugin
{
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
