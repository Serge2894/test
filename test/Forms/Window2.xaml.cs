using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using test.Common;

namespace test.Forms
{
    /// <summary>
    /// Interaction logic for Window2.xaml
    /// </summary>
    public partial class Window2 : Window, INotifyPropertyChanged
    {
        // Add static instance management for modeless behavior
        private static Window2 _instance;

        // Add External Event fields
        private static ParameterUpdateHandler _handler = null;
        private static ExternalEvent _externalEvent = null;

        public Document Doc;
        public List<string> TypeNames;
        public string ParamName;
        public string ParamDataType;

        // Add this property to track if window is closed
        public bool IsClosed { get; private set; } = false;

        // Store original window state for maximize/restore functionality
        private double _originalWidth;
        private double _originalHeight;
        private double _originalLeft;
        private double _originalTop;
        private bool _isMaximized = false;

        // Store all parameters for filtering
        private List<string> _allParameters = new List<string>();

        // Add these fields for modeless functionality
        private UIApplication _uiApp;
        private UIDocument _uidoc;

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

        private ObservableCollection<TypeItem> _typeItems;
        public ObservableCollection<TypeItem> TypeItems
        {
            get { return _typeItems; }
            set
            {
                _typeItems = value;
                OnPropertyChanged(nameof(TypeItems));
            }
        }

        private ObservableCollection<Window2ParameterItem> _parameterItems;
        public ObservableCollection<Window2ParameterItem> ParameterItems
        {
            get { return _parameterItems; }
            set
            {
                _parameterItems = value;
                OnPropertyChanged(nameof(ParameterItems));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Static method to show modeless window
        public static void ShowModeless(Document doc, List<Category> catList)
        {
            // Initialize the external event handler if not already done
            if (_handler == null)
            {
                _handler = new ParameterUpdateHandler();
                _externalEvent = ExternalEvent.Create(_handler);
            }

            // If window is already open, just bring it to front
            if (_instance != null)
            {
                _instance.Activate();
                _instance.Topmost = true;
                _instance.Topmost = false; // Flash to get attention
                return;
            }

            // Create new instance
            _instance = new Window2(doc, catList)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = false
            };

            // Handle window closing to clean up static reference
            _instance.Closed += (s, e) => _instance = null;

            // Show as modeless
            _instance.Show();
        }

        public Window2(Document doc, List<Category> catList)
        {
            InitializeComponent();

            Doc = doc;
            DataContext = this;

            // Initialize the external event handler if not already done
            if (_handler == null)
            {
                _handler = new ParameterUpdateHandler();
                _externalEvent = ExternalEvent.Create(_handler);
            }

            // Try to get UIApplication reference for transactions
            try
            {
                _uidoc = new UIDocument(doc);
                _uiApp = _uidoc.Application;
            }
            catch
            {
                // Fallback if we can't get UIApplication reference
                _uiApp = null;
            }

            // Store original window dimensions
            _originalWidth = this.Width;
            _originalHeight = this.Height;
            _originalLeft = this.Left;
            _originalTop = this.Top;

            // Initialize the categories, types, and parameters collections
            CategoryItems = new ObservableCollection<CategoryItem>();
            TypeItems = new ObservableCollection<TypeItem>();
            ParameterItems = new ObservableCollection<Window2ParameterItem>();

            // Load categories based on initial scope selection (Entire Model is default)
            LoadCategoriesBasedOnScope();

            // Initialize parameter search box
            txtParameterSearch.Text = "Search parameters...";
        }

        // Add this override method to track when window is closed
        protected override void OnClosed(EventArgs e)
        {
            IsClosed = true;
            base.OnClosed(e);
        }

        private void cmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This method is kept for compatibility but no longer used
            // The new multi-select categories functionality uses CategoryCheckBox_Changed
            LoadTypesForSelectedCategories();
        }

        private void lbxTypes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This method is kept for compatibility but no longer used
            // The new multi-select types functionality uses TypeCheckBox_Changed
            LoadParametersForSelectedTypes();
        }

        private void LoadParametersForSelectedTypes()
        {
            ParameterItems.Clear();
            _allParameters.Clear();

            var selectedTypes = TypeItems
                .Where(item => item.IsSelected && !item.IsSelectAll)
                .ToList();

            if (!selectedTypes.Any()) return;

            List<Parameter> allParameters = new List<Parameter>();

            foreach (var typeItem in selectedTypes)
            {
                Category currentCat = Utils.GetCategoryByName(Doc, typeItem.CategoryName);

                if (currentCat != null)
                {
                    FilteredElementCollector collector;

                    if (IsEntireModelChecked)
                    {
                        collector = new FilteredElementCollector(Doc);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
                    }

                    // Get element INSTANCES in the category and scope
                    collector.OfCategoryId(currentCat.Id);
                    collector.WhereElementIsNotElementType();

                    var instances = collector.ToList();

                    // Find instances of the selected type and collect parameters
                    foreach (Element instance in instances)
                    {
                        ElementId typeId = instance.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = Doc.GetElement(typeId);
                            if (elementType != null && elementType.Name == typeItem.TypeName)
                            {
                                // Get instance parameters from the actual instance
                                allParameters.AddRange(Utils.GetAllParmatersFromElement(instance));

                                // Get type parameters from the element type
                                allParameters.AddRange(Utils.GetAllParmatersFromElement(elementType));

                                break; // We only need one instance per type to get all parameters
                            }
                        }
                    }
                }
            }

            // List of parameter names to exclude (commonly read-only parameters)
            HashSet<string> excludedParameters = new HashSet<string>
            {
                "Family and Type",
                "Family",
                "Type",
                "Phase Created",
                "Phase Demolished",
            };

            // Filter out read-only parameters, excluded parameter names, and unsupported parameter types
            List<Parameter> filteredParams = allParameters
                .Where(p => !p.IsReadOnly &&
                           !excludedParameters.Contains(p.Definition.Name) &&
                           (p.StorageType == StorageType.String ||
                            p.StorageType == StorageType.Double ||
                            p.StorageType == StorageType.Integer))
                .GroupBy(x => x.Definition.Name)
                .Select(x => x.First())
                .OrderBy(x => x.Definition.Name)
                .ToList();

            // Store all parameter names for filtering
            _allParameters = filteredParams.Select(p => p.Definition.Name).ToList();

            // Create ParameterItems for the ListView
            foreach (Parameter param in filteredParams)
            {
                ParameterItems.Add(new Window2ParameterItem(param));
            }

            // Set ListView source
            lvParameters.ItemsSource = ParameterItems;

            // Reset parameter search box
            txtParameterSearch.Text = "Search parameters...";
        }

        private void ParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is Window2ParameterItem paramItem)
            {
                if (checkBox.IsChecked == true)
                {
                    // Uncheck all other parameters (single selection behavior)
                    foreach (Window2ParameterItem item in ParameterItems)
                    {
                        if (item != paramItem)
                        {
                            item.IsSelected = false;
                        }
                    }

                    // Set the selected parameter
                    ParamName = paramItem.ParameterName;

                    // Update value input field
                    tbxValue.Text = "";
                    tbxValue.IsEnabled = true;

                    var selectedTypes = TypeItems
                        .Where(item => item.IsSelected && !item.IsSelectAll)
                        .ToList();

                    Parameter curParam = GetParameterByNameFromSelectedTypes(selectedTypes, ParamName);

                    if (curParam != null)
                    {
                        if (curParam.StorageType == StorageType.String)
                        {
                            lblValue.Content = "Set Parameter Value (as text):";
                            ParamDataType = "string";
                        }
                        else if (curParam.StorageType == StorageType.Integer)
                        {
                            lblValue.Content = "Set Parameter Value (as integer):";
                            ParamDataType = "integer";
                        }
                        else if (curParam.StorageType == StorageType.Double)
                        {
                            lblValue.Content = "Set Parameter Value (as decimal):";
                            ParamDataType = "double";
                        }
                        else
                        {
                            ParamDataType = "none";
                            lblValue.Content = "Set Parameter Value:";
                            tbxValue.Text = "Cannot set this parameter";
                            tbxValue.IsEnabled = false;
                        }
                    }

                    // Update the search box to show selected parameter
                    if (!string.IsNullOrEmpty(ParamName))
                    {
                        txtParameterSearch.Text = ParamName;
                    }
                }
                else
                {
                    // If unchecked, clear the parameter selection
                    ParamName = "";
                    lblValue.Content = "Set Parameter Value:";
                    tbxValue.Text = "";
                    tbxValue.IsEnabled = false;
                    txtParameterSearch.Text = "Search parameters...";
                }
            }
        }

        private void lvParameters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This method is kept for compatibility but no longer used
            // The new checkbox functionality uses ParameterCheckBox_Changed
        }

        // Updated btnOK_Click method to use External Event instead of direct transaction
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            // Validate selections first
            if (!ValidateSelections())
            {
                return;
            }

            // Set the data for the handler and raise the external event
            _handler.SetData(this, Doc);
            _externalEvent.Raise();

            // Don't close the window - keep it open for more operations
        }

        // Add new method to validate selections
        private bool ValidateSelections()
        {
            var selectedCategories = SelectedCategoryNames;
            var selectedTypes = SelectedTypeNames;

            if (!selectedCategories.Any())
            {
                InfoDialog noCategoryDialog = new InfoDialog("No category is selected.")
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Topmost = true
                };
                noCategoryDialog.ShowDialog();
                return false;
            }

            if (!selectedTypes.Any())
            {
                InfoDialog noTypeDialog = new InfoDialog("No type is selected.")
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Topmost = true
                };
                noTypeDialog.ShowDialog();
                return false;
            }

            if (string.IsNullOrEmpty(ParamName))
            {
                InfoDialog noParamDialog = new InfoDialog("No parameter is selected.")
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Topmost = true
                };
                noParamDialog.ShowDialog();
                return false;
            }

            return true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // This will trigger the Closed event and clean up _instance
        }

        // Added method for close button functionality
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Maximize/Restore button functionality
        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (_isMaximized)
            {
                // Restore to original size
                this.Width = _originalWidth;
                this.Height = _originalHeight;
                this.Left = _originalLeft;
                this.Top = _originalTop;
                this.WindowState = WindowState.Normal;
                btnMaximize.Content = "🗖";
                _isMaximized = false;
            }
            else
            {
                // Store current position and size before maximizing
                if (this.WindowState == WindowState.Normal)
                {
                    _originalWidth = this.Width;
                    _originalHeight = this.Height;
                    _originalLeft = this.Left;
                    _originalTop = this.Top;
                }

                // Maximize to full screen
                this.WindowState = WindowState.Maximized;
                btnMaximize.Content = "🗗";
                _isMaximized = true;
            }
        }

        // Added method for window dragging functionality
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the left mouse button is pressed and window is not maximized
            if (e.LeftButton == MouseButtonState.Pressed && !_isMaximized)
            {
                // Drag the window
                DragMove();
            }
        }

        public string GetNewValue()
        {
            return tbxValue.Text;
        }

        // Property to check if Entire Model is selected
        public bool IsEntireModelChecked
        {
            get { return (bool)rbEntireModel.IsChecked; }
        }

        // Property to check if Active View is selected
        public bool IsActiveViewChecked
        {
            get { return (bool)rbActiveView.IsChecked; }
        }

        // Properties to return selected categories and types
        public List<string> SelectedCategoryNames
        {
            get
            {
                return CategoryItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.CategoryName)
                    .ToList();
            }
        }

        public List<string> SelectedTypeNames
        {
            get
            {
                return TypeItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.TypeName)
                    .ToList();
            }
        }

        // Add utility methods for modeless functionality
        public static bool IsWindowOpen()
        {
            return _instance != null;
        }

        public static void CloseExistingWindow()
        {
            if (_instance != null)
            {
                _instance.Close();
            }
        }

        #region Parameter Search Functionality

        private void txtParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<Window2ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = ParameterItems.ToList();
                }
                else
                {
                    filteredItems = ParameterItems
                        .Where(p => p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<Window2ParameterItem> filteredCollection = new ObservableCollection<Window2ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvParameters.ItemsSource = filteredCollection;

                // If there's exactly one match, automatically select it
                if (filteredItems.Count == 1)
                {
                    filteredItems.First().IsSelected = true;
                }
            }
        }

        private void txtParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                var selectedItem = ParameterItems.FirstOrDefault(item => item.IsSelected);

                if (selectedItem != null)
                {
                    textBox.Text = selectedItem.ParameterName;
                }
                else
                {
                    textBox.Text = "Search parameters...";
                }

                // Restore all parameters to ListView
                lvParameters.ItemsSource = ParameterItems;
            }
        }

        #endregion

        #region Type Search Functionality

        private void txtTypeSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search types...")
                    return;

                List<TypeItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = TypeItems.ToList();
                }
                else
                {
                    filteredItems = TypeItems
                        .Where(t => t.IsSelectAll || t.TypeDisplayName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<TypeItem> filteredCollection = new ObservableCollection<TypeItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvTypes.ItemsSource = filteredCollection;
            }
        }

        private void txtTypeSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search types...")
            {
                textBox.Text = "";
            }
        }

        private void txtTypeSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                var selectedItems = TypeItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

                if (selectedItems.Count == 0)
                {
                    textBox.Text = "Search types...";
                }
                else if (selectedItems.Count == 1)
                {
                    textBox.Text = selectedItems.First().TypeDisplayName;
                }
                else
                {
                    textBox.Text = $"{selectedItems.Count} types selected";
                }

                lvTypes.ItemsSource = TypeItems;
            }
        }

        #endregion

        #region Scope-based Methods

        private void LoadCategoriesBasedOnScope()
        {
            CategoryItems.Clear();
            TypeItems.Clear();
            ParameterItems.Clear();
            _allParameters.Clear();

            List<Category> availableCategories = GetCategoriesWithElementsInScope();

            // Add "Select All" option as the first item
            CategoryItems.Add(new CategoryItem("Select All Categories", true));

            // Add individual categories
            foreach (Category category in availableCategories.OrderBy(c => c.Name))
            {
                CategoryItems.Add(new CategoryItem(category));
            }

            // Set ListView source
            lvCategories.ItemsSource = CategoryItems;

            // Initialize search box
            txtCategorySearch.Text = "Search categories...";
        }

        private List<Category> GetCategoriesWithElementsInScope()
        {
            FilteredElementCollector collector;

            if (IsEntireModelChecked)
            {
                collector = new FilteredElementCollector(Doc);
            }
            else
            {
                collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
            }

            // Get all element INSTANCES (not types) in the scope
            var elementInstances = collector
                .WhereElementIsNotElementType()
                .ToList();

            // Get unique categories that have element instances
            var categoriesWithElements = elementInstances
                .Where(e => e.Category != null)
                .Select(e => e.Category)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .ToList();

            // List of category names to exclude
            HashSet<string> excludedCategoryNames = new HashSet<string>
            {
                "Survey Point",
                "Sun Path",
                "Project Information",
                "Project Base Point",
                "Primary Contours",
                "Material Assets",
                "Materials",
                "Legend Components",
                "Internal Origin",
                "Cameras",
                "Rooms",
                "HVAC Zones",
                "Pipe Segments",
                "Area Based Load Type",
                "Circuit Naming Scheme"
            };

            // Filter to only include model categories and exclude specific unwanted categories
            var modelCategories = categoriesWithElements
                .Where(c => c.CategoryType == CategoryType.Model &&
                           !excludedCategoryNames.Contains(c.Name))
                .ToList();

            return modelCategories;
        }

        private void LoadTypesForSelectedCategories()
        {
            TypeItems.Clear();
            ParameterItems.Clear();
            _allParameters.Clear();

            var selectedCategories = CategoryItems
                .Where(item => item.IsSelected && !item.IsSelectAll)
                .Select(item => item.CategoryName)
                .ToList();

            var typesByCategory = new Dictionary<string, HashSet<string>>();

            foreach (string categoryName in selectedCategories)
            {
                Category currentCat = Utils.GetCategoryByName(Doc, categoryName);

                if (currentCat != null)
                {
                    FilteredElementCollector collector;

                    if (IsEntireModelChecked)
                    {
                        collector = new FilteredElementCollector(Doc);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
                    }

                    // Get element INSTANCES in the category and scope
                    collector.OfCategoryId(currentCat.Id);
                    collector.WhereElementIsNotElementType();

                    var instances = collector.ToList();

                    // Get the type names from the instances
                    var typeNames = new HashSet<string>();
                    foreach (Element instance in instances)
                    {
                        ElementId typeId = instance.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = Doc.GetElement(typeId);
                            if (elementType != null)
                            {
                                typeNames.Add(elementType.Name);
                            }
                        }
                    }

                    typesByCategory[categoryName] = typeNames;
                }
            }

            // Add "Select All" option as the first item
            TypeItems.Add(new TypeItem("Select All Types", "", true));

            // Add types grouped by category
            foreach (var categoryGroup in typesByCategory.OrderBy(kvp => kvp.Key))
            {
                foreach (string typeName in categoryGroup.Value.OrderBy(name => name))
                {
                    string displayName = $"{typeName} ({categoryGroup.Key})";
                    TypeItems.Add(new TypeItem(typeName, categoryGroup.Key, false, displayName));
                }
            }

            // Set ListView source
            lvTypes.ItemsSource = TypeItems;

            // Reset parameter search box
            txtParameterSearch.Text = "Search parameters...";
            // Reset type search box
            txtTypeSearch.Text = "Search types...";
        }

        private List<ElementType> GetElementTypesByNameWithScope(List<string> categoryNames, List<string> typeNames)
        {
            List<ElementType> types = new List<ElementType>();

            foreach (string categoryName in categoryNames)
            {
                Category currentCat = Utils.GetCategoryByName(Doc, categoryName);

                if (currentCat != null)
                {
                    FilteredElementCollector collector;

                    if (IsEntireModelChecked)
                    {
                        collector = new FilteredElementCollector(Doc);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
                    }

                    // Get element INSTANCES in the category and scope
                    collector.OfCategoryId(currentCat.Id);
                    collector.WhereElementIsNotElementType();

                    var instances = collector.ToList();

                    // Get the element types from instances that match our type names
                    foreach (Element instance in instances)
                    {
                        ElementId typeId = instance.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = Doc.GetElement(typeId) as ElementType;
                            if (elementType != null && typeNames.Contains(elementType.Name))
                            {
                                // Avoid duplicates
                                if (!types.Any(t => t.Id == elementType.Id))
                                {
                                    types.Add(elementType as ElementType);
                                }
                            }
                        }
                    }
                }
            }

            return types;
        }

        private ElementType GetElementTypeByNameWithScope(string categoryName, string name)
        {
            Category currentCat = Utils.GetCategoryByName(Doc, categoryName);

            if (currentCat != null)
            {
                FilteredElementCollector collector;

                if (IsEntireModelChecked)
                {
                    collector = new FilteredElementCollector(Doc);
                }
                else
                {
                    collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
                }

                // Get element INSTANCES in the category and scope
                collector.OfCategoryId(currentCat.Id);
                collector.WhereElementIsNotElementType();

                var instances = collector.ToList();

                // Find the element type from instances
                foreach (Element instance in instances)
                {
                    ElementId typeId = instance.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        Element elementType = Doc.GetElement(typeId);
                        if (elementType != null && elementType.Name == name)
                        {
                            return elementType as ElementType;
                        }
                    }
                }
            }

            return null;
        }

        private Parameter GetParameterByNameFromSelectedTypes(List<TypeItem> selectedTypes, string paramName)
        {
            foreach (var typeItem in selectedTypes)
            {
                Category currentCat = Utils.GetCategoryByName(Doc, typeItem.CategoryName);

                if (currentCat != null)
                {
                    FilteredElementCollector collector;

                    if (IsEntireModelChecked)
                    {
                        collector = new FilteredElementCollector(Doc);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
                    }

                    // Get element INSTANCES in the category and scope
                    collector.OfCategoryId(currentCat.Id);
                    collector.WhereElementIsNotElementType();

                    var instances = collector.ToList();

                    // Find the element type from instances and get the parameter
                    foreach (Element instance in instances)
                    {
                        ElementId typeId = instance.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = Doc.GetElement(typeId);
                            if (elementType != null && elementType.Name == typeItem.TypeName)
                            {
                                // Check instance parameters first
                                Parameter instanceParam = instance.GetParameters(paramName).FirstOrDefault();
                                if (instanceParam != null &&
                                    (instanceParam.StorageType == StorageType.String ||
                                     instanceParam.StorageType == StorageType.Double ||
                                     instanceParam.StorageType == StorageType.Integer))
                                    return instanceParam;

                                // Then check type parameters
                                Parameter typeParam = elementType.GetParameters(paramName).FirstOrDefault();
                                if (typeParam != null &&
                                    (typeParam.StorageType == StorageType.String ||
                                     typeParam.StorageType == StorageType.Double ||
                                     typeParam.StorageType == StorageType.Integer))
                                    return typeParam;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private Parameter GetParameterByNameWithScope(List<string> categoryNames, string typeName, string paramName)
        {
            foreach (string categoryName in categoryNames)
            {
                Category currentCat = Utils.GetCategoryByName(Doc, categoryName);

                if (currentCat != null)
                {
                    FilteredElementCollector collector;

                    if (IsEntireModelChecked)
                    {
                        collector = new FilteredElementCollector(Doc);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(Doc, Doc.ActiveView.Id);
                    }

                    // Get element INSTANCES in the category and scope
                    collector.OfCategoryId(currentCat.Id);
                    collector.WhereElementIsNotElementType();

                    var instances = collector.ToList();

                    // Find the element type from instances and get the parameter
                    foreach (Element instance in instances)
                    {
                        ElementId typeId = instance.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = Doc.GetElement(typeId);
                            if (elementType != null && elementType.Name == typeName)
                            {
                                // Check instance parameters first
                                Parameter instanceParam = instance.GetParameters(paramName).FirstOrDefault();
                                if (instanceParam != null &&
                                    (instanceParam.StorageType == StorageType.String ||
                                     instanceParam.StorageType == StorageType.Double ||
                                     instanceParam.StorageType == StorageType.Integer))
                                    return instanceParam;

                                // Then check type parameters
                                Parameter curParam = elementType.GetParameters(paramName).FirstOrDefault();
                                if (curParam != null &&
                                    (curParam.StorageType == StorageType.String ||
                                     curParam.StorageType == StorageType.Double ||
                                     curParam.StorageType == StorageType.Integer))
                                    return curParam;
                            }
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region Radio Button Event Handlers

        private void rbEntireModel_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) // Only refresh if window is fully loaded
            {
                LoadCategoriesBasedOnScope();
            }
        }

        private void rbActiveView_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) // Only refresh if window is fully loaded
            {
                LoadCategoriesBasedOnScope();
            }
        }

        #endregion

        #region Category Selection Event Handlers

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
                    UpdateCategorySelectAllCheckboxState();
                }

                UpdateCategorySearchTextBox();
                LoadTypesForSelectedCategories();
            }
        }

        private void UpdateCategorySelectAllCheckboxState()
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

        private void UpdateCategorySearchTextBox()
        {
            var selectedItems = CategoryItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

            if (selectedItems.Count == 0)
            {
                txtCategorySearch.Text = "Search categories...";
            }
            else if (selectedItems.Count == 1)
            {
                txtCategorySearch.Text = selectedItems.First().CategoryName;
            }
            else
            {
                txtCategorySearch.Text = $"{selectedItems.Count} categories selected";
            }
        }

        #endregion

        #region Type Selection Event Handlers

        private void TypeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is TypeItem typeItem)
            {
                if (typeItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (TypeItem item in TypeItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual type checkbox
                    UpdateTypeSelectAllCheckboxState();
                }

                LoadParametersForSelectedTypes();
            }
        }

        private void UpdateTypeSelectAllCheckboxState()
        {
            var selectAllItem = TypeItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var typeItems = TypeItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = typeItems.Count(item => item.IsSelected);
                int totalCount = typeItems.Count;

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
                var selectedItems = CategoryItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

                if (selectedItems.Count == 0)
                {
                    textBox.Text = "Search categories...";
                }
                else if (selectedItems.Count == 1)
                {
                    textBox.Text = selectedItems.First().CategoryName;
                }
                else
                {
                    textBox.Text = $"{selectedItems.Count} categories selected";
                }

                lvCategories.ItemsSource = CategoryItems;
            }
        }

        #endregion

        private void tbxValue_TextChanged(object sender, TextChangedEventArgs e)
        {

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
                    System.Diagnostics.Debug.Print($"Unknown parameter data type: {paramDataType}");
                    break;
            }

            return string.Empty; // Validation passed
        }
    }

    // CategoryItem class to support "Select All" styling and multi-selection
    public class CategoryItem : INotifyPropertyChanged
    {
        private Category _category;
        private bool _isSelected;
        private string _categoryName;
        private bool _isSelectAll;

        public Category Category
        {
            get { return _category; }
            set
            {
                _category = value;
                OnPropertyChanged(nameof(Category));
            }
        }

        public string CategoryName
        {
            get { return _categoryName; }
            set
            {
                _categoryName = value;
                OnPropertyChanged(nameof(CategoryName));
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

        public bool IsSelectAll
        {
            get { return _isSelectAll; }
            set
            {
                _isSelectAll = value;
                OnPropertyChanged(nameof(IsSelectAll));
                OnPropertyChanged(nameof(FontWeight));
                OnPropertyChanged(nameof(TextColor));
            }
        }

        public string FontWeight
        {
            get { return IsSelectAll ? "Bold" : "Normal"; }
        }

        public string TextColor
        {
            get { return IsSelectAll ? "#000000" : "#000000"; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Constructor for regular categories
        public CategoryItem(Category category)
        {
            Category = category;
            CategoryName = category.Name;
            IsSelected = false;
            IsSelectAll = false;
        }

        // Constructor for "Select All" item
        public CategoryItem(string displayName, bool isSelectAll = false)
        {
            Category = null;
            CategoryName = displayName;
            IsSelected = false;
            IsSelectAll = isSelectAll;
        }
    }

    // TypeItem class to support "Select All" styling and multi-selection for types
    public class TypeItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _typeName;
        private string _categoryName;
        private string _typeDisplayName;
        private bool _isSelectAll;

        public string TypeName
        {
            get { return _typeName; }
            set
            {
                _typeName = value;
                OnPropertyChanged(nameof(TypeName));
            }
        }

        public string CategoryName
        {
            get { return _categoryName; }
            set
            {
                _categoryName = value;
                OnPropertyChanged(nameof(CategoryName));
            }
        }

        public string TypeDisplayName
        {
            get { return _typeDisplayName; }
            set
            {
                _typeDisplayName = value;
                OnPropertyChanged(nameof(TypeDisplayName));
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

        public bool IsSelectAll
        {
            get { return _isSelectAll; }
            set
            {
                _isSelectAll = value;
                OnPropertyChanged(nameof(IsSelectAll));
                OnPropertyChanged(nameof(FontWeight));
                OnPropertyChanged(nameof(TextColor));
            }
        }

        public string FontWeight
        {
            get { return IsSelectAll ? "Bold" : "Normal"; }
        }

        public string TextColor
        {
            get { return IsSelectAll ? "#000000" : "#000000"; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Constructor for regular types
        public TypeItem(string typeName, string categoryName, bool isSelectAll = false, string displayName = null)
        {
            TypeName = typeName;
            CategoryName = categoryName;
            TypeDisplayName = displayName ?? typeName;
            IsSelected = false;
            IsSelectAll = isSelectAll;
        }
    }

    // Window2ParameterItem class to support checkbox functionality with single selection
    public class Window2ParameterItem : INotifyPropertyChanged
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

        public string FontWeight
        {
            get { return "Normal"; }
        }

        public string TextColor
        {
            get { return "#000000"; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Constructor for parameters
        public Window2ParameterItem(Parameter parameter)
        {
            Parameter = parameter;
            ParameterName = parameter.Definition.Name;
            IsSelected = false;
        }
    }
}