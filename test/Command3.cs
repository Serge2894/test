using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Plumbing;
using test.Common;
using test.Forms;

namespace test
{
    [Transaction(TransactionMode.Manual)]
    public class Command3 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Open form to configure parameter mapping
            Window3 currentForm = new Window3(doc)
            {
                Width = 900,
                Height = 500,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Topmost = true,
            };

            bool? dialogResult = currentForm.ShowDialog();

            // Check if user clicked OK
            if (dialogResult != true)
            {
                return Result.Cancelled;
            }

            // Get form data
            string sourceParameterName = currentForm.SelectedSourceParameter;
            string targetParameterName = currentForm.SelectedTargetParameter;
            List<string> selectedCategories = currentForm.SelectedCategories;

            // Validate selections
            if (string.IsNullOrEmpty(sourceParameterName))
            {
                ShowInfoDialog("No Room Parameter is selected");
                return Result.Succeeded;
            }

            if (!selectedCategories.Any())
            {
                ShowInfoDialog("No category is selected");
                return Result.Succeeded;
            }

            if (string.IsNullOrEmpty(targetParameterName))
            {
                ShowInfoDialog("No target parameter is selected");
                return Result.Succeeded;
            }

            // Get all rooms in the entire model (host and linked)
            Dictionary<Document, List<Room>> roomsByDocument = new Dictionary<Document, List<Room>>();

            // First, get rooms from the host model
            FilteredElementCollector hostRoomCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .WhereElementIsNotElementType();

            List<Room> hostRooms = hostRoomCollector
                .Cast<SpatialElement>()
                .OfType<Room>()
                .Where(r => r.Area > 0) // Only consider placed rooms
                .ToList();

            if (hostRooms.Any())
            {
                roomsByDocument[doc] = hostRooms;
            }

            // Then, get rooms from linked models
            FilteredElementCollector linkCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance));

            foreach (RevitLinkInstance linkInstance in linkCollector)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) continue;

                FilteredElementCollector linkedRoomCollector = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(SpatialElement))
                    .WhereElementIsNotElementType();

                List<Room> linkedRooms = linkedRoomCollector
                    .Cast<SpatialElement>()
                    .OfType<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (linkedRooms.Any())
                {
                    roomsByDocument[linkDoc] = linkedRooms;
                }
            }

            if (!roomsByDocument.Any())
            {
                ShowInfoDialog("No room/s found in the model/linked model");
                return Result.Succeeded;
            }

            // Store link transforms for later use
            Dictionary<Document, Transform> linkTransforms = new Dictionary<Document, Transform>();
            foreach (RevitLinkInstance linkInstance in linkCollector)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc != null)
                {
                    linkTransforms[linkDoc] = linkInstance.GetTotalTransform();
                }
            }

            // Start transaction for host document
            using (Transaction t = new Transaction(doc, "Copy Room Parameters to Elements"))
            {
                t.Start();

                int totalRoomsProcessed = 0;
                int totalElementsProcessed = 0;
                int totalElementsUpdated = 0;
                List<string> errors = new List<string>();

                // Process rooms from each document
                foreach (var roomDocPair in roomsByDocument)
                {
                    Document roomDoc = roomDocPair.Key;
                    List<Room> rooms = roomDocPair.Value;
                    bool isLinkedRoomDoc = (roomDoc != doc);
                    Transform roomTransform = isLinkedRoomDoc && linkTransforms.ContainsKey(roomDoc)
                        ? linkTransforms[roomDoc]
                        : Transform.Identity;

                    foreach (Room room in rooms)
                    {
                        totalRoomsProcessed++;

                        // Get the source parameter value from the room
                        string sourceValue = Utils.GetParameterValueFromElementOrType(room, sourceParameterName);

                        if (string.IsNullOrEmpty(sourceValue))
                        {
                            continue; // Skip rooms without the source parameter value
                        }

                        // Get the source parameter to check its type
                        Parameter sourceParam = room.LookupParameter(sourceParameterName);
                        if (sourceParam == null)
                        {
                            // Try type parameter
                            ElementId typeId = room.GetTypeId();
                            if (typeId != ElementId.InvalidElementId)
                            {
                                Element roomType = room.Document.GetElement(typeId);
                                if (roomType != null)
                                {
                                    sourceParam = roomType.LookupParameter(sourceParameterName);
                                }
                            }
                        }

                        if (sourceParam == null)
                        {
                            continue;
                        }

                        // If room is in a linked model, we need to transform its location
                        if (isLinkedRoomDoc)
                        {
                            // Get elements in host model that are inside this linked room
                            List<Element> hostElements = GetHostElementsInLinkedRoom(doc, room, roomTransform, selectedCategories);

                            foreach (Element elem in hostElements)
                            {
                                totalElementsProcessed++;
                                try
                                {
                                    SetParameterValueWithTypeConversion(elem, targetParameterName, sourceValue, sourceParam.StorageType);
                                    totalElementsUpdated++;
                                }
                                catch (Exception ex)
                                {
                                    errors.Add($"Failed to update element {elem.Id}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            // Room is in host model - use original logic
                            List<Element> elementsInRoom = GetElementsInRoom(doc, room, selectedCategories);

                            foreach (Element elem in elementsInRoom)
                            {
                                totalElementsProcessed++;
                                try
                                {
                                    SetParameterValueWithTypeConversion(elem, targetParameterName, sourceValue, sourceParam.StorageType);
                                    totalElementsUpdated++;
                                }
                                catch (Exception ex)
                                {
                                    errors.Add($"Failed to update element {elem.Id}: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                // Commit transaction
                t.Commit();

                // Show results
                if (totalElementsUpdated > 0)
                {
                    ShowSuccessDialog("Model Updated Successfully");
                }
                else
                {
                    string resultMessage = $"Process completed:\n" +
                                         $"- Rooms processed: {totalRoomsProcessed}\n" +
                                         $"- Elements found: {totalElementsProcessed}\n" +
                                         $"- Elements updated: {totalElementsUpdated}";

                    if (errors.Any())
                    {
                        resultMessage += $"\n- Errors: {errors.Count}\n\n" +
                                       string.Join("\n", errors.Take(5));
                        if (errors.Count > 5)
                        {
                            resultMessage += $"\n... and {errors.Count - 5} more errors.";
                        }
                    }

                    ShowInfoDialog(resultMessage);
                }
            }

            return Result.Succeeded;
        }

        private List<Element> GetElementsInRoom(Document doc, Room room, List<string> categoryNames)
        {
            List<Element> elementsInRoom = new List<Element>();

            // Get room bounding box
            BoundingBoxXYZ roomBB = room.get_BoundingBox(null);
            if (roomBB == null) return elementsInRoom;

            // Create outline from room bounding box
            Outline roomOutline = new Outline(roomBB.Min, roomBB.Max);

            // Create bounding box filter
            BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(roomOutline);

            foreach (string categoryName in categoryNames)
            {
                Category category = Utils.GetCategoryByName(doc, categoryName);
                if (category == null) continue;

                // Create collector for entire model
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfCategoryId(category.Id)
                    .WhereElementIsNotElementType()
                    .WherePasses(bbFilter);

                // Check if elements are actually inside the room
                foreach (Element elem in collector)
                {
                    LocationPoint locPoint = elem.Location as LocationPoint;
                    if (locPoint != null)
                    {
                        // For point-based elements, check if point is in room
                        if (room.IsPointInRoom(locPoint.Point))
                        {
                            elementsInRoom.Add(elem);
                        }
                    }
                    else
                    {
                        // For curve-based or other elements, check if center point is in room
                        BoundingBoxXYZ elemBB = elem.get_BoundingBox(null);
                        if (elemBB != null)
                        {
                            XYZ centerPoint = (elemBB.Min + elemBB.Max) / 2;
                            if (room.IsPointInRoom(centerPoint))
                            {
                                elementsInRoom.Add(elem);
                            }
                        }
                    }
                }
            }

            return elementsInRoom;
        }

        private List<Element> GetHostElementsInLinkedRoom(Document hostDoc, Room linkedRoom, Transform linkTransform, List<string> categoryNames)
        {
            List<Element> elementsInRoom = new List<Element>();

            // Get room bounding box in linked coordinates
            BoundingBoxXYZ roomBB = linkedRoom.get_BoundingBox(null);
            if (roomBB == null) return elementsInRoom;

            // Transform the bounding box to host coordinates
            XYZ minTransformed = linkTransform.OfPoint(roomBB.Min);
            XYZ maxTransformed = linkTransform.OfPoint(roomBB.Max);

            // Create new bounding box in host coordinates
            BoundingBoxXYZ transformedBB = new BoundingBoxXYZ();
            transformedBB.Min = new XYZ(
                Math.Min(minTransformed.X, maxTransformed.X),
                Math.Min(minTransformed.Y, maxTransformed.Y),
                Math.Min(minTransformed.Z, maxTransformed.Z)
            );
            transformedBB.Max = new XYZ(
                Math.Max(minTransformed.X, maxTransformed.X),
                Math.Max(minTransformed.Y, maxTransformed.Y),
                Math.Max(minTransformed.Z, maxTransformed.Z)
            );

            // Create outline from transformed bounding box
            Outline roomOutline = new Outline(transformedBB.Min, transformedBB.Max);
            BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(roomOutline);

            foreach (string categoryName in categoryNames)
            {
                Category category = Utils.GetCategoryByName(hostDoc, categoryName);
                if (category == null) continue;

                // Get elements in host document
                FilteredElementCollector collector = new FilteredElementCollector(hostDoc)
                    .OfCategoryId(category.Id)
                    .WhereElementIsNotElementType()
                    .WherePasses(bbFilter);

                foreach (Element elem in collector)
                {
                    // Get element location in host coordinates
                    XYZ elementPoint = null;
                    LocationPoint locPoint = elem.Location as LocationPoint;

                    if (locPoint != null)
                    {
                        elementPoint = locPoint.Point;
                    }
                    else
                    {
                        BoundingBoxXYZ elemBB = elem.get_BoundingBox(null);
                        if (elemBB != null)
                        {
                            elementPoint = (elemBB.Min + elemBB.Max) / 2;
                        }
                    }

                    if (elementPoint != null)
                    {
                        // Transform element point to linked room coordinate system
                        XYZ transformedElementPoint = linkTransform.Inverse.OfPoint(elementPoint);

                        // Check if transformed point is in the linked room
                        if (linkedRoom.IsPointInRoom(transformedElementPoint))
                        {
                            elementsInRoom.Add(elem);
                        }
                    }
                }
            }

            return elementsInRoom;
        }

        private void SetParameterValueWithTypeConversion(Element elem, string paramName, string sourceValue, StorageType sourceType)
        {
            // Get target parameter from element (instance or type)
            Parameter targetParam = elem.LookupParameter(paramName);

            if (targetParam == null)
            {
                // Try type parameter
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element elemType = elem.Document.GetElement(typeId);
                    if (elemType != null)
                    {
                        targetParam = elemType.LookupParameter(paramName);
                    }
                }
            }

            if (targetParam == null || targetParam.IsReadOnly)
            {
                throw new Exception($"Parameter '{paramName}' not found or is read-only");
            }

            // Perform type conversion based on source and target types
            switch (sourceType)
            {
                case StorageType.String:
                    // String can only go to String
                    if (targetParam.StorageType == StorageType.String)
                    {
                        targetParam.Set(sourceValue);
                    }
                    else
                    {
                        throw new Exception($"Cannot convert String to {targetParam.StorageType}");
                    }
                    break;

                case StorageType.Integer:
                    int intValue = int.Parse(sourceValue);

                    if (targetParam.StorageType == StorageType.String)
                    {
                        targetParam.Set(sourceValue);
                    }
                    else if (targetParam.StorageType == StorageType.Double)
                    {
                        targetParam.Set((double)intValue);
                    }
                    else if (targetParam.StorageType == StorageType.Integer)
                    {
                        targetParam.Set(intValue);
                    }
                    else
                    {
                        throw new Exception($"Cannot convert Integer to {targetParam.StorageType}");
                    }
                    break;

                case StorageType.Double:
                    double doubleValue = double.Parse(sourceValue);

                    if (targetParam.StorageType == StorageType.String)
                    {
                        targetParam.Set(sourceValue);
                    }
                    else if (targetParam.StorageType == StorageType.Double)
                    {
                        targetParam.Set(doubleValue);
                    }
                    else if (targetParam.StorageType == StorageType.Integer)
                    {
                        throw new Exception("Cannot convert Double to Integer");
                    }
                    else
                    {
                        throw new Exception($"Cannot convert Double to {targetParam.StorageType}");
                    }
                    break;

                default:
                    throw new Exception($"Unsupported source parameter type: {sourceType}");
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

        internal static PushButtonData GetButtonData()
        {
            // use this method to define the properties for this command in the Revit ribbon
            string buttonInternalName = "btnCommand3";
            string buttonTitle = "Room to Elements";

            Common.ButtonDataClass myButtonData = new Common.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "Copy parameter values from rooms to all elements inside those rooms");

            return myButtonData.Data;
        }
    }
}