using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace TNovUtilsST
{
    public class TNovRebarType
    {
        public string Name { get; set; }
        public RebarBarType bartype;
        public Dictionary<string, TNovParameterValue> ValuesStorage = new Dictionary<string, TNovParameterValue>();

        public TNovRebarType(RebarBarType BarType)
        {
            Name = BarType.Name;
            bartype = BarType;

            foreach (Parameter param in BarType.ParametersMap)
            {
                string paramName = param.Definition.Name;
                TNovParameterValue mpv = new TNovParameterValue(param);
                ValuesStorage.Add(paramName, mpv);
            }
        }
    }
}
