using Autodesk.Revit.DB.Architecture;
using test.Common;
using test.Forms;

namespace test
{
    [Transaction(TransactionMode.Manual)]
    public class Command2 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            List<Category> categories = Utils.GetAllCategories(doc);

            // Use the static method to show modeless window
            Window2.ShowModeless(doc, categories);

            return Result.Succeeded;
        }

        internal static PushButtonData GetButtonData()
        {
            // use this method to define the properties for this command in the Revit ribbon
            string buttonInternalName = "btnCommand2";
            string buttonTitle = "Button 2";

            Common.ButtonDataClass myButtonData = new Common.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "This is a tooltip for Button 2");

            return myButtonData.Data;
        }
    }

    // External event handler to process the parameter updates in Revit context
    public class ParameterUpdateHandler : IExternalEventHandler
    {
        private Window2 _window;
        private Document _doc;

        public void SetData(Window2 window, Document doc)
        {
            _window = window;
            _doc = doc;
        }

        public void Execute(UIApplication app)
        {
            if (_window == null || _doc == null)
                return;

            try
            {
                ProcessWindowData();
            }
            catch (Exception ex)
            {
                ShowInfoDialog($"An error occurred: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Parameter Update Handler";
        }

        private void ProcessWindowData()
        {
            // Get the scope information from the form
            bool entireModelScope = _window.IsEntireModelChecked;
            string scopeDescription = entireModelScope ? "Entire Model" : "Active View";

            // Get selected categories and types from the form
            List<string> selectedCategoryNames = _window.SelectedCategoryNames;
            List<string> selectedTypeNames = _window.SelectedTypeNames;

            if (!selectedCategoryNames.Any())
            {
                ShowInfoDialog("No category is selected.");
                return;
            }

            if (!selectedTypeNames.Any())
            {
                ShowInfoDialog("No type is selected.");
                return;
            }

            // Get parameters using scope-aware method
            List<Parameter> paramList = GetParametersByNameWithScope(_doc, selectedCategoryNames,
                selectedTypeNames, _window.ParamName, entireModelScope);

            if (paramList.Count > 0)
            {
                string newValue = _window.GetNewValue();

                // Validate the input value based on parameter type
                string validationError = ValidateParameterValue(newValue, _window.ParamDataType);

                if (!string.IsNullOrEmpty(validationError))
                {
                    // Show error dialog for incorrect parameter type
                    ShowInfoDialog(validationError);
                    return;
                }

                Transaction t = new Transaction(_doc);
                t.Start($"Set type parameters - {scopeDescription}");

                int successCount = 0;
                List<string> errorMessages = new List<string>();

                foreach (Parameter param in paramList)
                {
                    try
                    {
                        if (_window.ParamDataType == "double")
                        {
                            double paramDouble = Convert.ToDouble(newValue);
                            param.Set(paramDouble);
                            successCount++;
                        }
                        else if (_window.ParamDataType == "integer")
                        {
                            int paramInt = Convert.ToInt32(newValue);
                            param.Set(paramInt);
                            successCount++;
                        }
                        else if (_window.ParamDataType == "string")
                        {
                            param.Set(newValue);
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Failed to set parameter on element {param.Element?.Id}: {ex.Message}");
                    }
                }

                t.Commit();
                t.Dispose();

                // Show results
                if (errorMessages.Any())
                {
                    string errorSummary = $"Completed with errors:\n" +
                                        $"Successfully updated: {successCount} parameters\n" +
                                        $"Errors: {errorMessages.Count}\n\n" +
                                        string.Join("\n", errorMessages.Take(5)); // Show first 5 errors

                    if (errorMessages.Count > 5)
                    {
                        errorSummary += $"\n... and {errorMessages.Count - 5} more errors.";
                    }

                    ShowInfoDialog(errorSummary);
                }
                else
                {
                    // Show success dialog
                    ShowSuccessDialog("Model updated successfully.");
                }
            }
            else
            {
                ShowInfoDialog("No parameter is selected.");
            }
        }

        private static void ShowInfoDialog(string message)
        {
            InfoDialog infoDialog = new InfoDialog(message)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Topmost = true
            };
            infoDialog.ShowDialog();
        }

        private static void ShowSuccessDialog(string message)
        {
            SuccessDialog successDialog = new SuccessDialog(message)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Topmost = true
            };
            successDialog.ShowDialog();
        }

        /// <summary>
        /// Validates the input value against the expected parameter type
        /// </summary>
        /// <param name="value">The input value to validate</param>
        /// <param name="paramDataType">The expected parameter data type</param>
        /// <returns>Error message if validation fails, empty string if validation passes</returns>
        private static string ValidateParameterValue(string value, string paramDataType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Please enter a parameter value.";
            }

            switch (paramDataType?.ToLower())
            {
                case "double":
                case "decimal":
                    if (!double.TryParse(value, out _))
                    {
                        return $"Please insert the correct parameter type.";
                    }
                    break;

                case "integer":
                case "int":
                    if (!int.TryParse(value, out _))
                    {
                        return $"Please insert the correct parameter type.";
                    }
                    break;

                case "string":
                case "text":
                    // String values are always valid
                    break;

                default:
                    // If we don't know the type, allow it but warn
                    Debug.Print($"Unknown parameter data type: {paramDataType}");
                    break;
            }

            return string.Empty; // Validation passed
        }

        private static List<Parameter> GetParametersByNameWithScope(Document doc, List<string> categoryNames,
            List<string> typeNames, string paramName, bool entireModel)
        {
            List<Parameter> returnList = new List<Parameter>();

            foreach (string categoryName in categoryNames)
            {
                Category currentCat = Utils.GetCategoryByName(doc, categoryName);

                if (currentCat != null)
                {
                    FilteredElementCollector collector;

                    if (entireModel)
                    {
                        collector = new FilteredElementCollector(doc);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                    }

                    // Get element INSTANCES in the category and scope
                    collector.OfCategoryId(currentCat.Id);
                    collector.WhereElementIsNotElementType();

                    var instances = collector.ToList();

                    // Find parameters from element types and instances that match our type names
                    foreach (Element instance in instances)
                    {
                        ElementId typeId = instance.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = doc.GetElement(typeId);
                            if (elementType != null && typeNames.Contains(elementType.Name))
                            {
                                // Check instance parameters first
                                Parameter instanceParam = instance.GetParameters(paramName).FirstOrDefault();
                                if (instanceParam != null &&
                                    !returnList.Any(p => p.Id == instanceParam.Id) &&
                                    (instanceParam.StorageType == StorageType.String ||
                                     instanceParam.StorageType == StorageType.Double ||
                                     instanceParam.StorageType == StorageType.Integer))
                                {
                                    returnList.Add(instanceParam);
                                }

                                // Then check type parameters
                                Parameter typeParam = elementType.GetParameters(paramName).FirstOrDefault();
                                if (typeParam != null &&
                                    !returnList.Any(p => p.Id == typeParam.Id) &&
                                    (typeParam.StorageType == StorageType.String ||
                                     typeParam.StorageType == StorageType.Double ||
                                     typeParam.StorageType == StorageType.Integer))
                                {
                                    returnList.Add(typeParam);
                                }
                            }
                        }
                    }
                }
            }

            return returnList;
        }
    }
}