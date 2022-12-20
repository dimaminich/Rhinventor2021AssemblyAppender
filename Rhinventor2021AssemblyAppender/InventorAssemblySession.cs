using System;
using System.Collections.Generic;
using Inventor;
using RHG = Rhino.Geometry;
using System.Linq;

namespace Rhinventor2021AssemblyAppender
{
    class InventorAssemblySession
    {
        public Application inventorApplication;
        public bool isDocumentOpen = false;
        public bool isSessionOpen = false;
        public CustomOptions options;

        public InventorAssemblySession(CustomOptions options)
        {
            this.options = options;
        }

        //-----------------------------------------------------------------------------------

        public bool openSession()
        {
            try
            {

                Type invtype = System.Type.GetTypeFromProgID("Inventor.Application");
                inventorApplication = (Application)System.Activator.CreateInstance(invtype);
                inventorApplication.Visible = true;
                isSessionOpen = true;

            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        //-----------------------------------------------------------------------------------

        public AssemblyDocument openAssemblyDocument(ParentComponent parentComponent)
        {
            AssemblyDocument assemblyDocument = (AssemblyDocument)inventorApplication.Documents.Open(parentComponent.compPath);

            setIProperty((Document)assemblyDocument, parentComponent.compiPropertyData);

            addDesignViewRepresentation(assemblyDocument);

            isDocumentOpen = true;

            return assemblyDocument;
        }

        private void addDesignViewRepresentation(AssemblyDocument assemblyDocument)
        {
            bool isExist = false;
            Inventor.DesignViewRepresentations representations = assemblyDocument.ComponentDefinition.RepresentationsManager.DesignViewRepresentations;
            foreach (Inventor.DesignViewRepresentation rep in representations)
            {
                if (rep.Name == options.identifierView)
                {
                    rep.Activate();
                    isExist = true;
                    break;
                }

            }

            if (!isExist)
            {
                assemblyDocument.ComponentDefinition.RepresentationsManager.DesignViewRepresentations.Add(options.identifierView);
            }
        }

        //-----------------------------------------------------------------------------------

        public void placeComponent(AssemblyDocument assemblyDocument,ChildComponent childComponent, RHG.Plane plane)
        {
            RHG.Plane transformedPlane = childComponent.transformPlaneToParent(plane);
            Inventor.Matrix matrix = parseRHPlaneToINVMatrix(transformedPlane);
            Inventor.ComponentOccurrence currentComponent = assemblyDocument
                .ComponentDefinition
                .Occurrences
                .Add(childComponent.compAssemblyFileTarget, matrix);

            manipulateParameter(currentComponent, childComponent);
            manipulatePointPosition(currentComponent, childComponent);
            manipulateIProperties(currentComponent, childComponent);

            setDesignViewRepresentation(currentComponent);

            currentComponent.Grounded = true;
            assemblyDocument.Update();
        }

        private Inventor.Matrix parseRHPlaneToINVMatrix(RHG.Plane plane)
        {
            Inventor.Matrix matrix = inventorApplication.TransientGeometry.CreateMatrix();
            Inventor.Point basePoint = inventorApplication.TransientGeometry.CreatePoint(
                plane.OriginX / 10, plane.OriginY / 10, plane.OriginZ / 10
                );
            Inventor.Vector xaxis = inventorApplication.TransientGeometry.CreateVector(
                plane.XAxis.X, plane.XAxis.Y, plane.XAxis.Z
                );
            Inventor.Vector yaxis = inventorApplication.TransientGeometry.CreateVector(
                plane.YAxis.X, plane.YAxis.Y, plane.YAxis.Z
                );
            Inventor.Vector zaxis = inventorApplication.TransientGeometry.CreateVector(
                plane.ZAxis.X, plane.ZAxis.Y, plane.ZAxis.Z
                );
            matrix.SetCoordinateSystem(basePoint, xaxis, yaxis, zaxis);

            return matrix;
        }

        private void manipulateParameter(Inventor.ComponentOccurrence currentComponent, ChildComponent childComponent)
        {
            string _sketch3dName;
            Inventor.PartComponentDefinition subComponentDef = getSubpartDefinition(currentComponent, childComponent, out _sketch3dName);

            Inventor.UserParameters inventorUserParameters = subComponentDef.Parameters.UserParameters;
            foreach (Inventor.UserParameter inventorUserParameter in inventorUserParameters)
            {
                string childParameter = childComponent.compParameterData[inventorUserParameter.Name];
                if (childParameter == null) continue;

                string inventorParameterFormat = inventorUserParameter.Expression;

                if (inventorParameterFormat.Contains("ul"))
                {
                    int value;
                    if (int.TryParse(childParameter, out value))
                    {
                        inventorUserParameter.Value = value;
                        inventorUserParameter.Precision = 1;
                    }
                }
                else if (inventorParameterFormat.Contains("mm"))
                {
                    double value;
                    if (double.TryParse(childParameter, out value))
                    {
                        inventorUserParameter.Value = value / 10;
                    }
                }
                else if (inventorParameterFormat.Contains("deg"))
                {
                    double value;
                    if (double.TryParse(childParameter, out value))
                    {
                        inventorUserParameter.Value = value * Math.PI / 180;
                    }
                }
                else
                {
                    inventorUserParameter.Value = childParameter;
                }
            }
        }

        private void manipulatePointPosition(Inventor.ComponentOccurrence currentComponent, ChildComponent childComponent)
        {
            string sketch3dName;
            Inventor.PartComponentDefinition subComponentDef = getSubpartDefinition(currentComponent, childComponent, out sketch3dName);
            Inventor.Sketch3D subComponentSketch3d = subComponentDef.Sketches3D[sketch3dName];
            Inventor.SketchPoints3D sketchPoints = subComponentSketch3d.SketchPoints3D;
            List<Inventor.Point> parsedPoints = parseRHPointToINVPoint(childComponent.remapPointsToPlane());

            for (int i = 1; i < parsedPoints.Count + 1; i++)
            {
                if (i > sketchPoints.Count) break;
                Inventor.Point targetPoint = parsedPoints[i - 1];
                Inventor.SketchPoint3D currentPoint = sketchPoints[i];
                currentPoint.MoveTo(targetPoint);
            }
        }

        private void manipulateIProperties(Inventor.ComponentOccurrence currentComponent, ChildComponent childComponent)
        {
            Document assemblyDocument = currentComponent.Definition.Document;
            recursiveSetIProperty(assemblyDocument, childComponent.compiPropertyData);
            setIProperty(assemblyDocument, childComponent.compiPropertyData);
        }

        private void recursiveSetIProperty(Document doc, List<Dictionary<string, object>> iPropertySet)
        {
            if (doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                AssemblyDocument assemblyDocument = (AssemblyDocument)doc;
                ComponentOccurrences subcomponents = assemblyDocument.ComponentDefinition.Occurrences;
                foreach (ComponentOccurrence subcomponent in subcomponents)
                {
                    Document nextdoc = subcomponent.Definition.Document;
                    recursiveSetIProperty(nextdoc, iPropertySet);
                }
            }
            else if (doc.DocumentType == DocumentTypeEnum.kPartDocumentObject)
            {
                setIProperty((Document)doc, iPropertySet);
            }
        }

        private void setIProperty(Document doc, List<Dictionary<string, object>> iPropertySet)
        {
            foreach (var iProperty in iPropertySet)
            {
                try
                {
                    string propertySetIdentifier = (string)iProperty["propertySet"];
                    PropertySet propertySet = doc.PropertySets[propertySetIdentifier];

                    string propertyName = (string)iProperty["propertyId"];
                    Property propertyField = propertySet[propertyName];

                    var value = iProperty["convertedValue"];

                    if (propertyName == "Part Number")
                    {
                        if (propertyField.Value.Contains("-"))
                        {
                            propertyField.Value = $"{value}{propertyField.Value}";
                        }
                        else if (!propertyField.Value.Contains("-") && propertyField.Value.Length != 0)
                        {
                            continue;
                        }
                        else
                        {
                            propertyField.Value = value;
                        }
                    }
                    else
                    {
                        propertyField.Value = value;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void setDesignViewRepresentation(ComponentOccurrence currentComponent)
        {
            try
            {
                currentComponent.SetDesignViewRepresentation(options.identifierView, "", true);
            }
            catch (Exception e)
            {
            }
        }

        private Inventor.PartComponentDefinition getSubpartDefinition(Inventor.ComponentOccurrence currentComponent, ChildComponent childComponent, out string sketch3dName)
        {
            string filename = System.IO.Path.GetFileName(childComponent.compAssemblyFileSource);
            //string assemblyname = filename.Split(new char[] { '_' })[0]; => Before changing
            string assemblyname = String.Join(options.separator, filename.Split(new char[] { options.separator[0] }).Reverse().Skip(1).Reverse());
            sketch3dName = $"{assemblyname}{options.separator}{options.identifier3DSketch}";
            string fullpartname = $"{childComponent.compLabel}{options.separator}{assemblyname}{options.separator}{options.identifier3DSketch}";

            Inventor.PartComponentDefinition subComponentDef;
            try
            {
                Inventor.ComponentOccurrence subComponent = currentComponent.Definition.Occurrences.ItemByName[fullpartname];
                subComponentDef = (Inventor.PartComponentDefinition)subComponent.Definition;
            }
            catch (Exception)
            {
                throw new ArgumentException("Cannot find part with SKELETT-substring," +
                    "check the name of the part");
            }

            if (subComponentDef == null)
                throw new ArgumentException("Cannot find part with SKELETT-substring," +
                    "check the name of the part");

            return subComponentDef;
        }

        private List<Inventor.Point> parseRHPointToINVPoint(List<RHG.Point3d> rhinoPoints)
        {
            List<Inventor.Point> parsedPoints = rhinoPoints.Select(p =>
            {
                Inventor.Point invPoint = inventorApplication.TransientGeometry.CreatePoint(
                        p.X / 10,
                        p.Y / 10,
                        p.Z / 10
                    );
                return invPoint;
            }).ToList();

            return parsedPoints;
        }

        //-----------------------------------------------------------------------------------

        public void closeDocument(AssemblyDocument assemblyDocument)
        {
            if (isDocumentOpen)
            {
                assemblyDocument.Close();
                isDocumentOpen = false;
            }
        }

        public void saveDocument(AssemblyDocument assemblyDocument)
        {
            assemblyDocument.Save2();
        }

        public void closeSession()
        {
            if (isSessionOpen)
            {
                inventorApplication.Quit();
                isSessionOpen = false;
            }
        }


    }

}

