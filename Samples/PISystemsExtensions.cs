using OSIsoft.AF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples
{
    public static class PISystemsExtensions
    {
        public static T Find<T>(this PISystems systems, string path)
            where T : AFObject
        {
            if (systems.DefaultPISystem == null)
            {
                throw new InvalidOperationException("Default PISystem must be set.");
            }

            return AFObject.FindObject(path, systems.DefaultPISystem) as T;
        }
    }
}
