namespace test.Common
{
    internal static class Utils
    {
        public static void SetParameterValueString(Element curElem, string paramName, string value)
        {
            // Try to find parameter on the element (instance)
            Parameter curParam = curElem.LookupParameter(paramName);

            // If not found on instance, try to find on type
            if (curParam == null)
            {
                ElementId typeId = curElem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element elementType = curElem.Document.GetElement(typeId);
                    if (elementType != null)
                    {
                        curParam = elementType.LookupParameter(paramName);
                    }
                }
            }

            if (curParam != null && !curParam.IsReadOnly)
            {
                try
                {
                    curParam.Set(value);
                }
                catch (Exception ex)
                {
                    // Log the error but continue processing other parameters
                    Debug.Print($"Failed to set parameter '{paramName}' on element {curElem.Id}: {ex.Message}");
                }
            }
        }

        public static string GetParameterValueAsString(Element curElem, string paramName)
        {
            Parameter curParam = curElem.LookupParameter(paramName);
            if (curParam != null)
            {
                return curParam.AsString();
            }
            else
                return "";
        }

        // New method to get parameter value from either instance or type
        public static string GetParameterValueFromElementOrType(Element element, string paramName)
        {
            // Try instance first
            Parameter param = element.LookupParameter(paramName);

            // If not found on instance, try type
            if (param == null)
            {
                ElementId typeId = element.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element elementType = element.Document.GetElement(typeId);
                    if (elementType != null)
                    {
                        param = elementType.LookupParameter(paramName);
                    }
                }
            }

            if (param != null)
            {
                if (param.StorageType == StorageType.String)
                {
                    return param.AsString() ?? "";
                }
                else if (param.StorageType == StorageType.Integer)
                {
                    return param.AsInteger().ToString();
                }
                else if (param.StorageType == StorageType.Double)
                {
                    return param.AsDouble().ToString();
                }
                else if (param.StorageType == StorageType.ElementId)
                {
                    ElementId elemId = param.AsElementId();
                    if (elemId != ElementId.InvalidElementId)
                    {
                        Element referencedElement = element.Document.GetElement(elemId);
                        return referencedElement?.Name ?? elemId.ToString();
                    }
                    return elemId.ToString();
                }
            }

            return "";
        }

        internal static List<Category> GetAllCategories(Document doc)
        {
            List<Category> categories = new List<Category>();

            foreach (Category curCat in doc.Settings.Categories)
            {
                categories.Add(curCat);
            }

            List<Category> sortedList = categories.OrderBy(x => x.Name).ToList();

            return sortedList;
        }

        internal static Category GetCategoryByName(Document doc, string categoryName)
        {
            List<Category> categories = GetAllCategories(doc);

            foreach (Category curCat in categories)
            {
                if (curCat.Name == categoryName)
                    return curCat;
            }

            return null;
        }

        internal static List<ElementType> GetElementTypesByName(Document doc, string categoryName, List<string> typeNames)
        {
            List<ElementType> types = new List<ElementType>();

            foreach (string type in typeNames)
            {
                ElementType currentType = GetElementTypeByName(doc, categoryName, type);

                if (currentType != null)
                    types.Add(currentType);
            }

            return types;
        }

        private static ElementType GetElementTypeByName(Document doc, string categoryName, string name)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(ElementType));

            foreach (ElementType curType in collector)
            {
                if (curType.Name == name && curType.Category.Name == categoryName)
                    return curType;
            }

            return null;
        }
        internal static List<Parameter> GetAllParmatersFromElement(Element curInstance)
        {
            List<Parameter> returnList = new List<Parameter>();

            foreach (Parameter curParam in curInstance.Parameters)
            {
                returnList.Add(curParam);
            }

            return returnList;
        }

        internal static Parameter GetParameterByName(Document doc, string categoryName, string typeName, string paramName)
        {
            ElementType curType = GetElementTypeByName(doc, categoryName, typeName);
            Parameter curParam = curType.GetParameters(paramName).FirstOrDefault();

            if (curParam != null)
                return curParam;

            return null;
        }

        internal static List<Parameter> GetParametersByName(Document doc, string categoryName, List<string> typeNames, string paramName)
        {
            List<Parameter> returnList = new List<Parameter>();

            foreach (string typeName in typeNames)
            {
                Parameter curParam = GetParameterByName(doc, categoryName, typeName, paramName);

                if (curParam != null)
                    returnList.Add(curParam);
            }

            return returnList;
        }
        internal static RibbonPanel CreateRibbonPanel(UIControlledApplication app, string tabName, string panelName)
        {
            RibbonPanel curPanel;

            if (GetRibbonPanelByName(app, tabName, panelName) == null)
                curPanel = app.CreateRibbonPanel(tabName, panelName);

            else
                curPanel = GetRibbonPanelByName(app, tabName, panelName);

            return curPanel;
        }

        internal static RibbonPanel GetRibbonPanelByName(UIControlledApplication app, string tabName, string panelName)
        {
            foreach (RibbonPanel tmpPanel in app.GetRibbonPanels(tabName))
            {
                if (tmpPanel.Name == panelName)
                    return tmpPanel;
            }

            return null;
        }
    }
}
