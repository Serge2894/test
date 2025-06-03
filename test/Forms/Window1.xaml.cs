using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window, INotifyPropertyChanged
    {
        private ObservableCollection<ParameterItem> _curtainWallParameterItems;
        public ObservableCollection<ParameterItem> CurtainWallParameterItems
        {
            get { return _curtainWallParameterItems; }
            set
            {
                _curtainWallParameterItems = value;
                OnPropertyChanged(nameof(CurtainWallParameterItems));
            }
        }

        private ObservableCollection<ParameterItem> _stairParameterItems;
        public ObservableCollection<ParameterItem> StairParameterItems
        {
            get { return _stairParameterItems; }
            set
            {
                _stairParameterItems = value;
                OnPropertyChanged(nameof(StairParameterItems));
            }
        }

        private ObservableCollection<ParameterItem> _raillingParameterItems;
        public ObservableCollection<ParameterItem> RaillingParameterItems
        {
            get { return _raillingParameterItems; }
            set
            {
                _raillingParameterItems = value;
                OnPropertyChanged(nameof(RaillingParameterItems));
            }
        }

        private List<Parameter> _raillingParameters;
        public List<Parameter> RaillingParameters
        {
            get { return _raillingParameters; }
            set
            {
                _raillingParameters = value;
                OnPropertyChanged(nameof(RaillingParameters));
            }
        }

        // New properties for Nested Families
        private ObservableCollection<FamilyItem> _parentFamilyItems;
        public ObservableCollection<FamilyItem> ParentFamilyItems
        {
            get { return _parentFamilyItems; }
            set
            {
                _parentFamilyItems = value;
                OnPropertyChanged(nameof(ParentFamilyItems));
            }
        }

        private ObservableCollection<FamilyItem> _nestedFamilyItems;
        public ObservableCollection<FamilyItem> NestedFamilyItems
        {
            get { return _nestedFamilyItems; }
            set
            {
                _nestedFamilyItems = value;
                OnPropertyChanged(nameof(NestedFamilyItems));
            }
        }

        private ObservableCollection<ParameterItem> _nestedFamilyParameterItems;
        public ObservableCollection<ParameterItem> NestedFamilyParameterItems
        {
            get { return _nestedFamilyParameterItems; }
            set
            {
                _nestedFamilyParameterItems = value;
                OnPropertyChanged(nameof(NestedFamilyParameterItems));
            }
        }

        private FamilyItem _selectedParentFamily;
        public FamilyItem SelectedParentFamily
        {
            get { return _selectedParentFamily; }
            set
            {
                _selectedParentFamily = value;
                OnPropertyChanged(nameof(SelectedParentFamily));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Document _doc; // Document member variable

        public Window1(Document doc) // Constructor that accepts the Revit Document
        {
            InitializeComponent();
            _doc = doc; // Initialize the Document
            DataContext = this;

            // Initialize the parameters list
            CurtainWallParameterItems = new ObservableCollection<ParameterItem>();
            StairParameterItems = new ObservableCollection<ParameterItem>();
            RaillingParameters = new List<Parameter>();
            RaillingParameterItems = new ObservableCollection<ParameterItem>();

            // Initialize nested families collections
            ParentFamilyItems = new ObservableCollection<FamilyItem>();
            NestedFamilyItems = new ObservableCollection<FamilyItem>();
            NestedFamilyParameterItems = new ObservableCollection<ParameterItem>();

            // Show Curtain Wall tab by default
            ShowCurtainWallContent();
            btnCurtainwall.Tag = "Selected";

            // Load initial parameters for Curtain Wall
            LoadCurtainWallParameters();

            // Set initial window size to vertical layout
            ResizeWindowToVertical();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnCurtainwall_Click(object sender, RoutedEventArgs e)
        {
            ShowCurtainWallContent();
            SetTabSelection("Curtainwall");
            LoadCurtainWallParameters();

            // Force change to vertical layout
            ResizeWindowToVertical();

            // Alternative: Use async version if above doesn't work
            // ResizeWindowToVerticalAsync();
        }

        private void btnStair_Click(object sender, RoutedEventArgs e)
        {
            ShowStairContent();
            SetTabSelection("Stair");
            LoadStairParameters();

            // Force change to vertical layout
            ResizeWindowToVertical();

            // Alternative: Use async version if above doesn't work
            // ResizeWindowToVerticalAsync();
        }

        private void btnRailling_Click(object sender, RoutedEventArgs e)
        {
            ShowRaillingContent();
            SetTabSelection("Railing");
            LoadRaillingParameters();

            // Force change to vertical layout
            ResizeWindowToVertical();

            // Alternative: Use async version if above doesn't work
            // ResizeWindowToVerticalAsync();
        }

        private void btnNestedFamilies_Click(object sender, RoutedEventArgs e)
        {
            ShowNestedFamiliesContent();
            SetTabSelection("NestedFamilies");
            LoadParentFamilies();

            // Force change to horizontal layout
            ResizeWindowToHorizontal();

            // Alternative: Use async version if above doesn't work
            // ResizeWindowToHorizontalAsync();
        }

        // Helper method to set tab selection styling
        private void SetTabSelection(string selectedTab)
        {
            // Clear all tab selections
            btnCurtainwall.Tag = null;
            btnStair.Tag = null;
            btnRailling.Tag = null;
            btnNestedFamilies.Tag = null;

            // Set selected tab
            switch (selectedTab)
            {
                case "Curtainwall":
                    btnCurtainwall.Tag = "Selected";
                    break;
                case "Stair":
                    btnStair.Tag = "Selected";
                    break;
                case "Railing":
                    btnRailling.Tag = "Selected";
                    break;
                case "NestedFamilies":
                    btnNestedFamilies.Tag = "Selected";
                    break;
            }
        }

        // Store the current window position before resizing
        private double _lastWindowLeft;
        private double _lastWindowTop;
        private bool _positionInitialized = false;

        // Method to resize window to vertical layout (400x600) while maintaining position
        private void ResizeWindowToVertical()
        {
            // Store current position if it's been initialized by user interaction
            if (_positionInitialized)
            {
                _lastWindowLeft = this.Left;
                _lastWindowTop = this.Top;
            }

            // Force clear any size constraints first
            this.MinWidth = 0;
            this.MinHeight = 0;
            this.MaxWidth = double.PositiveInfinity;
            this.MaxHeight = double.PositiveInfinity;

            // Force the size change
            this.Width = 400;
            this.Height = 600;

            // Apply layout updates immediately
            this.UpdateLayout();

            // Set new constraints after size change
            this.MinWidth = 400;
            this.MinHeight = 600;

            // Restore position or center on first load
            if (_positionInitialized)
            {
                // Keep the window in its last position, but ensure it's still visible on screen
                EnsureWindowOnScreen();
            }
            else
            {
                // Center only on first load
                CenterWindowOnScreen();
                _positionInitialized = true;
            }

            // Debug output to verify size change
            System.Diagnostics.Debug.WriteLine($"Resized to Vertical: {this.Width}x{this.Height}");
        }

        // Method to resize window to horizontal layout (900x500) while maintaining position
        private void ResizeWindowToHorizontal()
        {
            // Store current position if it's been initialized by user interaction
            if (_positionInitialized)
            {
                _lastWindowLeft = this.Left;
                _lastWindowTop = this.Top;
            }

            // Force clear any size constraints first
            this.MinWidth = 0;
            this.MinHeight = 0;
            this.MaxWidth = double.PositiveInfinity;
            this.MaxHeight = double.PositiveInfinity;

            // Force the size change
            this.Width = 900;
            this.Height = 500;

            // Apply layout updates immediately
            this.UpdateLayout();

            // Set new constraints after size change
            this.MinWidth = 900;
            this.MinHeight = 500;

            // Restore position or center on first load
            if (_positionInitialized)
            {
                // Keep the window in its last position, but ensure it's still visible on screen
                EnsureWindowOnScreen();
            }
            else
            {
                // Center only on first load
                CenterWindowOnScreen();
                _positionInitialized = true;
            }

            // Debug output to verify size change
            System.Diagnostics.Debug.WriteLine($"Resized to Horizontal: {this.Width}x{this.Height}");
        }

        // Alternative method using Dispatcher for delayed execution if needed
        private void ResizeWindowToVerticalAsync()
        {
            // Store current position
            if (_positionInitialized)
            {
                _lastWindowLeft = this.Left;
                _lastWindowTop = this.Top;
            }

            // Use Dispatcher to ensure resize happens after current UI operations complete
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Clear constraints
                this.MinWidth = 0;
                this.MinHeight = 0;
                this.MaxWidth = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;

                // Set new size
                this.Width = 400;
                this.Height = 600;

                // Update layout
                this.UpdateLayout();

                // Set constraints
                this.MinWidth = 400;
                this.MinHeight = 600;

                // Handle positioning
                if (_positionInitialized)
                {
                    EnsureWindowOnScreen();
                }
                else
                {
                    CenterWindowOnScreen();
                    _positionInitialized = true;
                }

                System.Diagnostics.Debug.WriteLine($"Async Resized to Vertical: {this.Width}x{this.Height}");
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ResizeWindowToHorizontalAsync()
        {
            // Store current position
            if (_positionInitialized)
            {
                _lastWindowLeft = this.Left;
                _lastWindowTop = this.Top;
            }

            // Use Dispatcher to ensure resize happens after current UI operations complete
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Clear constraints
                this.MinWidth = 0;
                this.MinHeight = 0;
                this.MaxWidth = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;

                // Set new size
                this.Width = 900;
                this.Height = 500;

                // Update layout
                this.UpdateLayout();

                // Set constraints
                this.MinWidth = 900;
                this.MinHeight = 500;

                // Handle positioning
                if (_positionInitialized)
                {
                    EnsureWindowOnScreen();
                }
                else
                {
                    CenterWindowOnScreen();
                    _positionInitialized = true;
                }

                System.Diagnostics.Debug.WriteLine($"Async Resized to Horizontal: {this.Width}x{this.Height}");
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // Method to center window on screen (only used on first load)
        private void CenterWindowOnScreen()
        {
            // Get screen dimensions
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Calculate center position
            double centerX = (screenWidth - this.Width) / 2;
            double centerY = (screenHeight - this.Height) / 2;

            // Set window position
            this.Left = centerX;
            this.Top = centerY;

            // Store the centered position
            _lastWindowLeft = this.Left;
            _lastWindowTop = this.Top;
        }

        // Method to ensure window stays on screen after resize
        private void EnsureWindowOnScreen()
        {
            // Get screen dimensions
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Start with the last known position
            double newLeft = _lastWindowLeft;
            double newTop = _lastWindowTop;

            // Adjust if window would go off the right edge of screen
            if (newLeft + this.Width > screenWidth)
            {
                newLeft = screenWidth - this.Width;
            }

            // Adjust if window would go off the bottom edge of screen  
            if (newTop + this.Height > screenHeight)
            {
                newTop = screenHeight - this.Height;
            }

            // Ensure window doesn't go off the left edge
            if (newLeft < 0)
            {
                newLeft = 0;
            }

            // Ensure window doesn't go off the top edge
            if (newTop < 0)
            {
                newTop = 0;
            }

            // Apply the adjusted position
            this.Left = newLeft;
            this.Top = newTop;

            // Update stored position
            _lastWindowLeft = this.Left;
            _lastWindowTop = this.Top;
        }

        // Override the window's LocationChanged event to track user-initiated moves
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            // Only update stored position if the window has been initialized
            // and this is likely a user-initiated move (not a programmatic resize)
            if (_positionInitialized && this.IsLoaded)
            {
                _lastWindowLeft = this.Left;
                _lastWindowTop = this.Top;
            }
        }

        private void ShowCurtainWallContent()
        {
            CurtainwallContent.Visibility = System.Windows.Visibility.Visible;
            StairContent.Visibility = System.Windows.Visibility.Collapsed;
            RaillingContent.Visibility = System.Windows.Visibility.Collapsed;
            NestedFamiliesContent.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ShowStairContent()
        {
            CurtainwallContent.Visibility = System.Windows.Visibility.Collapsed;
            StairContent.Visibility = System.Windows.Visibility.Visible;
            RaillingContent.Visibility = System.Windows.Visibility.Collapsed;
            NestedFamiliesContent.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ShowRaillingContent()
        {
            CurtainwallContent.Visibility = System.Windows.Visibility.Collapsed;
            StairContent.Visibility = System.Windows.Visibility.Collapsed;
            RaillingContent.Visibility = System.Windows.Visibility.Visible;
            NestedFamiliesContent.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ShowNestedFamiliesContent()
        {
            CurtainwallContent.Visibility = System.Windows.Visibility.Collapsed;
            StairContent.Visibility = System.Windows.Visibility.Collapsed;
            RaillingContent.Visibility = System.Windows.Visibility.Collapsed;
            NestedFamiliesContent.Visibility = System.Windows.Visibility.Visible;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            // Validate based on which tab is currently visible
            if (CurtainwallContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Curtain Wall tab validation
                if (!ValidateCurtainWallTab())
                    return;
            }
            else if (StairContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Stair tab validation
                if (!ValidateStairTab())
                    return;
            }
            else if (RaillingContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Railing tab validation
                if (!ValidateRailingTab())
                    return;
            }
            else if (NestedFamiliesContent.Visibility == System.Windows.Visibility.Visible)
            {
                // Nested Families tab validation
                if (!ValidateNestedFamiliesTab())
                    return;
            }

            // Make sure selected parameter names are updated before closing
            OnPropertyChanged(nameof(SelectedCurtainWallParameterNames));
            OnPropertyChanged(nameof(SelectedStairParameterNames));
            OnPropertyChanged(nameof(SelectedRailingParameterNames));
            OnPropertyChanged(nameof(SelectedNestedFamilyParameterNames));
            OnPropertyChanged(nameof(SelectedNestedFamilyNames));

            DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
        private bool ValidateCurtainWallTab()
        {
            // Check if curtain walls exist in the scope first
            List<Wall> curtainWalls = GetCurtainWallInstances();
            if (!curtainWalls.Any())
            {
                string scope = IsEntireModelChecked ? "Entire Model" : "Current View";
                ShowInfoDialog($"No Curtain Walls found in the {scope}.");
                return false;
            }

            // Check if no components are selected
            if (!IsCurtainPanelChecked && !IsCurtainWallMullionsChecked)
            {
                ShowInfoDialog("No Component/s is selected.");
                return false;
            }

            // Check if parameters are selected
            if (!SelectedCurtainWallParameterNames.Any())
            {
                ShowInfoDialog("No parameter selected.");
                return false;
            }

            return true;
        }

        private bool ValidateStairTab()
        {
            // Check if stairs exist in the scope first
            List<Stairs> stairs = GetStairInstances();
            if (!stairs.Any())
            {
                string scope = IsEntireModelChecked ? "Entire Model" : "Current View";
                ShowInfoDialog($"No Stairs found in the {scope}.");
                return false;
            }

            // Check if no components are selected
            if (!IsStairLandingChecked && !IsStairSupportChecked && !IsStairRunChecked)
            {
                ShowInfoDialog("No Component/s is selected.");
                return false;
            }

            // Check if parameters are selected
            if (!SelectedStairParameterNames.Any())
            {
                ShowInfoDialog("No parameter selected.");
                return false;
            }

            return true;
        }

        private bool ValidateRailingTab()
        {
            // Check if railings exist in the scope first
            List<Railing> railings = GetRailingInstances();
            if (!railings.Any())
            {
                string scope = IsEntireModelChecked ? "Entire Model" : "Current View";
                ShowInfoDialog($"No Railings found in the {scope}.");
                return false;
            }

            // Check if no components are selected
            if (!IsTopRailChecked && !IsHandrailChecked && !IsRailSupportChecked)
            {
                ShowInfoDialog("No Component/s is selected.");
                return false;
            }

            // Check if parameters are selected
            if (!SelectedRailingParameterNames.Any())
            {
                ShowInfoDialog("No parameter selected.");
                return false;
            }

            return true;
        }

        private bool ValidateNestedFamiliesTab()
        {
            // First check if parent families exist in the scope
            List<FamilyItem> availableParentFamilies = GetAvailableParentFamilies();
            if (!availableParentFamilies.Any())
            {
                string scope = IsEntireModelChecked ? "Entire Model" : "Current View";
                ShowInfoDialog($"No parent families found in the {scope}.");
                return false;
            }

            // Check if a parent family is selected
            if (SelectedParentFamily == null)
            {
                ShowInfoDialog("No parent family is selected.");
                return false;
            }

            // Check if nested families are selected
            if (!SelectedNestedFamilyNames.Any())
            {
                ShowInfoDialog("No nested family is selected.");
                return false;
            }

            // Check if parameters are selected
            if (!SelectedNestedFamilyParameterNames.Any())
            {
                ShowInfoDialog("No parameter selected.");
                return false;
            }

            return true;
        }

        private List<FamilyItem> GetAvailableParentFamilies()
        {
            // Get all family instances in the document
            FilteredElementCollector collector;
            if (IsEntireModelChecked == true)
            {
                collector = new FilteredElementCollector(_doc);
            }
            else
            {
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
            }

            var familyInstances = collector
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            // Group by family symbol and filter those that have nested families
            var parentFamilies = new List<FamilyItem>();

            foreach (var instance in familyInstances)
            {
                var familySymbol = instance.Symbol;
                var family = familySymbol.Family;

                // Check if this family has nested families
                if (HasNestedFamilies(family))
                {
                    // Create a unique identifier for the family
                    var familyItem = new FamilyItem
                    {
                        FamilyName = family.Name,
                        Family = family,
                        FamilySymbol = familySymbol
                    };

                    // Add only if not already added
                    if (!parentFamilies.Any(f => f.FamilyName == familyItem.FamilyName))
                    {
                        parentFamilies.Add(familyItem);
                    }
                }
            }

            return parentFamilies;
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

        #region Nested Families Methods

        private void LoadParentFamilies()
        {
            ParentFamilyItems.Clear();

            // Get all family instances in the document
            FilteredElementCollector collector;
            if (IsEntireModelChecked == true)
            {
                collector = new FilteredElementCollector(_doc);
            }
            else
            {
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
            }

            var familyInstances = collector
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            // Group by family symbol and filter those that have nested families
            var parentFamilies = new List<FamilyItem>();

            foreach (var instance in familyInstances)
            {
                var familySymbol = instance.Symbol;
                var family = familySymbol.Family;

                // Check if this family has nested families
                if (HasNestedFamilies(family))
                {
                    // Create a unique identifier for the family
                    var familyItem = new FamilyItem
                    {
                        FamilyName = family.Name,
                        Family = family,
                        FamilySymbol = familySymbol
                    };

                    // Add only if not already added
                    if (!parentFamilies.Any(f => f.FamilyName == familyItem.FamilyName))
                    {
                        parentFamilies.Add(familyItem);
                    }
                }
            }

            // Sort and add to collection
            foreach (var familyItem in parentFamilies.OrderBy(f => f.FamilyName))
            {
                ParentFamilyItems.Add(familyItem);
            }

            // Set ListView source
            lvParentFamilies.ItemsSource = ParentFamilyItems;

            // Initialize search box
            txtParentFamilySearch.Text = "Search parent families...";
        }

        private bool HasNestedFamilies(Family family)
        {
            try
            {
                // Check family instances in the project that use this family
                // and see if they have nested family instances
                FilteredElementCollector familyInstanceCollector = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType();

                foreach (FamilyInstance instance in familyInstanceCollector.Cast<FamilyInstance>())
                {
                    if (instance.Symbol.Family.Id == family.Id)
                    {
                        // Check if this family instance has sub-components (nested families)
                        var subComponents = instance.GetSubComponentIds();
                        if (subComponents != null && subComponents.Count > 0)
                        {
                            return true;
                        }
                    }
                }

                // Alternative method: Check if family has nested family parameters
                var familySymbols = family.GetFamilySymbolIds();
                foreach (ElementId symbolId in familySymbols)
                {
                    FamilySymbol symbol = _doc.GetElement(symbolId) as FamilySymbol;
                    if (symbol != null)
                    {
                        // Look for parameters that might indicate nested families
                        foreach (Parameter param in symbol.Parameters)
                        {
                            if (param.Definition.Name.Contains("Family") &&
                                param.StorageType == StorageType.ElementId)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error checking nested families for {family.Name}: {ex.Message}");
            }

            return false;
        }

        private void LoadNestedFamilies(FamilyItem parentFamily)
        {
            NestedFamilyItems.Clear();

            if (parentFamily?.Family == null) return;

            try
            {
                // Get all instances of the parent family
                FilteredElementCollector collector;
                if (IsEntireModelChecked == true)
                {
                    collector = new FilteredElementCollector(_doc);
                }
                else
                {
                    collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
                }

                var parentInstances = collector
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.Family.Id == parentFamily.Family.Id)
                    .ToList();

                var nestedFamilies = new List<FamilyItem>();

                foreach (var parentInstance in parentInstances)
                {
                    // Get sub-components (nested families)
                    var subComponentIds = parentInstance.GetSubComponentIds();

                    if (subComponentIds != null && subComponentIds.Count > 0)
                    {
                        foreach (ElementId subId in subComponentIds)
                        {
                            Element subElement = _doc.GetElement(subId);
                            if (subElement is FamilyInstance nestedInstance)
                            {
                                var nestedFamilyItem = new FamilyItem
                                {
                                    FamilyName = nestedInstance.Symbol.Family.Name,
                                    Family = nestedInstance.Symbol.Family,
                                    FamilySymbol = nestedInstance.Symbol,
                                    IsSelected = false
                                };

                                if (!nestedFamilies.Any(f => f.FamilyName == nestedFamilyItem.FamilyName))
                                {
                                    nestedFamilies.Add(nestedFamilyItem);
                                }
                            }
                        }
                    }
                }

                // Add "Select All" option as the first item
                NestedFamilyItems.Add(new FamilyItem("Select All Nested Families", true));

                // Add nested families
                foreach (var nestedFamily in nestedFamilies.OrderBy(f => f.FamilyName))
                {
                    NestedFamilyItems.Add(nestedFamily);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error loading nested families: {ex.Message}");
            }

            // Set ListView source
            lvNestedFamilies.ItemsSource = NestedFamilyItems;

            // Initialize search box
            txtNestedFamilySearch.Text = "Search nested families...";
        }

        private void LoadNestedFamilyParameters()
        {
            NestedFamilyParameterItems.Clear();

            var selectedNestedFamilies = NestedFamilyItems
                .Where(item => item.IsSelected && !item.IsSelectAll)
                .ToList();

            if (!selectedNestedFamilies.Any()) return;

            var allParameters = new List<Parameter>();

            // Get all instances of the parent family to find nested family instances
            if (SelectedParentFamily?.Family != null)
            {
                FilteredElementCollector collector;
                if (IsEntireModelChecked == true)
                {
                    collector = new FilteredElementCollector(_doc);
                }
                else
                {
                    collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
                }

                var parentInstances = collector
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.Family.Id == SelectedParentFamily.Family.Id)
                    .ToList();

                // Get parameters from actual nested family instances
                foreach (var parentInstance in parentInstances)
                {
                    var subComponentIds = parentInstance.GetSubComponentIds();

                    if (subComponentIds != null && subComponentIds.Count > 0)
                    {
                        foreach (ElementId subId in subComponentIds)
                        {
                            Element subElement = _doc.GetElement(subId);
                            if (subElement is FamilyInstance nestedInstance)
                            {
                                // Check if this nested family is one of the selected ones
                                if (selectedNestedFamilies.Any(f => f.FamilyName == nestedInstance.Symbol.Family.Name))
                                {
                                    // Get instance parameters from the actual nested family instance
                                    allParameters.AddRange(Utils.GetAllParmatersFromElement(nestedInstance));

                                    // Also get type parameters from the nested family symbol
                                    ElementId typeId = nestedInstance.GetTypeId();
                                    if (typeId != ElementId.InvalidElementId)
                                    {
                                        Element elementType = _doc.GetElement(typeId);
                                        if (elementType != null)
                                        {
                                            allParameters.AddRange(Utils.GetAllParmatersFromElement(elementType));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // If we don't have any parameters from instances, fall back to symbol parameters
            if (!allParameters.Any())
            {
                foreach (var nestedFamily in selectedNestedFamilies)
                {
                    if (nestedFamily.FamilySymbol != null)
                    {
                        // Get parameters from the nested family symbol
                        allParameters.AddRange(Utils.GetAllParmatersFromElement(nestedFamily.FamilySymbol));

                        // Also get parameters from family symbol type
                        ElementId typeId = nestedFamily.FamilySymbol.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element elementType = _doc.GetElement(typeId);
                            if (elementType != null)
                            {
                                allParameters.AddRange(Utils.GetAllParmatersFromElement(elementType));
                            }
                        }
                    }
                }
            }

            // Filter and deduplicate parameters
            HashSet<string> excludedParameters = new HashSet<string>
            {
                "Family and Type",
                "Family",
                "Type",
                "Phase Created",
                "Phase Demolished",
            };

            var distinctParameters = allParameters
                .Where(p => !p.IsReadOnly && !excludedParameters.Contains(p.Definition.Name))
                .GroupBy(x => x.Definition.Name)
                .Select(x => x.First())
                .OrderBy(x => x.Definition.Name)
                .ToList();

            // Add "Select All" option
            NestedFamilyParameterItems.Add(new ParameterItem("Select All Parameters", true));

            // Add individual parameters
            foreach (Parameter param in distinctParameters)
            {
                NestedFamilyParameterItems.Add(new ParameterItem(param));
            }

            // Set ListView source
            lvNestedFamilyParameters.ItemsSource = NestedFamilyParameterItems;

            // Initialize search box
            txtNestedFamilyParameterSearch.Text = "Search parameters...";
        }

        #endregion

        #region Nested Families Event Handlers

        private void ParentFamilyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is FamilyItem familyItem)
            {
                if (checkBox.IsChecked == true)
                {
                    // Uncheck all other parent families (single selection behavior)
                    foreach (FamilyItem item in ParentFamilyItems)
                    {
                        if (item != familyItem)
                        {
                            item.IsSelected = false;
                        }
                    }

                    // Set the selected parent family
                    SelectedParentFamily = familyItem;
                    LoadNestedFamilies(familyItem);

                    // Clear parameters when parent family changes
                    NestedFamilyParameterItems.Clear();
                    txtNestedFamilyParameterSearch.Text = "Search parameters...";
                }
                else
                {
                    // If unchecked, clear the parent family selection
                    SelectedParentFamily = null;
                    NestedFamilyItems.Clear();
                    NestedFamilyParameterItems.Clear();
                    txtNestedFamilySearch.Text = "Search nested families...";
                    txtNestedFamilyParameterSearch.Text = "Search parameters...";
                }
            }
        }

        private void NestedFamilyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is FamilyItem familyItem)
            {
                if (familyItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (FamilyItem item in NestedFamilyItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual nested family checkbox
                    UpdateNestedFamilySelectAllCheckboxState();
                }

                // Reload parameters when nested family selection changes
                LoadNestedFamilyParameters();
            }
        }

        private void UpdateNestedFamilySelectAllCheckboxState()
        {
            var selectAllItem = NestedFamilyItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var familyItems = NestedFamilyItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = familyItems.Count(item => item.IsSelected);
                int totalCount = familyItems.Count;

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

        private void NestedFamilyParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is ParameterItem paramItem)
            {
                if (paramItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (ParameterItem item in NestedFamilyParameterItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual parameter checkbox
                    UpdateNestedFamilyParameterSelectAllCheckboxState();
                }

                UpdateNestedFamilyParameterSearchTextBox();
            }
        }

        private void UpdateNestedFamilyParameterSelectAllCheckboxState()
        {
            var selectAllItem = NestedFamilyParameterItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var parameterItems = NestedFamilyParameterItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = parameterItems.Count(item => item.IsSelected);
                int totalCount = parameterItems.Count;

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

        private void UpdateNestedFamilyParameterSearchTextBox()
        {
            var selectedItems = NestedFamilyParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

            if (selectedItems.Count == 0)
            {
                txtNestedFamilyParameterSearch.Text = "Search parameters...";
            }
            else if (selectedItems.Count == 1)
            {
                txtNestedFamilyParameterSearch.Text = selectedItems.First().ParameterName;
            }
            else
            {
                txtNestedFamilyParameterSearch.Text = $"{selectedItems.Count} parameters selected";
            }
        }

        #endregion

        #region Nested Families Search Functionality

        private void txtParentFamilySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parent families...")
                    return;

                List<FamilyItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = ParentFamilyItems.ToList();
                }
                else
                {
                    filteredItems = ParentFamilyItems
                        .Where(f => f.FamilyName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<FamilyItem> filteredCollection = new ObservableCollection<FamilyItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvParentFamilies.ItemsSource = filteredCollection;
            }
        }

        private void txtParentFamilySearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parent families...")
            {
                textBox.Text = "";
            }
        }

        private void txtParentFamilySearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Search parent families...";
                lvParentFamilies.ItemsSource = ParentFamilyItems;
            }
        }

        private void txtNestedFamilySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search nested families...")
                    return;

                List<FamilyItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = NestedFamilyItems.ToList();
                }
                else
                {
                    filteredItems = NestedFamilyItems
                        .Where(f => f.IsSelectAll || f.FamilyName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<FamilyItem> filteredCollection = new ObservableCollection<FamilyItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvNestedFamilies.ItemsSource = filteredCollection;
            }
        }

        private void txtNestedFamilySearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search nested families...")
            {
                textBox.Text = "";
            }
        }

        private void txtNestedFamilySearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Search nested families...";
                lvNestedFamilies.ItemsSource = NestedFamilyItems;
            }
        }

        private void txtNestedFamilyParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = NestedFamilyParameterItems.ToList();
                }
                else
                {
                    filteredItems = NestedFamilyParameterItems
                        .Where(p => p.IsSelectAll || p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<ParameterItem> filteredCollection = new ObservableCollection<ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvNestedFamilyParameters.ItemsSource = filteredCollection;
            }
        }

        private void txtNestedFamilyParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtNestedFamilyParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                var selectedItems = NestedFamilyParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

                if (selectedItems.Count == 0)
                {
                    textBox.Text = "Search parameters...";
                }
                else if (selectedItems.Count == 1)
                {
                    textBox.Text = selectedItems.First().ParameterName;
                }
                else
                {
                    textBox.Text = $"{selectedItems.Count} parameters selected";
                }

                lvNestedFamilyParameters.ItemsSource = NestedFamilyParameterItems;
            }
        }

        #endregion
        private void LoadCurtainWallParameters()
        {
            List<Wall> curCurtainWalls = GetCurtainWallInstances();
            List<Parameter> allCurtainWallParameters = new List<Parameter>();

            foreach (Wall curtainWall in curCurtainWalls)
            {
                // Get instance parameters
                allCurtainWallParameters.AddRange(Utils.GetAllParmatersFromElement(curtainWall));

                // Get type parameters using GetTypeId()
                ElementId typeId = curtainWall.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element wallType = _doc.GetElement(typeId);
                    if (wallType != null)
                    {
                        allCurtainWallParameters.AddRange(Utils.GetAllParmatersFromElement(wallType));
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

            // Filter out read-only parameters and excluded parameter names
            List<Parameter> distinctParameters = allCurtainWallParameters
                .Where(p => !p.IsReadOnly && !excludedParameters.Contains(p.Definition.Name))
                .GroupBy(x => x.Definition.Name)
                .Select(x => x.First())
                .OrderBy(x => x.Definition.Name)
                .ToList();

            // Create ParameterItems for the ListView
            CurtainWallParameterItems.Clear();

            // Add "Select All" option as the first item
            CurtainWallParameterItems.Add(new ParameterItem("Select All Parameters", true));

            // Add individual parameters
            foreach (Parameter param in distinctParameters)
            {
                CurtainWallParameterItems.Add(new ParameterItem(param));
            }

            // Set the ListView's ItemsSource
            lvCurtainwallParameters.ItemsSource = CurtainWallParameterItems;

            // Initialize search box with placeholder text
            txtCurtainwallParameterSearch.Text = "Search parameters...";
        }

        private void LoadStairParameters()
        {
            List<Stairs> curStairs = GetStairInstances();
            List<Parameter> allStairParameters = new List<Parameter>();

            foreach (Stairs stair in curStairs)
            {
                // Get instance parameters
                allStairParameters.AddRange(Utils.GetAllParmatersFromElement(stair));

                // Get type parameters using GetTypeId()
                ElementId typeId = stair.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element stairType = _doc.GetElement(typeId);
                    if (stairType != null)
                    {
                        allStairParameters.AddRange(Utils.GetAllParmatersFromElement(stairType));
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

            // Filter out read-only parameters and excluded parameter names
            List<Parameter> distinctParameters = allStairParameters
                .Where(p => !p.IsReadOnly && !excludedParameters.Contains(p.Definition.Name))
                .GroupBy(x => x.Definition.Name)
                .Select(x => x.First())
                .OrderBy(x => x.Definition.Name)
                .ToList();

            // Create ParameterItems for the ListView
            StairParameterItems.Clear();

            // Add "Select All" option as the first item
            StairParameterItems.Add(new ParameterItem("Select All Parameters", true));

            // Add individual parameters
            foreach (Parameter param in distinctParameters)
            {
                StairParameterItems.Add(new ParameterItem(param));
            }

            // Set the ListView's ItemsSource
            lvStairParameters.ItemsSource = StairParameterItems;

            // Initialize search box with placeholder text
            txtStairParameterSearch.Text = "Search parameters...";
        }

        private void LoadRaillingParameters()
        {
            List<Railing> curRaillings = GetRailingInstances();
            List<Parameter> allRaillingParameters = new List<Parameter>();

            foreach (Railing railling in curRaillings)
            {
                // Get instance parameters
                allRaillingParameters.AddRange(Utils.GetAllParmatersFromElement(railling));

                // Get type parameters using GetTypeId()
                ElementId typeId = railling.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element railingType = _doc.GetElement(typeId);
                    if (railingType != null)
                    {
                        allRaillingParameters.AddRange(Utils.GetAllParmatersFromElement(railingType));
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

            // Filter out read-only parameters and excluded parameter names
            List<Parameter> distinctParameters = allRaillingParameters
                .Where(p => !p.IsReadOnly && !excludedParameters.Contains(p.Definition.Name))
                .GroupBy(x => x.Definition.Name)
                .Select(x => x.First())
                .OrderBy(x => x.Definition.Name)
                .ToList();

            // Store the full parameter list for filtering
            RaillingParameters = distinctParameters;

            // Create ParameterItems for the ListView
            RaillingParameterItems.Clear();

            // Add "Select All" option as the first item
            RaillingParameterItems.Add(new ParameterItem("Select All Parameters", true));

            // Add individual parameters
            foreach (Parameter param in distinctParameters)
            {
                RaillingParameterItems.Add(new ParameterItem(param));
            }

            // Set the ListView's ItemsSource
            lvRaillingParameters.ItemsSource = RaillingParameterItems;

            // Initialize search box with placeholder text
            txtRaillingParameterSearch.Text = "Search parameters...";
        }

        private List<Wall> GetCurtainWallInstances()
        {
            FilteredElementCollector collector;
            if (IsEntireModelChecked == true)
            {
                // Entire Model: Collect all Curtain Wall elements
                collector = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType(); // To get instances, not types

                return collector
                    .Cast<Wall>()
                    .Where(w => w.WallType.Kind == WallKind.Curtain)
                    .ToList();
            }
            else
            {
                // Active View: Collect all Wall elements visible in the active view
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
            }

            List<Wall> curtainWallInstances = collector
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .Where(w => w.WallType.Kind == WallKind.Curtain)
                    .ToList();

            return curtainWallInstances;
        }

        private List<Stairs> GetStairInstances()
        {
            FilteredElementCollector collector;
            if (IsEntireModelChecked == true)
            {
                // Entire Model: Collect all Stairs elements
                collector = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Stairs))
                    .WhereElementIsNotElementType(); // To get instances, not types

                return collector
                .Cast<Stairs>()
                .ToList();
            }
            else
            {
                // Active View: Collect all Stairs elements visible in the active view
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
            }

            List<Stairs> stairInstances = collector
                .OfClass(typeof(Stairs))
                .WhereElementIsNotElementType()
                .Cast<Stairs>()
                .ToList();

            return stairInstances;
        }

        private List<Railing> GetRailingInstances()
        {
            FilteredElementCollector collector;
            if (IsEntireModelChecked == true)
            {
                // Entire Model: Collect all Railings elements
                collector = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Railing))
                    .WhereElementIsNotElementType(); // To get instances, not types

                return collector
                    .Cast<Railing>()
                    .ToList();
            }
            else
            {
                // Active View: Collect all Railings elements visible in the active view
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                    .OfClass(typeof(Railing))
                    .WhereElementIsNotElementType();
            }

            return collector
                .Cast<Railing>()
                .ToList();
        }

        // This event handler will be called when the MouseDown event occurs on the Border
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the left mouse button is pressed
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Drag the window
                DragMove();
            }
        }

        // Event handlers for Curtain Wall parameters
        private void CurtainwallParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is ParameterItem paramItem)
            {
                if (paramItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (ParameterItem item in CurtainWallParameterItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual parameter checkbox
                    UpdateCurtainWallSelectAllCheckboxState();
                }

                UpdateCurtainWallSearchTextBox();
            }
        }

        private void UpdateCurtainWallSelectAllCheckboxState()
        {
            var selectAllItem = CurtainWallParameterItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var parameterItems = CurtainWallParameterItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = parameterItems.Count(item => item.IsSelected);
                int totalCount = parameterItems.Count;

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

        private void UpdateCurtainWallSearchTextBox()
        {
            var selectedItems = CurtainWallParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

            if (selectedItems.Count == 0)
            {
                txtCurtainwallParameterSearch.Text = "Search parameters...";
            }
            else if (selectedItems.Count == 1)
            {
                txtCurtainwallParameterSearch.Text = selectedItems.First().ParameterName;
            }
            else
            {
                txtCurtainwallParameterSearch.Text = $"{selectedItems.Count} parameters selected";
            }
        }

        // Curtain Wall search functionality
        private void txtCurtainwallParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = CurtainWallParameterItems.ToList();
                }
                else
                {
                    filteredItems = CurtainWallParameterItems
                        .Where(p => p.IsSelectAll || p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<ParameterItem> filteredCollection = new ObservableCollection<ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvCurtainwallParameters.ItemsSource = filteredCollection;
            }
        }

        private void txtCurtainwallParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtCurtainwallParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                var selectedItems = CurtainWallParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

                if (selectedItems.Count == 0)
                {
                    textBox.Text = "Search parameters...";
                }
                else if (selectedItems.Count == 1)
                {
                    textBox.Text = selectedItems.First().ParameterName;
                }
                else
                {
                    textBox.Text = $"{selectedItems.Count} parameters selected";
                }

                lvCurtainwallParameters.ItemsSource = CurtainWallParameterItems;
            }
        }

        // Event handlers for Stair parameters
        private void StairParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is ParameterItem paramItem)
            {
                if (paramItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (ParameterItem item in StairParameterItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual parameter checkbox
                    UpdateStairSelectAllCheckboxState();
                }

                UpdateStairSearchTextBox();
            }
        }

        private void UpdateStairSelectAllCheckboxState()
        {
            var selectAllItem = StairParameterItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var parameterItems = StairParameterItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = parameterItems.Count(item => item.IsSelected);
                int totalCount = parameterItems.Count;

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

        private void UpdateStairSearchTextBox()
        {
            var selectedItems = StairParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

            if (selectedItems.Count == 0)
            {
                txtStairParameterSearch.Text = "Search parameters...";
            }
            else if (selectedItems.Count == 1)
            {
                txtStairParameterSearch.Text = selectedItems.First().ParameterName;
            }
            else
            {
                txtStairParameterSearch.Text = $"{selectedItems.Count} parameters selected";
            }
        }

        // Stair search functionality
        private void txtStairParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = StairParameterItems.ToList();
                }
                else
                {
                    filteredItems = StairParameterItems
                        .Where(p => p.IsSelectAll || p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<ParameterItem> filteredCollection = new ObservableCollection<ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvStairParameters.ItemsSource = filteredCollection;
            }
        }

        private void txtStairParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtStairParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                var selectedItems = StairParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

                if (selectedItems.Count == 0)
                {
                    textBox.Text = "Search parameters...";
                }
                else if (selectedItems.Count == 1)
                {
                    textBox.Text = selectedItems.First().ParameterName;
                }
                else
                {
                    textBox.Text = $"{selectedItems.Count} parameters selected";
                }

                lvStairParameters.ItemsSource = StairParameterItems;
            }
        }

        // Updated individual parameter checkbox change handler for Railing
        private void ParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is ParameterItem paramItem)
            {
                if (paramItem.IsSelectAll)
                {
                    // Handle "Select All" checkbox
                    bool isChecked = checkBox.IsChecked == true;
                    foreach (ParameterItem item in RaillingParameterItems)
                    {
                        if (!item.IsSelectAll)
                        {
                            item.IsSelected = isChecked;
                        }
                    }
                }
                else
                {
                    // Handle individual parameter checkbox
                    UpdateSelectAllCheckboxState();
                }

                UpdateSearchTextBox();
            }
        }

        // Helper method to update the "Select All" checkbox state
        private void UpdateSelectAllCheckboxState()
        {
            var selectAllItem = RaillingParameterItems.FirstOrDefault(item => item.IsSelectAll);
            if (selectAllItem != null)
            {
                var parameterItems = RaillingParameterItems.Where(item => !item.IsSelectAll).ToList();
                int selectedCount = parameterItems.Count(item => item.IsSelected);
                int totalCount = parameterItems.Count;

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

        // Helper method to update search textbox with selection info
        private void UpdateSearchTextBox()
        {
            var selectedItems = RaillingParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

            if (selectedItems.Count == 0)
            {
                txtRaillingParameterSearch.Text = "Search parameters...";
            }
            else if (selectedItems.Count == 1)
            {
                txtRaillingParameterSearch.Text = selectedItems.First().ParameterName;
            }
            else
            {
                txtRaillingParameterSearch.Text = $"{selectedItems.Count} parameters selected";
            }
        }

        // Updated TextBox search functionality for parameter list
        private void txtRaillingParameterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.IsFocused)
            {
                string searchText = textBox.Text.ToLower();

                if (searchText == "search parameters...")
                    return;

                List<ParameterItem> filteredItems;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredItems = RaillingParameterItems.ToList();
                }
                else
                {
                    filteredItems = RaillingParameterItems
                        .Where(p => p.IsSelectAll || p.ParameterName.ToLower().Contains(searchText))
                        .ToList();
                }

                ObservableCollection<ParameterItem> filteredCollection = new ObservableCollection<ParameterItem>();
                foreach (var item in filteredItems)
                {
                    filteredCollection.Add(item);
                }

                lvRaillingParameters.ItemsSource = filteredCollection;
            }
        }

        private void txtRaillingParameterSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && textBox.Text == "Search parameters...")
            {
                textBox.Text = "";
            }
        }

        private void txtRaillingParameterSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                var selectedItems = RaillingParameterItems.Where(item => item.IsSelected && !item.IsSelectAll).ToList();

                if (selectedItems.Count == 0)
                {
                    textBox.Text = "Search parameters...";
                }
                else if (selectedItems.Count == 1)
                {
                    textBox.Text = selectedItems.First().ParameterName;
                }
                else
                {
                    textBox.Text = $"{selectedItems.Count} parameters selected";
                }

                lvRaillingParameters.ItemsSource = RaillingParameterItems;
            }
        }

        // Properties to return selected parameters and families
        public List<string> SelectedCurtainWallParameterNames
        {
            get
            {
                return CurtainWallParameterItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.ParameterName)
                    .ToList();
            }
        }

        public List<string> SelectedStairParameterNames
        {
            get
            {
                return StairParameterItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.ParameterName)
                    .ToList();
            }
        }

        public List<string> SelectedRailingParameterNames
        {
            get
            {
                return RaillingParameterItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.ParameterName)
                    .ToList();
            }
        }

        // New properties for Nested Families
        public List<string> SelectedNestedFamilyParameterNames
        {
            get
            {
                return NestedFamilyParameterItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.ParameterName)
                    .ToList();
            }
        }

        public List<string> SelectedNestedFamilyNames
        {
            get
            {
                return NestedFamilyItems
                    .Where(item => item.IsSelected && !item.IsSelectAll)
                    .Select(item => item.FamilyName)
                    .ToList();
            }
        }

        public string SelectedParentFamilyName
        {
            get
            {
                return SelectedParentFamily?.FamilyName ?? "";
            }
        }

        // Component selection properties
        public bool IsCurtainPanelChecked
        {
            get { return (bool)cbxbCurtainPanel.IsChecked; }
        }

        public bool IsCurtainWallMullionsChecked
        {
            get { return (bool)cbxCurtainwallmullions.IsChecked; }
        }

        public bool IsEntireModelChecked
        {
            get { return (bool)rbEntireModel.IsChecked; }
        }

        public bool IsStairLandingChecked
        {
            get { return (bool)cbxStairLanding.IsChecked; }
        }

        public bool IsStairSupportChecked
        {
            get { return (bool)cbxStairSupport.IsChecked; }
        }

        public bool IsStairRunChecked
        {
            get { return (bool)cbxStairRun.IsChecked; }
        }

        public bool IsTopRailChecked
        {
            get { return (bool)cbxTopRail.IsChecked; }
        }

        public bool IsHandrailChecked
        {
            get { return (bool)cbxHandrail.IsChecked; }
        }

        public bool IsRailSupportChecked
        {
            get { return (bool)cbxRailSupport.IsChecked; }
        }
    }

    // ParameterItem class to support "Select All" styling
    public class ParameterItem : INotifyPropertyChanged
    {
        private Parameter _parameter;
        private bool _isSelected;
        private string _parameterName;
        private bool _isSelectAll;

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

        // Constructor for regular parameters
        public ParameterItem(Parameter parameter)
        {
            Parameter = parameter;
            ParameterName = parameter.Definition.Name;
            IsSelected = false;
            IsSelectAll = false;
        }

        // Constructor for "Select All" item
        public ParameterItem(string displayName, bool isSelectAll = false)
        {
            Parameter = null;
            ParameterName = displayName;
            IsSelected = false;
            IsSelectAll = isSelectAll;
        }
    }

    // FamilyItem class for nested families functionality
    public class FamilyItem : INotifyPropertyChanged
    {
        private Family _family;
        private FamilySymbol _familySymbol;
        private bool _isSelected;
        private string _familyName;
        private bool _isSelectAll;

        public Family Family
        {
            get { return _family; }
            set
            {
                _family = value;
                OnPropertyChanged(nameof(Family));
            }
        }

        public FamilySymbol FamilySymbol
        {
            get { return _familySymbol; }
            set
            {
                _familySymbol = value;
                OnPropertyChanged(nameof(FamilySymbol));
            }
        }

        public string FamilyName
        {
            get { return _familyName; }
            set
            {
                _familyName = value;
                OnPropertyChanged(nameof(FamilyName));
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

        // Constructor for regular families
        public FamilyItem()
        {
            IsSelected = false;
            IsSelectAll = false;
        }

        // Constructor for "Select All" item
        public FamilyItem(string displayName, bool isSelectAll = false)
        {
            Family = null;
            FamilySymbol = null;
            FamilyName = displayName;
            IsSelected = false;
            IsSelectAll = isSelectAll;
        }
    }
}