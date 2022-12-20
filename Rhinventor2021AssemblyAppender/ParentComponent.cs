using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inventor;

namespace Rhinventor2021AssemblyAppender
{
    class ParentComponent
    {
        public string compPath { get; set; }
        public Rhino.Geometry.Plane compPlane { get; set; }
        public List<ChildComponent> childComponents { get; set; }
        public List<Dictionary<string, object>> compiPropertyData { get; set; }
    }
}

