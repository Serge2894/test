using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using test.Common;
using test.Forms;

namespace test
{
    [Transaction(TransactionMode.Manual)]
    public class Command1 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // step 2: open form and pass the parameters
            Window1 currentForm = new Window1(doc)
            {
                Width = 500,
                Height = 600,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Topmost = true,
            };

            bool? dialogResult = currentForm.ShowDialog();

            // Check if user clicked OK
            if (dialogResult != true)
            {
                return Result.Cancelled;
            }

            // step 4: get form data and do something
            bool entireModelScope = currentForm.IsEntireModelChecked;
            List<string> selectedCurtainWallParamNames = currentForm.SelectedCurtainWallParameterNames;
            List<string> selectedStairParamNames = currentForm.SelectedStairParameterNames;
            List<string> selectedRailingParamNames = currentForm.SelectedRailingParameterNames;
            List<string> selectedMEPInsulationParamNames = currentForm.SelectedMEPInsulationParameterNames;
            List<string> selectedNestedFamilyParamNames = currentForm.SelectedNestedFamilyParameterNames;
            List<string> selectedNestedFamilyNames = currentForm.SelectedNestedFamilyNames;
            string selectedParentFamilyName = currentForm.SelectedParentFamilyName;

            bool copyToCurtainPanel = currentForm.IsCurtainPanelChecked;
            bool copyToCurtainMullion = currentForm.IsCurtainWallMullionsChecked;
            bool copyToStairLanding = currentForm.IsStairLandingChecked;
            bool copyToStairSupport = currentForm.IsStairSupportChecked;
            bool copyToStairRun = currentForm.IsStairRunChecked;
            bool copyToTopRail = currentForm.IsTopRailChecked;
            bool copyToHandrail = currentForm.IsHandrailChecked;
            bool copyToRailSupport = currentForm.IsRailSupportChecked;
            bool copyToPipeInsulation = currentForm.IsPipeInsulationChecked;
            bool copyToDuctInsulation = currentForm.IsDuctInsulationChecked;

            bool operationCompleted = false;

            // Perform actions based on the selected tab
            if (currentForm.CurtainwallContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Check for Curtain Walls first
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                if (!entireModelScope)
                {
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                }

                List<Wall> curtainWalls = collector
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .Where(w => w.WallType.Kind == WallKind.Curtain)
                    .ToList();

                if (!curtainWalls.Any())
                {
                    // No elements found - show InfoDialog
                    InfoDialog infoDialog = new InfoDialog($"No Curtain Walls found in the {(entireModelScope ? "Entire Model" : "Current View")}.")
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    infoDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist, check if parameters are selected
                if (!selectedCurtainWallParamNames.Any())
                {
                    NoParameterDialog noParameterDialog = new NoParameterDialog()
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    noParameterDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist and parameters selected - perform operation
                using (Transaction t = new Transaction(doc, $"Copy Selected Parameters to Curtain Wall Elements"))
                {
                    t.Start();
                    foreach (Wall curtainWall in curtainWalls)
                    {
                        // Process each selected parameter
                        foreach (string selectedCurtainWallParamName in selectedCurtainWallParamNames)
                        {
                            // Get parameter value from curtain wall (either instance or type)
                            string parameterValue = Utils.GetParameterValueFromElementOrType(curtainWall, selectedCurtainWallParamName);

                            if (!string.IsNullOrEmpty(parameterValue))
                            {
                                // Get the curtain wall's mullions and panels
                                List<ElementId> mullionIds = curtainWall.CurtainGrid?.GetMullionIds()?.ToList() ?? new List<ElementId>();
                                List<ElementId> panelIds = curtainWall.CurtainGrid?.GetPanelIds()?.ToList() ?? new List<ElementId>();

                                // Convert the IDs to Element objects
                                List<Element> mullions = mullionIds.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
                                List<Element> panels = panelIds.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();

                                // Copy to mullions if the checkbox is checked
                                if (copyToCurtainMullion)
                                {
                                    foreach (Element mullion in mullions)
                                    {
                                        Utils.SetParameterValueString(mullion, selectedCurtainWallParamName, parameterValue);
                                    }
                                }

                                // Copy to panels if the checkbox is checked
                                if (copyToCurtainPanel)
                                {
                                    foreach (Element panel in panels)
                                    {
                                        Utils.SetParameterValueString(panel, selectedCurtainWallParamName, parameterValue);
                                    }
                                }
                            }
                            else
                            {
                                Debug.Print($"Parameter '{selectedCurtainWallParamName}' not found or has no value on curtain wall {curtainWall.Id}");
                            }
                        }
                    }
                    t.Commit();
                    operationCompleted = true;
                }
            }
            else if (currentForm.StairContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Check for Stairs first
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                if (!entireModelScope)
                {
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                }

                List<Stairs> stairs = collector
                    .OfClass(typeof(Stairs))
                    .Cast<Stairs>()
                    .ToList();

                if (!stairs.Any())
                {
                    // No elements found - show InfoDialog
                    InfoDialog infoDialog = new InfoDialog($"No Stairs found in the {(entireModelScope ? "Entire Model" : "Current View")}.")
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    infoDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist, check if parameters are selected
                if (!selectedStairParamNames.Any())
                {
                    NoParameterDialog noParameterDialog = new NoParameterDialog()
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    noParameterDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist and parameters selected - perform operation
                using (Transaction t = new Transaction(doc, $"Copy Selected Parameters to Stair Elements"))
                {
                    t.Start();
                    foreach (Stairs stair in stairs)
                    {
                        // Process each selected parameter
                        foreach (string selectedStairParamName in selectedStairParamNames)
                        {
                            // Get parameter value from stair (either instance or type)
                            string parameterValue = Utils.GetParameterValueFromElementOrType(stair, selectedStairParamName);

                            if (!string.IsNullOrEmpty(parameterValue))
                            {
                                // Get related stair sub-elements
                                List<ElementId> landingIds = stair.GetStairsLandings().ToList();
                                List<ElementId> supportIds = stair.GetStairsSupports().ToList();
                                List<ElementId> runIds = stair.GetStairsRuns().ToList();

                                if (copyToStairLanding)
                                {
                                    foreach (ElementId landingId in landingIds)
                                    {
                                        Element landing = doc.GetElement(landingId);
                                        if (landing != null)
                                        {
                                            Utils.SetParameterValueString(landing, selectedStairParamName, parameterValue);
                                        }
                                    }
                                }

                                // Apply the parameter value to supports if the checkbox is checked
                                if (copyToStairSupport)
                                {
                                    foreach (ElementId supportId in supportIds)
                                    {
                                        Element support = doc.GetElement(supportId);
                                        if (support != null)
                                        {
                                            Utils.SetParameterValueString(support, selectedStairParamName, parameterValue);
                                        }
                                    }
                                }

                                // Apply the parameter value to runs if the checkbox is checked
                                if (copyToStairRun)
                                {
                                    foreach (ElementId runId in runIds)
                                    {
                                        Element run = doc.GetElement(runId);
                                        if (run != null)
                                        {
                                            Utils.SetParameterValueString(run, selectedStairParamName, parameterValue);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.Print($"Parameter '{selectedStairParamName}' not found or has no value on stair {stair.Id}");
                            }
                        }
                    }
                    t.Commit();
                    operationCompleted = true;
                }
            }
            else if (currentForm.RaillingContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Check for Railings first
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                if (!entireModelScope)
                {
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                }

                List<Railing> railings = collector
                    .OfClass(typeof(Railing))
                    .Cast<Railing>()
                    .ToList();

                if (!railings.Any())
                {
                    // No elements found - show InfoDialog
                    InfoDialog infoDialog = new InfoDialog($"No Railings found in the {(entireModelScope ? "Entire Model" : "Current View")}.")
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    infoDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist, check if parameters are selected
                if (!selectedRailingParamNames.Any())
                {
                    NoParameterDialog noParameterDialog = new NoParameterDialog()
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    noParameterDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist and parameters selected - perform operation
                using (Transaction t = new Transaction(doc, $"Copy Selected Parameters to Railing Elements"))
                {
                    t.Start();
                    foreach (Railing railing in railings)
                    {
                        // Process each selected parameter
                        foreach (string selectedRailingParamName in selectedRailingParamNames)
                        {
                            // Get parameter value from railing (either instance or type)
                            string parameterValue = Utils.GetParameterValueFromElementOrType(railing, selectedRailingParamName);

                            if (!string.IsNullOrEmpty(parameterValue))
                            {
                                List<ElementId> topRailIds = railing.GetDependentElements(new ElementClassFilter(typeof(TopRail))).ToList();
                                List<ElementId> handRailIds = railing.GetHandRails().ToList();

                                // Get railing supports - need to use a different approach
                                List<ElementId> railSupportIds = new List<ElementId>();
                                try
                                {
                                    // Try different methods to get railing supports
                                    var allDependents = railing.GetDependentElements(null);
                                    foreach (ElementId depId in allDependents)
                                    {
                                        Element depElement = doc.GetElement(depId);
                                        if (depElement != null && depElement.Category != null)
                                        {
                                            // Check if it's a railing support (you might need to adjust this based on your Revit version)
                                            if (depElement.Category.Name.Contains("Railing") &&
                                                !depElement.Category.Name.Contains("Top Rail") &&
                                                depElement.GetType() != typeof(TopRail) &&
                                                depElement.GetType() != typeof(Railing))
                                            {
                                                railSupportIds.Add(depId);
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // If the above fails, try the original method
                                    railSupportIds = railing.GetDependentElements(new ElementClassFilter(typeof(Railing))).ToList();
                                }

                                if (copyToTopRail)
                                {
                                    foreach (ElementId topRailId in topRailIds)
                                    {
                                        Element topRail = doc.GetElement(topRailId);
                                        if (topRail != null)
                                        {
                                            Utils.SetParameterValueString(topRail, selectedRailingParamName, parameterValue);
                                        }
                                    }
                                }

                                if (copyToHandrail)
                                {
                                    foreach (ElementId handrailId in handRailIds)
                                    {
                                        Element handrail = doc.GetElement(handrailId);
                                        if (handrail != null)
                                        {
                                            Utils.SetParameterValueString(handrail, selectedRailingParamName, parameterValue);
                                        }
                                    }
                                }

                                if (copyToRailSupport)
                                {
                                    foreach (ElementId railSupportId in railSupportIds)
                                    {
                                        Element railSupport = doc.GetElement(railSupportId);
                                        if (railSupport != null)
                                        {
                                            Utils.SetParameterValueString(railSupport, selectedRailingParamName, parameterValue);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.Print($"Parameter '{selectedRailingParamName}' not found or has no value on railing {railing.Id}");
                            }
                        }
                    }
                    t.Commit();
                    operationCompleted = true;
                }
            }
            else if (currentForm.MEPInsulationContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Check for Pipes and Ducts first
                FilteredElementCollector pipeCollector = new FilteredElementCollector(doc);
                FilteredElementCollector ductCollector = new FilteredElementCollector(doc);

                if (!entireModelScope)
                {
                    pipeCollector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                    ductCollector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                }

                List<Pipe> pipes = pipeCollector
                    .OfClass(typeof(Pipe))
                    .Cast<Pipe>()
                    .ToList();

                List<Duct> ducts = ductCollector
                    .OfClass(typeof(Duct))
                    .Cast<Duct>()
                    .ToList();

                if (!pipes.Any() && !ducts.Any())
                {
                    // No elements found - show InfoDialog
                    InfoDialog infoDialog = new InfoDialog($"No Pipes or Ducts found in the {(entireModelScope ? "Entire Model" : "Current View")}.")
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    infoDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist, check if parameters are selected
                if (!selectedMEPInsulationParamNames.Any())
                {
                    NoParameterDialog noParameterDialog = new NoParameterDialog()
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    noParameterDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Elements exist and parameters selected - perform operation
                using (Transaction t = new Transaction(doc, $"Copy Selected Parameters to MEP Insulation Elements"))
                {
                    t.Start();

                    // Process pipes if selected
                    if (copyToPipeInsulation)
                    {
                        foreach (Pipe pipe in pipes)
                        {
                            // Process each selected parameter
                            foreach (string selectedMEPParamName in selectedMEPInsulationParamNames)
                            {
                                // Get parameter value from pipe (either instance or type)
                                string parameterValue = Utils.GetParameterValueFromElementOrType(pipe, selectedMEPParamName);

                                if (!string.IsNullOrEmpty(parameterValue))
                                {
                                    // Get pipe insulation
                                    List<Element> pipeInsulations = GetPipeInsulation(doc, pipe);

                                    foreach (Element insulation in pipeInsulations)
                                    {
                                        Utils.SetParameterValueString(insulation, selectedMEPParamName, parameterValue);
                                    }
                                }
                                else
                                {
                                    Debug.Print($"Parameter '{selectedMEPParamName}' not found or has no value on pipe {pipe.Id}");
                                }
                            }
                        }
                    }

                    // Process ducts if selected
                    if (copyToDuctInsulation)
                    {
                        foreach (Duct duct in ducts)
                        {
                            // Process each selected parameter
                            foreach (string selectedMEPParamName in selectedMEPInsulationParamNames)
                            {
                                // Get parameter value from duct (either instance or type)
                                string parameterValue = Utils.GetParameterValueFromElementOrType(duct, selectedMEPParamName);

                                if (!string.IsNullOrEmpty(parameterValue))
                                {
                                    // Get duct insulation
                                    List<Element> ductInsulations = GetDuctInsulation(doc, duct);

                                    foreach (Element insulation in ductInsulations)
                                    {
                                        Utils.SetParameterValueString(insulation, selectedMEPParamName, parameterValue);
                                    }
                                }
                                else
                                {
                                    Debug.Print($"Parameter '{selectedMEPParamName}' not found or has no value on duct {duct.Id}");
                                }
                            }
                        }
                    }

                    t.Commit();
                    operationCompleted = true;
                }
            }
            else if (currentForm.NestedFamiliesContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Check for Nested Families first - find families with nested families in the model/view
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                if (!entireModelScope)
                {
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                }

                var allFamilyInstances = collector
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                // Find parent families that have nested families
                var parentFamiliesWithNested = new List<FamilyInstance>();
                foreach (var instance in allFamilyInstances)
                {
                    var subComponentIds = instance.GetSubComponentIds();
                    if (subComponentIds != null && subComponentIds.Count > 0)
                    {
                        parentFamiliesWithNested.Add(instance);
                    }
                }

                if (!parentFamiliesWithNested.Any())
                {
                    // No parent families with nested families found - show InfoDialog
                    InfoDialog infoDialog = new InfoDialog($"No Parent families with nested families found in the {(entireModelScope ? "Entire Model" : "Current View")}.")
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    infoDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Parent families with nested families exist, check if selections are made
                if (string.IsNullOrEmpty(selectedParentFamilyName) || !selectedNestedFamilyParamNames.Any() || !selectedNestedFamilyNames.Any())
                {
                    NoParameterDialog noParameterDialog = new NoParameterDialog()
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    noParameterDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Now find specific instances of the selected parent family
                var selectedParentInstances = parentFamiliesWithNested
                    .Where(fi => fi.Symbol.Family.Name == selectedParentFamilyName)
                    .ToList();

                if (!selectedParentInstances.Any())
                {
                    // Selected parent family not found - show InfoDialog
                    InfoDialog infoDialog = new InfoDialog($"No Instances of parent family '{selectedParentFamilyName}' found in the {(entireModelScope ? "Entire Model" : "Current View")}.")
                    {
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        Topmost = true
                    };
                    infoDialog.ShowDialog();
                    return Result.Succeeded;
                }

                // Everything is selected and available - perform operation
                using (Transaction t = new Transaction(doc, $"Copy Selected Parameters to Nested Family Elements"))
                {
                    t.Start();

                    foreach (var parentInstance in selectedParentInstances)
                    {
                        // Get sub-components (nested families) from parent family instance
                        var subComponentIds = parentInstance.GetSubComponentIds();

                        if (subComponentIds != null && subComponentIds.Count > 0)
                        {
                            foreach (ElementId subId in subComponentIds)
                            {
                                Element subElement = doc.GetElement(subId);
                                if (subElement is FamilyInstance nestedInstance)
                                {
                                    // Check if this nested family is one of the selected ones
                                    if (selectedNestedFamilyNames.Contains(nestedInstance.Symbol.Family.Name))
                                    {
                                        // Process each selected parameter
                                        foreach (string selectedNestedFamilyParamName in selectedNestedFamilyParamNames)
                                        {
                                            // Get parameter value from parent family (either instance or type)
                                            string parameterValue = Utils.GetParameterValueFromElementOrType(parentInstance, selectedNestedFamilyParamName);

                                            if (!string.IsNullOrEmpty(parameterValue))
                                            {
                                                // Apply the parameter value to the nested family instance
                                                Utils.SetParameterValueString(nestedInstance, selectedNestedFamilyParamName, parameterValue);
                                            }
                                            else
                                            {
                                                Debug.Print($"Parameter '{selectedNestedFamilyParamName}' not found or has no value on parent family {parentInstance.Id}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    t.Commit();
                    operationCompleted = true;
                }
            }

            // Show success dialog if operation completed successfully
            if (operationCompleted)
            {
                SuccessDialog successDialog = new SuccessDialog("Model Updated Successfully")
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Topmost = true
                };
                successDialog.ShowDialog();
            }

            return Result.Succeeded;
        }

        // Helper methods for MEP Insulation
        private static List<Element> GetPipeInsulation(Document doc, Pipe pipe)
        {
            List<Element> insulationElements = new List<Element>();

            try
            {
                // Get pipe insulation by finding elements in the Pipe Insulation category
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_PipeInsulations)
                    .WhereElementIsNotElementType();

                // Get pipe location for spatial comparison
                LocationCurve pipeLocation = pipe.Location as LocationCurve;
                if (pipeLocation == null) return insulationElements;

                XYZ pipeStart = pipeLocation.Curve.GetEndPoint(0);
                XYZ pipeEnd = pipeLocation.Curve.GetEndPoint(1);
                XYZ pipeMidpoint = (pipeStart + pipeEnd) / 2;

                foreach (Element insulation in collector)
                {
                    // Check if this insulation is spatially close to our pipe
                    BoundingBoxXYZ insulationBB = insulation.get_BoundingBox(null);
                    if (insulationBB != null)
                    {
                        XYZ insulationCenter = (insulationBB.Min + insulationBB.Max) / 2;
                        double distance = pipeMidpoint.DistanceTo(insulationCenter);

                        // If insulation is within reasonable distance (adjust as needed)
                        if (distance < 5.0) // 5 feet tolerance
                        {
                            // Additional check: see if insulation parameters reference the pipe
                            foreach (Parameter param in insulation.Parameters)
                            {
                                if (param.StorageType == StorageType.ElementId)
                                {
                                    ElementId refId = param.AsElementId();
                                    if (refId == pipe.Id)
                                    {
                                        insulationElements.Add(insulation);
                                        break;
                                    }
                                }
                            }

                            // If no direct reference found but spatially close, include it
                            if (!insulationElements.Contains(insulation) && distance < 1.0) // 1 foot for very close
                            {
                                insulationElements.Add(insulation);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error getting pipe insulation for pipe {pipe.Id}: {ex.Message}");
            }

            return insulationElements;
        }

        private static List<Element> GetDuctInsulation(Document doc, Duct duct)
        {
            List<Element> insulationElements = new List<Element>();

            try
            {
                // Get duct insulation by finding elements in the Duct Insulation category
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DuctInsulations)
                    .WhereElementIsNotElementType();

                // Get duct location for spatial comparison
                LocationCurve ductLocation = duct.Location as LocationCurve;
                if (ductLocation == null) return insulationElements;

                XYZ ductStart = ductLocation.Curve.GetEndPoint(0);
                XYZ ductEnd = ductLocation.Curve.GetEndPoint(1);
                XYZ ductMidpoint = (ductStart + ductEnd) / 2;

                foreach (Element insulation in collector)
                {
                    // Check if this insulation is spatially close to our duct
                    BoundingBoxXYZ insulationBB = insulation.get_BoundingBox(null);
                    if (insulationBB != null)
                    {
                        XYZ insulationCenter = (insulationBB.Min + insulationBB.Max) / 2;
                        double distance = ductMidpoint.DistanceTo(insulationCenter);

                        // If insulation is within reasonable distance (adjust as needed)
                        if (distance < 5.0) // 5 feet tolerance
                        {
                            // Additional check: see if insulation parameters reference the duct
                            foreach (Parameter param in insulation.Parameters)
                            {
                                if (param.StorageType == StorageType.ElementId)
                                {
                                    ElementId refId = param.AsElementId();
                                    if (refId == duct.Id)
                                    {
                                        insulationElements.Add(insulation);
                                        break;
                                    }
                                }
                            }

                            // If no direct reference found but spatially close, include it
                            if (!insulationElements.Contains(insulation) && distance < 1.0) // 1 foot for very close
                            {
                                insulationElements.Add(insulation);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error getting duct insulation for duct {duct.Id}: {ex.Message}");
            }

            return insulationElements;
        }

        internal static PushButtonData GetButtonData()
        {
            // use this method to define the properties for this command in the Revit ribbon
            string buttonInternalName = "btnCommand1";
            string buttonTitle = "Button 1";

            Common.ButtonDataClass myButtonData = new Common.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "This is a tooltip for Button 1");

            return myButtonData.Data;
        }
    }
}