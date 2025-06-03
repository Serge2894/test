using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using test.Common;

namespace test.Forms
{
    /// <summary>
    /// Interaction logic for Window3.xaml
    /// </summary>
    public partial class Window3 : Window, INotifyPropertyChanged
    {
        private Document _doc;

        private ObservableCollection<Window3ParameterItem> _sourceParameterItems;
        public ObservableCollection<Window3ParameterItem> SourceParameterItems
        {
            get { return _sourceParameterItems; }
            set
            {
                _sourceParameterItems = value;
                OnPropertyChanged(nameof(SourceParameterItems));
            }
        }

        private ObservableCollection<Window3ParameterItem> _targetParameterItems;
        public ObservableCollection<Window3ParameterItem> TargetParameterItems
        {
            get { return _targetParameterItems; }
            set
            {
                _targetParameterItems = value;
                OnPropertyChanged(nameof(TargetParameterItems));
            }
        }

        private ObservableCollection<CategoryItem> _categoryItems;
        public ObservableCollection<CategoryItem> CategoryItems
        {
            get { return _categoryItems; }
            set
            {
                _categoryItems = value;
                OnPropertyChanged(nameof(CategoryItems));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _hasRooms = false;
        public bool HasRooms
        {
            get { return _hasRooms; }
            set
            {
                _hasRooms = value;
                OnPropertyChanged(nameof(HasRooms));
            }
        }

        private StorageType? _selectedSourceParameterType = null;

        public Window3(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            DataContext = this;

            SourceParameterItems = new ObservableCollection<Window3ParameterItem>();
            TargetParameterItems = new ObservableCollection<Window3ParameterItem>();
            CategoryItems = new ObservableCollection<CategoryItem>();

            LoadRoomParameters();
            LoadCategories();
        }

        private void LoadRoomParameters()
        {
            // Get all rooms from host and linked models
            List<Room> allRooms = new List<Room>();

            // Get rooms from host model
            FilteredElementCollector roomCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(SpatialElement))
                .WhereElementIsNotElementType();

            allRooms.AddRange(roomCollector.Cast<SpatialElement>().OfType<Room>().Where(r => r.Area > 0));

            // Get rooms from linked models
            FilteredElementCollector linkCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance));

            foreach (RevitLinkInstance linkInstance in linkCollector)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) continue;

                FilteredElementCollector linkedRoomCollector = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(SpatialElement))
                    .WhereElementIsNotElementType();

                allRooms.AddRange(linkedRoomCollector.Cast<SpatialElement>().OfType<Room>().Where(r => r.Area > 0));
            }

            if (!allRooms.Any())
            {
                HasRooms = false;
                return;
            }

            HasRooms = true;

            // Get parameters from the first room found
            Room sampleRoom = allRooms.First();
            List<Parameter> allParameters = new List<Parameter>();

            // Get instance parameters
            allParameters.AddRange(Utils.GetAllParmatersFromElement(sampleRoom));

            // Get type parameters
            ElementId typeId = sampleRoom.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element roomType = sampleRoom.Document.GetElement(typeId);
                if (roomType != null)
                {
                    allParameters.AddRange(Utils.GetAllParmatersFromElement(roomType));
                }
            }

            // Filter out read-only parameters
            HashSet<string> excludedParameters = new HashSet<string>
            {
                "Phase Created",
                "Phase Demolished",
            };

            List<Parameter> filteredParams = allParameters
                .Where(p => !p.IsReadOnly &&
                           !excludedParameters.Contains(p.Definition.Name) &&
                           (p.StorageType == StorageType.String ||
                            p.StorageType == StorageType.Double ||
                            p.StorageType == StorageType.Integer))
                .GroupBy(p => p.Definition.Name)
                .Select(g => g.First())
                .OrderBy(p => p.Definition.Name)
                .ToList();

            // Populate source parameter list
            SourceParameterItems.Clear();
            foreach (Parameter param in filteredParams)
            {
                SourceParameterItems.Add(new Window3ParameterItem(param));
            }

            // Set ListView source
            lvSourceParameters.ItemsSource = SourceParameterItems;
        }

        private void LoadCategories()
        {
            // Show loading message
            CategoryItems.Clear();

            // First check if there are any rooms
            List<Room> allRooms = new List<Room>();
            Dictionary<Room, Transform> roomTransforms = new Dictionary<Room, Transform>();

            // Get rooms from host model
            FilteredElementCollector roomCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(SpatialElement))
                .WhereElementIsNotElementType();

            var hostRooms = roomCollector.Cast<SpatialElement>().OfType<Room>().Where(r => r.Area > 0).ToList();
            foreach (var room in hostRooms)
            {
                allRooms.Add(room);
                roomTransforms[room] = Transform.Identity;
            }

            // Get rooms from linked models
            FilteredElementCollector linkCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance));

            foreach (RevitLinkInstance linkInstance in linkCollector)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) continue;

                Transform linkTransform = linkInstance.GetTotalTransform();

                FilteredElementCollector linkedRoomCollector = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(SpatialElement))
                    .WhereElementIsNotElementType();

                var linkedRooms = linkedRoomCollector.Cast<SpatialElement>().OfType<Room>().Where(r => r.Area > 0).ToList();
                foreach (var room in linkedRooms)
                {
                    allRooms.Add(room);
                    roomTransforms[room] = linkTransform;
                }
            }

            if (!allRooms.Any())
            {
                // Don't show in the categories list - it will be handled at form validation
                return;
            }

            CategoryItems.Add(new CategoryItem("Loading categories...", false));
            lvCategories.ItemsSource = CategoryItems;

            // Force UI update
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Clear for actual loading
            CategoryItems.Clear();

            // Collect all unique categories from elements inside rooms
            HashSet<ElementId> categoriesInRooms = new HashSet<ElementId>();

            // Check host model elements
            foreach (var roomPair in roomTransforms)
            {
                Room room = roomPair.Key;
                Transform roomTransform = roomPair.Value;
                bool isLinkedRoom = (room.Document != _doc);

                if (isLinkedRoom)
                {
                    // Room is in a linked model - check host elements
                    CheckHostElementsInLinkedRoom(room, roomTransform, categoriesInRooms);
                }
                else
                {
                    // Room is in host model - use original logic
                    CheckHostElementsInHostRoom(room, categoriesInRooms);
                }
            }

            // Convert category IDs to categories and filter out system categories and non-3D elements
            List<Category> categoriesFound = new List<Category>();

            // Categories to exclude
            HashSet<string> excludedCategoryNames = new HashSet<string>
            {
                "Area Boundary",
                "Areas",
                "Rooms",
                "Room Separation",
                "Lines",
                "Model Lines",
                "Detail Lines",
                "Room Tags",
                "Area Tags",
                "Curtain Panel Tags",
                "Detail Items",
                "Filled region",
                "Masking Region",
                "Revision Clouds",
                "Text Notes",
                "Generic Annotations",
                "Dimensions",
                "Grids",
                "Levels",
                "Reference Lines",
                "Reference Planes",
                "Reference Points",
                "Scope Boxes",
                "Section Boxes",
                "Elevation Marks",
                "Section Marks",
                "Callout Heads",
                "View Titles",
                "Viewports",
                "Sheets"
            };

            foreach (ElementId catId in categoriesInRooms)
            {
                try
                {
                    Category category = Category.GetCategory(_doc, catId);
                    if (category != null &&
                        category.CategoryType == CategoryType.Model &&
                        !excludedCategoryNames.Contains(category.Name) &&
                        category.AllowsBoundParameters &&
                        category.CanAddSubcategory &&
                        category.HasMaterialQuantities) // This helps filter for 3D elements
                    {
                        categoriesFound.Add(category);
                    }
                }
                catch
                {
                    // Skip if category cannot be retrieved
                }
            }

            categoriesFound = categoriesFound.OrderBy(c => c.Name).ToList();

            // Clear and populate with actual categories
            CategoryItems.Clear();

            if (categoriesFound.Count == 0)
            {
                CategoryItems.Add(new CategoryItem("No elements found inside rooms", false));
                ShowInfoDialog("No elements found inside rooms. Make sure your model has rooms with elements placed inside them.");
            }
            else
            {
                // Add "Select All" option
                CategoryItems.Add(new CategoryItem("Select All Categories", true));

                // Add individual categories
                foreach (Category category in categoriesFound)
                {
                    CategoryItems.Add(new CategoryItem(category));
                }
            }

            // Set ListView source
            lvCategories.ItemsSource = CategoryItems;
        }

        private void CheckHostElementsInHostRoom(Room room, HashSet<ElementId> categoriesInRooms)
        {
            // Get room bounding box
            BoundingBoxXYZ roomBB = room.get_BoundingBox(null);
            if (roomBB == null) return;

            // Create outline from room bounding box
            Outline roomOutline = new Outline(roomBB.Min, roomBB.Max);

            // Create bounding box filter
            BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(roomOutline);

            // Get all elements that might be in the room
            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(bbFilter);

            foreach (Element elem in collector)
            {
                // Skip non-model elements
                if (elem.Category == null || elem.Category.CategoryType != CategoryType.Model)
                    continue;

                // Check if element is actually inside the room
                bool isInRoom = false;

                LocationPoint locPoint = elem.Location as LocationPoint;
                if (locPoint != null)
                {
                    isInRoom = room.IsPointInRoom(locPoint.Point);
                }
                else
                {
                    // For non-point based elements, check center point
                    BoundingBoxXYZ elemBB = elem.get_BoundingBox(null);
                    if (elemBB != null)
                    {
                        XYZ centerPoint = (elemBB.Min + elemBB.Max) / 2;
                        isInRoom = room.IsPointInRoom(centerPoint);
                    }
                }

                if (isInRoom && elem.Category != null)
                {
                    categoriesInRooms.Add(elem.Category.Id);
                }
            }
        }

        private void CheckHostElementsInLinkedRoom(Room linkedRoom, Transform linkTransform, HashSet<ElementId> categoriesInRooms)
        {
            // Get room bounding box in linked coordinates
            BoundingBoxXYZ roomBB = linkedRoom.get_BoundingBox(null);
            if (roomBB == null) return;

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

            // Get all elements in host document that might be in the linked room
            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(bbFilter);

            foreach (Element elem in collector)
            {
                // Skip non-model elements
                if (elem.Category == null || elem.Category.CategoryType != CategoryType.Model)
                    continue;

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
                        categoriesInRooms.Add(elem.Category.Id);
                    }
                }
            }
        }

        private void LoadTargetParameters()
        {
            TargetParameterItems.Clear();

            var selectedCategories = SelectedCategories;
            if (!selectedCategories.Any())
            {
                return;
            }

            // Collect all parameters from elements in selected categories
            HashSet<string> allParameterNames = new HashSet<string>();
            List<Parameter> allParameters = new List<Parameter>();

            foreach (string categoryName in selectedCategories)
            {
                Category category = Utils.GetCategoryByName(_doc, categoryName);
                if (category == null) continue;

                // Get a few sample elements from this category
                FilteredElementCollector collector = new FilteredElementCollector(_doc)
                    .OfCategoryId(category.Id)
                    .WhereElementIsNotElementType();

                var sampleElements = collector.Take(5).ToList();

                foreach (Element elem in sampleElements)
                {
                    // Get instance parameters
                    var instanceParams = Utils.GetAllParmatersFromElement(elem);

                    // Get type parameters
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        Element elemType = _doc.GetElement(typeId);
                        if (elemType != null)
                        {
                            instanceParams.AddRange(Utils.GetAllParmatersFromElement(elemType));
                        }
                    }

                    // Add writable parameters
                    foreach (Parameter param in instanceParams)
                    {
                        if (!param.IsReadOnly &&
                            (param.StorageType == StorageType.String ||
                             param.StorageType == StorageType.Double ||
                             param.StorageType == StorageType.Integer) &&
                            !allParameterNames.Contains(param.Definition.Name))
                        {
                            allParameterNames.Add(param.Definition.Name);
                            allParameters.Add(param);
                        }
                    }
                }
            }

            // Filter parameters based on source parameter type compatibility
            List<Parameter> compatibleParams = new List<Parameter>();

            if (_selectedSourceParameterType.HasValue)
            {
                foreach (Parameter param in allParameters)
                {
                    bool isCompatible = false;

                    switch (_selectedSourceParameterType.Value)
                    {
                        case StorageType.String:
                            // String can only be copied to String
                            isCompatible = param.StorageType == StorageType.String;
                            break;

                        case StorageType.Integer:
                            // Integer can be copied to String or Double
                            isCompatible = param.StorageType == StorageType.String ||
                                         param.StorageType == StorageType.Double;
                            break;

                        case StorageType.Double:
                            // Double can only be copied to String (not Integer)
                            isCompatible = param.StorageType == StorageType.String;
                            break;
                    }

                    if (isCompatible)
                    {
                        compatibleParams.Add(param);
                    }
                }
            }
            else
            {
                // No source parameter selected, show all parameters
                compatibleParams = allParameters;
            }

            // Sort and add to collection
            var sortedParams = compatibleParams
                .GroupBy(p => p.Definition.Name)
                .Select(g => g.First())
                .OrderBy(p => p.Definition.Name)
                .ToList();

            foreach (Parameter param in sortedParams)
            {
                TargetParameterItems.Add(new Window3ParameterItem(param));
            }

            // Set ListView source
            lvTargetParameters.ItemsSource = TargetParameterItems;
        }

        private void CategoryCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is CategoryItem categoryItem)
            {
                if (categoryItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (CategoryItem item in CategoryItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual category checkbox
                    UpdateSelectAllCheckboxState();
                }

                // Reload target parameters when categories change
                LoadTargetParameters();
            }
        }

        private void UpdateSelectAllCheckboxState()
        {
            var selectAllItem = CategoryItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var categoryItems = CategoryItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = categoryItems.Count(item => item.IsSelected);
                int totalCount = categoryItems.Count;

                if (selectedCount == 0)
                {
                    selectAllItem.IsSelected = false;
                }
                else if (selectedCount == totalCount)
                {
                    selectAllItem.IsSelected = true;
                }
            }
        }

        #region Source Parameter Handlers

        private void SourceParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is Window3ParameterItem paramItem)
            {
                if (checkBox.IsChecked == true)
                {
                    // Uncheck all other parameters (single selection)
                    foreach (Window3ParameterItem item in SourceParameterItems)
                    {
                        if (item != paramItem)
                        {
                            item.IsSelected = false;
                        }
                    }

                    // Track the selected parameter type
                    _selectedSourceParameterType = paramItem.Parameter?.StorageType;
                }
                else
                {
                    _selectedSourceParameterType = null;
                }

                UpdateSourceParameterSearchTextBox();

                // Reload target parameters when source parameter changes
                LoadTargetParameters();
            }
        }

        private void UpdateSourceParameterSearchTextBox()
        {
            var selectedItem = SourceParameterItems.FirstOrDefault(item => item.IsSelected);

            if (selectedItem != null)
            {
                txtSourceParameterSearch.Text = selectedItem.ParameterName;
            }
            else
            {
                txtSourceParameterSearch.Text = "Search parameters...";
            }
        }

        private void txtSourceParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<Window3ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = SourceParameterItems.ToList();
                }
                else
                {
                    filteredItems = SourceParameterItems
                        .Where(p => p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<Window3ParameterItem> filteredCollection = new ObservableCollection<Window3ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvSourceParameters.ItemsSource = filteredCollection;
            }
        }

        private void txtSourceParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtSourceParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                UpdateSourceParameterSearchTextBox();
                lvSourceParameters.ItemsSource = SourceParameterItems;
            }
        }

        #endregion

        #region Target Parameter Handlers

        private void TargetParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is Window3ParameterItem paramItem)
            {
                if (checkBox.IsChecked == true)
                {
                    // Uncheck all other parameters (single selection)
                    foreach (Window3ParameterItem item in TargetParameterItems)
                    {
                        if (item != paramItem)
                        {
                            item.IsSelected = false;
                        }
                    }
                }

                UpdateTargetParameterSearchTextBox();
            }
        }

        private void UpdateTargetParameterSearchTextBox()
        {
            var selectedItem = TargetParameterItems.FirstOrDefault(item => item.IsSelected);

            if (selectedItem != null)
            {
                txtTargetParameterSearch.Text = selectedItem.ParameterName;
            }
            else
            {
                txtTargetParameterSearch.Text = "Search parameters...";
            }
        }

        private void txtTargetParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<Window3ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = TargetParameterItems.ToList();
                }
                else
                {
                    filteredItems = TargetParameterItems
                        .Where(p => p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<Window3ParameterItem> filteredCollection = new ObservableCollection<Window3ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvTargetParameters.ItemsSource = filteredCollection;
            }
        }

        private void txtTargetParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtTargetParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                UpdateTargetParameterSearchTextBox();
                lvTargetParameters.ItemsSource = TargetParameterItems;
            }
        }

        #endregion

        #region Category Search Functionality

        private void txtCategorySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search categories...")
                    return;

                List<CategoryItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = CategoryItems.ToList();
                }
                else
                {
                    filteredItems = CategoryItems
                        .Where(c => c.IsSelectAll || c.CategoryName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<CategoryItem> filteredCollection = new ObservableCollection<CategoryItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvCategories.ItemsSource = filteredCollection;
            }
        }

        private void txtCategorySearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search categories...")
            {
                textBox.Text = "";
            }
        }

        private void txtCategorySearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Search categories...";
                lvCategories.ItemsSource = CategoryItems;
            }
        }

        #endregion

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            // Check if there are rooms first
            if (!HasRooms)
            {
                ShowInfoDialog("No room/s found in the model/linked model");
                return;
            }

            // Validation
            var selectedSourceParam = SourceParameterItems.FirstOrDefault(item => item.IsSelected);
            if (selectedSourceParam == null)
            {
                ShowInfoDialog("No Room Parameter is selected");
                return;
            }

            if (!SelectedCategories.Any())
            {
                ShowInfoDialog("No category is selected");
                return;
            }

            var selectedTargetParam = TargetParameterItems.FirstOrDefault(item => item.IsSelected);
            if (selectedTargetParam == null)
            {
                ShowInfoDialog("No target parameter is selected");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void ShowInfoDialog(string message)
        {
            InfoDialog infoDialog = new InfoDialog(message)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Topmost = true
            };
            infoDialog.ShowDialog();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Properties to expose selections
        public string SelectedSourceParameter
        {
            get
            {
                var selectedItem = SourceParameterItems.FirstOrDefault(item => item.IsSelected);
                return selectedItem?.ParameterName ?? "";
            }
        }

        public string SelectedTargetParameter
        {
            get
            {
                var selectedItem = TargetParameterItems.FirstOrDefault(item => item.IsSelected);
                return selectedItem?.ParameterName ?? "";
            }
        }

        public List<string> SelectedCategories
        {
            get
            {
                return CategoryItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.CategoryName)
                    .ToList();
            }
        }
    }

    // Parameter item class for the list views
    public class Window3ParameterItem : INotifyPropertyChanged
    {
        private Parameter _parameter;
        private bool _isSelected;
        private string _parameterName;

        public Parameter Parameter
        {
            get { return _parameter; }
            set
            {
                _parameter = value;
                OnPropertyChanged(nameof(Parameter));
            }
        }

        public string ParameterName
        {
            get { return _parameterName; }
            set
            {
                _parameterName = value;
                OnPropertyChanged(nameof(ParameterName));
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Window3ParameterItem(Parameter parameter)
        {
            Parameter = parameter;
            ParameterName = parameter.Definition.Name;
            IsSelected = false;
        }
    }
}